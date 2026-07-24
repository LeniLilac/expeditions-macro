using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Navigation;

public sealed class PlayMenuBindingException : InvalidOperationException
{
    public PlayMenuBindingException(char key)
        : base(
            $"The Play interface did not open with {key}.\n\n" +
            $"Open Anime Expeditions Settings > Keybinds, set Toggle Play Menu to {key}, and try again. " +
            "The Play menu key under Expeditions Macro Settings > Controls must use the same letter.")
    {
        Key = key;
    }

    public char Key { get; }
}

internal static class LobbyPlayNavigator
{
    internal const int MaximumAttempts = 3;

    internal static async Task OpenWithVerificationAsync(
        char playMenuKey,
        Func<ImageFrame> capture,
        Func<ImageFrame, bool> isLobby,
        Func<ImageFrame, bool> isOpen,
        Func<char, CancellationToken, Task> pressKey,
        Func<TimeSpan, CancellationToken, Task<bool>> waitForOpen,
        Action<int>? keyAttemptStarted,
        Action<int>? keyAttemptMissed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(isLobby);
        ArgumentNullException.ThrowIfNull(isOpen);
        ArgumentNullException.ThrowIfNull(pressKey);
        ArgumentNullException.ThrowIfNull(waitForOpen);

        char key = char.ToUpperInvariant(playMenuKey);
        if (!char.IsAsciiLetter(key)) throw new ArgumentOutOfRangeException(nameof(playMenuKey));

        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame current = capture();
            if (isOpen(current))
            {
                if (await waitForOpen(
                        TimeSpan.FromSeconds(3),
                        cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
                continue;
            }

            if (!isLobby(current))
            {
                if (await waitForOpen(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false)) return;
                continue;
            }

            keyAttemptStarted?.Invoke(attempt);
            await pressKey(key, cancellationToken).ConfigureAwait(false);
            if (await waitForOpen(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false)) return;

            current = capture();
            if (isOpen(current) &&
                await waitForOpen(
                    TimeSpan.FromSeconds(3),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            keyAttemptMissed?.Invoke(attempt);
        }

        throw new PlayMenuBindingException(key);
    }

}
