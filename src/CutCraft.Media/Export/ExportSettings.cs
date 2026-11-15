namespace CutCraft.Media.Export;

public enum ExportResolution
{
    Hd720p,
    Fhd1080p,
    Uhd4k
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
    public ExportQuality Quality { get; set; } = ExportQuality.Standard;
    public int CustomWidth { get; set; } = 1920;
    public int CustomHeight { get; set; } = 1080;
    public double FrameRate { get; set; } = 30.0;
    public string VideoCodec { get; set; } = "libx264";
    public string AudioCodec { get; set; } = "aac";
    public int VideoBitrateKbps { get; set; } = 8000;
    public int AudioBitrateKbps { get; set; } = 192;
    public string Preset { get; set; } = "medium"; // ultrafast, fast, medium, slow

    public (int width, int height) GetDimensions()
    {
        return Resolution switch
        {
            ExportResolution.Hd720p => (1280, 720),
            ExportResolution.Fhd1080p => (1920, 1080),
            ExportResolution.Uhd4k => (3840, 2160),
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
