using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CutCraft.Core.Models;

namespace CutCraft.Core.Subtitles;

public static class SrtSubtitleService
{
    private static readonly Regex TimestampRegex = new(
        @"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})",
        RegexOptions.Compiled);

    public static List<CaptionItem> ParseSrt(string srtContent)
    {
        var result = new List<CaptionItem>();
        if (string.IsNullOrWhiteSpace(srtContent)) return result;

        var lines = srtContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int index = 0;
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            // Check if line is index
            if (int.TryParse(line, out var parsedIndex))
            {
                i++;
                if (i >= lines.Length) break;
                line = lines[i].Trim();
            }

            // Parse timestamp line
            var match = TimestampRegex.Match(line);
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

                index++;
                result.Add(new CaptionItem(index, start, end, textBuilder.ToString()));
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
            sb.AppendLine(caption.Text);
            sb.AppendLine();
            counter++;
        }

        return sb.ToString();
    }

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
}
