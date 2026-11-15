using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CutCraft.Core.Models;

namespace CutCraft.Media.Analysis;

public interface IMediaAnalyzer
{
    Task<MediaAsset> AnalyzeAssetAsync(string filePath, CancellationToken ct = default);
}

public class FFprobeMediaAnalyzer : IMediaAnalyzer
{
    private readonly string _ffprobePath;

    public FFprobeMediaAnalyzer(string? ffprobePath = null)
    {
        _ffprobePath = ffprobePath ?? "ffprobe";
    }

    public async Task<MediaAsset> AnalyzeAssetAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        var asset = new MediaAsset
        {
            FilePath = filePath,
            Name = Path.GetFileName(filePath),
            FileSizeBytes = fileInfo.Length,
            Type = DetectTypeByExtension(filePath)
        };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return asset;

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                ParseFfprobeJson(stdout, asset);
            }
        }
        catch
        {
            if (asset.Type == AssetType.Image)
            {
                asset.Duration = TimeSpan.FromSeconds(5);
            }
        }

        return asset;
    }

    private static void ParseFfprobeJson(string json, MediaAsset asset)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("format", out var format))
        {
            if (format.TryGetProperty("duration", out var durProp) &&
                double.TryParse(durProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var durSeconds))
            {
                asset.Duration = TimeSpan.FromSeconds(durSeconds);
            }
        }

        if (root.TryGetProperty("streams", out var streams))
        {
            bool hasVideo = false;
            bool hasAudio = false;

            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.GetProperty("codec_type").GetString();
                if (codecType == "video" && !hasVideo)
                {
                    hasVideo = true;
                    if (stream.TryGetProperty("width", out var w)) asset.Width = w.GetInt32();
                    if (stream.TryGetProperty("height", out var h)) asset.Height = h.GetInt32();
                    if (stream.TryGetProperty("r_frame_rate", out var fpsProp))
                    {
                        var parts = fpsProp.GetString()?.Split('/');
                        if (parts != null && parts.Length == 2 &&
                            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) && den > 0)
                        {
                            asset.FrameRate = Math.Round(num / den, 2);
                        }
                    }
                }
                else if (codecType == "audio")
                {
                    hasAudio = true;
                }
            }

            asset.HasAudio = hasAudio;
            if (hasVideo) asset.Type = AssetType.Video;
            else if (hasAudio) asset.Type = AssetType.Audio;
        }
    }

    private static AssetType DetectTypeByExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";
        return ext switch
        {
            ".mp3" or ".wav" or ".aac" or ".m4a" or ".flac" or ".ogg" or ".wma" => AssetType.Audio,
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif" => AssetType.Image,
            _ => AssetType.Video
        };
    }
}
