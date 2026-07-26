using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Challenges;

public static class ChallengeMatchStateDetector
{
    public static ChallengeScreenMatch Detect(
        ImageFrame image)
    {
        ValidateClient(image);
        double defeat =
            TerminalScreenDetector.Score(image, "defeat");
        double victory = ChallengeVictoryDetector.Score(
            image,
            ActionButtonDetector.Score(
                image,
                "challenge_victory_close"),
            ActionButtonDetector.Score(
                image,
                "challenge_victory_party"));
        double gameMode =
            ChallengeScreenDetector.GameModeSelectorScore(
                image);
        IReadOnlyDictionary<ChallengeScreenState, double>
            scores =
            new Dictionary<ChallengeScreenState, double>
            {
                [ChallengeScreenState.Defeat] = defeat,
                [ChallengeScreenState.Victory] = victory,
                [ChallengeScreenState.GameModeSelector] =
                    gameMode,
            };
        ChallengeScreenState state =
            defeat >= ChallengeScreenDetector.Threshold(
                ChallengeScreenState.Defeat)
                ? ChallengeScreenState.Defeat
                : victory >=
                    ChallengeScreenDetector.Threshold(
                        ChallengeScreenState.Victory)
                    ? ChallengeScreenState.Victory
                    : gameMode >=
                        ChallengeScreenDetector.Threshold(
                            ChallengeScreenState
                                .GameModeSelector)
                        ? ChallengeScreenState
                            .GameModeSelector
                        : ChallengeScreenState.None;
        double confidence =
            state == ChallengeScreenState.None
                ? scores.Values.Max()
                : scores[state];
        return Trace(
            state,
            confidence,
            scores);
    }

    private static ChallengeScreenMatch Trace(
        ChallengeScreenState state,
        double confidence,
        IReadOnlyDictionary<
            ChallengeScreenState,
            double> scores)
    {
        VisionTrace.Emit(
            "challenge_match_screen",
            state.ToString(),
            confidence,
            new { Scores = scores });
        return new ChallengeScreenMatch(
            state,
            confidence);
    }

    private static void ValidateClient(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != ChallengeScreenDetector.ClientWidth ||
            image.Height != ChallengeScreenDetector.ClientHeight)
        {
            throw new InvalidDataException(
                $"Challenge match detector input must be an RGB {ChallengeScreenDetector.ClientWidth} by {ChallengeScreenDetector.ClientHeight} client image.");
        }
    }
}
