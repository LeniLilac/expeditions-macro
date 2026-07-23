using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private static string DescribeUnavailable(
        string detail,
        DateTimeOffset nextEligibleUtc,
        bool returnToScheduler) =>
        returnToScheduler
            ? $"{detail} The next eligible time is {nextEligibleUtc:HH:mm} UTC."
            : $"{detail} Waiting for the next global reset at {nextEligibleUtc:HH:mm} UTC.";

    private async Task WaitUntilAsync(
        RobloxWindow window,
        DateTimeOffset untilUtc,
        bool dailyLimit,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        InactivityKeepAlive keepAlive = new();
        while (DateTimeOffset.UtcNow < untilUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = untilUtc - DateTimeOffset.UtcNow;
            string message = dailyLimit
                ? $"Daily limit reached. Checking again after midnight UTC in {FormatRemaining(remaining)}."
                : $"Waiting for the next Challenge reset in {FormatRemaining(remaining)}.";
            report("Waiting", 0, message, dailyLimit ? "daily_limit" : "cooldown", null);
            await keepAlive.TryPulseAsync((key, token) => _automation.TapLetterKeyAsync(window, key, token), cancellationToken).ConfigureAwait(false);
            await Task.Delay(remaining < TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        log("Challenge wait finished. Rechecking the selector.", MacroEventLevel.Information, null, null);
    }
}
