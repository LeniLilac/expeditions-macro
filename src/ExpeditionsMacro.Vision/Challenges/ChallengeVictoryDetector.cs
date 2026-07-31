namespace ExpeditionsMacro.Vision.Challenges;

internal static class ChallengeVictoryDetector
{
    public static double Score(
        double closeAction,
        double partyAction)
    {
        if (closeAction == 0 ||
            partyAction == 0)
        {
            return 0;
        }

        // Unit portraits, roster size, reward values, and reward colors vary
        // independently of the terminal. The wide View Party action and isolated
        // Close action are live, independent controls unique to Victory.
        return Math.Clamp(
            0.45 * closeAction +
            0.55 * partyAction,
            0,
            1);
    }
}
