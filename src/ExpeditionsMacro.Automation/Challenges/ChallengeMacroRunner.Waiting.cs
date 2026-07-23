using ExpeditionsMacro.Automation.Activity;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task WaitUntilAsync(
        RobloxWindow window,
        ChallengePreset preset,
        IDetectorPack detector,
        char playMenuKey,
        DateTimeOffset untilUtc,
        bool dailyLimit,
        Func<DateTimeOffset, CancellationToken, Task>? idleWorkflow,
        Action<string, MacroEventLevel, string?, double?> log,
        Action<string, int, string, string?, double?> report,
        CancellationToken cancellationToken)
    {
        if (preset.IdleBehavior == ChallengeIdleBehavior.RunExpeditions && idleWorkflow is not null)
        {
            report("Waiting", 0, dailyLimit ? "Daily Challenge limits reached. Running Expeditions until midnight UTC." : "Challenges complete. Running Expeditions until the next reset.", null, null);
            await PrepareSchedulerHandoffAsync(window, preset, detector, log, report, cancellationToken).ConfigureAwait(false);
            await idleWorkflow(untilUtc, cancellationToken).ConfigureAwait(false);
            await ReturnFromIdleModeAsync(window, preset, detector, playMenuKey, log, report, cancellationToken).ConfigureAwait(false);
            return;
        }

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
