using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

internal static class ChallengePrestartTimeoutPolicy
{
    public static RobloxSessionUnavailableException CreateException(
        bool teleportingSeen,
        ChallengeScreenState lastObservedState)
    {
        if (!teleportingSeen)
        {
            return new RobloxSessionUnavailableException(
                "Roblox did not reach a recognized Challenge prestart screen before the stage-load deadline.");
        }

        return lastObservedState == ChallengeScreenState.Teleporting
            ? new RobloxSessionUnavailableException(
                "Roblox remained on the Teleporting screen for three minutes and did not reach the Challenge prestart screen.")
            : new RobloxSessionUnavailableException(
                "Roblox left the Teleporting screen, but the loaded Challenge screen was not recognized before the three-minute deadline.");
    }
}
