using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CutCraft.Core.Models;

namespace CutCraft.Core.Subtitles;

public enum SubtitleValidationSeverity
{
    Info,
    Warning,
    Error
}

public class SubtitleValidationIssue
{
    public int CaptionIndex { get; set; }
    public string CaptionId { get; set; } = string.Empty;
    public SubtitleValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class SrtSubtitleService
{
    private static readonly Regex SrtTimestampRegex = new(
        @"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})",
        RegexOptions.Compiled);

    private static readonly Regex VttTimestampRegex = new(
        @"(?:(\d{2}):)?(\d{2}):(\d{2})\.(\d{3})\s*-->\s*(?:(\d{2}):)?(\d{2}):(\d{2})\.(\d{3})",
        RegexOptions.Compiled);

    #region SRT Parsing and Generation

    public static List<CaptionItem> ParseSrt(string srtContent)
    {
        var result = new List<CaptionItem>();
        if (string.IsNullOrWhiteSpace(srtContent)) return result;

        var lines = srtContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int counter = 0;
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            // Optional numeric index line
            if (int.TryParse(line, out _))
            {
                i++;
                if (i >= lines.Length) break;
                line = lines[i].Trim();
            }

            // Parse timestamp line
            var match = SrtTimestampRegex.Match(line);
            if (match.Success)
            {
                var start = ParseTimeSpan(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                var end = ParseTimeSpan(match.Groups[5].Value, match.Groups[6].Value, match.Groups[7].Value, match.Groups[8].Value);

                i++;
                var textBuilder = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    if (textBuilder.Length > 0) textBuilder.AppendLine();
                    textBuilder.Append(lines[i].Trim());
                    i++;
                }

                counter++;
                var rawText = textBuilder.ToString();
                var (speaker, cleanText) = ExtractSpeaker(rawText);

                result.Add(new CaptionItem(counter, start, end, cleanText, speaker));
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    public static string GenerateSrt(IEnumerable<CaptionItem> captions)
    {
        var sb = new StringBuilder();
        int counter = 1;

        foreach (var caption in captions.OrderBy(c => c.StartTime))
        {
            sb.AppendLine(counter.ToString());
            sb.AppendLine($"{FormatSrtTime(caption.StartTime)} --> {FormatSrtTime(caption.EndTime)}");
            var text = string.IsNullOrWhiteSpace(caption.Speaker)
                ? caption.Text
                : $"[{caption.Speaker}] {caption.Text}";
            sb.AppendLine(text);
            sb.AppendLine();
            counter++;
        }

        return sb.ToString();
    }

    #endregion

    #region WebVTT (.vtt) Parsing and Generation

    public static List<CaptionItem> ParseVtt(string vttContent)
    {
        var result = new List<CaptionItem>();
        if (string.IsNullOrWhiteSpace(vttContent)) return result;

        var lines = vttContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int counter = 0;
        int i = 0;

        // Skip WEBVTT header and metadata comments
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }
            break;
        }

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            // Skip cue identifier line if followed by timestamp
            if (i + 1 < lines.Length && VttTimestampRegex.IsMatch(lines[i + 1]))
            {
                i++;
                line = lines[i].Trim();
            }

            var match = VttTimestampRegex.Match(line);
            if (match.Success)
            {
                var startHours = string.IsNullOrEmpty(match.Groups[1].Value) ? "00" : match.Groups[1].Value;
                var startMin = match.Groups[2].Value;
                var startSec = match.Groups[3].Value;
                var startMs = match.Groups[4].Value;

                var endHours = string.IsNullOrEmpty(match.Groups[5].Value) ? "00" : match.Groups[5].Value;
                var endMin = match.Groups[6].Value;
                var endSec = match.Groups[7].Value;
                var endMs = match.Groups[8].Value;

                var start = ParseTimeSpan(startHours, startMin, startSec, startMs);
                var end = ParseTimeSpan(endHours, endMin, endSec, endMs);

                i++;
                var textBuilder = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    if (textBuilder.Length > 0) textBuilder.AppendLine();
                    textBuilder.Append(lines[i].Trim());
                    i++;
                }

                counter++;
                var rawText = textBuilder.ToString();
                var (speaker, cleanText) = ExtractSpeaker(rawText);

                result.Add(new CaptionItem(counter, start, end, cleanText, speaker));
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    public static string GenerateVtt(IEnumerable<CaptionItem> captions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        int counter = 1;
        foreach (var caption in captions.OrderBy(c => c.StartTime))
        {
            sb.AppendLine(counter.ToString());
            sb.AppendLine($"{FormatVttTime(caption.StartTime)} --> {FormatVttTime(caption.EndTime)}");
            var text = string.IsNullOrWhiteSpace(caption.Speaker)
                ? caption.Text
                : $"<v {caption.Speaker}>{caption.Text}</v>";
            sb.AppendLine(text);
            sb.AppendLine();
            counter++;
        }

        return sb.ToString();
    }

    #endregion

    #region Timing & Manipulation Operations

    public static void ShiftCaptions(IEnumerable<CaptionItem> captions, TimeSpan offset)
    {
        foreach (var caption in captions)
        {
            var newStart = caption.StartTime + offset;
            var duration = caption.Duration;

            if (newStart < TimeSpan.Zero)
            {
                newStart = TimeSpan.Zero;
            }

            caption.StartTime = newStart;
            caption.EndTime = newStart + duration;
        }
    }

    public static void RenumberCaptions(IEnumerable<CaptionItem> captions)
    {
        int index = 1;
        foreach (var caption in captions.OrderBy(c => c.StartTime))
        {
            caption.Index = index++;
        }
    }

    public static (CaptionItem Left, CaptionItem Right) SplitCaption(CaptionItem caption, TimeSpan splitPoint)
    {
        if (splitPoint <= caption.StartTime || splitPoint >= caption.EndTime)
        {
            throw new ArgumentOutOfRangeException(nameof(splitPoint), "Split point must be strictly between StartTime and EndTime.");
        }

        var text = caption.Text ?? string.Empty;
        string leftText = text;
        string rightText = text;

        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            double ratio = (splitPoint - caption.StartTime).TotalMilliseconds / caption.Duration.TotalMilliseconds;
            int splitIndex = (int)Math.Round(words.Length * ratio);
            splitIndex = Math.Clamp(splitIndex, 1, words.Length - 1);

            leftText = string.Join(" ", words.Take(splitIndex));
            rightText = string.Join(" ", words.Skip(splitIndex));
        }

        var left = new CaptionItem(caption.Index, caption.StartTime, splitPoint, leftText, caption.Speaker, caption.StylePreset);
        var right = new CaptionItem(caption.Index + 1, splitPoint, caption.EndTime, rightText, caption.Speaker, caption.StylePreset);

        return (left, right);
    }

    public static CaptionItem MergeCaptions(CaptionItem first, CaptionItem second)
    {
        var start = first.StartTime < second.StartTime ? first.StartTime : second.StartTime;
        var end = first.EndTime > second.EndTime ? first.EndTime : second.EndTime;

        var combinedText = string.IsNullOrWhiteSpace(first.Text)
            ? second.Text
            : string.IsNullOrWhiteSpace(second.Text)
                ? first.Text
                : $"{first.Text.Trim()} {second.Text.Trim()}";

        var speaker = !string.IsNullOrWhiteSpace(first.Speaker) ? first.Speaker : second.Speaker;

        return new CaptionItem(first.Index, start, end, combinedText, speaker, first.StylePreset);
    }

    public static List<SubtitleValidationIssue> ValidateCaptions(IEnumerable<CaptionItem> captions)
    {
        var issues = new List<SubtitleValidationIssue>();
        var ordered = captions.OrderBy(c => c.StartTime).ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];

            if (current.EndTime <= current.StartTime)
            {
                issues.Add(new SubtitleValidationIssue
                {
                    CaptionIndex = current.Index,
                    CaptionId = current.Id,
                    Severity = SubtitleValidationSeverity.Error,
                    Message = $"Caption #{current.Index} has invalid duration (end {current.EndTime:mm\\:ss\\.fff} <= start {current.StartTime:mm\\:ss\\.fff})."
                });
            }

            if (string.IsNullOrWhiteSpace(current.Text))
            {
                issues.Add(new SubtitleValidationIssue
                {
                    CaptionIndex = current.Index,
                    CaptionId = current.Id,
                    Severity = SubtitleValidationSeverity.Warning,
                    Message = $"Caption #{current.Index} is empty."
                });
            }

            if (current.Duration.TotalSeconds < 0.4 && !string.IsNullOrWhiteSpace(current.Text))
            {
                issues.Add(new SubtitleValidationIssue
                {
                    CaptionIndex = current.Index,
                    CaptionId = current.Id,
                    Severity = SubtitleValidationSeverity.Warning,
                    Message = $"Caption #{current.Index} duration is very short ({current.Duration.TotalSeconds:F2}s)."
                });
            }

            if (current.CharactersPerSecond > 26.0)
            {
                issues.Add(new SubtitleValidationIssue
                {
                    CaptionIndex = current.Index,
                    CaptionId = current.Id,
                    Severity = SubtitleValidationSeverity.Warning,
                    Message = $"Caption #{current.Index} reading speed is high ({current.CharactersPerSecond:F1} chars/sec)."
                });
            }

            // Check overlap with next
            if (i < ordered.Count - 1)
            {
                var next = ordered[i + 1];
                if (current.EndTime > next.StartTime)
                {
                    current.HasOverlapWarning = true;
                    issues.Add(new SubtitleValidationIssue
                    {
                        CaptionIndex = current.Index,
                        CaptionId = current.Id,
                        Severity = SubtitleValidationSeverity.Warning,
                        Message = $"Caption #{current.Index} overlaps with #{next.Index} by {(current.EndTime - next.StartTime).TotalMilliseconds:F0}ms."
                    });
                }
            }
        }

