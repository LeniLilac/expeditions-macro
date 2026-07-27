using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private static readonly TimeSpan ExtractionTransitionTimeout =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConfirmationDismissalTimeout =
        TimeSpan.FromSeconds(5);

    private async Task ExtractAtCheckpointAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ImageFrame checkpointFrame,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        Action<
            string,
            MacroEventLevel,
            string?,
            double?> log,
        CancellationToken cancellationToken)
    {
        ExtractionTransactionState transaction =
            new();
        if (!transaction.TryBegin())
        {
            throw new InvalidOperationException(
                "Could not begin checkpoint extraction.");
        }
        log(
            "Checkpoint has an Extract button; opening extraction confirmation.",
            MacroEventLevel.Information,
            "checkpoint",
            null);
        await ClickActionAsync(
            window,
            detector,
            "extract",
            checkpointFrame,
            cancellationToken).ConfigureAwait(false);
        bool found =
            await WaitForStateWithTimeoutAsync(
                window,
                detector,
                "extract_confirm",
                ExtractionTransitionTimeout,
                preset,
                report,
                cancellationToken)
            .ConfigureAwait(false);
        if (!found)
        {
            throw new RobloxUiUnavailableException(
                "Extraction confirmation did not appear within 30 seconds. The macro stopped without clicking Extract again to avoid a delayed duplicate action.");
        }
        await ConfirmExtractionAsync(
            window,
            detector,
            preset,
            transaction,
            clientImage: null,
            report,
            log,
            cancellationToken).ConfigureAwait(false);
        log(
            "Extraction confirmed. Waiting for Victory or an early Defeat screen.",
            MacroEventLevel.Success,
            "extract_confirm",
            null);
    }

    private async Task ConfirmExtractionAsync(
        RobloxWindow window,
        IDetectorPack detector,
        ExpeditionPreset preset,
        ExtractionTransactionState transaction,
        ImageFrame? clientImage,
        Action<
            string,
            int,
            string,
            string?,
            double?> report,
        Action<
            string,
            MacroEventLevel,
            string?,
            double?> log,
        CancellationToken cancellationToken)
    {
        if (!transaction.TryConfirm())
        {
            throw new InvalidOperationException(
                "Extraction confirmation was already clicked.");
        }
        ConfirmationDismissalState dismissal =
            new();
        while (dismissal.TryBeginAttempt())
        {
            ImageFrame frame =
                clientImage ??
                CaptureClient(window, detector);
            clientImage = null;
            IReadOnlyDictionary<string, double>
                scores = detector.ScoreStates(frame);
            if (!ExpeditionRunPolicy.IsStateDetected(
                    detector.Manifest,
                    scores,
                    "extract_confirm"))
            {
                if (!dismissal.TryComplete() ||
                    !transaction.TryComplete())
                {
                    throw new InvalidOperationException(
                        "Could not complete extraction confirmation handling.");
                }
                return;
            }

            report(
                "Extraction",
                0,
                dismissal.Attempts == 1
                    ? "Confirming extraction and waiting for the dialog to close."
                    : $"Extraction confirmation is still visible; retrying the focused click ({dismissal.Attempts}/{ConfirmationDismissalState.MaximumAttempts}).",
                "extract_confirm",
                scores["extract_confirm"]);
            await ClickActionAsync(
                window,
                detector,
                "extract_confirm",
                frame,
                cancellationToken).ConfigureAwait(false);
            bool dismissed =
                await WaitForStateToClearAsync(
                    window,
                    detector,
                    "extract_confirm",
                    ConfirmationDismissalTimeout,
                    preset,
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
            if (dismissed)
            {
                if (!dismissal.TryComplete() ||
                    !transaction.TryComplete())
                {
                    throw new InvalidOperationException(
                        "Could not complete extraction confirmation handling.");
                }
                log(
                    $"Extraction confirmation closed after {dismissal.Attempts} focused click attempt(s).",
                    MacroEventLevel.Success,
                    "extract_confirm",
                    null);
                await Task.Delay(
                    700,
                    cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!dismissal.TryMarkStillVisible())
            {
                throw new InvalidOperationException(
                    "Could not continue extraction confirmation handling.");
            }
            log(
                $"Extraction confirmation remained visible after click attempt {dismissal.Attempts}/{ConfirmationDismissalState.MaximumAttempts}.",
                MacroEventLevel.Warning,
                "extract_confirm",
                scores["extract_confirm"]);
        }

        throw new RobloxUiUnavailableException(
            $"The Extraction confirmation remained visible after {ConfirmationDismissalState.MaximumAttempts} focused click attempts. " +
            "Roblox did not acknowledge the button; retry after the client is responsive.");
    }
}
