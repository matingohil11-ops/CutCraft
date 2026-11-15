namespace CutCraft.Core.Models;

public enum TransitionType
{
    None,
    CrossFade,
    FadeToBlack,
    FadeToWhite,
    WipeLeft,
    WipeRight,
    WipeUp,
    WipeDown,
    SlideLeft,
    SlideRight,
    Dissolve,
    CircleCrop,
    Radial
}

public class Transition
{
    public TransitionType Type { get; set; } = TransitionType.None;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1.0);

    public string ToFFmpegXfadeTransition()
    {
        return Type switch
        {
            TransitionType.CrossFade => "fade",
            TransitionType.FadeToBlack => "fadeblack",
            TransitionType.FadeToWhite => "fadewhite",
            TransitionType.WipeLeft => "wipeleft",
            TransitionType.WipeRight => "wiperight",
            TransitionType.WipeUp => "wipeup",
            TransitionType.WipeDown => "wipedown",
            TransitionType.SlideLeft => "slideleft",
            TransitionType.SlideRight => "slideright",
            TransitionType.Dissolve => "dissolve",
            TransitionType.CircleCrop => "circlecrop",
            TransitionType.Radial => "radial",
            _ => "fade"
        };
    }
}