        return issues;
    }

    public static int SearchAndReplace(IEnumerable<CaptionItem> captions, string searchText, string replaceText, bool matchCase = false)
    {
        if (string.IsNullOrEmpty(searchText)) return 0;

        int count = 0;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var caption in captions)
        {
            if (caption.Text.Contains(searchText, comparison))
            {
                if (matchCase)
                {
                    caption.Text = caption.Text.Replace(searchText, replaceText);
                }
                else
                {
                    caption.Text = Regex.Replace(caption.Text, Regex.Escape(searchText), replaceText, RegexOptions.IgnoreCase);
                }
                count++;
            }
        }

        return count;
    }

    #endregion

    #region File I/O

    public static void SaveSrtFile(IEnumerable<CaptionItem> captions, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var srtText = GenerateSrt(captions);
        File.WriteAllText(filePath, srtText, Encoding.UTF8);
    }

    public static List<CaptionItem> LoadSrtFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Subtitle file not found: {filePath}");

        var srtText = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseSrt(srtText);
    }

    public static void SaveVttFile(IEnumerable<CaptionItem> captions, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var vttText = GenerateVtt(captions);
        File.WriteAllText(filePath, vttText, Encoding.UTF8);
    }

    public static List<CaptionItem> LoadVttFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Subtitle file not found: {filePath}");

        var vttText = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseVtt(vttText);
    }

    #endregion

    #region Helpers

    private static (string? Speaker, string CleanText) ExtractSpeaker(string text)
    {
        // Matches [Speaker Name] or <v Speaker Name> or Speaker:
        var bracketMatch = Regex.Match(text, @"^\[([^\]]+)\]\s*(.*)$", RegexOptions.Singleline);
        if (bracketMatch.Success)
        {
            return (bracketMatch.Groups[1].Value.Trim(), bracketMatch.Groups[2].Value.Trim());
        }

        var voiceTagMatch = Regex.Match(text, @"^<v\s+([^>]+)>(.*?)(?:</v>)?$", RegexOptions.Singleline);
        if (voiceTagMatch.Success)
        {
            return (voiceTagMatch.Groups[1].Value.Trim(), voiceTagMatch.Groups[2].Value.Trim());
        }

        return (null, text);
    }

    private static TimeSpan ParseTimeSpan(string h, string m, string s, string ms)
    {
        int hours = int.Parse(h, CultureInfo.InvariantCulture);
        int minutes = int.Parse(m, CultureInfo.InvariantCulture);
        int seconds = int.Parse(s, CultureInfo.InvariantCulture);
        int milliseconds = int.Parse(ms, CultureInfo.InvariantCulture);

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }

    private static string FormatSrtTime(TimeSpan t)
    {
        int hours = (int)t.TotalHours;
        int minutes = t.Minutes;
        int seconds = t.Seconds;
        int milliseconds = t.Milliseconds;

        return $"{hours:D2}:{minutes:D2}:{seconds:D2},{milliseconds:D3}";
    }

    private static string FormatVttTime(TimeSpan t)
    {
        int hours = (int)t.TotalHours;
        int minutes = t.Minutes;
        int seconds = t.Seconds;
        int milliseconds = t.Milliseconds;

        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    #endregion
}
