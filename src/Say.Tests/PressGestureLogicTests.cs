using Say.Models;
using Say.Services;

namespace Say.Tests;

public class PressGestureLogicTests
{
    [Fact]
    public void Press_WhenIdle_StartsTracking()
    {
        Assert.Equal(PressOutcome.StartTracking, PressGestureLogic.OnPress(DictationState.Idle, latched: false));
    }

    [Fact]
    public void Press_WhenLatchedListening_Stops()
    {
        Assert.Equal(PressOutcome.Stop, PressGestureLogic.OnPress(DictationState.Listening, latched: true));
    }

    [Fact]
    public void Press_WhenPushToTalkListening_IsIgnored()
    {
        Assert.Equal(PressOutcome.Ignore, PressGestureLogic.OnPress(DictationState.Listening, latched: false));
    }

    [Theory]
    [InlineData(DictationState.Processing)]
    [InlineData(DictationState.Done)]
    public void Press_WhenBusy_IsIgnored(DictationState state)
    {
        Assert.Equal(PressOutcome.Ignore, PressGestureLogic.OnPress(state, latched: false));
    }

    [Fact]
    public void Release_QuickTap_Latches()
    {
        Assert.Equal(
            PressOutcome.Latch,
            PressGestureLogic.OnRelease(tracking: true, heldMs: 120, DictationState.Listening));
    }

    [Fact]
    public void Release_Hold_Stops()
    {
        Assert.Equal(
            PressOutcome.Stop,
            PressGestureLogic.OnRelease(tracking: true, heldMs: 1500, DictationState.Listening));
    }

    [Fact]
    public void Release_ExactlyAtThreshold_Stops()
    {
        Assert.Equal(
            PressOutcome.Stop,
            PressGestureLogic.OnRelease(
                tracking: true, heldMs: PressGestureLogic.TapThresholdMs, DictationState.Listening));
    }

    [Fact]
    public void Release_WithoutTracking_IsIgnored()
    {
        Assert.Equal(
            PressOutcome.Ignore,
            PressGestureLogic.OnRelease(tracking: false, heldMs: 50, DictationState.Listening));
    }

    [Fact]
    public void Release_WhenSessionNeverStarted_IsIgnored()
    {
        Assert.Equal(
            PressOutcome.Ignore,
            PressGestureLogic.OnRelease(tracking: true, heldMs: 50, DictationState.Idle));
    }
}
