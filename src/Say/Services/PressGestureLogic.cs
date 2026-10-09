using Say.Models;

namespace Say.Services;

public enum PressOutcome
{
    Ignore,
    StartTracking,
    Latch,
    Stop,
}

public static class PressGestureLogic
{
    public const int TapThresholdMs = 350;

    public static PressOutcome OnPress(DictationState state, bool latched) => state switch
    {
        DictationState.Listening when latched => PressOutcome.Stop,
        DictationState.Idle => PressOutcome.StartTracking,
        _ => PressOutcome.Ignore,
    };

    public static PressOutcome OnRelease(bool tracking, int heldMs, DictationState state)
    {
        if (!tracking || state != DictationState.Listening)
        {
            return PressOutcome.Ignore;
        }

        return heldMs < TapThresholdMs ? PressOutcome.Latch : PressOutcome.Stop;
    }
}
