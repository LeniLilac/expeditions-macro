using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task UiScaleOnly_CalibratesWithoutCheckingGameSettings()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                SettingsOpenFrame = frames.Scale080,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.Lobby),
                normalizeUiScale: true,
                normalizeGameSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Equal(0, result.ChangedSettings);
        Assert.Equal(["1.00"], automation.AppliedScaleValues);
        Assert.Empty(automation.Drags);
        Assert.Equal(1, automation.PitchPreparationCount);
    }

    [Fact]
    public async Task
        GameSettingsOnly_CorrectsProfileWithoutChangingUiScale()
    {
        TestFrames frames = new();
        ImageFrame wrongGameplay =
            ReplaceToggle(
                frames.Gameplay,
                638,
                222,
                enabled: false);
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                SettingsOpenFrame = wrongGameplay,
                GameplayFrameAfterToggle = frames.Gameplay,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(frames.Lobby),
                normalizeUiScale: false,
                normalizeGameSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(1, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.AppliedScaleValues);
        Assert.Single(
            automation.Clicks,
            point => point == (638, 222));
        Assert.Equal(1, automation.PitchPreparationCount);
    }

    [Fact]
    public async Task
        GameSettingsOnly_NonCanonicalScaleStopsWithoutTypingScale()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                SettingsOpenFrame = frames.Scale120,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RunAsync(
                    new LobbyDetector(frames.Lobby),
                    normalizeUiScale: false,
                    normalizeGameSettings: true,
                    progress: null,
                    log: null,
                    CancellationToken.None));

        Assert.Contains(
            "Enable Check and fix UI Scale",
            error.Message,
            StringComparison.Ordinal);
        Assert.Empty(automation.AppliedScaleValues);
        Assert.Equal(1, automation.PitchPreparationCount);
        Assert.Same(frames.Lobby, automation.CurrentFrame);
    }
}
