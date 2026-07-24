using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed record ChallengeDebugNavigationResult(
    ChallengeType Type,
    ChallengeMapId Map);

public sealed partial class ChallengeMacroRunner
{
    public async Task<ChallengeDebugNavigationResult>
        DebugNavigateAsync(
            ChallengePreset preset,
            ChallengeType type,
            IDetectorPack detector,
            char playMenuKey,
            IProgress<MacroProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        preset.Validate();
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        if (!detector.SupportsChallengeMaps)
        {
            throw new InvalidDataException(
                DetectorPackCapabilities
                    .ChallengeMapsUnavailableMessage(
                        detector.Manifest));
        }

        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        ChallengePreset navigationPreset = preset with
        {
            AutoRecover = true,
        };
        void Report(
            string phase,
            int percent,
            string message,
            string? state = null,
            double? confidence = null) =>
            progress?.Report(new MacroProgress(
                phase,
                percent,
                message,
                state,
                confidence));
        void Log(
            string message,
            MacroEventLevel level = MacroEventLevel.Information,
            string? state = null,
            double? confidence = null) =>
            progress?.Report(new MacroProgress(
                "Debug navigation",
                0,
                message,
                state,
                confidence));

        Focus(window);
        await EnsureClientSizeAsync(
            window,
            detector.Manifest.ClientWidth,
            detector.Manifest.ClientHeight,
            cancellationToken).ConfigureAwait(false);
        await EnsureChallengeListAsync(
            window,
            navigationPreset,
            detector,
            playMenuKey,
            Log,
            Report,
            recovered: () => { },
            cancellationToken).ConfigureAwait(false);
        await WaitForChallengeSelectorAsync(
            window,
            navigationPreset,
            detector,
            TimeSpan.FromSeconds(12),
            Report,
            cancellationToken).ConfigureAwait(false);
        ChallengeMapId map = await RecognizeMapAsync(
            window,
            navigationPreset,
            detector,
            type,
            Log,
            cancellationToken).ConfigureAwait(false);
        ChallengeScreenMatch detail =
            await OpenChallengeTypeAsync(
                window,
                navigationPreset,
                detector,
                type,
                Report,
                cancellationToken).ConfigureAwait(false);
        if (detail.State == ChallengeScreenState.ChallengeCooldown)
        {
            throw new InvalidOperationException(
                $"{Label(type)} Challenge is currently on cooldown.");
        }

        ImageFrame available = CaptureClient(window, detector);
        (int X, int Y)? stage =
            ChallengeScreenDetector.ActionFor(
                ChallengeScreenState.ChallengeAvailable,
                available);
        if (stage is null)
        {
            throw new InvalidOperationException(
                "The Challenge Select Stage button could not be located.");
        }
        await ClickAsync(
            window,
            stage.Value.X,
            stage.Value.Y,
            cancellationToken).ConfigureAwait(false);
        ImageFrame preview = await WaitForScreenAsync(
            window,
            navigationPreset,
            detector,
            ChallengeScreenState.PreviewReady,
            TimeSpan.FromSeconds(15),
            Report,
            cancellationToken).ConfigureAwait(false);
        (int X, int Y)? start =
            ChallengeScreenDetector.ActionFor(
                ChallengeScreenState.PreviewReady,
                preview);
        if (start is null)
        {
            throw new InvalidOperationException(
                "The Challenge preview Start button could not be located.");
        }
        await ClickAsync(
            window,
            start.Value.X,
            start.Value.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForPrestartAfterPreviewAsync(
            window,
            navigationPreset,
            detector,
            Report,
            cancellationToken).ConfigureAwait(false);
        Report(
            "Debug navigation",
            100,
            $"{Label(type)} Challenge on {Label(map)} reached prestart.",
            "prestart",
            1);
        return new ChallengeDebugNavigationResult(type, map);
    }
}
