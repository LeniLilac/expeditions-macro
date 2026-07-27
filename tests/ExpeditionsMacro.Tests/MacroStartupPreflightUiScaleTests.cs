using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task
        NonCanonicalScale_DoesNotRequireAccessibilitySelectionRing()
    {
        TestFrames frames = new();
        ImageFrame unfocusedScale =
            RemoveInputAccessibilitySelectionRing(
                frames.Scale120);
        GameSettingsUiScaleInputMatch input =
            GameSettingsNavigationDetector
                .DetectUiScaleInput(unfocusedScale);
        Assert.True(input.Available);
        Assert.False(input.Focused);
        PreflightAutomation automation =
            new(frames, frames.NonLobby)
            {
                SettingsOpenFrame = unfocusedScale,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunUiScaleAsync(
                new LobbyDetector(frames.Lobby),
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Contains(
            (input.ActionX, input.ActionY),
            automation.Clicks);
        Assert.Contains(
            TimeSpan.FromMilliseconds(2500),
            clock.Delays);
        Assert.Equal(
            ["1.00"],
            automation.AppliedScaleValues);
    }

    [Fact]
    public async Task
        DisabledUiScaleCheck_SkipsScaleMeasurementAndSettingsInput()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby);
        TestClock clock = new();
        int lobbyObservations = 0;
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        GameSettingsNormalizationResult result =
            await service.RunAsync(
                new LobbyDetector(
                    frames.Lobby,
                    observationCompleted: () =>
                        lobbyObservations++),
                normalizeUiScale: false,
                normalizeGameSettings: false,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.Equal(0, result.ChangedSettings);
        Assert.False(result.UiScaleChanged);
        Assert.Empty(automation.Clicks);
        Assert.Empty(automation.Keys);
        Assert.Empty(automation.Drags);
        Assert.Empty(automation.AppliedScaleValues);
        Assert.Equal(0, automation.PitchPreparationCount);
        Assert.True(lobbyObservations >= 3);
    }

    [Fact]
    public async Task
        EnabledAutoFix_CalibratesNonCanonicalScaleBeforeLobby()
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
                normalizeGameSettings: true,
                progress: null,
                log: null,
                CancellationToken.None);

        Assert.True(result.UiScaleChanged);
        Assert.Equal(0, result.ChangedSettings);
        Assert.Equal(["1.00"], automation.AppliedScaleValues);
    }

    private static ImageFrame
        RemoveInputAccessibilitySelectionRing(
        ImageFrame source)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int y = 180; y < 232; y++)
        {
            for (int x = 330; x < 384; x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                byte red = pixels[pixel];
                byte green = pixels[pixel + 1];
                byte blue = pixels[pixel + 2];
                if (blue > 120 &&
                    green > red * 1.35 &&
                    blue > red * 1.25)
                {
                    pixels[pixel] = 35;
                    pixels[pixel + 1] = 35;
                    pixels[pixel + 2] = 35;
                }
            }
        }
        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }
}
