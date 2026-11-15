using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using CutCraft.Core.Models;

namespace CutCraft.Media.Export;

public interface IMediaExporter
{
    Task<ExportResult> ExportProjectAsync(
        Project project,
        ExportSettings settings,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}

public class FFmpegProcessExporter : IMediaExporter
{
    private readonly string _ffmpegPath;
    private readonly FFmpegFilterGraphBuilder _builder;

    public FFmpegProcessExporter(string? ffmpegPath = null)
    {
        _ffmpegPath = ffmpegPath ?? "ffmpeg";
        _builder = new FFmpegFilterGraphBuilder();
    }

    public async Task<ExportResult> ExportProjectAsync(
        Project project,
        ExportSettings settings,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ExportResult
        {
            OutputFilePath = settings.OutputPath
        };

        var commandArgs = _builder.BuildExportCommand(project, settings);
        var totalDuration = project.Duration;
        if (totalDuration <= TimeSpan.Zero)
        {
            totalDuration = TimeSpan.FromSeconds(5);
        }

        var outDir = Path.GetDirectoryName(settings.OutputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = commandArgs.FullCommandLine,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var errorOutput = new System.Text.StringBuilder();

        // Regex to parse FFmpeg progress: time=00:01:23.45
        var timeRegex = new Regex(@"time=(\d+):(\d+):(\d+\.\d+)", RegexOptions.Compiled);

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null) return;
            errorOutput.AppendLine(e.Data);

            var match = timeRegex.Match(e.Data);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var hours) &&
                double.TryParse(match.Groups[2].Value, out var minutes) &&
                double.TryParse(match.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                var currentTime = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                var pct = Math.Min(1.0, currentTime.TotalSeconds / totalDuration.TotalSeconds);
                progress?.Report(pct);
            }
        };

        try
        {
            process.Start();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && File.Exists(settings.OutputPath))
            {
                result.Success = true;
                result.Duration = totalDuration;
                result.FileSizeBytes = new FileInfo(settings.OutputPath).Length;
                progress?.Report(1.0);
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"FFmpeg exited with code {process.ExitCode}: {errorOutput}";
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
            result.Success = false;
            result.ErrorMessage = "Export was canceled by user.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
