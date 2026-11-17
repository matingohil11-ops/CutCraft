using CutCraft.Core.Models;
using Xunit;

namespace CutCraft.Core.Tests;

public class MediaFormatsTests
{
    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".mov")]
    [InlineData(".avi")]
    [InlineData(".wmv")]
    [InlineData(".webm")]
    [InlineData(".flv")]
    [InlineData(".ts")]
    [InlineData(".mts")]
    [InlineData(".m2ts")]
    [InlineData(".3gp")]
    [InlineData(".ogv")]
    [InlineData(".vob")]
    [InlineData(".mpg")]
    [InlineData(".mpeg")]
    [InlineData(".m4v")]
    [InlineData(".mxf")]
    [InlineData(".rmvb")]
    [InlineData(".asf")]
    [InlineData(".h264")]
    [InlineData(".h265")]
    [InlineData(".hevc")]
    [InlineData(".divx")]
    public void IsVideoExtension_RecognizesMajorVideoFormats(string extension)
    {
        Assert.True(MediaFormats.IsVideoExtension(extension));
        Assert.Equal(AssetType.Video, MediaFormats.DetectAssetType($"sample{extension}"));
        Assert.True(MediaFormats.IsSupportedMedia($"sample{extension}"));
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".aac")]
    [InlineData(".m4a")]
    [InlineData(".flac")]
    [InlineData(".ogg")]
    [InlineData(".opus")]
    [InlineData(".wma")]
    [InlineData(".aiff")]
    [InlineData(".mka")]
    [InlineData(".ape")]
    [InlineData(".ac3")]
    [InlineData(".dts")]
    [InlineData(".amr")]
    [InlineData(".mid")]
    public void IsAudioExtension_RecognizesMajorAudioFormats(string extension)
    {
        Assert.True(MediaFormats.IsAudioExtension(extension));
        Assert.Equal(AssetType.Audio, MediaFormats.DetectAssetType($"music_track{extension}"));
        Assert.True(MediaFormats.IsSupportedMedia($"music_track{extension}"));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    [InlineData(".gif")]
    [InlineData(".tiff")]
    [InlineData(".ico")]
    [InlineData(".heic")]
    [InlineData(".avif")]
    [InlineData(".svg")]
    public void IsImageExtension_RecognizesMajorImageFormats(string extension)
    {
        Assert.True(MediaFormats.IsImageExtension(extension));
        Assert.Equal(AssetType.Image, MediaFormats.DetectAssetType($"graphic{extension}"));
        Assert.True(MediaFormats.IsSupportedMedia($"graphic{extension}"));
    }

    [Fact]
    public void CaseInsensitive_ExtensionMatching()
    {
        Assert.True(MediaFormats.IsVideoExtension("CLIP.MP4"));
        Assert.True(MediaFormats.IsVideoExtension("CLIP.MKV"));
        Assert.True(MediaFormats.IsAudioExtension("SONG.WAV"));
        Assert.True(MediaFormats.IsAudioExtension("SONG.FLAC"));
        Assert.True(MediaFormats.IsImageExtension("PHOTO.JPEG"));
        Assert.True(MediaFormats.IsImageExtension("PHOTO.PNG"));
    }

    [Fact]
    public void UnknownExtensions_AreHandledGracefully()
    {
        Assert.False(MediaFormats.IsSupportedMedia("document.pdf"));
        Assert.False(MediaFormats.IsSupportedMedia("archive.zip"));
        Assert.False(MediaFormats.IsSupportedMedia("data.bin"));
        Assert.False(MediaFormats.IsSupportedMedia(""));
        Assert.False(MediaFormats.IsSupportedMedia(null));
    }

    [Fact]
    public void FilterStrings_ContainMajorExtensions()
    {
        var mediaFilter = MediaFormats.MediaImportFilter;
        Assert.Contains("*.mp4", mediaFilter);
        Assert.Contains("*.mkv", mediaFilter);
        Assert.Contains("*.webm", mediaFilter);
        Assert.Contains("*.flv", mediaFilter);
        Assert.Contains("*.ts", mediaFilter);
        Assert.Contains("*.mp3", mediaFilter);
        Assert.Contains("*.wav", mediaFilter);
        Assert.Contains("*.flac", mediaFilter);
        Assert.Contains("*.png", mediaFilter);
        Assert.Contains("*.webp", mediaFilter);

        var audioFilter = MediaFormats.AudioImportFilter;
        Assert.Contains("*.mp3", audioFilter);
        Assert.Contains("*.wav", audioFilter);
        Assert.Contains("*.flac", audioFilter);
        Assert.Contains("*.ogg", audioFilter);
        Assert.Contains("*.opus", audioFilter);
    }
}
