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

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Vm.PreviewStateChanged += OnPreviewStateChanged;

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

        VideoPreviewElement.MediaFailed += (s, args) =>
        {
            _mediaOpened = false;
            _isMediaElementPlaying = false;
            var msg = args.ErrorException?.Message ?? "Unsupported video format or codec";
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
                }
                catch { }
                _currentMediaSource = null;
                _mediaOpened = false;
                _isMediaElementPlaying = false;
            }
            return;
        }

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
            Filter = "Media Files (*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.png;*.jpg)|*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.png;*.jpg|All files (*.*)|*.*",
            Title = "Import Media Assets",
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
            Filter = "SubRip Subtitles (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Import Subtitles"
        };

        if (ofd.ShowDialog() == true)
        {
            Vm.ImportSrtFile(ofd.FileName);
        }
    }

    private void ExportSrt_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "SubRip Subtitles (*.srt)|*.srt|All files (*.*)|*.*",
            Title = "Export Subtitles",
            DefaultExt = ".srt",
            FileName = $"{Vm.CurrentProject.Name}_subtitles.srt"
        };

        if (sfd.ShowDialog() == true)
        {
            Vm.ExportSrtFile(sfd.FileName);
        }
    }

    private async void AddMusicAtPlayhead_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Audio & Music Files (*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.ogg)|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.ogg|All files (*.*)|*.*",
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

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Allow text fields to receive Space and typing keystrokes without interception
        if (e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase ||
            Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (e.Key == Key.Escape && Vm.IsUpdateNotificationVisible)
        {
            Vm.CloseUpdateNotification();
            e.Handled = true;
            return;
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
