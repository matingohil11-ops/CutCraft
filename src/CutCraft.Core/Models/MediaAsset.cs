namespace CutCraft.Core.Models;

public enum AssetType
{
    Video,
    Audio,
    Image
}

public class MediaAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; } = AssetType.Video;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; } = 30.0;
    public bool HasAudio { get; set; } = true;
    public long FileSizeBytes { get; set; }
    public string? ThumbnailPath { get; set; }
}
