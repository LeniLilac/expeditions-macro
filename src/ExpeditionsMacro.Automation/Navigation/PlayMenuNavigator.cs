using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Navigation;

internal static class PlayMenuNavigator
{
    internal const int MaximumAttempts = 3;

    internal static async Task<ImageFrame> OpenWithRetriesAsync(
        char playMenuKey,
        Func<ImageFrame> capture,
        Func<char, CancellationToken, Task> pressKey,
        Func<TimeSpan, CancellationToken, Task<ImageFrame?>> waitForPreview,
        Action<int>? attemptStarted,
        Action<int>? attemptMissed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(pressKey);
        ArgumentNullException.ThrowIfNull(waitForPreview);
        char key = char.ToUpperInvariant(playMenuKey);
        if (!char.IsAsciiLetter(key)) throw new ArgumentOutOfRangeException(nameof(playMenuKey));

        TimeSpan transitionTimeout = TimeSpan.FromSeconds(4);
        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame current = capture();
            if (ChallengeScreenDetector.Detect(current).State ==
                ChallengeScreenState.PostMatchPreview)
            {
                ImageFrame? verified = await waitForPreview(
                    transitionTimeout,
                    cancellationToken).ConfigureAwait(false);
                if (verified is not null) return verified;
                continue;
            }

            attemptStarted?.Invoke(attempt);
            await pressKey(key, cancellationToken).ConfigureAwait(false);
            ImageFrame? preview = await waitForPreview(transitionTimeout, cancellationToken).ConfigureAwait(false);
            if (preview is not null) return preview;
            attemptMissed?.Invoke(attempt);
        }

        throw new InvalidOperationException(
            $"The Play menu did not open after {MaximumAttempts} {key}-key attempts. Confirm Anime Expeditions' Toggle Play Menu binding is also set to {key}.");
    }
}
