using System.IO;

namespace CutCraft.Core.Diagnostics;

public static class CrashLogger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CutCraft",
        "Logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, $"cutcraft_{DateTime.UtcNow:yyyyMMdd}.log");

    public static void Initialize()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            LogInfo("CutCraft session started.");
        }
        catch { }
    }

    public static void LogInfo(string message)
    {
        WriteEntry("INFO", message);
    }

    public static void LogWarning(string message)
    {
        WriteEntry("WARN", message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message} | Exception: {ex}" : message;
        WriteEntry("ERROR", fullMessage);
    }

    private static void WriteEntry(string level, string message)
    {
        try
        {
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            File.AppendAllLines(LogFilePath, new[] { line });
        }
        catch { }
    }
}
