using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task
        SlowDetector_CompletesRequiredLobbyObservationsPastSoftDeadline()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);
        LobbyDetector detector = new(
            frames.Lobby,
            observationCompleted: () =>
                clock.Advance(
                    TimeSpan.FromSeconds(5)));

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                detector,
                normalizeUiScale: false,
                normalizeGameSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.True(
            clock.UtcNow >=
            DateTimeOffset.Parse(
                "2026-07-25T12:00:15Z"));
    }
}
