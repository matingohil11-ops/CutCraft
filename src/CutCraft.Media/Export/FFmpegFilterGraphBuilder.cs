using System.Globalization;
using System.IO;
using System.Text;
using CutCraft.Core.Models;

namespace CutCraft.Media.Export;

public class FFmpegCommandArgs
{
    public List<string> InputFiles { get; set; } = new();
    public string FilterComplex { get; set; } = string.Empty;
    public string VideoMap { get; set; } = string.Empty;
    public string AudioMap { get; set; } = string.Empty;
    public string FullCommandLine { get; set; } = string.Empty;
}

public class FFmpegFilterGraphBuilder
{
    public FFmpegCommandArgs BuildExportCommand(Project project, ExportSettings settings)
    {
        var (width, height) = settings.GetDimensions();
        var fps = settings.FrameRate > 0 ? settings.FrameRate : project.FrameRate;
        var inputs = new List<string>();
        var inputIndexMap = new Dictionary<string, int>();

        int GetInputIndex(string path)
        {
            if (inputIndexMap.TryGetValue(path, out var idx))
                return idx;
            idx = inputs.Count;
            inputs.Add(path);
            inputIndexMap[path] = idx;
            return idx;
        }

        var videoTracks = project.Tracks.Where(t => t.Type == TrackType.Video && !t.IsMuted).OrderBy(t => t.Order).ToList();
        var audioTracks = project.Tracks.Where(t => t.Type == TrackType.Audio && !t.IsMuted).OrderBy(t => t.Order).ToList();

        var filterGraph = new StringBuilder();
        var videoSegmentLabels = new List<string>();
        var audioSegmentLabels = new List<string>();

        int segIndex = 0;
        foreach (var track in videoTracks)
        {
            foreach (var clip in track.Clips.OrderBy(c => c.TimelineStart))
            {
                if (string.IsNullOrEmpty(clip.AssetPath) || !File.Exists(clip.AssetPath))
                    continue;

                int inIdx = GetInputIndex(clip.AssetPath);
                string vLabel = $"v_seg_{segIndex}";
                string aLabel = $"a_seg_{segIndex}";

                double inSec = clip.InPoint.TotalSeconds;
                double outSec = clip.OutPoint.TotalSeconds;
                double speed = Math.Max(0.1, clip.Speed);

                // Video filter chain for this clip
                var vFilters = new List<string>();
                vFilters.Add($"trim=start={inSec.ToString("F3", CultureInfo.InvariantCulture)}:end={outSec.ToString("F3", CultureInfo.InvariantCulture)}");
                vFilters.Add($"setpts={((1.0 / speed)).ToString("F4", CultureInfo.InvariantCulture)}*(PTS-STARTPTS)");
                vFilters.Add($"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2");
                vFilters.Add($"fps={fps.ToString("F2", CultureInfo.InvariantCulture)}");

                // Clip effects
                foreach (var effect in clip.Effects)
                {
                    ApplyEffectFilter(effect, vFilters, clip);
                }

                filterGraph.Append($"[{inIdx}:v]{string.Join(",", vFilters)}[{vLabel}];\n");
                videoSegmentLabels.Add($"[{vLabel}]");

                // Audio filter chain for this clip (if not muted)
                if (!clip.IsMuted)
                {
                    var aFilters = new List<string>();
                    aFilters.Add($"atrim=start={inSec.ToString("F3", CultureInfo.InvariantCulture)}:end={outSec.ToString("F3", CultureInfo.InvariantCulture)}");
                    aFilters.Add("asetpts=PTS-STARTPTS");

                    // Tempo/Speed for audio
                    if (Math.Abs(speed - 1.0) > 0.01)
                    {
                        aFilters.Add($"atempo={speed.ToString("F2", CultureInfo.InvariantCulture)}");
                    }

                    if (Math.Abs(clip.Volume - 1.0) > 0.01)
                    {
                        aFilters.Add($"volume={clip.Volume.ToString("F2", CultureInfo.InvariantCulture)}");
                    }

                    filterGraph.Append($"[{inIdx}:a]{string.Join(",", aFilters)}[{aLabel}];\n");
                    audioSegmentLabels.Add($"[{aLabel}]");
                }

                segIndex++;
            }
        }

        string finalVideoOut = "v_out";
        string finalAudioOut = "a_out";

        // Concat video segments
        if (videoSegmentLabels.Count > 1)
        {
            filterGraph.Append($"{string.Join("", videoSegmentLabels)}concat=n={videoSegmentLabels.Count}:v=1:a=0[v_concat];\n");
            finalVideoOut = "v_concat";
        }
        else if (videoSegmentLabels.Count == 1)
        {
            finalVideoOut = videoSegmentLabels[0].Trim('[', ']');
        }
        else
        {
            // Empty project fallback: black screen
            filterGraph.Append($"color=c=black:s={width}x{height}:r={fps}:d=5[v_empty];\n");
            finalVideoOut = "v_empty";
        }

        // Apply text overlays over the concatenated video
        if (project.Overlays.Count > 0)
        {
            string currentOverlayInput = finalVideoOut;
            for (int i = 0; i < project.Overlays.Count; i++)
            {
                var overlay = project.Overlays[i];
                string nextOverlayOut = $"v_overlay_{i}";
                int posX = (int)(overlay.PositionX * width);
                int posY = (int)(overlay.PositionY * height);
                double tStart = overlay.StartTime.TotalSeconds;
                double tEnd = (overlay.StartTime + overlay.Duration).TotalSeconds;

                var safeText = EscapeDrawText(overlay.Text);
                var drawTextFilter = $"drawtext=text='{safeText}':fontsize={overlay.FontSize}:fontcolor={overlay.ColorHex.Replace("#", "0x")}:x={posX}:y={posY}:enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";

                filterGraph.Append($"[{currentOverlayInput}]{drawTextFilter}[{nextOverlayOut}];\n");
                currentOverlayInput = nextOverlayOut;
            }
            finalVideoOut = currentOverlayInput;
        }

        // Concat & Mix Audio
        var musicAudioLabels = new List<string>();
        foreach (var aTrack in audioTracks)
        {
            foreach (var clip in aTrack.Clips.OrderBy(c => c.TimelineStart))
            {
                if (string.IsNullOrEmpty(clip.AssetPath) || !File.Exists(clip.AssetPath)) continue;
                int aInIdx = GetInputIndex(clip.AssetPath);
                string mLabel = $"m_seg_{segIndex++}";

                double inSec = clip.InPoint.TotalSeconds;
                double outSec = clip.OutPoint.TotalSeconds;
                double delayMs = clip.TimelineStart.TotalMilliseconds;

                var aFilters = new List<string>();
                aFilters.Add($"atrim=start={inSec.ToString("F3", CultureInfo.InvariantCulture)}:end={outSec.ToString("F3", CultureInfo.InvariantCulture)}");
                aFilters.Add("asetpts=PTS-STARTPTS");
                if (delayMs > 0)
                {
                    aFilters.Add($"adelay={delayMs:F0}|{delayMs:F0}");
                }
                if (Math.Abs(clip.Volume - 1.0) > 0.01)
                {
                    aFilters.Add($"volume={clip.Volume.ToString("F2", CultureInfo.InvariantCulture)}");
                }

                filterGraph.Append($"[{aInIdx}:a]{string.Join(",", aFilters)}[{mLabel}];\n");
                musicAudioLabels.Add($"[{mLabel}]");
            }
        }

        // Combine video audio & music audio
        var allAudioLabels = new List<string>();
        if (audioSegmentLabels.Count > 1)
        {
            filterGraph.Append($"{string.Join("", audioSegmentLabels)}concat=n={audioSegmentLabels.Count}:v=0:a=1[a_video_concat];\n");
            allAudioLabels.Add("[a_video_concat]");
        }
        else if (audioSegmentLabels.Count == 1)
        {
            allAudioLabels.Add(audioSegmentLabels[0]);
        }

        allAudioLabels.AddRange(musicAudioLabels);

        if (allAudioLabels.Count > 1)
        {
            filterGraph.Append($"{string.Join("", allAudioLabels)}amix=inputs={allAudioLabels.Count}:duration=first:dropout_transition=2[a_mixed];\n");
            finalAudioOut = "a_mixed";
        }
        else if (allAudioLabels.Count == 1)
        {
            finalAudioOut = allAudioLabels[0].Trim('[', ']');
        }
        else
        {
            // Silent audio fallback
            filterGraph.Append("anullsrc=r=48000:cl=stereo:d=5[a_silent];\n");
            finalAudioOut = "a_silent";
        }

        // Build command line
        var sb = new StringBuilder();
        sb.Append("-y ");
        foreach (var file in inputs)
        {
            sb.Append($"-i \"{file}\" ");
        }

        var filterComplexStr = filterGraph.ToString().TrimEnd('\n', ';');
        sb.Append($"-filter_complex \"{filterComplexStr}\" ");
        sb.Append($"-map \"[{finalVideoOut}]\" -map \"[{finalAudioOut}]\" ");
        sb.Append($"-c:v {settings.VideoCodec} -preset {settings.Preset} -b:v {settings.VideoBitrateKbps}k ");
        sb.Append($"-c:a {settings.AudioCodec} -b:a {settings.AudioBitrateKbps}k ");
        sb.Append($"\"{settings.OutputPath}\"");

        return new FFmpegCommandArgs
        {
            InputFiles = inputs,
            FilterComplex = filterComplexStr,
            VideoMap = $"[{finalVideoOut}]",
            AudioMap = $"[{finalAudioOut}]",
            FullCommandLine = sb.ToString()
        };
    }

