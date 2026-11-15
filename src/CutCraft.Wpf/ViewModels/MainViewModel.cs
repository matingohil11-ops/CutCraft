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

namespace CutCraft.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TimelineEngine _timelineEngine = new();
    private readonly IMediaAnalyzer _mediaAnalyzer = new FFprobeMediaAnalyzer();
    private readonly IMediaExporter _mediaExporter = new FFmpegProcessExporter();
    private readonly IAudioCueService _audioCues = new SystemAudioCueService();
    private readonly UpdateManager _updateManager = new();

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
    private string _appVersionDisplay = "v0.0";

    [ObservableProperty]
    private string _updateButtonText = "🔄 Check Updates";

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
        if (CurrentProject.Duration > TimeSpan.Zero && PlayheadPosition >= CurrentProject.Duration)
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
            ActiveVideoPath = videoClip.AssetPath;
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
            ActiveCaptionText = activeCap.Text;
            ActiveCaptionVisibility = Visibility.Visible;
        }
        else
        {
            ActiveCaptionText = "";
            ActiveCaptionVisibility = Visibility.Collapsed;
        }

        PreviewStateChanged?.Invoke(ActiveVideoPath, ActiveVideoPosition, isPlaying);
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

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        try
        {
            UpdateButtonText = "🔄 Checking...";
            Notifier.Announce("Checking for CutCraft updates...");
            var result = await _updateManager.CheckForUpdatesAsync();
            UpdateButtonText = result.HasUpdate ? $"⬆ Update to {result.LatestVersion}" : "✔ v0.0 Up to date";
            Notifier.Announce(result.Message);
        }
        catch (Exception ex)
        {
            UpdateButtonText = "🔄 Check Updates";
            Notifier.Announce($"Update check failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ShowAbout()
    {
        Notifier.Announce($"CutCraft {AppVersionDisplay} - Accessible C# Desktop Video Editor");
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
                var newClip = new Clip
                {
                    Name = asset.Name,
                    AssetId = asset.Id,
                    AssetPath = asset.FilePath,
                    Type = asset.Type,
                    InPoint = TimeSpan.Zero,
                    OutPoint = asset.Duration > TimeSpan.Zero ? asset.Duration : TimeSpan.FromSeconds(5),
                    TimelineStart = targetTrack.Duration
                };

                CommandManager.Execute(new AddClipCommand(targetTrack, newClip));
                SelectedClip = newClip;
            }

            UpdatePlaybackState(IsPlaying);
            Notifier.Announce($"Imported {asset.Name} ({asset.Duration.TotalSeconds:F1}s)");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to import media: {ex.Message}");
        }
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
        if (SelectedClip == null)
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

        var cmd = new TrimClipCommand(SelectedClip, TrimEdge.Start, newInPoint);
        CommandManager.Execute(cmd);
        _audioCues.PlayCutCue();
        UpdatePlaybackState(IsPlaying);
    }

    [RelayCommand]
    public void TrimEndAtPlayhead()
    {
        if (SelectedClip == null)
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

        var cmd = new TrimClipCommand(SelectedClip, TrimEdge.End, newOutPoint);
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

        var cmd = new RemoveClipCommand(SelectedTrack, SelectedClip);
        CommandManager.Execute(cmd);
        SelectedClip = null;
        UpdatePlaybackState(IsPlaying);
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

    [RelayCommand]
    public void AddCaption(string text)
    {
        int index = CurrentProject.Captions.Count + 1;
        var start = PlayheadPosition;
        var end = start + TimeSpan.FromSeconds(3);
        var caption = new CaptionItem(index, start, end, text);

        CurrentProject.Captions.Add(caption);
        SelectedCaption = caption;
        UpdatePlaybackState(IsPlaying);
        Notifier.Announce($"Added caption: '{text}' at {start:mm\\:ss}");
    }

    [RelayCommand]
    public void ImportSrtFile(string filePath)
    {
        try
        {
            var items = SrtSubtitleService.LoadSrtFile(filePath);
            CurrentProject.Captions.Clear();
            foreach (var item in items)
            {
                CurrentProject.Captions.Add(item);
            }
            UpdatePlaybackState(IsPlaying);
            Notifier.Announce($"Imported {items.Count} subtitles from {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to import SRT: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ExportSrtFile(string filePath)
    {
        try
        {
            SrtSubtitleService.SaveSrtFile(CurrentProject.Captions, filePath);
            Notifier.Announce($"Exported subtitles to {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Notifier.Announce($"Failed to export SRT: {ex.Message}");
        }
    }

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
}
