using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

public sealed record GameSettingsNormalizationResult(
    int ChangedSettings,
    bool UiScaleChanged);

internal sealed class GameSettingsNormalizer
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan PageTimeout =
        TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ToggleTimeout =
        TimeSpan.FromSeconds(2);
    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly StableGameSettingsControlWaiter
        _controlWaiter;

    public GameSettingsNormalizer(
        IRobloxAutomation automation)
        : this(
            automation,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token))
    {
    }

    internal GameSettingsNormalizer(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
        _controlWaiter = new(
            utcNow,
            delay,
            PollInterval);
    }

    public async Task<int> NormalizeAsync(
        RobloxWindow window,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        int changes = 0;
        changes += await NormalizePageAsync(
            window,
            GameSettingsPage.Gameplay,
            RequiredGameSettings.Profile.Where(
                requirement =>
                    GameSettingsScreenDetector.PageFor(
                        requirement.Setting) ==
                    GameSettingsPage.Gameplay),
            status,
            cancellationToken).ConfigureAwait(false);
        changes += await NormalizePageAsync(
            window,
            GameSettingsPage.Graphics,
            RequiredGameSettings.Profile.Where(
                requirement =>
                    GameSettingsScreenDetector.PageFor(
                        requirement.Setting) ==
                    GameSettingsPage.Graphics),
            status,
            cancellationToken).ConfigureAwait(false);
        await SelectPageAsync(
            window,
            GameSettingsPage.Units,
            cancellationToken).ConfigureAwait(false);
        await ClampUnitsScrollbarAsync(
            window,
            bottom: false,
            cancellationToken).ConfigureAwait(false);
        changes += await NormalizeSettingsAsync(
            window,
            RequiredGameSettings.Profile.Where(
                requirement =>
                    GameSettingsScreenDetector.PageFor(
                        requirement.Setting) ==
                    GameSettingsPage.Units &&
                    !GameSettingsScreenDetector
                        .RequiresUnitsBottom(
                            requirement.Setting)),
            status,
            cancellationToken).ConfigureAwait(false);
        await ClampUnitsScrollbarAsync(
            window,
            bottom: true,
            cancellationToken).ConfigureAwait(false);
        changes += await NormalizeSettingsAsync(
            window,
            RequiredGameSettings.Profile.Where(
                requirement =>
                    GameSettingsScreenDetector.PageFor(
                        requirement.Setting) ==
                    GameSettingsPage.Units &&
                    GameSettingsScreenDetector
                        .RequiresUnitsBottom(
                            requirement.Setting)),
            status,
            cancellationToken).ConfigureAwait(false);
        changes += await NormalizePageAsync(
            window,
            GameSettingsPage.Miscellaneous,
            RequiredGameSettings.Profile.Where(
                requirement =>
                    GameSettingsScreenDetector.PageFor(
                        requirement.Setting) ==
                    GameSettingsPage.Miscellaneous),
            status,
            cancellationToken).ConfigureAwait(false);
        return changes;
    }

    private async Task<int> NormalizePageAsync(
        RobloxWindow window,
        GameSettingsPage page,
        IEnumerable<RequiredGameSettingState> requirements,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        await SelectPageAsync(
            window,
            page,
            cancellationToken).ConfigureAwait(false);
        return await NormalizeSettingsAsync(
            window,
            requirements,
            status,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> NormalizeSettingsAsync(
        RobloxWindow window,
        IEnumerable<RequiredGameSettingState> requirements,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        int changes = 0;
        foreach (RequiredGameSettingState requirement in
                 requirements)
        {
            if (await EnsureSettingAsync(
                    window,
                    requirement,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                changes++;
                status?.Invoke(
                    $"Corrected {Label(requirement.Setting)}.");
            }
        }
        return changes;
    }

    private async Task SelectPageAsync(
        RobloxWindow window,
        GameSettingsPage target,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ImageFrame current = Capture(window);
            if (GameSettingsScreenDetector
                    .DetectPage(current).Page == target)
            {
                return;
            }

            (int x, int y) =
                GameSettingsScreenDetector.PageAction(target);
            await _automation.ClickClientAsync(
                window,
                x,
                y,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForStableAsync(
                    window,
                    image =>
                        GameSettingsScreenDetector
                            .DetectPage(image).Page ==
                        target,
                    PageTimeout,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            $"Anime Expeditions did not open the {PageLabel(target)} settings page. No further settings were clicked.");
    }

    private async Task<bool> EnsureSettingAsync(
        RobloxWindow window,
        RequiredGameSettingState requirement,
        CancellationToken cancellationToken)
    {
        GameSettingToggleState desired =
            requirement.Enabled
                ? GameSettingToggleState.Enabled
                : GameSettingToggleState.Disabled;
        bool clicked = false;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            GameSettingToggleMatch? current =
                await _controlWaiter.WaitForToggleAsync(
                        () => Capture(window),
                        requirement.Setting,
                        ToggleTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (current is null)
            {
                throw new InvalidOperationException(
                    $"Anime Expeditions displayed an unrecognized {Label(requirement.Setting)} control. It was not clicked.");
            }
            if (current.Value.State == desired) return clicked;

            await _automation.ClickClientAsync(
                window,
                current.Value.ActionX,
                current.Value.ActionY,
                cancellationToken).ConfigureAwait(false);
            clicked = true;
            if (await WaitForStableAsync(
                    window,
                    image =>
                        GameSettingsScreenDetector
                            .DetectToggle(
                                image,
                                requirement.Setting)
                            .State == desired,
                    ToggleTimeout,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }
        }

        throw new RobloxUiUnavailableException(
            $"Anime Expeditions ignored two attempts to change {Label(requirement.Setting)}. The macro stopped before starting a match.");
    }

    private async Task ClampUnitsScrollbarAsync(
        RobloxWindow window,
        bool bottom,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            GameSettingsScrollbarThumb? thumb =
                await _controlWaiter
                    .WaitForUnitsScrollbarAsync(
                        () => Capture(window),
                        PageTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (thumb is null)
            {
                throw new RobloxUiUnavailableException(
                    "Anime Expeditions did not expose the Units settings scrollbar. No Units settings were clicked.");
            }
            if (bottom
                    ? thumb.Value.IsAtBottom
                    : thumb.Value.IsAtTop)
            {
                return;
            }

            await _automation.DragClientAsync(
                window,
                thumb.Value.X,
                thumb.Value.CenterY,
                thumb.Value.X,
                bottom
                    ? GameSettingsScreenDetector
                        .UnitsScrollbarBottomY
                    : GameSettingsScreenDetector
                        .UnitsScrollbarTopY,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForStableAsync(
                    window,
                    image =>
                    {
                        GameSettingsScrollbarThumb? observed =
                            GameSettingsScreenDetector
                                .FindUnitsScrollbarThumb(image);
                        return observed is not null &&
                            (bottom
                                ? observed.Value.IsAtBottom
                                : observed.Value.IsAtTop);
                    },
                    PageTimeout,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            $"Anime Expeditions did not move the Units settings scrollbar to the {(bottom ? "bottom" : "top")} position.");
    }

    private ImageFrame Capture(RobloxWindow window)
    {
        ValidateWindow(window);
        return _automation.CaptureClient(window);
    }

    private void ValidateWindow(RobloxWindow window)
    {
        RobloxWindow? current = _automation.FindWindow();
        if (current is null ||
            current.Value.Handle != window.Handle)
        {
            throw new RobloxSessionUnavailableException(
                "The Roblox window changed while checking game settings.");
        }
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while checking game settings.");
        }

        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                GameSettingsScreenDetector.ClientWidth ||
            bounds.Height !=
                GameSettingsScreenDetector.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox changed size while checking game settings.");
        }
    }

    private async Task<bool> WaitForStableAsync(
        RobloxWindow window,
        Func<ImageFrame, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        int stableFrames = 0;
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending:
                       stableFrames == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(Capture(window)))
            {
                stableFrames++;
                if (stableFrames >= 2) return true;
            }
            else
            {
                stableFrames = 0;
            }
            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private static string PageLabel(
        GameSettingsPage page) =>
        page == GameSettingsPage.Miscellaneous
            ? "Misc"
            : page.ToString();

    internal static string Label(
        RequiredGameSetting setting) =>
        setting switch
        {
            RequiredGameSetting.AutoSkipWaves =>
                "Auto Skip Waves",
            RequiredGameSetting.AutoVoteStart =>
                "Auto Vote Start",
            RequiredGameSetting.ShowMatchEndRewards =>
                "Show Match End Rewards",
            RequiredGameSetting.DisplayPinnedQuests =>
                "Display Pinned Quests",
            RequiredGameSetting.SelectUnitOnPlacement =>
                "Select Unit on Placement",
            RequiredGameSetting.DisplayPathVisualizers =>
                "Display Path Visualizers",
            RequiredGameSetting.AutoRetry =>
                "Auto Retry",
            RequiredGameSetting.AutoNext =>
                "Auto Next",
            RequiredGameSetting.ShowCameraShake =>
                "Show Camera Shake",
            RequiredGameSetting.ShowDepthOfField =>
                "Show Depth of Field",
            RequiredGameSetting.LowDetailMode =>
                "Low Detail Mode",
            RequiredGameSetting.NightTimeEnabled =>
                "Night Time Enabled",
            RequiredGameSetting.EventThemeEnabled =>
                "Event Theme Enabled",
            RequiredGameSetting.ShowOtherUnitVfx =>
                "Show Other Unit VFX",
            RequiredGameSetting.ShowOwnUnitVfx =>
                "Show Own Unit VFX",
            RequiredGameSetting.ShowAbilityEffects =>
                "Show Ability Effects",
            RequiredGameSetting.ShowUnitAuraVfx =>
                "Show Unit Aura VFX",
            RequiredGameSetting.ShowTraitAuraVfx =>
                "Show Trait Aura VFX",
            RequiredGameSetting.ShowDamageIndicators =>
                "Show Damage Indicators",
            RequiredGameSetting.StrictPhantomPlacement =>
                "Strict Phantom Placement",
            RequiredGameSetting.PrioritizePhantomPlacement =>
                "Prioritize Phantom Placement",
            RequiredGameSetting.AutoUpgradePlacedUnits =>
                "Auto-Upgrade Placed Units",
            RequiredGameSetting.AutoAbilitiesOnPlacement =>
                "Auto Abilities on Placement",
            RequiredGameSetting.DisplayUpdateLogOnLogin =>
                "Display Update Log on Login",
            RequiredGameSetting.AutoSprint =>
                "Auto Sprint",
            _ => setting.ToString(),
        };
}
