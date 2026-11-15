namespace CutCraft.Media.Export;

public enum ExportResolution
{
    Hd720p,
    Fhd1080p,
    Uhd4k,
    VerticalShorts, // 1080x1920 (9:16)
    Square          // 1080x1080 (1:1)
}

public enum ExportFormat
{
    Mp4,
    Gif,
    AudioOnlyMp3
}

public enum ExportQuality
{
    Draft,
    Standard,
    High
}

public class ExportSettings
{
    public string OutputPath { get; set; } = string.Empty;
    public ExportResolution Resolution { get; set; } = ExportResolution.Fhd1080p;
    public ExportFormat Format { get; set; } = ExportFormat.Mp4;
    public ExportQuality Quality { get; set; } = ExportQuality.Standard;
    public int CustomWidth { get; set; } = 1920;
    public int CustomHeight { get; set; } = 1080;
    public double FrameRate { get; set; } = 30.0;
    public string VideoCodec { get; set; } = "libx264";
    public string AudioCodec { get; set; } = "aac";
    public int VideoBitrateKbps { get; set; } = 8000;
    public int AudioBitrateKbps { get; set; } = 192;
    public string Preset { get; set; } = "medium"; // ultrafast, fast, medium, slow

    // Phase 8: Audio Normalization & Ducking
    public bool NormalizeAudio { get; set; } = false;
    public bool EnableAudioDucking { get; set; } = false;
    public double DuckingAttenuation { get; set; } = 0.25;

    public (int width, int height) GetDimensions()
    {
        return Resolution switch
        {
            ExportResolution.Hd720p => (1280, 720),
            ExportResolution.Fhd1080p => (1920, 1080),
            ExportResolution.Uhd4k => (3840, 2160),
            ExportResolution.VerticalShorts => (1080, 1920),
            ExportResolution.Square => (1080, 1080),
            _ => (CustomWidth > 0 ? CustomWidth : 1920, CustomHeight > 0 ? CustomHeight : 1080)
        };
    }
}

public class ExportResult
{
    public bool Success { get; set; }
    public string OutputFilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
}
