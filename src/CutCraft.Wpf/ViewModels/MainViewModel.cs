using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutCraft.Core.Accessibility;
using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Serialization;
using CutCraft.Core.Subtitles;
using CutCraft.Core.Timeline;
using CutCraft.Core.Updates;
using CutCraft.Media.Analysis;
using CutCraft.Media.Export;
using CutCraft.Media.Proxy;

namespace CutCraft.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TimelineEngine _timelineEngine = new();
    private readonly IMediaAnalyzer _mediaAnalyzer = new FFprobeMediaAnalyzer();
    private readonly IMediaExporter _mediaExporter = new FFmpegProcessExporter();
    private readonly IAudioCueService _audioCues = new SystemAudioCueService();
    private readonly IProxyMediaService _proxyService = new FFmpegProxyMediaService();
    private readonly UpdateManager _updateManager = new();

    public IProxyMediaService ProxyMediaService => _proxyService;

    private readonly DispatcherTimer _playbackTimer;
    private readonly Stopwatch _playbackStopwatch = new();
    private TimeSpan _playbackStartTime = TimeSpan.Zero;

    [ObservableProperty]
    private Project _currentProject = Project.CreateDefault("My First Video");

    [ObservableProperty]
    private TimeSpan _playheadPosition = TimeSpan.Zero;

    [ObservableProperty]
    private string _currentTimecode = "00:00:00;00";

    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private Track? _selectedTrack;

    [ObservableProperty]
    private MediaAsset? _selectedAsset;

    [ObservableProperty]
    private CaptionItem? _selectedCaption;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _playPauseButtonText = "▶ Play";

    [ObservableProperty]
    private string? _activeVideoPath;

    [ObservableProperty]
    private TimeSpan _activeVideoPosition = TimeSpan.Zero;

    [ObservableProperty]
    private bool _hasActiveVideo;

    [ObservableProperty]
    private string _previewOverlayMessage = "CutCraft Video Preview Screen";

    [ObservableProperty]
    private string _activeCaptionText = "";

    [ObservableProperty]
    private Visibility _activeCaptionVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private CaptionStylePreset _activeCaptionStyle = CaptionStylePreset.Default;

    [ObservableProperty]
    private string _activeCaptionSpeaker = "";

    // Full Screen mode properties and events
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullScreenButtonText))]
    [NotifyPropertyChangedFor(nameof(FullScreenButtonTooltip))]
    private bool _isFullScreen;

    public string FullScreenButtonText => IsFullScreen ? "🗗 Exit Full Screen" : "⛶ Full Screen";
    public string FullScreenButtonTooltip => IsFullScreen ? "Exit Full Screen (F11 or Esc)" : "Enter Full Screen (F11)";

    public event EventHandler? ToggleFullScreenRequested;
    public event EventHandler? ExitFullScreenRequested;

    // Subtitle Editor form properties
    [ObservableProperty]
    private string _captionEditorText = "";

    [ObservableProperty]
    private string _captionEditorSpeaker = "";

    [ObservableProperty]
    private double _captionEditorStartSeconds;

    [ObservableProperty]
    private double _captionEditorEndSeconds;

    [ObservableProperty]
    private CaptionStylePreset _captionEditorStyle = CaptionStylePreset.Default;

    [ObservableProperty]
    private string _subtitleValidationSummary = "";

    [ObservableProperty]
    private bool _hasSelectedCaption;

    // Text Overlay / Title Card observables
    [ObservableProperty]
    private TextOverlay? _selectedOverlay;

    [ObservableProperty]
    private bool _hasSelectedOverlay;

    // Active Preview Overlay state
    [ObservableProperty]
    private TextOverlay? _activeOverlay;

    [ObservableProperty]
    private string _activeOverlayText = "";

    [ObservableProperty]
    private string? _activeOverlaySubtitle;

    [ObservableProperty]
    private TitleCardStyle _activeOverlayStyle = TitleCardStyle.Custom;

    [ObservableProperty]
    private string _activeOverlayFont = "Segoe UI";

    [ObservableProperty]
    private double _activeOverlayFontSize = 48.0;

    [ObservableProperty]
    private string _activeOverlayColor = "#FFFFFFFF";

    [ObservableProperty]
    private string? _activeOverlayBackground;

    [ObservableProperty]
    private string? _activeOverlayBorder;

    [ObservableProperty]
    private int _activeOverlayBorderWidth;

    [ObservableProperty]
    private string? _activeOverlayShadowColor;

    [ObservableProperty]
    private int _activeOverlayShadowDepth;

    [ObservableProperty]
    private OverlayAlignment _activeOverlayAlignment = OverlayAlignment.Center;

    [ObservableProperty]
    private VerticalAlignment _activeOverlayVerticalAlignment = VerticalAlignment.Center;

    [ObservableProperty]
    private HorizontalAlignment _activeOverlayHorizontalAlignment = HorizontalAlignment.Center;

    [ObservableProperty]
    private double _activeOverlayPosX = 0.5;

    [ObservableProperty]
    private double _activeOverlayPosY = 0.5;

    [ObservableProperty]
    private Visibility _activeOverlayVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private double _activeOverlayOpacity = 1.0;

    [ObservableProperty]
    private double _activeOverlayTranslateX = 0.0;

    [ObservableProperty]
    private double _activeOverlayTranslateY = 0.0;

    // Overlay Editor form properties
    [ObservableProperty]
    private string _overlayEditorText = "";

    [ObservableProperty]
    private string _overlayEditorSubtitle = "";

    [ObservableProperty]
    private string _overlayEditorFont = "Segoe UI";

    [ObservableProperty]
    private double _overlayEditorFontSize = 48.0;

    [ObservableProperty]
    private string _overlayEditorColor = "#FFFFFFFF";

    [ObservableProperty]
    private string _overlayEditorBackground = "#CC111827";

    [ObservableProperty]
    private TitleCardStyle _overlayEditorStyle = TitleCardStyle.Custom;

    [ObservableProperty]
    private TextAnimationType _overlayEditorAnimation = TextAnimationType.Fade;

    [ObservableProperty]
    private double _overlayEditorAnimationDuration = 0.6;

    [ObservableProperty]
    private double _overlayEditorStartSeconds;

    [ObservableProperty]
    private double _overlayEditorDurationSeconds = 4.0;

    [ObservableProperty]
    private OverlayAlignment _overlayEditorAlignment = OverlayAlignment.Center;

    [ObservableProperty]
    private double _overlayEditorPosX = 0.5;

    [ObservableProperty]
    private double _overlayEditorPosY = 0.5;

    [ObservableProperty]
    private bool _overlayEditorIsBold;

    [ObservableProperty]
    private bool _overlayEditorIsItalic;

    [ObservableProperty]
    private string _overlayEditorBorderColor = "";

    [ObservableProperty]
    private int _overlayEditorBorderWidth;

    partial void OnSelectedCaptionChanged(CaptionItem? value)
    {
        HasSelectedCaption = value != null;
        if (value != null)
        {
            CaptionEditorText = value.Text ?? "";
            CaptionEditorSpeaker = value.Speaker ?? "";
            CaptionEditorStartSeconds = value.StartTime.TotalSeconds;
            CaptionEditorEndSeconds = value.EndTime.TotalSeconds;
            CaptionEditorStyle = value.StylePreset;
        }
    }

    partial void OnSelectedOverlayChanged(TextOverlay? value)
    {
        HasSelectedOverlay = value != null;
        if (value != null)
        {
            OverlayEditorText = value.Text ?? "";
            OverlayEditorSubtitle = value.SubtitleText ?? "";
            OverlayEditorFont = value.FontName ?? "Segoe UI";
            OverlayEditorFontSize = value.FontSize;
            OverlayEditorColor = value.ColorHex ?? "#FFFFFFFF";
            OverlayEditorBackground = value.BackgroundColorHex ?? "";
            OverlayEditorStyle = value.Style;
            OverlayEditorAnimation = value.Animation;
            OverlayEditorAnimationDuration = value.AnimationDuration;
            OverlayEditorStartSeconds = value.StartTime.TotalSeconds;
            OverlayEditorDurationSeconds = value.Duration.TotalSeconds;
            OverlayEditorAlignment = value.Alignment;
            OverlayEditorPosX = value.PositionX;
            OverlayEditorPosY = value.PositionY;
            OverlayEditorIsBold = value.IsBold;
            OverlayEditorIsItalic = value.IsItalic;
            OverlayEditorBorderColor = value.BorderColorHex ?? "";
            OverlayEditorBorderWidth = value.BorderWidth;
        }
    }

    [ObservableProperty]
    private string _appVersionDisplay = $"v{UpdateManager.CurrentVersionString}";

    [ObservableProperty]
    private string _updateButtonText = "🔄 Check Updates";

    [ObservableProperty]
    private string _updateButtonAccessibleText = "Check for CutCraft updates";

    // User persistent settings
    public UserSettings Settings { get; } = UserSettings.Load();

    [ObservableProperty]
    private bool _isAutoCheckUpdatesEnabled = true;

    [ObservableProperty]
    private bool _isAutoCheckUpdatesDisabled = false;

    // Accessible Update Notification UI State
    [ObservableProperty]
    private bool _isUpdateNotificationVisible = false;

    [ObservableProperty]
    private Visibility _updateNotificationVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _updateActionButtonsVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _upToDateOrErrorDismissVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility _downloadProgressVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string _updateNotificationTitle = "";

    [ObservableProperty]
    private string _updateNotificationMessage = "";

    [ObservableProperty]
    private string _installedVersionDisplay = UpdateManager.CurrentVersionString;

    [ObservableProperty]
    private string _latestVersionDisplay = UpdateManager.CurrentVersionString;

    [ObservableProperty]
    private bool _isDownloadingUpdate = false;

    [ObservableProperty]
    private double _downloadProgressPercent = 0.0;

    [ObservableProperty]
    private string _downloadProgressText = "";

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    [ObservableProperty]
    private double _zoomLevel = 100.0; // pixels per second

    [ObservableProperty]
    private string _accessibilityStatus = "Ready. Press F1 for keyboard shortcuts.";

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private double _exportProgress;

    [ObservableProperty]
    private bool _isSnappingEnabled = true;

    [ObservableProperty]
    private bool _isRippleEditEnabled = false;

    [ObservableProperty]
    private string _rippleModeButtonText = "🌊 Ripple: OFF";

    partial void OnIsRippleEditEnabledChanged(bool value)
    {
        RippleModeButtonText = value ? "🌊 Ripple: ON" : "🌊 Ripple: OFF";
    }

    public double PlayheadSeconds
    {
        get => PlayheadPosition.TotalSeconds;
        set => SeekTo(TimeSpan.FromSeconds(Math.Max(0, value)));
    }

    public CommandManager CommandManager { get; } = new();
    public TimelineAccessibilityNotifier Notifier { get; }

    public event Action<string?, TimeSpan, bool>? PreviewStateChanged;

    public MainViewModel()
    {
        Notifier = new TimelineAccessibilityNotifier();
        Notifier.SpokenMessageGenerated += (s, msg) => AccessibilityStatus = msg;
        CommandManager.AccessibilityNotification += (s, msg) => AccessibilityStatus = msg;

        SelectedTrack = CurrentProject.Tracks.FirstOrDefault();

        // Initialize startup update checking setting from persistent user preferences
        IsAutoCheckUpdatesEnabled = Settings.AutoCheckUpdatesOnStartup;
        IsAutoCheckUpdatesDisabled = !Settings.AutoCheckUpdatesOnStartup;

        _playbackTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(20) // ~50fps smooth playhead & preview sync
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        UpdatePlaybackState(false);
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        var elapsed = _playbackStopwatch.Elapsed;
        var scaledElapsed = TimeSpan.FromSeconds(elapsed.TotalSeconds * PlaybackSpeed);
        var newPosition = _playbackStartTime + scaledElapsed;

        if (CurrentProject.Duration > TimeSpan.Zero && newPosition >= CurrentProject.Duration)
        {
            PlayheadPosition = CurrentProject.Duration;
            CurrentTimecode = TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate);
            OnPropertyChanged(nameof(PlayheadSeconds));
            PausePlayback();
            Notifier.Announce("Reached end of timeline.");
            return;
        }

        PlayheadPosition = newPosition;
        CurrentTimecode = TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate);
        OnPropertyChanged(nameof(PlayheadSeconds));

        UpdatePlaybackState(true);
    }

    [RelayCommand]
    public void ToggleFullScreen()
    {
        ToggleFullScreenRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ExitFullScreen()
    {
        ExitFullScreenRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            PausePlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    public void StartPlayback()
    {
        if (CurrentProject.Duration <= TimeSpan.Zero)
        {
            Notifier.Announce("Timeline is empty. Import media to begin.");
            PreviewOverlayMessage = "CutCraft Video Preview Screen (Import media to begin)";
            return;
        }

        if (PlayheadPosition >= CurrentProject.Duration)
        {
            PlayheadPosition = TimeSpan.Zero;
            OnPropertyChanged(nameof(PlayheadSeconds));
        }

        IsPlaying = true;
        PlayPauseButtonText = "⏸ Pause";
        _playbackStartTime = PlayheadPosition;
        _playbackStopwatch.Restart();
        _playbackTimer.Start();

        UpdatePlaybackState(true);
        Notifier.Announce("Playback started.");
    }

    public void PausePlayback()
    {
        IsPlaying = false;
        PlayPauseButtonText = "▶ Play";
        _playbackStopwatch.Stop();
        _playbackTimer.Stop();

        UpdatePlaybackState(false);
        Notifier.Announce("Playback paused.");
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        // Find active video clip at playhead position across unmuted video tracks
        var videoClip = CurrentProject.Tracks
            .Where(t => t.Type == TrackType.Video && !t.IsMuted)
            .SelectMany(t => t.Clips)
            .FirstOrDefault(c => PlayheadPosition >= c.TimelineStart && PlayheadPosition < c.TimelineEnd);

        if (videoClip != null && !string.IsNullOrEmpty(videoClip.AssetPath))
        {
            HasActiveVideo = true;
            ActiveVideoPath = _proxyService.GetActiveAssetPath(new MediaAsset { FilePath = videoClip.AssetPath, Id = videoClip.AssetId, Type = videoClip.Type });
            var relativeSec = (PlayheadPosition - videoClip.TimelineStart).TotalSeconds * videoClip.Speed;
            ActiveVideoPosition = videoClip.InPoint + TimeSpan.FromSeconds(relativeSec);
        }
        else
        {
            HasActiveVideo = false;
            ActiveVideoPath = null;
            ActiveVideoPosition = TimeSpan.Zero;
            PreviewOverlayMessage = CurrentProject.Tracks.Any(t => t.Clips.Count > 0)
                ? "No media at current playhead position"
                : "CutCraft Video Preview Screen (Import media to begin)";
        }

        // Active subtitle caption
        var activeCap = CurrentProject.Captions
            .FirstOrDefault(c => PlayheadPosition >= c.StartTime && PlayheadPosition <= c.EndTime);
        if (activeCap != null && !string.IsNullOrWhiteSpace(activeCap.Text))
        {
            ActiveCaptionText = !string.IsNullOrWhiteSpace(activeCap.Speaker)
                ? $"[{activeCap.Speaker}] {activeCap.Text}"
                : activeCap.Text;
            ActiveCaptionSpeaker = activeCap.Speaker ?? "";
            ActiveCaptionStyle = activeCap.StylePreset;
            ActiveCaptionVisibility = Visibility.Visible;
        }
        else
        {
            ActiveCaptionText = "";
            ActiveCaptionSpeaker = "";
            ActiveCaptionVisibility = Visibility.Collapsed;
        }

        // Active animated text overlay / title card
        var activeOv = CurrentProject.Overlays
            .FirstOrDefault(o => PlayheadPosition >= o.StartTime && PlayheadPosition <= o.EndTime);
        if (activeOv != null && !string.IsNullOrWhiteSpace(activeOv.Text))
        {
            ActiveOverlay = activeOv;
            ActiveOverlayText = activeOv.Text;
            ActiveOverlaySubtitle = activeOv.SubtitleText;
            ActiveOverlayStyle = activeOv.Style;
            ActiveOverlayFont = activeOv.FontName;
            ActiveOverlayFontSize = activeOv.FontSize;
            ActiveOverlayColor = activeOv.ColorHex;
            ActiveOverlayBackground = activeOv.BackgroundColorHex;
            ActiveOverlayBorder = activeOv.BorderColorHex;
            ActiveOverlayBorderWidth = activeOv.BorderWidth;
            ActiveOverlayShadowColor = activeOv.ShadowColorHex;
            ActiveOverlayShadowDepth = activeOv.ShadowDepth;
            ActiveOverlayAlignment = activeOv.Alignment;
            ActiveOverlayPosX = activeOv.PositionX;
            ActiveOverlayPosY = activeOv.PositionY;
            ActiveOverlayVisibility = Visibility.Visible;

            if (activeOv.Style == TitleCardStyle.LowerThird)
            {
                ActiveOverlayVerticalAlignment = VerticalAlignment.Bottom;
                ActiveOverlayHorizontalAlignment = HorizontalAlignment.Left;
            }
            else if (activeOv.Style == TitleCardStyle.Minimalist)
            {
                ActiveOverlayVerticalAlignment = VerticalAlignment.Bottom;
                ActiveOverlayHorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                ActiveOverlayVerticalAlignment = VerticalAlignment.Center;
                ActiveOverlayHorizontalAlignment = HorizontalAlignment.Center;
            }

            double progress = (PlayheadPosition - activeOv.StartTime).TotalSeconds;
            double remaining = (activeOv.EndTime - PlayheadPosition).TotalSeconds;
            double aDur = Math.Max(0.1, Math.Min(activeOv.AnimationDuration, activeOv.Duration.TotalSeconds / 2));

            switch (activeOv.Animation)
            {
                case TextAnimationType.Fade:
                    if (progress < aDur) ActiveOverlayOpacity = Math.Clamp(progress / aDur, 0.0, 1.0);
                    else if (remaining < aDur) ActiveOverlayOpacity = Math.Clamp(remaining / aDur, 0.0, 1.0);
                    else ActiveOverlayOpacity = 1.0;
                    ActiveOverlayTranslateX = 0;
                    ActiveOverlayTranslateY = 0;
                    break;
                case TextAnimationType.SlideUp:
                    if (progress < aDur)
                    {
                        double factor = 1.0 - (progress / aDur);
                        ActiveOverlayTranslateY = factor * 40.0;
                        ActiveOverlayOpacity = Math.Clamp(progress / aDur, 0.0, 1.0);
                    }
                    else if (remaining < aDur)
                    {
                        double factor = 1.0 - (remaining / aDur);
                        ActiveOverlayTranslateY = factor * 40.0;
                        ActiveOverlayOpacity = Math.Clamp(remaining / aDur, 0.0, 1.0);
                    }
                    else
                    {
                        ActiveOverlayTranslateY = 0;
                        ActiveOverlayOpacity = 1.0;
                    }
                    ActiveOverlayTranslateX = 0;
                    break;
                case TextAnimationType.SlideLeft:
                    if (progress < aDur)
                    {
                        double factor = 1.0 - (progress / aDur);
                        ActiveOverlayTranslateX = -factor * 80.0;
                        ActiveOverlayOpacity = Math.Clamp(progress / aDur, 0.0, 1.0);
                    }
                    else if (remaining < aDur)
                    {
                        double factor = 1.0 - (remaining / aDur);
                        ActiveOverlayTranslateX = factor * 80.0;
                        ActiveOverlayOpacity = Math.Clamp(remaining / aDur, 0.0, 1.0);
                    }
                    else
                    {
                        ActiveOverlayTranslateX = 0;
                        ActiveOverlayOpacity = 1.0;
                    }
                    ActiveOverlayTranslateY = 0;
                    break;
                default:
                    ActiveOverlayOpacity = 1.0;
                    ActiveOverlayTranslateX = 0;
                    ActiveOverlayTranslateY = 0;
                    break;
            }
        }
        else
        {
            ActiveOverlay = null;
            ActiveOverlayText = "";
            ActiveOverlaySubtitle = null;
            ActiveOverlayVisibility = Visibility.Collapsed;
            ActiveOverlayOpacity = 1.0;
            ActiveOverlayTranslateX = 0;
            ActiveOverlayTranslateY = 0;
        }

        PreviewStateChanged?.Invoke(ActiveVideoPath, ActiveVideoPosition, isPlaying);
    }

    public async Task<string?> GenerateProxyForActiveClipAsync()
    {
        if (string.IsNullOrEmpty(ActiveVideoPath)) return null;
        var asset = CurrentProject.MediaLibrary.FirstOrDefault(a => a.FilePath == ActiveVideoPath)
                    ?? new MediaAsset { FilePath = ActiveVideoPath, Name = Path.GetFileName(ActiveVideoPath), Type = AssetType.Video };

        Notifier.Announce($"Generating preview proxy for {asset.Name}...");
        PreviewOverlayMessage = $"Generating preview proxy for {asset.Name}...";
        try
        {
            var proxyPath = await _proxyService.GenerateProxyAsync(asset);
            if (File.Exists(proxyPath) && proxyPath != asset.FilePath)
            {
                Notifier.Announce($"Preview proxy ready for {asset.Name}");
                ActiveVideoPath = proxyPath;
                UpdatePlaybackState(IsPlaying);
                return proxyPath;
            }
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Proxy generation failed: {ex.Message}");
        }
        return null;
    }

    [RelayCommand]
    public void JumpToStart()
    {
        SeekTo(TimeSpan.Zero);
    }

    [RelayCommand]
    public void JumpToEnd()
    {
        SeekTo(CurrentProject.Duration);
    }

    private UpdateInfo? _pendingUpdate;

    [RelayCommand]
    public void SetAutoCheckUpdates(string mode)
    {
        bool enable = string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase);
        Settings.AutoCheckUpdatesOnStartup = enable;
        Settings.Save();
        IsAutoCheckUpdatesEnabled = enable;
        IsAutoCheckUpdatesDisabled = !enable;

        var stateStr = enable ? "enabled" : "disabled";
        Notifier.Announce($"Automatic update check on startup is now {stateStr}.");
    }

    /// <summary>
    /// Checks for updates on startup if enabled. If no update or offline, does not interrupt the user.
    /// If update available (and not skipped), displays notification with options.
    /// </summary>
    public async Task CheckStartupUpdatesAsync()
    {
        if (!Settings.AutoCheckUpdatesOnStartup)
        {
            return;
        }

        try
        {
            var result = await _updateManager.CheckForUpdatesAsync();
            if (result.HasUpdate && result.Update != null)
            {
                // Verify user hasn't chosen to skip this version
                if (!string.IsNullOrWhiteSpace(Settings.SkippedVersion) &&
                    string.Equals(Settings.SkippedVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _pendingUpdate = result.Update;
                InstalledVersionDisplay = result.CurrentVersion;
                LatestVersionDisplay = result.LatestVersion;
                UpdateNotificationTitle = $"CutCraft Update Available: Version {result.LatestVersion}";
                UpdateNotificationMessage = string.IsNullOrWhiteSpace(result.Update.ReleaseNotes)
                    ? result.Message
                    : $"{result.Message}\n\n{result.Update.ReleaseNotes}";

                UpdateActionButtonsVisibility = Visibility.Visible;
                UpToDateOrErrorDismissVisibility = Visibility.Collapsed;
                DownloadProgressVisibility = Visibility.Collapsed;
                IsUpdateNotificationVisible = true;
                UpdateNotificationVisibility = Visibility.Visible;

                UpdateButtonText = $"⬆ Install {result.LatestVersion}";
                UpdateButtonAccessibleText = $"Update available: version {result.LatestVersion}. Click to review options.";

                Notifier.Announce($"Update notification: CutCraft version {result.LatestVersion} is available. Currently installed is {result.CurrentVersion}. Press Alt+D to Download and Install, Alt+R to Remind Me Later, or Alt+S to Skip This Version.");
            }
        }
        catch
        {
            // Do not interrupt user on startup if check fails
        }
    }

    /// <summary>
    /// Manual check for updates (invoked from Settings menu, Help menu, status bar, or About).
    /// </summary>
    [RelayCommand]
    public async Task ManualCheckForUpdatesAsync()
    {
        try
        {
            UpdateButtonText = "🔄 Checking...";
            UpdateButtonAccessibleText = "Checking for CutCraft updates...";
            Notifier.Announce("Checking for CutCraft updates...");

            var result = await _updateManager.CheckForUpdatesAsync();
            InstalledVersionDisplay = result.CurrentVersion;
            LatestVersionDisplay = result.LatestVersion;

            if (!result.IsSuccess)
            {
                _pendingUpdate = null;
                UpdateNotificationTitle = "Update Check Failed";
                UpdateNotificationMessage = result.Message;
                UpdateActionButtonsVisibility = Visibility.Collapsed;
                UpToDateOrErrorDismissVisibility = Visibility.Visible;
                DownloadProgressVisibility = Visibility.Collapsed;
                IsUpdateNotificationVisible = true;
                UpdateNotificationVisibility = Visibility.Visible;

                UpdateButtonText = "🔄 Check Updates";
                UpdateButtonAccessibleText = "Update check failed. Click to try again.";
                Notifier.Announce($"Update check failed: {result.Message}");
                return;
            }

            if (result.HasUpdate && result.Update != null)
            {
                _pendingUpdate = result.Update;
                UpdateNotificationTitle = $"CutCraft Update Available: Version {result.LatestVersion}";
                UpdateNotificationMessage = string.IsNullOrWhiteSpace(result.Update.ReleaseNotes)
                    ? result.Message
                    : $"{result.Message}\n\n{result.Update.ReleaseNotes}";

                UpdateActionButtonsVisibility = Visibility.Visible;
                UpToDateOrErrorDismissVisibility = Visibility.Collapsed;
                DownloadProgressVisibility = Visibility.Collapsed;
                IsUpdateNotificationVisible = true;
                UpdateNotificationVisibility = Visibility.Visible;

                UpdateButtonText = $"⬆ Install {result.LatestVersion}";
                UpdateButtonAccessibleText = $"Update available: version {result.LatestVersion}. Click to review options.";
                Notifier.Announce($"Update available: CutCraft {result.LatestVersion}! Currently installed is {result.CurrentVersion}. Options: Download and Install, Remind Me Later, Skip This Version.");
            }
            else
            {
                _pendingUpdate = null;
                UpdateNotificationTitle = "CutCraft is Up to Date";
                UpdateNotificationMessage = "You are using the latest version.";
                UpdateActionButtonsVisibility = Visibility.Collapsed;
                UpToDateOrErrorDismissVisibility = Visibility.Visible;
                DownloadProgressVisibility = Visibility.Collapsed;
                IsUpdateNotificationVisible = true;
                UpdateNotificationVisibility = Visibility.Visible;

                UpdateButtonText = $"✔ {AppVersionDisplay} Up to date";
                UpdateButtonAccessibleText = $"CutCraft is up to date (version {AppVersionDisplay}).";
                Notifier.Announce($"CutCraft is up to date. You are using the latest version (v{result.CurrentVersion}).");
            }
        }
        catch (Exception ex)
        {
            UpdateButtonText = "🔄 Check Updates";
            UpdateButtonAccessibleText = "Update check failed. Click to try again.";
            UpdateNotificationTitle = "Update Check Error";
            UpdateNotificationMessage = $"Unexpected error checking updates: {ex.Message}";
            UpdateActionButtonsVisibility = Visibility.Collapsed;
            UpToDateOrErrorDismissVisibility = Visibility.Visible;
            DownloadProgressVisibility = Visibility.Collapsed;
            IsUpdateNotificationVisible = true;
            UpdateNotificationVisibility = Visibility.Visible;
            Notifier.Announce($"Update check error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        await ManualCheckForUpdatesAsync();
    }

    [RelayCommand]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (_pendingUpdate == null)
            return;

        try
        {
            IsDownloadingUpdate = true;
            DownloadProgressVisibility = Visibility.Visible;
            UpdateActionButtonsVisibility = Visibility.Collapsed;
            UpToDateOrErrorDismissVisibility = Visibility.Collapsed;
            DownloadProgressPercent = 0.0;
            DownloadProgressText = "Preparing download...";
            UpdateButtonText = "⏳ Updating...";
            UpdateButtonAccessibleText = "Downloading update in progress...";

            Notifier.Announce($"Downloading CutCraft {_pendingUpdate.Version} update...");

            int lastAnnouncedMilestone = 0;
            var progress = new Progress<double>(value =>
            {
                var percent = Math.Clamp(value * 100.0, 0.0, 100.0);
                DownloadProgressPercent = percent;
                DownloadProgressText = $"Downloading update: {percent:F0}%...";

                // Announce milestones politely to screen reader (e.g. at 25%, 50%, 75%, 100%)
                int currentMilestone = (int)(percent / 25) * 25;
                if (currentMilestone > lastAnnouncedMilestone && currentMilestone <= 100)
                {
                    lastAnnouncedMilestone = currentMilestone;
                    Notifier.Announce($"Download progress: {currentMilestone}%");
                }
            });

            var destinationDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updates");
            var updateZip = await _updateManager.DownloadUpdateAsync(_pendingUpdate, destinationDir, progress);

            DownloadProgressPercent = 100.0;
            DownloadProgressText = "Download complete. Applying update and restarting CutCraft...";
            Notifier.Announce("Update downloaded. Applying update and restarting CutCraft...");

            // Apply update passing current process PID for safe file unlocking
            var applied = _updateManager.ApplyUpdate(updateZip, restartApp: true, Process.GetCurrentProcess().Id);
            if (applied)
            {
                // Gracefully shut down WPF app so files can be updated
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            else
            {
                throw new InvalidOperationException("Failed to launch updater process.");
            }
        }
        catch (Exception ex)
        {
            IsDownloadingUpdate = false;
            DownloadProgressVisibility = Visibility.Collapsed;
            UpdateActionButtonsVisibility = Visibility.Collapsed;
            UpToDateOrErrorDismissVisibility = Visibility.Visible;
            UpdateNotificationTitle = "Update Installation Failed";
            UpdateNotificationMessage = $"Error: {ex.Message}";
            UpdateButtonText = "🔄 Check Updates";
            UpdateButtonAccessibleText = "Update failed. Click to try again.";
            Notifier.Announce($"Update failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RemindMeLater()
    {
        IsUpdateNotificationVisible = false;
        UpdateNotificationVisibility = Visibility.Collapsed;
        Notifier.Announce("Update reminder postponed. CutCraft will remind you on next startup.");
    }

    [RelayCommand]
    public void SkipThisVersion()
    {
        if (_pendingUpdate != null)
        {
            var skipped = _pendingUpdate.Version;
            Settings.SkippedVersion = skipped;
            Settings.Save();
            _pendingUpdate = null;
            IsUpdateNotificationVisible = false;
            UpdateNotificationVisibility = Visibility.Collapsed;
            UpdateButtonText = $"✔ {AppVersionDisplay} Up to date";
            UpdateButtonAccessibleText = $"CutCraft is up to date (version {AppVersionDisplay}). Version {skipped} skipped.";
            Notifier.Announce($"CutCraft version {skipped} skipped. You will not be notified again for this version.");
        }
        else
        {
            IsUpdateNotificationVisible = false;
            UpdateNotificationVisibility = Visibility.Collapsed;
        }
    }

    [RelayCommand]
    public void CloseUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
        UpdateNotificationVisibility = Visibility.Collapsed;
    }

    [RelayCommand]
    public void ShowAbout()
    {
        InstalledVersionDisplay = UpdateManager.CurrentVersionString;
        LatestVersionDisplay = UpdateManager.CurrentVersionString;
        UpdateNotificationTitle = $"About CutCraft {AppVersionDisplay}";
        UpdateNotificationMessage = $"CutCraft - Accessible C# Desktop Video Editor\nInstalled Version: {UpdateManager.CurrentVersionString}\nAutomatic update check on startup: {(Settings.AutoCheckUpdatesOnStartup ? "Enabled" : "Disabled")}";
        UpdateActionButtonsVisibility = Visibility.Collapsed;
        UpToDateOrErrorDismissVisibility = Visibility.Visible;
        DownloadProgressVisibility = Visibility.Collapsed;
        IsUpdateNotificationVisible = true;
        UpdateNotificationVisibility = Visibility.Visible;
        Notifier.Announce($"CutCraft {AppVersionDisplay} - Accessible C# Desktop Video Editor. Installed Version {UpdateManager.CurrentVersionString}.");
    }

    [RelayCommand]
    public void NewProject()
    {
        if (IsPlaying) PausePlayback();
        CurrentProject = Project.CreateDefault("New Video Project");
        CommandManager.Clear();
        PlayheadPosition = TimeSpan.Zero;
        OnPropertyChanged(nameof(PlayheadSeconds));
        SelectedClip = null;
        SelectedTrack = CurrentProject.Tracks.FirstOrDefault();
        UpdatePlaybackState(false);
        Notifier.Announce("Created new project.");
    }

    [RelayCommand]
    public void OpenProject(string filePath)
    {
        try
        {
            if (IsPlaying) PausePlayback();
            CurrentProject = ProjectSerializer.LoadFromFile(filePath);
            CommandManager.Clear();
            PlayheadPosition = TimeSpan.Zero;
            OnPropertyChanged(nameof(PlayheadSeconds));
            SelectedClip = null;
            SelectedTrack = CurrentProject.Tracks.FirstOrDefault();
            UpdatePlaybackState(false);
            Notifier.Announce($"Loaded project {CurrentProject.Name}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Error loading project: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SaveProject(string filePath)
    {
        try
        {
            ProjectSerializer.SaveToFile(CurrentProject, filePath);
            Notifier.Announce($"Saved project to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Error saving project: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ImportMediaAsync(string filePath)
    {
        try
        {
            Notifier.Announce($"Importing {Path.GetFileName(filePath)}...");
            var asset = await _mediaAnalyzer.AnalyzeAssetAsync(filePath);
            CurrentProject.MediaLibrary.Add(asset);

            var targetTrack = asset.Type == AssetType.Audio
                ? CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Audio)
                : CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Video);

            if (targetTrack != null)
            {
                if (IsPlaying) PausePlayback();

                var clipDuration = asset.Duration > TimeSpan.Zero ? asset.Duration : TimeSpan.FromSeconds(5);
                var newClip = new Clip
                {
                    Name = asset.Name,
                    AssetId = asset.Id,
                    AssetPath = asset.FilePath,
                    Type = asset.Type,
                    InPoint = TimeSpan.Zero,
                    OutPoint = clipDuration,
                    TimelineStart = targetTrack.Duration
                };

                CommandManager.Execute(new AddClipCommand(targetTrack, newClip));
                SelectedClip = newClip;

                // Move playhead to start of new clip so preview updates immediately
                SeekTo(newClip.TimelineStart);
            }

            OnPropertyChanged(nameof(CurrentProject));
            OnPropertyChanged(nameof(PlayheadSeconds));
            UpdatePlaybackState(IsPlaying);
            Notifier.Announce($"Imported {asset.Name} ({asset.Duration.TotalSeconds:F1}s)");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to import media: {ex.Message}");
        }
    }

    public void SyncImportedMediaDimensions(TimeSpan naturalDuration, int naturalWidth, int naturalHeight)
    {
        if (string.IsNullOrEmpty(ActiveVideoPath)) return;

        var asset = CurrentProject.MediaLibrary.FirstOrDefault(a => a.FilePath == ActiveVideoPath);
        if (asset != null)
        {
            if (naturalDuration > TimeSpan.Zero && (asset.Duration <= TimeSpan.FromSeconds(5) || asset.Duration == TimeSpan.Zero))
            {
                asset.Duration = naturalDuration;
            }
            if (naturalWidth > 0) asset.Width = naturalWidth;
            if (naturalHeight > 0) asset.Height = naturalHeight;
        }

        foreach (var track in CurrentProject.Tracks)
        {
            foreach (var clip in track.Clips.Where(c => c.AssetPath == ActiveVideoPath))
            {
                if (naturalDuration > TimeSpan.Zero && (clip.OutPoint <= TimeSpan.FromSeconds(5) || clip.OutPoint == TimeSpan.Zero))
                {
                    clip.OutPoint = naturalDuration;
                }
            }
        }

        OnPropertyChanged(nameof(CurrentProject));
        OnPropertyChanged(nameof(PlayheadSeconds));
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public async Task AddMusicAtPlayheadAsync(string filePath)
    {
        try
        {
            Notifier.Announce($"Adding music {Path.GetFileName(filePath)} at current point ({CurrentTimecode})...");
            var asset = await _mediaAnalyzer.AnalyzeAssetAsync(filePath);

            if (!CurrentProject.MediaLibrary.Any(a => a.FilePath == asset.FilePath))
            {
                CurrentProject.MediaLibrary.Add(asset);
            }

            var audioTrack = CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Audio);
            if (audioTrack == null)
            {
                audioTrack = new Track
                {
                    Name = "Audio / Music",
                    Type = TrackType.Audio,
                    Order = CurrentProject.Tracks.Count
                };
                CurrentProject.Tracks.Add(audioTrack);
            }

            var newClip = new Clip
            {
                Name = asset.Name,
                AssetId = asset.Id,
                AssetPath = asset.FilePath,
                Type = AssetType.Audio,
                InPoint = TimeSpan.Zero,
                OutPoint = asset.Duration > TimeSpan.Zero ? asset.Duration : TimeSpan.FromSeconds(10),
                TimelineStart = PlayheadPosition,
                Volume = 1.0
            };

            int insertIndex = audioTrack.Clips.TakeWhile(c => c.TimelineStart <= PlayheadPosition).Count();
            CommandManager.Execute(new AddClipCommand(audioTrack, newClip, insertIndex));
            SelectedClip = newClip;
            SelectedTrack = audioTrack;

            UpdatePlaybackState(IsPlaying);
            Notifier.Announce($"Added music '{asset.Name}' at {TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate)} to {audioTrack.Name}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to add music: {ex.Message}");
        }
    }

    [RelayCommand]
    public void AddSelectedAssetAtPlayhead()
    {
        if (SelectedAsset == null)
        {
            Notifier.Announce("No media asset selected.");
            return;
        }

        var targetTrack = SelectedAsset.Type == AssetType.Audio
            ? CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Audio)
            : (SelectedTrack ?? CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Video));

        if (targetTrack == null)
        {
            targetTrack = new Track
            {
                Name = SelectedAsset.Type == AssetType.Audio ? "Audio / Music" : "Video Track",
                Type = SelectedAsset.Type == AssetType.Audio ? TrackType.Audio : TrackType.Video,
                Order = CurrentProject.Tracks.Count
            };
            CurrentProject.Tracks.Add(targetTrack);
        }

        var newClip = new Clip
        {
            Name = SelectedAsset.Name,
            AssetId = SelectedAsset.Id,
            AssetPath = SelectedAsset.FilePath,
            Type = SelectedAsset.Type,
            InPoint = TimeSpan.Zero,
            OutPoint = SelectedAsset.Duration > TimeSpan.Zero ? SelectedAsset.Duration : TimeSpan.FromSeconds(5),
            TimelineStart = PlayheadPosition,
            Volume = 1.0
        };

        int insertIndex = targetTrack.Clips.TakeWhile(c => c.TimelineStart <= PlayheadPosition).Count();
        CommandManager.Execute(new AddClipCommand(targetTrack, newClip, insertIndex));
        SelectedClip = newClip;
        SelectedTrack = targetTrack;

        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Placed '{SelectedAsset.Name}' at {TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate)} on {targetTrack.Name}");
    }

    [RelayCommand]
    public void MoveSelectedClipToPlayhead()
    {
        if (SelectedClip == null || SelectedTrack == null)
        {
            Notifier.Announce("No clip selected to move to current point.");
            return;
        }

        var cmd = new MoveClipCommand(SelectedTrack, SelectedTrack, SelectedClip, PlayheadPosition);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Moved clip '{SelectedClip.Name}' to {TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate)}");
    }

    [RelayCommand]
    public void SplitAtPlayhead()
    {
        if (SelectedTrack == null)
        {
            Notifier.Announce("No track selected to split.");
            return;
        }

        var clipToSplit = SelectedClip ?? _timelineEngine.GetClipAtTimeOnTrack(SelectedTrack, PlayheadPosition);
        if (clipToSplit == null)
        {
            Notifier.Announce("No clip at current playhead position to split.");
            return;
        }

        if (PlayheadPosition <= clipToSplit.TimelineStart || PlayheadPosition >= clipToSplit.TimelineEnd)
        {
            Notifier.Announce("Playhead must be inside the clip to split it.");
            return;
        }

        var cmd = new SplitClipCommand(SelectedTrack, clipToSplit, PlayheadPosition);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        SelectedClip = SelectedTrack.Clips.FirstOrDefault(c => c.TimelineStart == PlayheadPosition);
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void TrimStartAtPlayhead()
    {
        if (SelectedClip == null || SelectedTrack == null)
        {
            Notifier.Announce("No clip selected to trim.");
            return;
        }

        if (PlayheadPosition <= SelectedClip.TimelineStart || PlayheadPosition >= SelectedClip.TimelineEnd)
        {
            Notifier.Announce("Playhead must be inside the selected clip to set In-Point.");
            return;
        }

        var relativeSeconds = (PlayheadPosition - SelectedClip.TimelineStart).TotalSeconds * SelectedClip.Speed;
        var newInPoint = SelectedClip.InPoint + TimeSpan.FromSeconds(relativeSeconds);

        ICommand cmd = IsRippleEditEnabled
            ? new RippleTrimClipCommand(SelectedTrack, SelectedClip, TrimEdge.Start, newInPoint)
            : new TrimClipCommand(SelectedClip, TrimEdge.Start, newInPoint);

        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void TrimEndAtPlayhead()
    {
        if (SelectedClip == null || SelectedTrack == null)
        {
            Notifier.Announce("No clip selected to trim.");
            return;
        }

        if (PlayheadPosition <= SelectedClip.TimelineStart || PlayheadPosition >= SelectedClip.TimelineEnd)
        {
            Notifier.Announce("Playhead must be inside the selected clip to set Out-Point.");
            return;
        }

        var relativeSeconds = (PlayheadPosition - SelectedClip.TimelineStart).TotalSeconds * SelectedClip.Speed;
        var newOutPoint = SelectedClip.InPoint + TimeSpan.FromSeconds(relativeSeconds);

        ICommand cmd = IsRippleEditEnabled
            ? new RippleTrimClipCommand(SelectedTrack, SelectedClip, TrimEdge.End, newOutPoint)
            : new TrimClipCommand(SelectedClip, TrimEdge.End, newOutPoint);

        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void DeleteSelectedClip()
    {
        if (SelectedClip == null || SelectedTrack == null)
        {
            Notifier.Announce("No clip selected to delete.");
            return;
        }

        ICommand cmd = IsRippleEditEnabled
            ? new RippleDeleteClipCommand(SelectedTrack, SelectedClip)
            : new RemoveClipCommand(SelectedTrack, SelectedClip);

        CommandManager.Execute(cmd);
        SelectedClip = null;
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void RippleDeleteSelectedClip()
    {
        if (SelectedClip == null || SelectedTrack == null)
        {
            Notifier.Announce("No clip selected to ripple delete.");
            return;
        }

        var cmd = new RippleDeleteClipCommand(SelectedTrack, SelectedClip);
        CommandManager.Execute(cmd);
        SelectedClip = null;
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void CloseGapAtPlayhead()
    {
        var track = SelectedTrack ?? CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Video);
        if (track == null) return;

        var gap = _timelineEngine.FindGapAt(track, PlayheadPosition);
        if (gap == null)
        {
            Notifier.Announce("No blank gap found at current playhead position.");
            return;
        }

        var cmd = new CloseGapCommand(track, gap);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void CloseAllGapsOnSelectedTrack()
    {
        var track = SelectedTrack ?? CurrentProject.Tracks.FirstOrDefault(t => t.Type == TrackType.Video);
        if (track == null) return;

        var cmd = new CloseAllGapsCommand(track);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void ToggleRippleEditMode()
    {
        IsRippleEditEnabled = !IsRippleEditEnabled;
        Notifier.Announce($"Timeline ripple edit mode {(IsRippleEditEnabled ? "enabled" : "disabled")}");
    }

    [RelayCommand]
    public void ChangeSpeed(double multiplier)
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to change speed.");
            return;
        }

        var cmd = new ChangeClipSpeedCommand(SelectedClip, multiplier);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void ApplyTransition(TransitionType type)
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to set transition.");
            return;
        }

        var transition = type != TransitionType.None ? new Transition { Type = type, Duration = TimeSpan.FromSeconds(1.0) } : null;
        var cmd = new SetClipTransitionCommand(SelectedClip, isTransitionIn: true, transition);
        CommandManager.Execute(cmd);
    }

    [RelayCommand]
    public void DetachAudio()
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to detach audio.");
            return;
        }

        var cmd = new DetachAudioCommand(CurrentProject, SelectedClip);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
    }

    #region Subtitles and Captions Management

    [RelayCommand]
    public void AddCaption(string? text = null)
    {
        var captionText = !string.IsNullOrWhiteSpace(text) ? text : "New Subtitle Text";
        int index = CurrentProject.Captions.Count + 1;
        var start = PlayheadPosition;
        var end = start + TimeSpan.FromSeconds(3);
        var caption = new CaptionItem(index, start, end, captionText);

        var cmd = new AddCaptionCommand(CurrentProject, caption);
        CommandManager.Execute(cmd);
        SelectedCaption = caption;
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Added caption #{index}: '{captionText}' at {start:mm\\:ss}");
    }

    [RelayCommand]
    public void RemoveSelectedCaption()
    {
        if (SelectedCaption == null) return;
        var target = SelectedCaption;
        var cmd = new RemoveCaptionCommand(CurrentProject, target);
        CommandManager.Execute(cmd);
        SelectedCaption = CurrentProject.Captions.FirstOrDefault();
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Removed caption #{target.Index}.");
    }

    [RelayCommand]
    public void CommitCaptionEdit()
    {
        if (SelectedCaption == null) return;

        var start = TimeSpan.FromSeconds(Math.Max(0, CaptionEditorStartSeconds));
        var end = TimeSpan.FromSeconds(Math.Max(CaptionEditorStartSeconds + 0.1, CaptionEditorEndSeconds));
        var newValues = new CaptionItem(
            SelectedCaption.Index,
            start,
            end,
            CaptionEditorText,
            string.IsNullOrWhiteSpace(CaptionEditorSpeaker) ? null : CaptionEditorSpeaker.Trim(),
            CaptionEditorStyle);

        var cmd = new UpdateCaptionCommand(SelectedCaption, newValues);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Saved changes to caption #{SelectedCaption.Index}.");
    }

    [RelayCommand]
    public void SplitSelectedCaptionAtPlayhead()
    {
        if (SelectedCaption == null) return;
        if (PlayheadPosition <= SelectedCaption.StartTime || PlayheadPosition >= SelectedCaption.EndTime)
        {
            Notifier.Announce("Playhead must be inside selected caption to split.");
            return;
        }

        var cmd = new SplitCaptionCommand(CurrentProject, SelectedCaption, PlayheadPosition);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce("Caption split into two parts.");
    }

    [RelayCommand]
    public void MergeSelectedCaptionWithNext()
    {
        if (SelectedCaption == null) return;
        var list = CurrentProject.Captions.OrderBy(c => c.StartTime).ToList();
        int idx = list.IndexOf(SelectedCaption);
        if (idx < 0 || idx >= list.Count - 1)
        {
            Notifier.Announce("No subsequent caption available to merge with.");
            return;
        }

        var next = list[idx + 1];
        var cmd = new MergeCaptionsCommand(CurrentProject, SelectedCaption, next);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Merged caption #{SelectedCaption.Index} with #{next.Index}.");
    }

    [RelayCommand]
    public void ShiftCaptions(double seconds)
    {
        if (CurrentProject.Captions.Count == 0) return;
        var offset = TimeSpan.FromSeconds(seconds);
        var cmd = new ShiftCaptionsCommand(CurrentProject, offset);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Shifted all subtitles by {seconds:+#0.00;-#0.00}s.");
    }

    [RelayCommand]
    public void ShiftCaptionsEarlier() => ShiftCaptions(-0.5);

    [RelayCommand]
    public void ShiftCaptionsLater() => ShiftCaptions(0.5);

    [RelayCommand]
    public void ValidateSubtitles()
    {
        var issues = SrtSubtitleService.ValidateCaptions(CurrentProject.Captions);
        if (issues.Count == 0)
        {
            SubtitleValidationSummary = $"✓ All {CurrentProject.Captions.Count} captions are perfectly valid. No timing or reading speed issues detected.";
            Notifier.Announce("Subtitle validation passed with 0 issues.");
        }
        else
        {
            SubtitleValidationSummary = $"Found {issues.Count} issue(s):\n" + string.Join("\n", issues.Take(4).Select(i => $"• {i.Message}"));
            Notifier.Announce($"Subtitle validation found {issues.Count} potential issues.");
        }
    }

    [RelayCommand]
    public void ImportSubtitlesFile(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var items = ext == ".vtt"
                ? SrtSubtitleService.LoadVttFile(filePath)
                : SrtSubtitleService.LoadSrtFile(filePath);

            CurrentProject.Captions.Clear();
            foreach (var item in items)
            {
                CurrentProject.Captions.Add(item);
            }
            SelectedCaption = CurrentProject.Captions.FirstOrDefault();
            UpdatePlaybackState(IsPlaying);
            Notifier.Announce($"Imported {items.Count} subtitles from {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to import subtitles: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ExportSubtitlesFile(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".vtt")
            {
                SrtSubtitleService.SaveVttFile(CurrentProject.Captions, filePath);
            }
            else
            {
                SrtSubtitleService.SaveSrtFile(CurrentProject.Captions, filePath);
            }
            Notifier.Announce($"Exported subtitles to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to export subtitles: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ImportSrtFile(string filePath) => ImportSubtitlesFile(filePath);

    [RelayCommand]
    public void ExportSrtFile(string filePath) => ExportSubtitlesFile(filePath);

    #endregion

    #region Titles and Animated Text Overlays Management

    [RelayCommand]
    public void AddTitleCardPreset(TitleCardStyle style)
    {
        TextOverlay overlay = style switch
        {
            TitleCardStyle.LowerThird => TitleCardPresets.CreateLowerThird("Presenter Name", "Designation", PlayheadPosition),
            TitleCardStyle.CinematicHeader => TitleCardPresets.CreateCinematicHeader("EPISODE TITLE", PlayheadPosition),
            TitleCardStyle.BoldHeadline => TitleCardPresets.CreateBoldHeadline("BREAKING HIGHLIGHT", PlayheadPosition),
            TitleCardStyle.Minimalist => TitleCardPresets.CreateMinimalist("Chapter One: The Beginning", PlayheadPosition),
            TitleCardStyle.NeonGlow => TitleCardPresets.CreateNeonGlow("CYBER SOUNDS", PlayheadPosition),
            TitleCardStyle.VintageRetro => TitleCardPresets.CreateVintageRetro("Classic Chronicles", PlayheadPosition),
            _ => new TextOverlay { StartTime = PlayheadPosition, Text = "Title Overlay" }
        };

        var cmd = new AddTextOverlayCommand(CurrentProject, overlay);
        CommandManager.Execute(cmd);
        SelectedOverlay = overlay;
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Added {style} title card at {PlayheadPosition:mm\\:ss}.");
    }

    [RelayCommand] public void AddLowerThirdPreset() => AddTitleCardPreset(TitleCardStyle.LowerThird);
    [RelayCommand] public void AddCinematicHeaderPreset() => AddTitleCardPreset(TitleCardStyle.CinematicHeader);
    [RelayCommand] public void AddBoldHeadlinePreset() => AddTitleCardPreset(TitleCardStyle.BoldHeadline);
    [RelayCommand] public void AddMinimalistPreset() => AddTitleCardPreset(TitleCardStyle.Minimalist);
    [RelayCommand] public void AddNeonGlowPreset() => AddTitleCardPreset(TitleCardStyle.NeonGlow);
    [RelayCommand] public void AddVintageRetroPreset() => AddTitleCardPreset(TitleCardStyle.VintageRetro);

    [RelayCommand] public void ApplyLowerThirdPreset() => ApplyPresetToSelectedOverlay(TitleCardStyle.LowerThird);
    [RelayCommand] public void ApplyCinematicHeaderPreset() => ApplyPresetToSelectedOverlay(TitleCardStyle.CinematicHeader);
    [RelayCommand] public void ApplyBoldHeadlinePreset() => ApplyPresetToSelectedOverlay(TitleCardStyle.BoldHeadline);
    [RelayCommand] public void ApplyNeonGlowPreset() => ApplyPresetToSelectedOverlay(TitleCardStyle.NeonGlow);

    [RelayCommand]
    public void AddDefaultOverlay()
    {
        AddTitleCardPreset(TitleCardStyle.CinematicHeader);
    }

    [RelayCommand]
    public void RemoveSelectedOverlay()
    {
        if (SelectedOverlay == null) return;
        var target = SelectedOverlay;
        var cmd = new RemoveTextOverlayCommand(CurrentProject, target);
        CommandManager.Execute(cmd);
        SelectedOverlay = CurrentProject.Overlays.FirstOrDefault();
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Removed title overlay '{target.Text}'.");
    }

    [RelayCommand]
    public void CommitOverlayEdit()
    {
        if (SelectedOverlay == null) return;

        var start = TimeSpan.FromSeconds(Math.Max(0, OverlayEditorStartSeconds));
        var duration = TimeSpan.FromSeconds(Math.Max(0.5, OverlayEditorDurationSeconds));

        var newValues = new TextOverlay
        {
            Text = OverlayEditorText,
            SubtitleText = string.IsNullOrWhiteSpace(OverlayEditorSubtitle) ? null : OverlayEditorSubtitle,
            FontName = OverlayEditorFont,
            FontSize = OverlayEditorFontSize,
            ColorHex = OverlayEditorColor,
            BackgroundColorHex = string.IsNullOrWhiteSpace(OverlayEditorBackground) ? null : OverlayEditorBackground,
            Style = OverlayEditorStyle,
            Animation = OverlayEditorAnimation,
            AnimationDuration = OverlayEditorAnimationDuration,
            StartTime = start,
            Duration = duration,
            Alignment = OverlayEditorAlignment,
            PositionX = OverlayEditorPosX,
            PositionY = OverlayEditorPosY,
            IsBold = OverlayEditorIsBold,
            IsItalic = OverlayEditorIsItalic,
            BorderColorHex = string.IsNullOrWhiteSpace(OverlayEditorBorderColor) ? null : OverlayEditorBorderColor,
            BorderWidth = OverlayEditorBorderWidth
        };

        var cmd = new UpdateTextOverlayCommand(SelectedOverlay, newValues);
        CommandManager.Execute(cmd);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Updated title card '{SelectedOverlay.Text}'.");
    }

    [RelayCommand]
    public void ApplyPresetToSelectedOverlay(TitleCardStyle style)
    {
        if (SelectedOverlay == null) return;

        TextOverlay template = style switch
        {
            TitleCardStyle.LowerThird => TitleCardPresets.CreateLowerThird(),
            TitleCardStyle.CinematicHeader => TitleCardPresets.CreateCinematicHeader(),
            TitleCardStyle.BoldHeadline => TitleCardPresets.CreateBoldHeadline(),
            TitleCardStyle.Minimalist => TitleCardPresets.CreateMinimalist(),
            TitleCardStyle.NeonGlow => TitleCardPresets.CreateNeonGlow(),
            TitleCardStyle.VintageRetro => TitleCardPresets.CreateVintageRetro(),
            _ => new TextOverlay()
        };

        var cmd = new ApplyTitleCardPresetCommand(SelectedOverlay, template);
        CommandManager.Execute(cmd);
        OnSelectedOverlayChanged(SelectedOverlay);
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Applied {style} preset to '{SelectedOverlay.Text}'.");
    }

    [RelayCommand]
    public void DuplicateSelectedOverlay()
    {
        if (SelectedOverlay == null) return;

        var dup = SelectedOverlay.Clone();
        dup.StartTime = PlayheadPosition;
        var cmd = new AddTextOverlayCommand(CurrentProject, dup);
        CommandManager.Execute(cmd);
        SelectedOverlay = dup;
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Duplicated title card at {PlayheadPosition:mm\\:ss}.");
    }

    #endregion

    [RelayCommand]
    public void Undo()
    {
        if (CommandManager.CanUndo)
        {
            CommandManager.Undo();
            _audioCues.PlayUndoCue();
            UpdatePlaybackState(IsPlaying);
        }
        else
        {
            Notifier.Announce("Nothing to undo.");
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (CommandManager.CanRedo)
        {
            CommandManager.Redo();
            _audioCues.PlayRedoCue();
            UpdatePlaybackState(IsPlaying);
        }
        else
        {
            Notifier.Announce("Nothing to redo.");
        }
    }

    [RelayCommand]
    public void StepForward(int frameCount = 1)
    {
        var frameDelta = TimeSpan.FromSeconds(frameCount / CurrentProject.FrameRate);
        SeekTo(PlayheadPosition + frameDelta);
    }

    [RelayCommand]
    public void StepBackward(int frameCount = 1)
    {
        var frameDelta = TimeSpan.FromSeconds(frameCount / CurrentProject.FrameRate);
        var target = PlayheadPosition - frameDelta;
        SeekTo(target < TimeSpan.Zero ? TimeSpan.Zero : target);
    }

    [RelayCommand]
    public void SelectNextClip()
    {
        if (SelectedTrack == null || SelectedTrack.Clips.Count == 0) return;

        int currentIndex = SelectedClip != null ? SelectedTrack.Clips.IndexOf(SelectedClip) : -1;
        int nextIndex = Math.Min(SelectedTrack.Clips.Count - 1, currentIndex + 1);
        SelectedClip = SelectedTrack.Clips[nextIndex];
        SeekTo(SelectedClip.TimelineStart);
        Notifier.AnnounceClipSelected(SelectedClip, nextIndex, SelectedTrack.Clips.Count);
    }

    [RelayCommand]
    public void SelectPreviousClip()
    {
        if (SelectedTrack == null || SelectedTrack.Clips.Count == 0) return;

        int currentIndex = SelectedClip != null ? SelectedTrack.Clips.IndexOf(SelectedClip) : 1;
        int prevIndex = Math.Max(0, currentIndex - 1);
        SelectedClip = SelectedTrack.Clips[prevIndex];
        SeekTo(SelectedClip.TimelineStart);
        Notifier.AnnounceClipSelected(SelectedClip, prevIndex, SelectedTrack.Clips.Count);
    }

    public void SeekTo(TimeSpan targetTime)
    {
        var clamped = targetTime < TimeSpan.Zero ? TimeSpan.Zero : targetTime;
        if (CurrentProject.Duration > TimeSpan.Zero && clamped > CurrentProject.Duration)
        {
            clamped = CurrentProject.Duration;
        }

        if (IsSnappingEnabled && !IsPlaying)
        {
            clamped = _timelineEngine.SnapTime(CurrentProject, clamped);
        }

        PlayheadPosition = clamped;
        CurrentTimecode = TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate);
        OnPropertyChanged(nameof(PlayheadSeconds));

        if (IsPlaying)
        {
            _playbackStartTime = PlayheadPosition;
            _playbackStopwatch.Restart();
        }

        UpdatePlaybackState(IsPlaying);
        _audioCues.PlaySeekCue();
    }

    [RelayCommand]
    public async Task ExportVideoAsync(ExportSettings settings)
    {
        if (IsPlaying) PausePlayback();

        IsExporting = true;
        ExportProgress = 0.0;
        Notifier.Announce($"Exporting video to {Path.GetFileName(settings.OutputPath)}...");

        var progress = new Progress<double>(p => ExportProgress = p);
        var result = await _mediaExporter.ExportProjectAsync(CurrentProject, settings, progress);

        IsExporting = false;
        if (result.Success)
        {
            Notifier.Announce($"Export completed successfully! File saved to {result.OutputFilePath}");
        }
        else
        {
            Notifier.Announce($"Export failed: {result.ErrorMessage}");
        }
    }

    // Phase 8: Audio Fade Commands
    [RelayCommand]
    public void SetClipAudioFade(Tuple<double, double> fades)
    {
        if (SelectedClip == null) return;
        var cmd = new SetAudioFadeCommand(SelectedClip, fades.Item1, fades.Item2);
        CommandManager.Execute(cmd);
    }

    // Phase 9: Transformation Commands
    [RelayCommand]
    public void RotateSelectedClip(int angle = -1)
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to rotate.");
            return;
        }
        var cmd = new RotateClipCommand(SelectedClip, angle);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
    }

    [RelayCommand]
    public void ToggleFlipHorizontal()
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to flip.");
            return;
        }
        var cmd = new ToggleClipFlipCommand(SelectedClip, true);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
    }

    [RelayCommand]
    public void ToggleFlipVertical()
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected to flip.");
            return;
        }
        var cmd = new ToggleClipFlipCommand(SelectedClip, false);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
    }

    [RelayCommand]
    public void SetSelectedClipPanZoom(PanZoomType type)
    {
        if (SelectedClip == null)
        {
            Notifier.Announce("No clip selected for Pan & Zoom.");
            return;
        }
        var cmd = new SetPanZoomCommand(SelectedClip, type);
        CommandManager.Execute(cmd);
    }

    // Phase 10: Marker & Snapping Commands
    [RelayCommand]
    public void AddMarkerAtPlayhead()
    {
        var markerNumber = CurrentProject.Markers.Count + 1;
        var newMarker = new TimelineMarker(PlayheadPosition, $"Marker {markerNumber}", "#FFCC00");
        var cmd = new AddMarkerCommand(CurrentProject, newMarker);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        Notifier.Announce($"Added marker at {TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate)}");
    }

    [RelayCommand]
    public void JumpToNextMarker()
    {
        var nextMarker = CurrentProject.Markers
            .Where(m => m.Time > PlayheadPosition)
            .OrderBy(m => m.Time)
            .FirstOrDefault();

        if (nextMarker != null)
        {
            SeekTo(nextMarker.Time);
            Notifier.Announce($"Jumped to {nextMarker.Label} at {TimelineEngine.FormatTimecode(nextMarker.Time, CurrentProject.FrameRate)}");
        }
        else
        {
            Notifier.Announce("No more markers ahead.");
        }
    }

    [RelayCommand]
    public void JumpToPreviousMarker()
    {
        var prevMarker = CurrentProject.Markers
            .Where(m => m.Time < PlayheadPosition)
            .OrderByDescending(m => m.Time)
            .FirstOrDefault();

        if (prevMarker != null)
        {
            SeekTo(prevMarker.Time);
            Notifier.Announce($"Jumped to {prevMarker.Label} at {TimelineEngine.FormatTimecode(prevMarker.Time, CurrentProject.FrameRate)}");
        }
        else
        {
            Notifier.Announce("No previous markers.");
        }
    }

    [RelayCommand]
    public void DeleteMarker(TimelineMarker marker)
    {
        if (marker == null) return;
        var cmd = new RemoveMarkerCommand(CurrentProject, marker);
        CommandManager.Execute(cmd);
        Notifier.Announce($"Removed {marker.Label}");
    }

    [RelayCommand]
    public void ToggleSnapping()
    {
        IsSnappingEnabled = !IsSnappingEnabled;
        Notifier.Announce($"Timeline magnetic snapping {(IsSnappingEnabled ? "enabled" : "disabled")}");
    }
}
