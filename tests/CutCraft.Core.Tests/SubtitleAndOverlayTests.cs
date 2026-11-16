using CutCraft.Core.Commands;
using CutCraft.Core.Models;
using CutCraft.Core.Subtitles;
using CutCraft.Media.Export;
using Xunit;

namespace CutCraft.Core.Tests;

public class SubtitleAndOverlayTests
{
    [Fact]
    public void SrtSubtitleService_WebVtt_ParsesAndGeneratesFormat()
    {
        var sampleVtt = """
        WEBVTT

        1
        00:00:01.000 --> 00:00:04.500
        <v Narrator>Welcome to the CutCraft accessible editor.</v>

        2
        00:00:05.200 --> 00:00:08.800
        This test checks WebVTT parsing fidelity.
        """;

        var captions = SrtSubtitleService.ParseVtt(sampleVtt);

        Assert.Equal(2, captions.Count);
        Assert.Equal(1, captions[0].Index);
        Assert.Equal(TimeSpan.FromSeconds(1), captions[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(4.5), captions[0].EndTime);
        Assert.Equal("Narrator", captions[0].Speaker);
        Assert.Equal("Welcome to the CutCraft accessible editor.", captions[0].Text);

        Assert.Equal(2, captions[1].Index);
        Assert.Equal("This test checks WebVTT parsing fidelity.", captions[1].Text);

        var generatedVtt = SrtSubtitleService.GenerateVtt(captions);
        Assert.Contains("WEBVTT", generatedVtt);
        Assert.Contains("00:00:01.000 --> 00:00:04.500", generatedVtt);
        Assert.Contains("<v Narrator>Welcome to the CutCraft accessible editor.</v>", generatedVtt);
    }

    [Fact]
    public void SrtSubtitleService_ShiftCaptions_OffsetsTimingsAccurately()
    {
        var captions = new List<CaptionItem>
        {
            new CaptionItem(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), "First"),
            new CaptionItem(2, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(10), "Second")
        };

        // Shift forward by +1.5 seconds
        SrtSubtitleService.ShiftCaptions(captions, TimeSpan.FromSeconds(1.5));
        Assert.Equal(TimeSpan.FromSeconds(3.5), captions[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(6.5), captions[0].EndTime);

        // Shift backward by -2.0 seconds
        SrtSubtitleService.ShiftCaptions(captions, TimeSpan.FromSeconds(-2.0));
        Assert.Equal(TimeSpan.FromSeconds(1.5), captions[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(4.5), captions[0].EndTime);
    }

    [Fact]
    public void SrtSubtitleService_SplitAndMerge_PreservesTextAndDuration()
    {
        var original = new CaptionItem(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8), "The quick brown fox jumps over the lazy dog");
        var (left, right) = SrtSubtitleService.SplitCaption(original, TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(2), left.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(5), left.EndTime);
        Assert.Equal(TimeSpan.FromSeconds(5), right.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(8), right.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(left.Text));
        Assert.False(string.IsNullOrWhiteSpace(right.Text));

        // Now merge them back
        var merged = SrtSubtitleService.MergeCaptions(left, right);
        Assert.Equal(TimeSpan.FromSeconds(2), merged.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(8), merged.EndTime);
        Assert.Contains("quick brown", merged.Text);
        Assert.Contains("lazy dog", merged.Text);
    }

    [Fact]
    public void SrtSubtitleService_Validation_DetectsOverlapsAndFastReadingSpeed()
    {
        var captions = new List<CaptionItem>
        {
            // Normal
            new CaptionItem(1, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3), "Normal subtitle"),
            // Overlapping with #1 (starts at 2s, but #1 ends at 3s)
            new CaptionItem(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), "Overlapping cue"),
            // High CPS: 100 characters in 1 second
            new CaptionItem(3, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7), "This is an extremely long and rapid sentence designed specifically to trigger reading speed warnings!"),
            // Inverted timestamps (error)
            new CaptionItem(4, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(9), "Invalid duration")
        };

        var issues = SrtSubtitleService.ValidateCaptions(captions);

        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Message.Contains("overlaps"));
        Assert.Contains(issues, i => i.Message.Contains("reading speed is high"));
        Assert.Contains(issues, i => i.Severity == SubtitleValidationSeverity.Error);
    }

    [Fact]
    public void SrtSubtitleService_SearchAndReplace_ReplacesKeywords()
    {
        var captions = new List<CaptionItem>
        {
            new CaptionItem(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Hello world from CutCraft"),
            new CaptionItem(2, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8), "Goodbye world")
        };

        int count = SrtSubtitleService.SearchAndReplace(captions, "world", "universe");

        Assert.Equal(2, count);
        Assert.Equal("Hello universe from CutCraft", captions[0].Text);
        Assert.Equal("Goodbye universe", captions[1].Text);
    }

    [Fact]
    public void CaptionCommands_AddRemoveUpdate_UndoRedoWorksReliably()
    {
        var project = Project.CreateDefault("Caption Command Test");
        var cmdManager = new CommandManager();

        var caption = new CaptionItem(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Initial Caption Text");
        var addCmd = new AddCaptionCommand(project, caption);

        // Execute Add
        cmdManager.Execute(addCmd);
        Assert.Single(project.Captions);
        Assert.Equal("Initial Caption Text", project.Captions[0].Text);

        // Undo Add
        cmdManager.Undo();
        Assert.Empty(project.Captions);

        // Redo Add
        cmdManager.Redo();
        Assert.Single(project.Captions);

        // Update Caption
        var updatedSnapshot = new CaptionItem(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), "Edited Caption Text", "Speaker A", CaptionStylePreset.YellowCinema);
        var updateCmd = new UpdateCaptionCommand(caption, updatedSnapshot);
        cmdManager.Execute(updateCmd);

        Assert.Equal("Edited Caption Text", caption.Text);
        Assert.Equal("Speaker A", caption.Speaker);
        Assert.Equal(CaptionStylePreset.YellowCinema, caption.StylePreset);
        Assert.Equal(TimeSpan.FromSeconds(2), caption.StartTime);

        // Undo Update
        cmdManager.Undo();
        Assert.Equal("Initial Caption Text", caption.Text);
        Assert.Null(caption.Speaker);

        // Remove Caption
        var removeCmd = new RemoveCaptionCommand(project, caption);
        cmdManager.Execute(removeCmd);
        Assert.Empty(project.Captions);

        // Undo Remove
        cmdManager.Undo();
        Assert.Single(project.Captions);
    }

    [Fact]
    public void TitleCardPresets_CreateWellFormattedTemplates()
    {
        var lowerThird = TitleCardPresets.CreateLowerThird("Dr. Alan Grant", "Paleontologist", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
        Assert.Equal("Dr. Alan Grant", lowerThird.Text);
        Assert.Equal("Paleontologist", lowerThird.SubtitleText);
        Assert.Equal(TitleCardStyle.LowerThird, lowerThird.Style);
        Assert.Equal(TextAnimationType.SlideUp, lowerThird.Animation);
        Assert.Equal(OverlayAlignment.Left, lowerThird.Alignment);
        Assert.NotNull(lowerThird.BackgroundColorHex);

        var cinematic = TitleCardPresets.CreateCinematicHeader("JURASSIC HORIZON", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(4));
        Assert.Equal("JURASSIC HORIZON", cinematic.Text);
        Assert.Equal(TitleCardStyle.CinematicHeader, cinematic.Style);
        Assert.Equal(TextAnimationType.Fade, cinematic.Animation);
        Assert.Equal(OverlayAlignment.Center, cinematic.Alignment);

        var headline = TitleCardPresets.CreateBoldHeadline("BREAKING DISCOVERY");
        Assert.Equal(TitleCardStyle.BoldHeadline, headline.Style);
        Assert.Equal(TextAnimationType.ZoomIn, headline.Animation);

        var neon = TitleCardPresets.CreateNeonGlow("SYNTHWAVE 2099");
        Assert.Equal(TitleCardStyle.NeonGlow, neon.Style);
        Assert.Contains("00F0FF", neon.ColorHex);
    }

    [Fact]
    public void TextOverlayCommands_AddRemovePreset_UndoRedoWorksReliably()
    {
        var project = Project.CreateDefault("Overlay Command Test");
        var cmdManager = new CommandManager();

        var overlay = TitleCardPresets.CreateCinematicHeader("ORIGINAL TITLE", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));
        var addCmd = new AddTextOverlayCommand(project, overlay);

        cmdManager.Execute(addCmd);
        Assert.Single(project.Overlays);
        Assert.Equal("ORIGINAL TITLE", project.Overlays[0].Text);

        // Apply LowerThird Preset to existing overlay
        var presetTemplate = TitleCardPresets.CreateLowerThird();
        var applyCmd = new ApplyTitleCardPresetCommand(overlay, presetTemplate);
        cmdManager.Execute(applyCmd);

        Assert.Equal(TitleCardStyle.LowerThird, overlay.Style);
        Assert.Equal(TextAnimationType.SlideUp, overlay.Animation);
        Assert.Equal(OverlayAlignment.Left, overlay.Alignment);

        // Undo Preset application
        cmdManager.Undo();
        Assert.Equal(TitleCardStyle.CinematicHeader, overlay.Style);
        Assert.Equal(TextAnimationType.Fade, overlay.Animation);

        // Remove Overlay
        var removeCmd = new RemoveTextOverlayCommand(project, overlay);
        cmdManager.Execute(removeCmd);
        Assert.Empty(project.Overlays);

        // Undo Remove
        cmdManager.Undo();
        Assert.Single(project.Overlays);
    }

    [Fact]
    public void FFmpegFilterGraphBuilder_GeneratesAnimatedTextAndStyledCaptions()
    {
        var project = Project.CreateDefault("FFmpeg Overlay Export Test");
        var tempFile = Path.Combine(Path.GetTempPath(), "test_render_sub.mp4");
        File.WriteAllText(tempFile, "dummy");

        project.Tracks[0].Clips.Add(new Clip
        {
            Name = "Base Clip",
            AssetPath = tempFile,
            InPoint = TimeSpan.Zero,
            OutPoint = TimeSpan.FromSeconds(10),
            TimelineStart = TimeSpan.Zero
        });

        // Add SlideUp Lower Third overlay
        var lowerThird = TitleCardPresets.CreateLowerThird("Alice Walker", "Director of Photography", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));
        project.Overlays.Add(lowerThird);

        // Add Yellow Cinema caption
        project.Captions.Add(new CaptionItem(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(9), "Styled dialogue with yellow cinema font", null, CaptionStylePreset.YellowCinema));

        var builder = new FFmpegFilterGraphBuilder();
        var settings = new ExportSettings
        {
            OutputPath = Path.Combine(Path.GetTempPath(), "output_styled.mp4"),
            Resolution = ExportResolution.Fhd1080p,
            Quality = ExportQuality.Standard
        };

        var command = builder.BuildExportCommand(project, settings);

        Assert.NotNull(command);
        // Verify SlideUp animation alpha expression
        Assert.Contains("drawtext=text='Alice Walker'", command.FilterComplex);
        Assert.Contains("alpha='if(lt(t,", command.FilterComplex);
        // Verify secondary subtitle for lower third
        Assert.Contains("Director of Photography", command.FilterComplex);
        // Verify YellowCinema caption filter
        Assert.Contains("fontcolor=yellow", command.FilterComplex);
        Assert.Contains("Styled dialogue with yellow cinema font", command.FilterComplex);

        try { File.Delete(tempFile); } catch { }
    }
}
