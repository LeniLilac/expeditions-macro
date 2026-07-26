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
