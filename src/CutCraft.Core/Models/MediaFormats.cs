using System.Collections.Frozen;
using System.IO;

namespace CutCraft.Core.Models;

/// <summary>
/// Comprehensive registry of all supported video, audio, and image media extensions
/// recognized and playable by CutCraft.
/// </summary>
public static class MediaFormats
{
    // ==========================================
    // 1. VIDEO EXTENSIONS (50+ containers/codecs)
    // ==========================================
    public static readonly FrozenSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // MP4 & QuickTime family
        ".mp4", ".m4v", ".mp4v", ".m4p", ".mov", ".qt",

        // Matroska family
        ".mkv", ".mk3d", ".mks",

        // AVI & Windows Media
        ".avi", ".wmv", ".asf",

        // Web & Flash Video
        ".webm", ".flv", ".f4v", ".f4p", ".f4a", ".f4b",

        // MPEG Transport Stream & AVCHD
        ".ts", ".mts", ".m2ts", ".m2t", ".trp", ".tp",

        // MPEG Program Stream & DVD
        ".mpg", ".mpeg", ".m1v", ".m2v", ".mpv", ".mpe", ".vob", ".dat", ".mod", ".tod",

        // 3GPP Mobile Video
        ".3gp", ".3gpp", ".3g2", ".3gp2",

        // Ogg Video
        ".ogv", ".ogg",

        // RealMedia
        ".rm", ".rmvb", ".rv",

        // Professional, Broadcast & Camcorder
        ".mxf", ".nut", ".roq", ".y4m",

        // Windows Media Center & DVR
        ".wtv", ".dvr-ms",

        // Raw Codec Bitstreams & High Efficiency
        ".h264", ".264", ".h265", ".265", ".hevc", ".av1", ".ivf", ".vc1", ".bik", ".smk",

        // DivX / XviD
        ".divx", ".xvid"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // ==========================================
    // 2. AUDIO EXTENSIONS (30+ formats)
    // ==========================================
    public static readonly FrozenSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // MPEG Audio
        ".mp3", ".mpa", ".m2a", ".mp1",

        // Linear PCM / Waveform
        ".wav", ".wave", ".bwf",

        // Advanced Audio Coding & Apple
        ".aac", ".m4a", ".m4b", ".alac",

        // Lossless Audio
        ".flac", ".ape", ".wv", ".tta",

        // Ogg & Opus
        ".ogg", ".oga", ".opus", ".spx",

        // Windows Media Audio
        ".wma",

        // Matroska Audio
        ".mka",

        // Audio Interchange File Format
        ".aiff", ".aif", ".aifc",

        // Multichannel Surround & Cinema
        ".ac3", ".eac3", ".dts", ".dtshd", ".truehd",

        // Speech & Mobile
        ".amr", ".awb",

        // MIDI
        ".mid", ".midi", ".kar",

        // Web & Legacy Audio
        ".caf", ".weba", ".voc", ".ra", ".ram"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // ==========================================
    // 3. IMAGE EXTENSIONS (20+ formats)
    // ==========================================
    public static readonly FrozenSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Standard Web & Photo
        ".png", ".jpg", ".jpeg", ".jpe", ".jfif",

        // Bitmap
        ".bmp", ".dib",

        // Modern Formats
        ".webp", ".avif", ".heic", ".heif",

        // Animated & Graphics
        ".gif",

        // High Dynamic Range & Print
        ".tiff", ".tif",

        // Icons & Vector
        ".ico", ".svg"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given file path or extension belongs to any supported video format.
    /// </summary>
    public static bool IsVideoExtension(string? filePathOrExt)
    {
        var ext = NormalizeExtension(filePathOrExt);
        return !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
    }

    /// <summary>
    /// Checks whether the given file path or extension belongs to any supported audio format.
    /// </summary>
    public static bool IsAudioExtension(string? filePathOrExt)
    {
        var ext = NormalizeExtension(filePathOrExt);
        return !string.IsNullOrEmpty(ext) && AudioExtensions.Contains(ext);
    }

