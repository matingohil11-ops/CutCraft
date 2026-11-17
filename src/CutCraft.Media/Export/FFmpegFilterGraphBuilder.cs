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

        var baseVideoTrack = project.Tracks.FirstOrDefault(t => t.Type == TrackType.Video && !t.IsMuted);
        var overlayTracks = project.Tracks.Where(t => (t.Type == TrackType.Overlay || (t.Type == TrackType.Video && t != baseVideoTrack)) && !t.IsMuted).OrderBy(t => t.Order).ToList();
        var audioTracks = project.Tracks.Where(t => t.Type == TrackType.Audio && !t.IsMuted).OrderBy(t => t.Order).ToList();

        var filterGraph = new StringBuilder();
        var baseVideoSegments = new List<string>();
        var baseAudioSegments = new List<string>();

        int segIndex = 0;

        // 1. Process Base Video Track (clips, transitions, filters, speeds)
        if (baseVideoTrack != null)
        {
            var orderedClips = baseVideoTrack.Clips.OrderBy(c => c.TimelineStart).ToList();
            for (int i = 0; i < orderedClips.Count; i++)
            {
                var clip = orderedClips[i];
                if (string.IsNullOrEmpty(clip.AssetPath) || !File.Exists(clip.AssetPath))
                    continue;

                int inIdx = GetInputIndex(clip.AssetPath);
                string vLabel = $"v_seg_{segIndex}";
                string aLabel = $"a_seg_{segIndex}";

                double inSec = clip.InPoint.TotalSeconds;
                double outSec = clip.OutPoint.TotalSeconds;
                double speed = Math.Max(0.1, clip.Speed);

                // Video filters
                var vFilters = new List<string>();
                if (clip.Type == AssetType.Image)
                {
                    vFilters.Add("loop=loop=-1:size=1:start=0");
                }
                vFilters.Add($"trim=start={inSec.ToString("F3", CultureInfo.InvariantCulture)}:end={outSec.ToString("F3", CultureInfo.InvariantCulture)}");
                vFilters.Add($"setpts={((1.0 / speed)).ToString("F4", CultureInfo.InvariantCulture)}*(PTS-STARTPTS)");
                vFilters.Add($"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2");
                vFilters.Add($"fps={fps.ToString("F2", CultureInfo.InvariantCulture)}");

                // Phase 9: Apply clip transformations (Orientation, Flip, Pan & Zoom)
                ApplyClipTransformFilters(clip, vFilters, width, height);

                // Apply clip effects
                foreach (var effect in clip.Effects)
                {
                    ApplyVideoEffectFilter(effect, vFilters, clip);
                }

                filterGraph.Append($"[{inIdx}:v]{string.Join(",", vFilters)}[{vLabel}];\n");
                baseVideoSegments.Add($"[{vLabel}]");

                // Audio filters
                if (!clip.IsMuted)
                {
                    var aFilters = new List<string>();
                    aFilters.Add($"atrim=start={inSec.ToString("F3", CultureInfo.InvariantCulture)}:end={outSec.ToString("F3", CultureInfo.InvariantCulture)}");
                    aFilters.Add("asetpts=PTS-STARTPTS");

                    // Multi-stage atempo for speeds beyond 0.5 - 2.0 range
                    ApplyAudioTempoFilters(speed, aFilters);

                    if (Math.Abs(clip.Volume - 1.0) > 0.01)
                    {
                        aFilters.Add($"volume={clip.Volume.ToString("F2", CultureInfo.InvariantCulture)}");
                    }

                    // Phase 8: Apply audio fade in / fade out
                    ApplyClipAudioFadeFilters(clip, aFilters);

                    // Apply audio effects (fades)
                    foreach (var effect in clip.Effects)
                    {
                        ApplyAudioEffectFilter(effect, aFilters, clip);
                    }

                    filterGraph.Append($"[{inIdx}:a]{string.Join(",", aFilters)}[{aLabel}];\n");
                    baseAudioSegments.Add($"[{aLabel}]");
                }

                segIndex++;
            }
        }

        string currentBaseVideoOut = "v_base_out";

        // Connect base video segments with transitions (xfade) or concat
        if (baseVideoSegments.Count > 1)
        {
            var orderedClips = baseVideoTrack!.Clips.OrderBy(c => c.TimelineStart).ToList();
            bool hasXfade = orderedClips.Any(c => c.TransitionIn != null && c.TransitionIn.Type != TransitionType.None);

            if (hasXfade)
            {
                // Chain xfade transitions between clips
                string currentXfadeInput = baseVideoSegments[0].Trim('[', ']');
                double runningOffset = orderedClips[0].TimelineDuration.TotalSeconds;

                for (int i = 1; i < baseVideoSegments.Count; i++)
                {
                    var nextSeg = baseVideoSegments[i].Trim('[', ']');
                    string xfadeOut = $"v_xfade_{i}";
                    var trans = orderedClips[i].TransitionIn ?? new Transition { Type = TransitionType.CrossFade, Duration = TimeSpan.FromSeconds(1) };
                    double transDur = Math.Min(trans.Duration.TotalSeconds, orderedClips[i].TimelineDuration.TotalSeconds * 0.5);
                    double offset = Math.Max(0.1, runningOffset - transDur);

                    string transName = trans.ToFFmpegXfadeTransition();
                    filterGraph.Append($"[{currentXfadeInput}][{nextSeg}]xfade=transition={transName}:duration={transDur.ToString("F2", CultureInfo.InvariantCulture)}:offset={offset.ToString("F2", CultureInfo.InvariantCulture)}[{xfadeOut}];\n");

                    currentXfadeInput = xfadeOut;
                    runningOffset = offset + orderedClips[i].TimelineDuration.TotalSeconds;
                }
                currentBaseVideoOut = currentXfadeInput;
            }
            else
            {
                filterGraph.Append($"{string.Join("", baseVideoSegments)}concat=n={baseVideoSegments.Count}:v=1:a=0[v_concat];\n");
                currentBaseVideoOut = "v_concat";
            }
        }
        else if (baseVideoSegments.Count == 1)
        {
            currentBaseVideoOut = baseVideoSegments[0].Trim('[', ']');
        }
        else
        {
            filterGraph.Append($"color=c=black:s={width}x{height}:r={fps}:d=5[v_empty];\n");
            currentBaseVideoOut = "v_empty";
        }

        // 2. Process Multi-Track Overlays & Picture-in-Picture (PiP)
        string overlayCompOut = currentBaseVideoOut;
        int overlayIdx = 0;
        foreach (var ovTrack in overlayTracks)
        {
            foreach (var clip in ovTrack.Clips.OrderBy(c => c.TimelineStart))
            {
                if (string.IsNullOrEmpty(clip.AssetPath) || !File.Exists(clip.AssetPath)) continue;
                int ovInput = GetInputIndex(clip.AssetPath);

                string ovPrepared = $"v_ov_prep_{overlayIdx}";
                string nextComp = $"v_comp_{overlayIdx}";

                double scale = Math.Clamp(clip.Scale, 0.05, 1.0);
                int ovW = (int)(width * scale);
                int ovH = (int)(height * scale);
                int posX = (int)(clip.PositionX * (width - ovW));
                int posY = (int)(clip.PositionY * (height - ovH));

                double tStart = clip.TimelineStart.TotalSeconds;
                double tEnd = clip.TimelineEnd.TotalSeconds;

                var ovFilters = new List<string>();
                if (clip.Type == AssetType.Image)
                {
                    ovFilters.Add("loop=loop=-1:size=1:start=0");
                }
                ovFilters.Add($"scale={ovW}:{ovH}:force_original_aspect_ratio=decrease");
                if (clip.Opacity < 0.99)
                {
                    ovFilters.Add($"format=rgba,colorchannelmixer=aa={clip.Opacity.ToString("F2", CultureInfo.InvariantCulture)}");
                }

                filterGraph.Append($"[{ovInput}:v]{string.Join(",", ovFilters)}[{ovPrepared}];\n");
                filterGraph.Append($"[{overlayCompOut}][{ovPrepared}]overlay={posX}:{posY}:enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'[{nextComp}];\n");

                overlayCompOut = nextComp;
                overlayIdx++;
            }
        }

        // 3. Process Text Overlays (Title Cards, Lower Thirds & Animated Text)
        string textCompOut = overlayCompOut;
        for (int i = 0; i < project.Overlays.Count; i++)
        {
            var overlay = project.Overlays[i];
            string nextTextOut = $"v_txt_{i}";
            int posX = (int)(overlay.PositionX * width);
            int posY = (int)(overlay.PositionY * height);
            double tStart = overlay.StartTime.TotalSeconds;
            double tEnd = (overlay.StartTime + overlay.Duration).TotalSeconds;
            double aDur = Math.Max(0.1, Math.Min(overlay.AnimationDuration, overlay.Duration.TotalSeconds / 2));

            var safeText = EscapeDrawText(overlay.Text);
            var color = FormatHexColor(overlay.ColorHex);

            var drawTextParts = new List<string>
            {
                $"text='{safeText}'",
                $"fontsize={overlay.FontSize}",
                $"fontcolor={color}"
            };

            // Positioning & Dynamic Animation Expressions
            string xExpr;
            string yExpr;

            switch (overlay.Alignment)
            {
                case OverlayAlignment.Center:
                    xExpr = $"(w-text_w)/2";
                    break;
                case OverlayAlignment.Right:
                    xExpr = $"w-text_w-{Math.Max(20, width - posX)}";
                    break;
                case OverlayAlignment.Left:
                default:
                    xExpr = $"{posX}";
                    break;
            }
            yExpr = $"{posY}";

            switch (overlay.Animation)
            {
                case TextAnimationType.SlideUp:
                    yExpr = $"if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),{posY}+(1-(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)})*60,{posY})";
                    drawTextParts.Add($"alpha='if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)},if(gt(t,{tEnd.ToString("F3", CultureInfo.InvariantCulture)}-{aDur.ToString("F3", CultureInfo.InvariantCulture)}),({tEnd.ToString("F3", CultureInfo.InvariantCulture)}-t)/{aDur.ToString("F3", CultureInfo.InvariantCulture)},1))'");
                    break;
                case TextAnimationType.SlideLeft:
                    xExpr = $"if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),{posX}-(1-(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)})*120,{posX})";
                    drawTextParts.Add($"alpha='if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)},if(gt(t,{tEnd.ToString("F3", CultureInfo.InvariantCulture)}-{aDur.ToString("F3", CultureInfo.InvariantCulture)}),({tEnd.ToString("F3", CultureInfo.InvariantCulture)}-t)/{aDur.ToString("F3", CultureInfo.InvariantCulture)},1))'");
                    break;
                case TextAnimationType.Fade:
                    drawTextParts.Add($"alpha='if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)},if(gt(t,{tEnd.ToString("F3", CultureInfo.InvariantCulture)}-{aDur.ToString("F3", CultureInfo.InvariantCulture)}),({tEnd.ToString("F3", CultureInfo.InvariantCulture)}-t)/{aDur.ToString("F3", CultureInfo.InvariantCulture)},1))'");
                    break;
                case TextAnimationType.ZoomIn:
                    drawTextParts.Add($"alpha='if(lt(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)}+{aDur.ToString("F3", CultureInfo.InvariantCulture)}),(t-{tStart.ToString("F3", CultureInfo.InvariantCulture)})/{aDur.ToString("F3", CultureInfo.InvariantCulture)},if(gt(t,{tEnd.ToString("F3", CultureInfo.InvariantCulture)}-{aDur.ToString("F3", CultureInfo.InvariantCulture)}),({tEnd.ToString("F3", CultureInfo.InvariantCulture)}-t)/{aDur.ToString("F3", CultureInfo.InvariantCulture)},1))'");
                    break;
            }

            drawTextParts.Add($"x={xExpr}");
            drawTextParts.Add($"y={yExpr}");

            // Background box banner styling
            if (!string.IsNullOrEmpty(overlay.BackgroundColorHex))
            {
                drawTextParts.Add("box=1");
                drawTextParts.Add($"boxcolor={FormatHexColor(overlay.BackgroundColorHex)}");
                drawTextParts.Add($"boxborderw={overlay.BoxPadding}");
            }

            // Outline / Border styling
            if (overlay.BorderWidth > 0 && !string.IsNullOrEmpty(overlay.BorderColorHex))
            {
                drawTextParts.Add($"borderw={overlay.BorderWidth}");
                drawTextParts.Add($"bordercolor={FormatHexColor(overlay.BorderColorHex)}");
            }

            // Drop shadow styling
            if (overlay.ShadowDepth > 0 && !string.IsNullOrEmpty(overlay.ShadowColorHex))
            {
                drawTextParts.Add($"shadowcolor={FormatHexColor(overlay.ShadowColorHex)}");
                drawTextParts.Add($"shadowx={overlay.ShadowDepth}");
                drawTextParts.Add($"shadowy={overlay.ShadowDepth}");
            }

            drawTextParts.Add($"enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'");

            var drawText = $"drawtext={string.Join(":", drawTextParts)}";
            filterGraph.Append($"[{textCompOut}]{drawText}[{nextTextOut}];\n");
            textCompOut = nextTextOut;

            // Subtitle secondary text for lower-thirds
            if (!string.IsNullOrWhiteSpace(overlay.SubtitleText))
            {
                string nextSubOut = $"v_txt_sub_{i}";
                var safeSub = EscapeDrawText(overlay.SubtitleText);
                var subFontSize = Math.Max(14, (int)(overlay.FontSize * 0.65));
                var subPosY = posY + (int)overlay.FontSize + 8;
                var drawSub = $"drawtext=text='{safeSub}':fontsize={subFontSize}:fontcolor=0x00BCF9:x={posX}:y={subPosY}:enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";
                filterGraph.Append($"[{textCompOut}]{drawSub}[{nextSubOut}];\n");
                textCompOut = nextSubOut;
            }
        }

        // 4. Process Captions / Subtitles Track
        string captionCompOut = textCompOut;
        if (project.Captions.Count > 0)
        {
            int capIdx = 0;
            foreach (var cap in project.Captions.OrderBy(c => c.StartTime))
            {
                string nextCapOut = $"v_cap_{capIdx}";
                double tStart = cap.StartTime.TotalSeconds;
                double tEnd = cap.EndTime.TotalSeconds;
                var safeCap = EscapeDrawText(cap.Text);

                string drawCap;
                switch (cap.StylePreset)
                {
                    case CaptionStylePreset.YellowCinema:
                        drawCap = $"drawtext=text='{safeCap}':fontsize={height / 28}:fontcolor=yellow:shadowcolor=black@0.8:shadowx=2:shadowy=2:x=(w-text_w)/2:y=h-(text_h*2.5):enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";
                        break;
                    case CaptionStylePreset.HighContrastWhite:
                        drawCap = $"drawtext=text='{safeCap}':fontsize={height / 28}:fontcolor=white:borderw=2:bordercolor=black:x=(w-text_w)/2:y=h-(text_h*2.5):enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";
                        break;
                    case CaptionStylePreset.TopHeader:
                        drawCap = $"drawtext=text='{safeCap}':fontsize={height / 30}:fontcolor=white:box=1:boxcolor=black@0.65:boxborderw=8:x=(w-text_w)/2:y=h*0.08:enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";
                        break;
                    case CaptionStylePreset.BoxedDark:
                    case CaptionStylePreset.Default:
                    default:
                        // Position captions cleanly centered at bottom with dark background box
                        drawCap = $"drawtext=text='{safeCap}':fontsize={height / 30}:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=8:x=(w-text_w)/2:y=h-(text_h*2.5):enable='between(t,{tStart.ToString("F3", CultureInfo.InvariantCulture)},{tEnd.ToString("F3", CultureInfo.InvariantCulture)})'";
                        break;
                }

                filterGraph.Append($"[{captionCompOut}]{drawCap}[{nextCapOut}];\n");
                captionCompOut = nextCapOut;
                capIdx++;
            }
        }

        string finalVideoOut = captionCompOut;

        // 5. Audio Mixing (Video Clips Audio + Music Tracks)
        string? baseAudioOut = null;
        if (baseAudioSegments.Count > 1)
        {
            filterGraph.Append($"{string.Join("", baseAudioSegments)}concat=n={baseAudioSegments.Count}:v=0:a=1[a_base_concat];\n");
            baseAudioOut = "a_base_concat";
        }
        else if (baseAudioSegments.Count == 1)
        {
            baseAudioOut = baseAudioSegments[0].Trim('[', ']');
        }

        // Music / Voiceover tracks
        int musicSegIndex = 0;
        var musicAudioLabels = new List<string>();
        foreach (var aTrack in audioTracks)
        {
            foreach (var clip in aTrack.Clips.OrderBy(c => c.TimelineStart))
            {
                if (string.IsNullOrEmpty(clip.AssetPath) || !File.Exists(clip.AssetPath)) continue;
                int aInIdx = GetInputIndex(clip.AssetPath);
                string mLabel = $"a_music_{musicSegIndex++}";

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

                // Phase 8: Apply audio fade in / fade out to music track
                ApplyClipAudioFadeFilters(clip, aFilters);

                foreach (var effect in clip.Effects)
                {
                    ApplyAudioEffectFilter(effect, aFilters, clip);
                }

                filterGraph.Append($"[{aInIdx}:a]{string.Join(",", aFilters)}[{mLabel}];\n");
                musicAudioLabels.Add($"[{mLabel}]");
            }
        }

        string finalAudioOut;

        // Phase 8: Audio Ducking & Mixing
        if (settings.EnableAudioDucking && baseAudioOut != null && musicAudioLabels.Count > 0)
        {
            string musicCombined = "a_music_all";
            if (musicAudioLabels.Count > 1)
            {
                filterGraph.Append($"{string.Join("", musicAudioLabels)}amix=inputs={musicAudioLabels.Count}:duration=longest[a_music_all];\n");
            }
            else
            {
                musicCombined = musicAudioLabels[0].Trim('[', ']');
            }

            // Sidechain compressor ducks music whenever speech/dialogue is active on base video
            filterGraph.Append($"[{musicCombined}][{baseAudioOut}]sidechaincompress=threshold=0.03:ratio=4:attack=50:release=300[a_music_ducked];\n");
            filterGraph.Append($"[{baseAudioOut}][a_music_ducked]amix=inputs=2:duration=first:dropout_transition=2[a_mixed];\n");
            finalAudioOut = "a_mixed";
        }
        else
        {
            var allAudioLabels = new List<string>();
            if (baseAudioOut != null)
            {
                allAudioLabels.Add($"[{baseAudioOut}]");
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
                filterGraph.Append("anullsrc=r=48000:cl=stereo:d=5[a_silent];\n");
                finalAudioOut = "a_silent";
            }
        }

        // Phase 8: Audio Normalization (EBU R128 loudnorm)
        if (settings.NormalizeAudio)
        {
            filterGraph.Append($"[{finalAudioOut}]loudnorm=I=-16:TP=-1.5:LRA=11[a_loudnorm];\n");
            finalAudioOut = "a_loudnorm";
        }

        // Phase 11: GIF format export support (two-pass high-quality palette)
        if (settings.Format == ExportFormat.Gif)
        {
            filterGraph.Append($"[{finalVideoOut}]split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer[v_gif_out];\n");
            finalVideoOut = "v_gif_out";
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

        if (settings.Format == ExportFormat.Gif)
        {
            // GIF export: video stream only
            sb.Append($"-map \"[{finalVideoOut}]\" ");
            sb.Append($"\"{settings.OutputPath}\"");
        }
        else if (settings.Format == ExportFormat.AudioOnlyMp3)
        {
            // Audio-only MP3 export
            sb.Append($"-map \"[{finalAudioOut}]\" -vn -c:a libmp3lame -b:a {settings.AudioBitrateKbps}k ");
            sb.Append($"\"{settings.OutputPath}\"");
        }
        else
        {
            // Standard Video + Audio MP4
            sb.Append($"-map \"[{finalVideoOut}]\" -map \"[{finalAudioOut}]\" ");
            sb.Append($"-c:v {settings.VideoCodec} -preset {settings.Preset} -b:v {settings.VideoBitrateKbps}k ");
            sb.Append($"-c:a {settings.AudioCodec} -b:a {settings.AudioBitrateKbps}k ");
            sb.Append($"\"{settings.OutputPath}\"");
        }

        return new FFmpegCommandArgs
        {
            InputFiles = inputs,
            FilterComplex = filterComplexStr,
            VideoMap = settings.Format == ExportFormat.AudioOnlyMp3 ? string.Empty : $"[{finalVideoOut}]",
            AudioMap = settings.Format == ExportFormat.Gif ? string.Empty : $"[{finalAudioOut}]",
            FullCommandLine = sb.ToString()
        };
    }

    private static void ApplyClipTransformFilters(Clip clip, List<string> vFilters, int width, int height)
    {
        if (clip.Rotation == 90)
        {
            vFilters.Add("transpose=1");
        }
        else if (clip.Rotation == 180)
        {
            vFilters.Add("hflip,vflip");
        }
        else if (clip.Rotation == 270)
        {
            vFilters.Add("transpose=2");
        }

        if (clip.FlipHorizontal)
        {
            vFilters.Add("hflip");
        }
        if (clip.FlipVertical)
        {
            vFilters.Add("vflip");
        }

        switch (clip.PanZoom)
        {
            case PanZoomType.ZoomIn:
                vFilters.Add($"zoompan=z='min(zoom+0.0015,1.3)':d=125:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s={width}x{height}");
                break;
            case PanZoomType.ZoomOut:
                vFilters.Add($"zoompan=z='if(lte(zoom,1.0),1.3,max(1.001,zoom-0.0015))':d=125:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s={width}x{height}");
                break;
            case PanZoomType.PanLeftToRight:
                vFilters.Add($"zoompan=z=1.2:x='if(lte(on,1),0,min(x+1,iw-iw/zoom))':y='ih/2-(ih/zoom/2)':d=125:s={width}x{height}");
                break;
            case PanZoomType.PanRightToLeft:
                vFilters.Add($"zoompan=z=1.2:x='if(lte(on,1),iw-iw/zoom,max(x-1,0))':y='ih/2-(ih/zoom/2)':d=125:s={width}x{height}");
                break;
        }
    }

    private static void ApplyClipAudioFadeFilters(Clip clip, List<string> aFilters)
    {
        if (clip.AudioFadeIn > 0)
        {
            aFilters.Add($"afade=t=in:st=0:d={clip.AudioFadeIn.ToString("F2", CultureInfo.InvariantCulture)}");
        }
        if (clip.AudioFadeOut > 0)
        {
            var outStart = Math.Max(0, clip.TimelineDuration.TotalSeconds - clip.AudioFadeOut);
            aFilters.Add($"afade=t=out:st={outStart.ToString("F2", CultureInfo.InvariantCulture)}:d={clip.AudioFadeOut.ToString("F2", CultureInfo.InvariantCulture)}");
        }
    }

    private static void ApplyVideoEffectFilter(ClipEffect effect, List<string> vFilters, Clip clip)
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
            case EffectType.Sepia:
                vFilters.Add("colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131");
                break;
            case EffectType.Invert:
                vFilters.Add("negate");
                break;
            case EffectType.Blur:
                var radius = effect.Parameters.GetValueOrDefault("radius", 5.0);
                vFilters.Add($"boxblur={radius:F0}:1");
                break;
            case EffectType.Sharpen:
                vFilters.Add("unsharp=5:5:1.0:5:5:0.0");
                break;
            case EffectType.Vignette:
                var angle = effect.Parameters.GetValueOrDefault("angle", 0.5);
                vFilters.Add($"vignette={angle.ToString("F2", CultureInfo.InvariantCulture)}");
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
            case EffectType.Hue:
                var hueDegrees = effect.Parameters.GetValueOrDefault("degrees", 0.0);
                vFilters.Add($"hue=h={hueDegrees.ToString("F1", CultureInfo.InvariantCulture)}");
                break;
        }
    }

    private static void ApplyAudioEffectFilter(ClipEffect effect, List<string> aFilters, Clip clip)
    {
        switch (effect.Type)
        {
            case EffectType.AudioFadeIn:
                var inDuration = effect.Parameters.GetValueOrDefault("duration", 1.0);
                aFilters.Add($"afade=t=in:st=0:d={inDuration.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
            case EffectType.AudioFadeOut:
                var outDuration = effect.Parameters.GetValueOrDefault("duration", 1.0);
                var clipDur = clip.TimelineDuration.TotalSeconds;
                var outStart = Math.Max(0, clipDur - outDuration);
                aFilters.Add($"afade=t=out:st={outStart.ToString("F2", CultureInfo.InvariantCulture)}:d={outDuration.ToString("F2", CultureInfo.InvariantCulture)}");
                break;
        }
    }

    private static void ApplyAudioTempoFilters(double speed, List<string> aFilters)
    {
        if (Math.Abs(speed - 1.0) < 0.01) return;

        // FFmpeg's atempo filter must be between 0.5 and 2.0. Multiple atempo filters can be chained.
        double remaining = speed;
        while (remaining > 2.0)
        {
            aFilters.Add("atempo=2.0");
            remaining /= 2.0;
        }
        while (remaining < 0.5)
        {
            aFilters.Add("atempo=0.5");
            remaining /= 0.5;
        }
        if (Math.Abs(remaining - 1.0) > 0.01)
        {
            aFilters.Add($"atempo={remaining.ToString("F2", CultureInfo.InvariantCulture)}");
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

    private static string FormatHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "white";
        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6 || clean.Length == 8)
        {
            return "0x" + clean;
        }
        return hex;
    }
}
