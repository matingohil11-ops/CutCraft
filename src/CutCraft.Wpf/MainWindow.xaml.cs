using System.IO;
using System.Windows;
using System.Windows.Input;
using CutCraft.Core.Models;
using CutCraft.Wpf.ViewModels;
using CutCraft.Wpf.Views;
using Microsoft.Win32;

namespace CutCraft.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;
    private string? _currentMediaSource;
    private bool _isMediaElementPlaying;
    private bool _mediaOpened;
    private TimeSpan? _pendingSeekPosition;
    private DateTime _lastSeekTime = DateTime.MinValue;

    private bool _isFullScreen;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Vm.PreviewStateChanged += OnPreviewStateChanged;
        Vm.ToggleFullScreenRequested += (s, args) => ToggleFullScreen();
        Vm.ExitFullScreenRequested += (s, args) => ExitFullScreen();

        VideoPreviewElement.MediaOpened += (s, args) =>
        {
            _mediaOpened = true;
            if (VideoPreviewElement.NaturalDuration.HasTimeSpan)
            {
                var naturalDuration = VideoPreviewElement.NaturalDuration.TimeSpan;
                var naturalWidth = VideoPreviewElement.NaturalVideoWidth;
                var naturalHeight = VideoPreviewElement.NaturalVideoHeight;
                Vm.SyncImportedMediaDimensions(naturalDuration, naturalWidth, naturalHeight);
            }

            var targetPos = _pendingSeekPosition ?? Vm.ActiveVideoPosition;
            try
            {
                VideoPreviewElement.Position = targetPos;
                if (Vm.IsPlaying)
                {
                    VideoPreviewElement.Play();
                    _isMediaElementPlaying = true;
                }
                else
                {
                    VideoPreviewElement.Pause();
                    _isMediaElementPlaying = false;
                }
            }
            catch { }
            _pendingSeekPosition = null;
        };

        VideoPreviewElement.MediaFailed += async (s, args) =>
        {
            _mediaOpened = false;
            _isMediaElementPlaying = false;
            var msg = args.ErrorException?.Message ?? "Unsupported video format or codec";

            // If format is unsupported natively by MediaElement, attempt transparent ultrafast proxy generation
            try
            {
                Vm.Notifier.Announce("Media format requires proxy rendering. Generating preview proxy...");
                var proxyPath = await Vm.GenerateProxyForActiveClipAsync();
                if (!string.IsNullOrEmpty(proxyPath) && File.Exists(proxyPath))
                {
                    _currentMediaSource = proxyPath;
                    VideoPreviewElement.Source = new Uri(proxyPath, UriKind.Absolute);
                    return;
                }
            }
            catch { }

            Vm.Notifier.Announce($"Media preview notice: {msg}");
            Vm.PreviewOverlayMessage = $"Playback notice: {msg}";
            Vm.HasActiveVideo = false;
        };

        // Check for updates on startup if enabled in settings
        _ = Vm.CheckStartupUpdatesAsync();
    }

    private void OnPreviewStateChanged(string? sourcePath, TimeSpan position, bool isPlaying)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            if (_currentMediaSource != null)
            {
                try
                {
                    VideoPreviewElement.Stop();
                    VideoPreviewElement.Source = null;
                    ImagePreviewElement.Source = null;
                    ImagePreviewElement.Visibility = Visibility.Collapsed;
                    VideoPreviewElement.Visibility = Visibility.Visible;
                }
                catch { }
                _currentMediaSource = null;
                _mediaOpened = false;
                _isMediaElementPlaying = false;
            }
            return;
        }

        // Handle still image extensions in preview viewport
        if (MediaFormats.IsImageExtension(sourcePath))
        {
            try
            {
                if (_currentMediaSource != sourcePath)
                {
                    _currentMediaSource = sourcePath;
                    VideoPreviewElement.Stop();
                    VideoPreviewElement.Source = null;

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    ImagePreviewElement.Source = bitmap;
                    Vm.SyncImportedMediaDimensions(TimeSpan.FromSeconds(5), bitmap.PixelWidth, bitmap.PixelHeight);
                }

                ImagePreviewElement.Visibility = Visibility.Visible;
                VideoPreviewElement.Visibility = Visibility.Collapsed;
                _mediaOpened = true;
            }
            catch (Exception ex)
            {
                Vm.PreviewOverlayMessage = $"Failed to preview image: {ex.Message}";
            }
            return;
        }

        // Ensure video element is visible for audio/video media
        ImagePreviewElement.Visibility = Visibility.Collapsed;
        VideoPreviewElement.Visibility = Visibility.Visible;

        // Apply clip volume and mute state to preview player
        var activeClip = Vm.CurrentProject.Tracks
            .Where(t => t.Type == TrackType.Video && !t.IsMuted)
            .SelectMany(t => t.Clips)
            .FirstOrDefault(c => Vm.PlayheadPosition >= c.TimelineStart && Vm.PlayheadPosition < c.TimelineEnd);

        if (activeClip != null)
        {
            VideoPreviewElement.IsMuted = activeClip.IsMuted;
            VideoPreviewElement.Volume = activeClip.IsMuted ? 0.0 : Math.Clamp(activeClip.Volume * Vm.CurrentProject.MasterVolume, 0.0, 1.0);
        }

        if (_currentMediaSource != sourcePath)
        {
            _currentMediaSource = sourcePath;
            _mediaOpened = false;
            _isMediaElementPlaying = false;
            _pendingSeekPosition = position;
            try
            {
                VideoPreviewElement.Source = new Uri(sourcePath, UriKind.Absolute);
            }
            catch (Exception ex)
            {
                Vm.PreviewOverlayMessage = $"Failed to open video: {ex.Message}";
                Vm.HasActiveVideo = false;
                return;
            }
            return;
        }

        if (!_mediaOpened)
        {
            _pendingSeekPosition = position;
            return;
        }

        try
        {
            if (isPlaying)
            {
                if (!_isMediaElementPlaying)
                {
                    VideoPreviewElement.Position = position;
                    VideoPreviewElement.Play();
                    _isMediaElementPlaying = true;
                    _lastSeekTime = DateTime.UtcNow;
                }
                else
                {
                    // Avoid micro-seeking every 20ms which kills video rendering.
                    // Only resync if significant drift (>1.2s) occurs after user jump.
                    if (DateTime.UtcNow - _lastSeekTime > TimeSpan.FromMilliseconds(500))
                    {
                        var drift = (VideoPreviewElement.Position - position).Duration();
                        if (drift > TimeSpan.FromMilliseconds(1200))
                        {
                            VideoPreviewElement.Position = position;
                            _lastSeekTime = DateTime.UtcNow;
                        }
                    }
                }
            }
            else
            {
                if (_isMediaElementPlaying)
                {
                    VideoPreviewElement.Pause();
                    _isMediaElementPlaying = false;
                }
                VideoPreviewElement.Position = position;
            }
        }
        catch { }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "CutCraft Project (*.cutcraft;*.json)|*.cutcraft;*.json|All files (*.*)|*.*",
            Title = "Open Video Project"
        };

        if (ofd.ShowDialog() == true)
        {
            Vm.OpenProject(ofd.FileName);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "CutCraft Project (*.cutcraft)|*.cutcraft|JSON Project (*.json)|*.json",
            Title = "Save Video Project",
            FileName = $"{Vm.CurrentProject.Name}.cutcraft",
            DefaultExt = ".cutcraft"
        };

        if (sfd.ShowDialog() == true)
        {
            Vm.SaveProject(sfd.FileName);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var exportWindow = new ExportWindow(Vm.CurrentProject)
        {
            Owner = this
        };
        exportWindow.ShowDialog();
    }

    private async void ImportMedia_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = MediaFormats.MediaImportFilter,
            Title = "Import Media Assets (All Formats Supported)",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            foreach (var file in ofd.FileNames)
            {
                await Vm.ImportMediaAsync(file);
            }
        }
    }

    private void ImportSrt_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Subtitle Files (*.srt;*.vtt)|*.srt;*.vtt|SubRip Subtitles (*.srt)|*.srt|WebVTT (*.vtt)|*.vtt|All files (*.*)|*.*",
            Title = "Import Subtitles"
        };

        if (ofd.ShowDialog() == true)
        {
            Vm.ImportSubtitlesFile(ofd.FileName);
        }
    }

    private void ExportSrt_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "SubRip Subtitles (*.srt)|*.srt|WebVTT (*.vtt)|*.vtt|All files (*.*)|*.*",
            Title = "Export Subtitles",
            DefaultExt = ".srt",
            FileName = $"{Vm.CurrentProject.Name}_subtitles.srt"
        };

        if (sfd.ShowDialog() == true)
        {
            Vm.ExportSubtitlesFile(sfd.FileName);
        }
    }

    private async void AddMusicAtPlayhead_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = MediaFormats.AudioImportFilter,
            Title = $"Add Music at Current Point ({Vm.CurrentTimecode})",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            foreach (var file in ofd.FileNames)
            {
                await Vm.AddMusicAtPlayheadAsync(file);
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                foreach (var file in files)
                {
                    if (MediaFormats.IsSupportedMedia(file))
                    {
                        if (MediaFormats.IsAudioExtension(file) && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                        {
                            await Vm.AddMusicAtPlayheadAsync(file);
                        }
                        else
                        {
                            await Vm.ImportMediaAsync(file);
                        }
                    }
                    else if (file.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
                    {
                        Vm.ImportSubtitlesFile(file);
                    }
                    else if (file.EndsWith(".cutcraft", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        Vm.OpenProject(file);
                    }
                }
            }
        }
    }

    private void Clip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Clip clip)
        {
            Vm.SelectedClip = clip;
            var track = Vm.CurrentProject.Tracks.FirstOrDefault(t => t.Clips.Contains(clip));
            if (track != null)
            {
                Vm.SelectedTrack = track;
            }
            Vm.Notifier.Announce($"Selected clip: {clip.Name}");
        }
    }

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            EnterFullScreen();
        }
        else
        {
            ExitFullScreen();
        }
    }

    public void EnterFullScreen()
    {
        if (_isFullScreen) return;

        _previousWindowState = WindowState;
        _previousWindowStyle = WindowStyle;
        _previousResizeMode = ResizeMode;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        WindowState = WindowState.Maximized;

        _isFullScreen = true;
        Vm.IsFullScreen = true;
        Vm.Notifier.Announce("Full screen mode enabled. Press F11 or Esc to exit full screen.");
    }

    public void ExitFullScreen()
    {
        if (!_isFullScreen) return;

        WindowStyle = _previousWindowStyle;
        ResizeMode = _previousResizeMode;
        if (_previousWindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = _previousWindowState;
        }

        _isFullScreen = false;
        Vm.IsFullScreen = false;
        Vm.Notifier.Announce("Full screen mode disabled.");
    }

    private void PreviewContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Global F11 Full Screen shortcut - works anywhere across the entire application
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        // Allow text fields to receive Space and typing keystrokes without interception
        if (e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase ||
            Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            if (e.Key == Key.Escape && _isFullScreen)
            {
                ExitFullScreen();
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (Vm.IsUpdateNotificationVisible)
            {
                Vm.CloseUpdateNotification();
                e.Handled = true;
                return;
            }

            if (_isFullScreen)
            {
                ExitFullScreen();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Space:
                Vm.TogglePlayPause();
                e.Handled = true;
                return;

            case Key.K when Keyboard.Modifiers == ModifierKeys.None:
                Vm.TogglePlayPause();
                e.Handled = true;
                return;

            case Key.J when Keyboard.Modifiers == ModifierKeys.None:
                Vm.StepBackward();
                e.Handled = true;
                return;

            case Key.L when Keyboard.Modifiers == ModifierKeys.None:
                Vm.StepForward();
                e.Handled = true;
                return;

            case Key.Left when Keyboard.Modifiers == ModifierKeys.None:
                Vm.StepBackward();
                e.Handled = true;
                return;

            case Key.Right when Keyboard.Modifiers == ModifierKeys.None:
                Vm.StepForward();
                e.Handled = true;
                return;

            case Key.Home when Keyboard.Modifiers == ModifierKeys.None:
                Vm.JumpToStart();
                e.Handled = true;
                return;

            case Key.End when Keyboard.Modifiers == ModifierKeys.None:
                Vm.JumpToEnd();
                e.Handled = true;
                return;

            case Key.S when Keyboard.Modifiers == ModifierKeys.None:
                Vm.SplitAtPlayhead();
                e.Handled = true;
                return;

            case Key.I when Keyboard.Modifiers == ModifierKeys.None:
                Vm.TrimStartAtPlayhead();
                e.Handled = true;
                return;

            case Key.O when Keyboard.Modifiers == ModifierKeys.None:
                Vm.TrimEndAtPlayhead();
                e.Handled = true;
                return;

            case Key.Delete when Keyboard.Modifiers == ModifierKeys.None:
                Vm.DeleteSelectedClip();
                e.Handled = true;
                return;

            case Key.M when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                AddMusicAtPlayhead_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;

            case Key.M when Keyboard.Modifiers == ModifierKeys.None:
                Vm.AddMarkerAtPlayhead();
                e.Handled = true;
                return;
        }

        base.OnPreviewKeyDown(e);
    }
}