    /// <summary>
    /// Checks whether the given file path or extension belongs to any supported image format.
    /// </summary>
    public static bool IsImageExtension(string? filePathOrExt)
    {
        var ext = NormalizeExtension(filePathOrExt);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// Checks whether the file is any recognized media type (video, audio, or image).
    /// </summary>
    public static bool IsSupportedMedia(string? filePathOrExt)
    {
        var ext = NormalizeExtension(filePathOrExt);
        return !string.IsNullOrEmpty(ext) &&
               (VideoExtensions.Contains(ext) || AudioExtensions.Contains(ext) || ImageExtensions.Contains(ext));
    }

    /// <summary>
    /// Detects the AssetType (Video, Audio, or Image) based on the file extension.
    /// </summary>
    public static AssetType DetectAssetType(string filePath)
    {
        var ext = NormalizeExtension(filePath);
        if (AudioExtensions.Contains(ext)) return AssetType.Audio;
        if (ImageExtensions.Contains(ext)) return AssetType.Image;
        return AssetType.Video;
    }

    /// <summary>
    /// Standardized OpenFileDialog filter for importing all media files into CutCraft.
    /// </summary>
    public static string MediaImportFilter { get; } =
        "All Supported Media Files|" +
        "*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.ts;*.mts;*.m2ts;*.3gp;*.ogv;*.vob;*.mpg;*.mpeg;*.m4v;*.mxf;*.rm;*.rmvb;*.asf;*.f4v;*.wtv;*.dvr-ms;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.aiff;*.mka;*.ape;*.ac3;*.dts;*.amr;*.mid;*.midi;*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif;*.tiff;*.tif;*.ico;*.heic;*.avif|" +
        "All Video Files (*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.ts;*.3gp;*.mpg;*.mxf;...)|" +
        "*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.webm;*.ts;*.mts;*.m2ts;*.m2t;*.3gp;*.3g2;*.ogv;*.vob;*.mpg;*.mpeg;*.m1v;*.m2v;*.m4v;*.mxf;*.rm;*.rmvb;*.asf;*.f4v;*.wtv;*.dvr-ms;*.h264;*.h265;*.hevc;*.av1;*.divx;*.xvid|" +
        "All Audio Files (*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.aiff;...)|" +
        "*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.aifc;*.alac;*.mka;*.ape;*.ac3;*.eac3;*.dts;*.dtshd;*.amr;*.awb;*.mid;*.midi;*.caf;*.weba|" +
        "All Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif;*.tiff;...)|" +
        "*.png;*.jpg;*.jpeg;*.jpe;*.jfif;*.bmp;*.dib;*.webp;*.gif;*.tiff;*.tif;*.ico;*.heic;*.heif;*.avif;*.svg|" +
        "MP4 / M4V Video (*.mp4;*.m4v)|*.mp4;*.m4v|" +
        "Matroska Video (*.mkv)|*.mkv;*.mk3d|" +
        "QuickTime Video (*.mov;*.qt)|*.mov;*.qt|" +
        "AVI Video (*.avi)|*.avi|" +
        "Windows Media Video (*.wmv;*.asf)|*.wmv;*.asf|" +
        "WebM Video (*.webm)|*.webm|" +
        "Flash Video (*.flv;*.f4v)|*.flv;*.f4v|" +
        "MPEG & Transport Streams (*.ts;*.mts;*.m2ts;*.mpg;*.mpeg;*.vob)|*.ts;*.mts;*.m2ts;*.m2t;*.mpg;*.mpeg;*.vob;*.dat|" +
        "All Files (*.*)|*.*";

    /// <summary>
    /// Standardized OpenFileDialog filter for adding audio & music files.
    /// </summary>
    public static string AudioImportFilter { get; } =
        "All Supported Audio Files|" +
        "*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.aifc;*.alac;*.mka;*.ape;*.ac3;*.eac3;*.dts;*.dtshd;*.amr;*.awb;*.mid;*.midi;*.caf;*.weba|" +
        "MP3 Audio (*.mp3)|*.mp3;*.mpa;*.m2a|" +
        "WAV Uncompressed Audio (*.wav)|*.wav;*.wave;*.bwf|" +
        "AAC / M4A Audio (*.m4a;*.aac)|*.m4a;*.aac;*.m4b;*.alac|" +
        "FLAC & Lossless Audio (*.flac;*.ape)|*.flac;*.ape;*.wv;*.tta|" +
        "Ogg / Opus Audio (*.ogg;*.opus)|*.ogg;*.oga;*.opus;*.spx|" +
        "Windows Media Audio (*.wma)|*.wma|" +
        "All Files (*.*)|*.*";

    private static string NormalizeExtension(string? filePathOrExt)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExt)) return string.Empty;
        var ext = Path.HasExtension(filePathOrExt)
            ? Path.GetExtension(filePathOrExt)
            : (filePathOrExt.StartsWith('.') ? filePathOrExt : "." + filePathOrExt);
        return ext.ToLowerInvariant();
    }
}
