using System.Diagnostics;
using System.IO;
using CutCraft.Core.Models;

namespace CutCraft.Media.Proxy;

public interface IProxyMediaService
{
    bool IsProxyEnabled { get; set; }
    Task<string> GenerateProxyAsync(MediaAsset asset, IProgress<double>? progress = null, CancellationToken ct = default);
    string GetActiveAssetPath(MediaAsset asset);
}

public class FFmpegProxyMediaService : IProxyMediaService
{
    private readonly string _ffmpegPath;
    private readonly string _proxyCacheDir;

    public bool IsProxyEnabled { get; set; } = true;

    public FFmpegProxyMediaService(string? ffmpegPath = null, string? proxyCacheDir = null)
    {
        _ffmpegPath = ffmpegPath ?? "ffmpeg";
        _proxyCacheDir = proxyCacheDir ?? Path.Combine(Path.GetTempPath(), "CutCraft", "Proxies");

        if (!Directory.Exists(_proxyCacheDir))
        {
            Directory.CreateDirectory(_proxyCacheDir);
        }
    }

    public async Task<string> GenerateProxyAsync(MediaAsset asset, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (asset.Type != AssetType.Video)
        {
            return asset.FilePath;
        }

        var proxyFileName = $"{Path.GetFileNameWithoutExtension(asset.FilePath)}_proxy_{asset.Id}.mp4";
        var proxyPath = Path.Combine(_proxyCacheDir, proxyFileName);

        if (File.Exists(proxyPath))
        {
            return proxyPath;
        }

        // Generate fast 720p H.264 proxy with ultrafast preset for zero latency scrubbing
        var arguments = $"-y -i \"{asset.FilePath}\" -vf \"scale=1280:720:force_original_aspect_ratio=decrease\" -c:v libx264 -preset ultrafast -crf 28 -c:a aac -b:a 128k \"{proxyPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode == 0 && File.Exists(proxyPath))
        {
            return proxyPath;
        }

        // Fallback to original if proxy generation fails
        return asset.FilePath;
    }

    public string GetActiveAssetPath(MediaAsset asset)
    {
        if (!IsProxyEnabled) return asset.FilePath;

        var proxyFileName = $"{Path.GetFileNameWithoutExtension(asset.FilePath)}_proxy_{asset.Id}.mp4";
        var proxyPath = Path.Combine(_proxyCacheDir, proxyFileName);
        return File.Exists(proxyPath) ? proxyPath : asset.FilePath;
    }
}
