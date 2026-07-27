using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Automation.Events;

internal static class EventRunPolicy
{
    public static string? RecoveryCandidate(
        EventScreenState eventState,
        string? recoveryState)
    {
        if (eventState ==
            EventScreenState.GameModeSelector)
        {
            return "play";
        }

        return recoveryState is
            "afk" or
            "disconnect" or
            "lobby"
                ? recoveryState
                : null;
    }

    public static string RecoveryLabel(
        string state) =>
        state switch
        {
            "afk" => "the AFK Chamber",
            "disconnect" => "a disconnect",
            "lobby" => "the lobby",
            "play" => "the Play interface",
            _ => state,
        };
}

internal sealed class EventTerminalRuntimeGuard
{
    private readonly int _stableDetections;
    private readonly Func<DateTimeOffset>? _utcNow;
    private ObservationWaitBudget? _confirmationBudget;

    public EventTerminalRuntimeGuard(
        int stableDetections,
        Func<DateTimeOffset>? utcNow = null)
    {
        _stableDetections =
            Math.Max(2, stableDetections);
        _utcNow = utcNow;
    }

    public bool ShouldEnforceRuntimeLimit(
        bool hasTerminalCandidate,
        bool confirmationPending)
    {
        if (!hasTerminalCandidate)
        {
            _confirmationBudget = null;
            return true;
        }

        _confirmationBudget ??= new ObservationWaitBudget(
            TimeSpan.FromSeconds(3),
            _stableDetections,
            _utcNow);
        _confirmationBudget.MarkObserved();
        return !confirmationPending ||
            !_confirmationBudget.ShouldObserve(
                confirmationPending: true);
    }
}
