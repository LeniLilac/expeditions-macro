using System.Diagnostics;
using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    public async Task DebugNavigateAsync(
        ExpeditionPreset preset,
        IDetectorPack detector,
        char playMenuKey,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        preset.Validate();
        playMenuKey = ValidatePlayMenuKey(playMenuKey);
        RobloxWindow window = _automation.FindWindow() ??
            throw new RobloxSessionUnavailableException(
                "No visible Roblox window was found.");
        ExpeditionPreset navigationPreset = preset with
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
            Log,
            cancellationToken).ConfigureAwait(false);
        string? initial = await ProbeStableRecoveryStateAsync(
            window,
            detector,
            navigationPreset,
            allowNavigationEntry: true,
            cancellationToken).ConfigureAwait(false);
        if (initial is null)
        {
            bool alreadyReady = await WaitForStateWithTimeoutAsync(
                window,
                detector,
                "start",
                TimeSpan.FromSeconds(3),
                navigationPreset,
                Report,
                cancellationToken).ConfigureAwait(false);
            if (!alreadyReady)
            {
                throw new InvalidOperationException(
                    "The current screen is not a supported lobby, post-match, Play, or Expedition navigation state.");
            }
        }
        else
        {
            DiscordRunReporter reporter = new(
                _discord,
                webhookUrl: string.Empty,
                "Expeditions Debug",
                "expeditions-debug",
                Log);
            Stopwatch runtime = Stopwatch.StartNew();
            await RecoverToPrestartAsync(
                window,
                initial,
                navigationPreset,
                detector,
                reporter,
                notify: false,
                runtime,
                victories: 0,
                defeats: 0,
                playMenuKey,
                Report,
                Log,
                cancellationToken).ConfigureAwait(false);
            await reporter.FlushAsync().ConfigureAwait(false);
        }
        Report(
            "Debug navigation",
            100,
            $"Expedition Map {preset.MapNumber}, Difficulty {preset.Difficulty} reached prestart.",
            "start",
            1);
    }
}
