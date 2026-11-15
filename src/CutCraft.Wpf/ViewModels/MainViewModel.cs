using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CutCraft.Core.Accessibility;
using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Serialization;
using CutCraft.Core.Subtitles;
using CutCraft.Core.Timeline;
using CutCraft.Media.Analysis;
using CutCraft.Media.Export;

namespace CutCraft.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TimelineEngine _timelineEngine = new();
    private readonly IMediaAnalyzer _mediaAnalyzer = new FFprobeMediaAnalyzer();
    private readonly IMediaExporter _mediaExporter = new FFmpegProcessExporter();
    private readonly IAudioCueService _audioCues = new SystemAudioCueService();

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
    private double _zoomLevel = 100.0; // pixels per second

    [ObservableProperty]
    private string _accessibilityStatus = "Ready. Press F1 for keyboard shortcuts.";

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private double _exportProgress;

    public CommandManager CommandManager { get; } = new();
    public TimelineAccessibilityNotifier Notifier { get; }

    public MainViewModel()
    {
        Notifier = new TimelineAccessibilityNotifier();
        Notifier.SpokenMessageGenerated += (s, msg) => AccessibilityStatus = msg;
        CommandManager.AccessibilityNotification += (s, msg) => AccessibilityStatus = msg;

        SelectedTrack = CurrentProject.Tracks.FirstOrDefault();
    }

    [RelayCommand]
    public void NewProject()
    {
        CurrentProject = Project.CreateDefault("New Video Project");
        CommandManager.Clear();
        PlayheadPosition = TimeSpan.Zero;
        SelectedClip = null;
        SelectedTrack = CurrentProject.Tracks.FirstOrDefault();
        Notifier.Announce("Created new project.");
    }

    [RelayCommand]
    public void OpenProject(string filePath)
    {
        try
        {
            CurrentProject = ProjectSerializer.LoadFromFile(filePath);
            CommandManager.Clear();
            PlayheadPosition = TimeSpan.Zero;
            SelectedClip = null;
            SelectedTrack = CurrentProject.Tracks.FirstOrDefault();
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
        PlayheadPosition = targetTime;
        CurrentTimecode = TimelineEngine.FormatTimecode(PlayheadPosition, CurrentProject.FrameRate);
        _audioCues.PlaySeekCue();
    }

    [RelayCommand]
    public async Task ExportVideoAsync(ExportSettings settings)
    {
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