    private static void ApplyEffectFilter(ClipEffect effect, List<string> vFilters, Clip clip)
    {
        switch (effect.Type)
        {
            case EffectType.Brightness:
                var brightness = effect.Parameters.GetValueOrDefault("value", 0.0);
                vFilters.Add($"eq=brightness={brightness.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
            case EffectType.Contrast:
                var contrast = effect.Parameters.GetValueOrDefault("value", 1.0);
                vFilters.Add($"eq=contrast={contrast.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
            case EffectType.Saturation:
                var saturation = effect.Parameters.GetValueOrDefault("value", 1.0);
                vFilters.Add($"eq=saturation={saturation.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
            case EffectType.BlackAndWhite:
                vFilters.Add("hue=s=0");
                break;
            case EffectType.Blur:
                var radius = effect.Parameters.GetValueOrDefault("radius", 5.0);
                vFilters.Add($"boxblur={radius:F0}:1");
                break;
            case EffectType.FadeIn:
                var inDuration = effect.Parameters.GetValueOrDefault("duration", 1.0);
                vFilters.Add($"fade=t=in:st=0:d={inDuration.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
            case EffectType.FadeOut:
                var outDuration = effect.Parameters.GetValueOrDefault("duration", 1.0);
                var clipDur = clip.TimelineDuration.TotalSeconds;
                var outStart = Math.Max(0, clipDur - outDuration);
                vFilters.Add($"fade=t=out:st={outStart.ToString("F2", CultureInfo.InvariantCulture)}:d={outDuration.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
        }
    }

    private static string EscapeDrawText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace(":", "\\:")
            .Replace("%", "\\%");
    }
}
