namespace ExpeditionsMacro.Core.Models;

public enum RequiredGameSetting
{
    AutoSkipWaves,
    AutoVoteStart,
    ShowMatchEndRewards,
    DisplayPinnedQuests,
    SelectUnitOnPlacement,
    DisplayPathVisualizers,
    AutoRetry,
    AutoNext,
    ShowCameraShake,
    ShowDepthOfField,
    LowDetailMode,
    NightTimeEnabled,
    ShowOtherUnitVfx,
    ShowOwnUnitVfx,
    ShowAbilityEffects,
    ShowUnitAuraVfx,
    ShowTraitAuraVfx,
    ShowDamageIndicators,
    StrictPhantomPlacement,
    PrioritizePhantomPlacement,
    AutoUpgradePlacedUnits,
    AutoAbilitiesOnPlacement,
    DisplayUpdateLogOnLogin,
    AutoSprint,
}

public readonly record struct RequiredGameSettingState(
    RequiredGameSetting Setting,
    bool Enabled);

public static class RequiredGameSettings
{
    public static IReadOnlyList<RequiredGameSettingState> Profile { get; } =
    [
        new(RequiredGameSetting.AutoSkipWaves, true),
        new(RequiredGameSetting.AutoVoteStart, false),
        new(RequiredGameSetting.ShowMatchEndRewards, false),
        new(RequiredGameSetting.DisplayPinnedQuests, false),
        new(RequiredGameSetting.SelectUnitOnPlacement, false),
        new(RequiredGameSetting.DisplayPathVisualizers, false),
        new(RequiredGameSetting.AutoRetry, false),
        new(RequiredGameSetting.AutoNext, false),
        new(RequiredGameSetting.ShowCameraShake, false),
        new(RequiredGameSetting.ShowDepthOfField, false),
        new(RequiredGameSetting.LowDetailMode, true),
        new(RequiredGameSetting.NightTimeEnabled, false),
        new(RequiredGameSetting.ShowOtherUnitVfx, false),
        new(RequiredGameSetting.ShowOwnUnitVfx, false),
        new(RequiredGameSetting.ShowAbilityEffects, false),
        new(RequiredGameSetting.ShowUnitAuraVfx, false),
        new(RequiredGameSetting.ShowTraitAuraVfx, false),
        new(RequiredGameSetting.ShowDamageIndicators, false),
        new(RequiredGameSetting.StrictPhantomPlacement, true),
        new(RequiredGameSetting.PrioritizePhantomPlacement, true),
        new(RequiredGameSetting.AutoUpgradePlacedUnits, true),
        new(RequiredGameSetting.AutoAbilitiesOnPlacement, true),
        new(RequiredGameSetting.DisplayUpdateLogOnLogin, false),
        new(RequiredGameSetting.AutoSprint, true),
    ];
}
