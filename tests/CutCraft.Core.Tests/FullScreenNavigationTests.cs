using CutCraft.Core.Timeline;
using Xunit;

namespace CutCraft.Core.Tests;

public class FullScreenNavigationTests
{
    [Fact]
    public void FullScreenStateController_InitialState_IsWindowed()
    {
        var controller = new FullScreenStateController();
        Assert.False(controller.IsFullScreen);
        Assert.Equal("⛶ Full Screen", controller.ButtonText);
        Assert.Equal("Enter Full Screen (F11)", controller.ButtonTooltip);
        Assert.Equal("Full screen mode disabled.", controller.StatusAnnouncement);
    }

    [Fact]
    public void FullScreenStateController_Toggle_SwitchesStateAndFiresEvent()
    {
        var controller = new FullScreenStateController();
        bool? eventState = null;
        controller.FullScreenChanged += (s, isFull) => eventState = isFull;

        // Toggle on (F11 pressed)
        var result1 = controller.Toggle();
        Assert.True(result1);
        Assert.True(controller.IsFullScreen);
        Assert.True(eventState);
        Assert.Equal("🗗 Exit Full Screen", controller.ButtonText);
        Assert.Equal("Exit Full Screen (F11 or Esc)", controller.ButtonTooltip);
        Assert.Contains("Press F11 or Esc", controller.StatusAnnouncement);

        // Toggle off (F11 pressed again)
        var result2 = controller.Toggle();
        Assert.False(result2);
        Assert.False(controller.IsFullScreen);
        Assert.False(eventState);
        Assert.Equal("⛶ Full Screen", controller.ButtonText);
    }

    [Fact]
    public void FullScreenStateController_EnterAndExit_AreIdempotent()
    {
        var controller = new FullScreenStateController();
        int eventCount = 0;
        controller.FullScreenChanged += (s, isFull) => eventCount++;

        Assert.True(controller.Enter());
        Assert.Equal(1, eventCount);
        Assert.True(controller.IsFullScreen);

        // Repeated Enter should not fire duplicate event
        Assert.False(controller.Enter());
        Assert.Equal(1, eventCount);

        // Exit (Escape pressed)
        Assert.True(controller.Exit());
        Assert.Equal(2, eventCount);
        Assert.False(controller.IsFullScreen);

        // Repeated Exit should not fire duplicate event
        Assert.False(controller.Exit());
        Assert.Equal(2, eventCount);
    }
}
