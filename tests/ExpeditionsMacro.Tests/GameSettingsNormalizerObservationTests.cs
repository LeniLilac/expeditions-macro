using ExpeditionsMacro.Automation.Settings;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed partial class MacroStartupPreflightServiceTests
{
    [Fact]
    public async Task ToggleWaitsForStableControlPastSoftDeadline()
    {
        TestFrames frames = new();
        ImageFrame wrong = ReplaceToggle(
            frames.Gameplay,
            638,
            222,
            enabled: false);
        ImageFrame unknown = Obscure(
            wrong,
            630,
            214,
            17,
            17);
        TestClock clock = new();
        int gameplayCaptures = 0;
        PreflightAutomation automation =
            new(frames, wrong)
            {
                GameplayFrameAfterToggle = frames.Gameplay,
                CaptureOverride = current =>
                {
                    if (!ReferenceEquals(current, wrong))
                    {
                        return current;
                    }

                    gameplayCaptures++;
                    clock.Advance(TimeSpan.FromSeconds(5));
                    return gameplayCaptures <= 4
                        ? unknown
                        : current;
                },
            };
        GameSettingsNormalizer normalizer = new(
            automation,
            () => clock.UtcNow,
            clock.DelayAsync);

        int changes = await normalizer.NormalizeAsync(
            automation.FindWindow()!.Value,
            status: null,
            CancellationToken.None);

        Assert.Equal(1, changes);
        Assert.True(gameplayCaptures >= 6);
        Assert.Equal(
            1,
            automation.Clicks.Count(
                point => point == (638, 222)));
    }

    [Fact]
    public async Task ScrollbarWaitsForStableThumbPastSoftDeadline()
    {
        TestFrames frames = new();
        ImageFrame missingThumb = Obscure(
            frames.UnitsTop,
            663,
            178,
            11,
            280);
        TestClock clock = new();
        int topCaptures = 0;
        PreflightAutomation automation =
            new(frames, frames.Gameplay)
            {
                CaptureOverride = current =>
                {
                    if (!ReferenceEquals(
                            current,
                            frames.UnitsTop))
                    {
                        return current;
                    }

                    topCaptures++;
                    clock.Advance(TimeSpan.FromSeconds(5));
                    return topCaptures <= 5
                        ? missingThumb
                        : current;
                },
            };
        GameSettingsNormalizer normalizer = new(
            automation,
            () => clock.UtcNow,
            clock.DelayAsync);

        await normalizer.NormalizeAsync(
            automation.FindWindow()!.Value,
            status: null,
            CancellationToken.None);

        Assert.True(topCaptures >= 7);
        Assert.Single(automation.Drags);
    }

    [Fact]
    public async Task IgnoredToggleRetainsTwoClickCap()
    {
        TestFrames frames = new();
        ImageFrame wrong = ReplaceToggle(
            frames.Gameplay,
            638,
            222,
            enabled: false);
        TestClock clock = new();
        PreflightAutomation automation =
            new(frames, wrong)
            {
                GameplayFrameAfterToggle = wrong,
                IgnoreGameplayToggleClicks = true,
            };
        GameSettingsNormalizer normalizer = new(
            automation,
            () => clock.UtcNow,
            clock.DelayAsync);

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => normalizer.NormalizeAsync(
                automation.FindWindow()!.Value,
                status: null,
                CancellationToken.None));

        Assert.Equal(
            2,
            automation.Clicks.Count(
                point => point == (638, 222)));
    }

    [Fact]
    public async Task IgnoredScrollbarRetainsTwoDragCap()
    {
        TestFrames frames = new();
        TestClock clock = new();
        PreflightAutomation automation =
            new(frames, frames.Gameplay)
            {
                IgnoreUnitsScrollbarDrags = true,
            };
        GameSettingsNormalizer normalizer = new(
            automation,
            () => clock.UtcNow,
            clock.DelayAsync);

        await Assert.ThrowsAsync<RobloxUiUnavailableException>(
            () => normalizer.NormalizeAsync(
                automation.FindWindow()!.Value,
                status: null,
                CancellationToken.None));

        Assert.Equal(2, automation.Drags.Count);
    }

    private static ImageFrame Obscure(
        ImageFrame source,
        int x,
        int y,
        int width,
        int height)
    {
        byte[] pixels = source.Pixels.ToArray();
        for (int row = y; row < y + height; row++)
        {
            for (int column = x;
                 column < x + width;
                 column++)
            {
                int pixel =
                    (row * source.Width + column) * 3;
                pixels[pixel] = 35;
                pixels[pixel + 1] = 35;
                pixels[pixel + 2] = 35;
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
