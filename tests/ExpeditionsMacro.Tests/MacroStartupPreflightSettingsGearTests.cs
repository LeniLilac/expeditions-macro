using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task VoiceLayout_UsesSameDetectedGearToOpenAndClose()
    {
        TestFrames frames = new();
        ImageFrame voiceSelected =
            MoveSelectedGearToVoiceOffset(
                frames.Gameplay);
        PreflightAutomation automation =
            new(frames, frames.VoiceClosed)
            {
                SettingsOpenFrame = voiceSelected,
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

        Assert.False(result.UiScaleChanged);
        Assert.Equal(
            2,
            automation.Clicks.Count(
                click => click ==
                    (
                        RobloxSettingsButtonDetector
                            .VoiceActionX,
                        RobloxSettingsButtonDetector
                            .ActionY)));
        Assert.DoesNotContain(
            RobloxKeyboardKey.Backslash,
            automation.Keys);
        Assert.DoesNotContain(
            RobloxKeyboardKey.RightArrow,
            automation.Keys);
        GameSettingsPanelMatch panel =
            GameSettingsScreenDetector.DetectPanel(
                voiceSelected);
        Assert.DoesNotContain(
            (panel.CloseX, panel.CloseY),
            automation.Clicks);
        Assert.Same(
            frames.VoiceClosed,
            automation.CurrentFrame);
    }

    [Fact]
    public async Task HighContrastVoiceLayout_StillOpensAndClosesSettings()
    {
        TestFrames frames = new();
        ImageFrame closed =
            Brighten(frames.VoiceClosed, amount: 65);
        ImageFrame selected =
            MoveSelectedGearToVoiceOffset(
                frames.Gameplay);
        PreflightAutomation automation =
            new(frames, closed)
            {
                SettingsOpenFrame = selected,
            };
        MacroStartupPreflightService service =
            CreateService(
                automation,
                new TestClock());

        await service.RunUiScaleAsync(
            new LobbyDetector(frames.Lobby),
            progress: null,
            log: null,
            CancellationToken.None);

        Assert.Equal(
            2,
            automation.Clicks.Count(
                click => click ==
                    (
                        RobloxSettingsButtonDetector
                            .VoiceActionX,
                        RobloxSettingsButtonDetector
                            .ActionY)));
        Assert.Same(closed, automation.CurrentFrame);
    }

    [Fact]
    public async Task HighContrastNoVoiceLayout_StillOpensAndClosesSettings()
    {
        TestFrames frames = new();
        ImageFrame closed =
            Brighten(frames.Lobby, amount: 65);
        PreflightAutomation automation =
            new(frames, closed)
            {
                SettingsOpenFrame = frames.Gameplay,
            };
        MacroStartupPreflightService service =
            CreateService(
                automation,
                new TestClock());

        await service.RunUiScaleAsync(
            new LobbyDetector(frames.Lobby),
            progress: null,
            log: null,
            CancellationToken.None);

        Assert.Equal(
            2,
            automation.Clicks.Count(
                click => click ==
                    (
                        RobloxSettingsButtonDetector
                            .NoVoiceActionX,
                        RobloxSettingsButtonDetector
                            .ActionY)));
        Assert.Same(closed, automation.CurrentFrame);
    }

    [Fact]
    public async Task MissingGear_IsAHardCompatibilityFailure()
    {
        TestFrames frames = new();
        ImageFrame missingGear =
            PaintTopBarRegion(
                frames.Lobby,
                left: 212,
                right: 296);
        PreflightAutomation automation =
            new(frames, missingGear);
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        RobloxSettingsButtonUnavailableException error =
            await Assert.ThrowsAsync<
                RobloxSettingsButtonUnavailableException>(
                () => service.RunUiScaleAsync(
                    new LobbyDetector(frames.Lobby),
                    progress: null,
                    log: null,
                    CancellationToken.None));

        Assert.Contains(
            "Settings gear",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "voice chat",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(automation.Clicks);
        Assert.False(
            RobloxRuntimeRecoveryPolicy
                .IsRestartCandidate(error));
    }

    [Fact]
    public async Task RecognizedGearClickWithoutPanelReportsOpenPhase()
    {
        TestFrames frames = new();
        PreflightAutomation automation =
            new(frames, frames.Lobby)
            {
                IgnoreSettingsGearClicks = true,
            };
        TestClock clock = new();
        MacroStartupPreflightService service =
            CreateService(automation, clock);

        RobloxUiUnavailableException error =
            await Assert.ThrowsAsync<
                RobloxUiUnavailableException>(
                () => service.RunUiScaleAsync(
                    new LobbyDetector(frames.Lobby),
                    progress: null,
                    log: null,
                    CancellationToken.None));

        Assert.Contains(
            "gear was clicked",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "opening",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Single(automation.Clicks);
        Assert.DoesNotContain(
            RobloxKeyboardKey.Backslash,
            automation.Keys);
    }

    private static ImageFrame MoveSelectedGearToVoiceOffset(
        ImageFrame source)
    {
        byte[] pixels = source.Pixels.ToArray();
        const int halfSize = 18;
        for (int y =
                 RobloxSettingsButtonDetector.ActionY -
                 halfSize;
             y <=
                 RobloxSettingsButtonDetector.ActionY +
                 halfSize;
             y++)
        {
            for (int offsetX = -halfSize;
                 offsetX <= halfSize;
                 offsetX++)
            {
                int sourceX =
                    RobloxSettingsButtonDetector
                        .NoVoiceActionX +
                    offsetX;
                int targetX =
                    RobloxSettingsButtonDetector
                        .VoiceActionX +
                    offsetX;
                int sourcePixel =
                    (y * source.Width + sourceX) * 3;
                int targetPixel =
                    (y * source.Width + targetX) * 3;
                pixels[targetPixel] =
                    source.Pixels[sourcePixel];
                pixels[targetPixel + 1] =
                    source.Pixels[sourcePixel + 1];
                pixels[targetPixel + 2] =
                    source.Pixels[sourcePixel + 2];
                pixels[sourcePixel] = 20;
                pixels[sourcePixel + 1] = 20;
                pixels[sourcePixel + 2] = 20;
            }
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private static ImageFrame PaintTopBarRegion(
        ImageFrame source,
        int left,
        int right)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int y = 15; y <= 53; y++)
        {
            for (int x = left; x <= right; x++)
            {
                int pixel =
                    (y * source.Width + x) * 3;
                pixels[pixel] = 20;
                pixels[pixel + 1] = 20;
                pixels[pixel + 2] = 20;
            }
        }

        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }

    private static ImageFrame Brighten(
        ImageFrame source,
        byte amount)
    {
        byte[] pixels = source.Pixels
            .Select(value =>
                (byte)Math.Min(
                    byte.MaxValue,
                    value + amount))
            .ToArray();
        return new ImageFrame(
            source.Width,
            source.Height,
            source.Format,
            pixels,
            takeOwnership: true);
    }
}
