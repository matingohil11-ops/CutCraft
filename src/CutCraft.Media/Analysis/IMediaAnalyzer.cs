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
            // If ffprobe is not installed or fails, try pure C# media metadata fallback parser
            TryReadMediaMetadataFallback(filePath, asset);
        }

        if (asset.Duration == TimeSpan.Zero)
        {
            TryReadMediaMetadataFallback(filePath, asset);
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

    private static void TryReadMediaMetadataFallback(string filePath, MediaAsset asset)
    {
        try
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";
            if (ext is ".mp4" or ".mov" or ".m4v" or ".m4a")
            {
                ReadMp4Metadata(filePath, asset);
            }
            else if (ext is ".wav")
            {
                ReadWavMetadata(filePath, asset);
            }
            else if (asset.Type == AssetType.Image)
            {
                asset.Duration = TimeSpan.FromSeconds(5);
            }
        }
        catch
        {
            if (asset.Type == AssetType.Image)
            {
                asset.Duration = TimeSpan.FromSeconds(5);
            }
        }
    }

    private static void ReadMp4Metadata(string filePath, MediaAsset asset)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);

        while (fs.Position + 8 <= fs.Length)
        {
            long atomStart = fs.Position;
            uint size = ReadBigEndianUInt32(br);
            byte[] typeBytes = br.ReadBytes(4);
            string type = System.Text.Encoding.ASCII.GetString(typeBytes);

            long atomSize = size == 1 ? (long)ReadBigEndianUInt64(br) : (size == 0 ? fs.Length - atomStart : size);
            if (atomSize < 8) break;

            if (type == "moov")
            {
                long moovEnd = Math.Min(fs.Length, atomStart + atomSize);
                while (fs.Position + 8 <= moovEnd)
                {
                    long subStart = fs.Position;
                    uint subSize = ReadBigEndianUInt32(br);
                    string subType = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
                    long subAtomSize = subSize == 1 ? (long)ReadBigEndianUInt64(br) : (subSize == 0 ? moovEnd - subStart : subSize);
                    if (subAtomSize < 8) break;

                    if (subType == "mvhd")
                    {
                        byte version = br.ReadByte();
                        fs.Seek(3, SeekOrigin.Current);
                        if (version == 0)
                        {
                            fs.Seek(8, SeekOrigin.Current);
                            uint timescale = ReadBigEndianUInt32(br);
                            uint duration = ReadBigEndianUInt32(br);
                            if (timescale > 0 && asset.Duration == TimeSpan.Zero)
                            {
                                asset.Duration = TimeSpan.FromSeconds((double)duration / timescale);
                            }
                        }
                        else if (version == 1)
                        {
                            fs.Seek(16, SeekOrigin.Current);
                            uint timescale = ReadBigEndianUInt32(br);
                            ulong duration = ReadBigEndianUInt64(br);
                            if (timescale > 0 && asset.Duration == TimeSpan.Zero)
                            {
                                asset.Duration = TimeSpan.FromSeconds((double)duration / timescale);
                            }
                        }
                    }
                    else if (subType == "trak" && (asset.Width == 0 || asset.Height == 0))
                    {
                        long trakEnd = Math.Min(moovEnd, subStart + subAtomSize);
                        while (fs.Position + 8 <= trakEnd)
                        {
                            long tStart = fs.Position;
                            uint tSize = ReadBigEndianUInt32(br);
                            string tType = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
                            long tAtomSize = tSize == 1 ? (long)ReadBigEndianUInt64(br) : (tSize == 0 ? trakEnd - tStart : tSize);
                            if (tAtomSize < 8) break;

                            if (tType == "tkhd")
                            {
                                byte tVer = br.ReadByte();
                                fs.Seek(3, SeekOrigin.Current);
                                int skip = tVer == 0 ? 72 : 84;
                                fs.Seek(skip, SeekOrigin.Current);
                                uint wFixed = ReadBigEndianUInt32(br);
                                uint hFixed = ReadBigEndianUInt32(br);
                                int w = (int)(wFixed >> 16);
                                int h = (int)(hFixed >> 16);
                                if (w > 0 && h > 0)
                                {
                                    asset.Width = w;
                                    asset.Height = h;
                                }
                                break;
                            }
                            fs.Position = tStart + tAtomSize;
                        }
                    }

                    fs.Position = subStart + subAtomSize;
                }
                break;
            }

            fs.Position = atomStart + atomSize;
        }
    }

    private static void ReadWavMetadata(string filePath, MediaAsset asset)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);

        if (fs.Length < 44) return;
        var riff = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
        if (riff != "RIFF") return;
        br.ReadUInt32();
        var wave = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
        if (wave != "WAVE") return;

        uint byteRate = 0;
        while (fs.Position + 8 <= fs.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
            uint chunkSize = br.ReadUInt32();
            if (chunkId == "fmt ")
            {
                br.ReadUInt16();
                ushort channels = br.ReadUInt16();
                uint sampleRate = br.ReadUInt32();
                byteRate = br.ReadUInt32();
                fs.Seek(chunkSize - 12, SeekOrigin.Current);
            }
            else if (chunkId == "data")
            {
                if (byteRate > 0 && asset.Duration == TimeSpan.Zero)
                {
                    asset.Duration = TimeSpan.FromSeconds((double)chunkSize / byteRate);
                }
                break;
            }
            else
            {
                fs.Seek(chunkSize, SeekOrigin.Current);
            }
        }
    }

    private static uint ReadBigEndianUInt32(BinaryReader br)
    {
        var bytes = br.ReadBytes(4);
        if (bytes.Length < 4) return 0;
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static ulong ReadBigEndianUInt64(BinaryReader br)
    {
        var bytes = br.ReadBytes(8);
        if (bytes.Length < 8) return 0;
        return ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32)
             | ((ulong)bytes[4] << 24) | ((ulong)bytes[5] << 16) | ((ulong)bytes[6] << 8) | bytes[7];
    }
}
