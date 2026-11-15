namespace CutCraft.Core.Updates;

public class UpdateInfo
{
    public string Version { get; set; } = "0.0";
    public string Name { get; set; } = "CutCraft v0.0";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public DateTime? ReleaseDate { get; set; }
    public bool Mandatory { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = "";
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public bool IsSuccess { get; set; } = true;
    public bool IsNetworkError { get; set; }
    public string? ErrorMessage { get; set; }
    public UpdateInfo? Update { get; set; }
    public string Message { get; set; } = "";
    public string CurrentVersion { get; set; } = "0.0";
    public string LatestVersion { get; set; } = "0.0";
}
