using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Vision.Settings;

internal readonly record struct GameSettingLayoutEntry(
    GameSettingsPage Page,
    int X,
    int Y,
    bool UnitsBottom = false);

internal static class GameSettingsLayout
{
    public const int PanelCenterX = 404;
    public const int PanelCenterY = 304;
    public const int BaseCloseOffsetX = 261;
    public const int BaseCloseOffsetY = -146;

    public static readonly ScreenRegion CloseSearch =
        new(550, 105, 201, 101);

    public static readonly ScreenRegion[] TabRegions =
    [
        new(140, 187, 97, 21),
        new(140, 210, 97, 21),
        new(140, 234, 97, 21),
        new(140, 257, 97, 21),
        new(140, 281, 97, 21),
        new(140, 304, 97, 21),
        new(140, 327, 97, 21),
        new(140, 351, 97, 21),
        new(140, 375, 97, 21),
    ];

    public static readonly GameSettingsPage[] TabPages =
    [
        GameSettingsPage.All,
        GameSettingsPage.Audio,
        GameSettingsPage.Gameplay,
        GameSettingsPage.Graphics,
        GameSettingsPage.Units,
        GameSettingsPage.Enemies,
        GameSettingsPage.Miscellaneous,
        GameSettingsPage.Keybinds,
        GameSettingsPage.Testing,
    ];

    public static IReadOnlyDictionary<
        RequiredGameSetting,
        GameSettingLayoutEntry> Settings
    { get; } =
        new Dictionary<RequiredGameSetting, GameSettingLayoutEntry>
        {
            [RequiredGameSetting.AutoSkipWaves] =
                new(GameSettingsPage.Gameplay, 638, 222),
            [RequiredGameSetting.AutoVoteStart] =
                new(GameSettingsPage.Gameplay, 436, 257),
            [RequiredGameSetting.ShowMatchEndRewards] =
                new(GameSettingsPage.Gameplay, 638, 257),
            [RequiredGameSetting.DisplayPinnedQuests] =
                new(GameSettingsPage.Gameplay, 436, 293),
            [RequiredGameSetting.SelectUnitOnPlacement] =
                new(GameSettingsPage.Gameplay, 638, 293),
            [RequiredGameSetting.DisplayPathVisualizers] =
                new(GameSettingsPage.Gameplay, 638, 328),
            [RequiredGameSetting.AutoRetry] =
                new(GameSettingsPage.Gameplay, 436, 364),
            [RequiredGameSetting.AutoNext] =
                new(GameSettingsPage.Gameplay, 638, 364),
            [RequiredGameSetting.ShowCameraShake] =
                new(GameSettingsPage.Graphics, 436, 222),
            [RequiredGameSetting.ShowDepthOfField] =
                new(GameSettingsPage.Graphics, 638, 222),
            [RequiredGameSetting.LowDetailMode] =
                new(GameSettingsPage.Graphics, 436, 257),
            [RequiredGameSetting.NightTimeEnabled] =
                new(GameSettingsPage.Graphics, 638, 257),
            [RequiredGameSetting.EventThemeEnabled] =
                new(GameSettingsPage.Graphics, 436, 293),
            [RequiredGameSetting.ShowOtherUnitVfx] =
                new(GameSettingsPage.Units, 638, 222),
            [RequiredGameSetting.ShowOwnUnitVfx] =
                new(GameSettingsPage.Units, 436, 257),
            [RequiredGameSetting.ShowAbilityEffects] =
                new(GameSettingsPage.Units, 638, 257),
            [RequiredGameSetting.ShowUnitAuraVfx] =
                new(GameSettingsPage.Units, 436, 293),
            [RequiredGameSetting.ShowTraitAuraVfx] =
                new(GameSettingsPage.Units, 638, 293),
            [RequiredGameSetting.ShowDamageIndicators] =
                new(GameSettingsPage.Units, 638, 328),
            [RequiredGameSetting.StrictPhantomPlacement] =
                new(GameSettingsPage.Units, 436, 315, true),
            [RequiredGameSetting.PrioritizePhantomPlacement] =
                new(GameSettingsPage.Units, 638, 315, true),
            [RequiredGameSetting.AutoUpgradePlacedUnits] =
                new(GameSettingsPage.Units, 436, 351, true),
            [RequiredGameSetting.AutoAbilitiesOnPlacement] =
                new(GameSettingsPage.Units, 638, 351, true),
            [RequiredGameSetting.DisplayUpdateLogOnLogin] =
                new(GameSettingsPage.Miscellaneous, 436, 364),
            [RequiredGameSetting.AutoSprint] =
                new(GameSettingsPage.Miscellaneous, 638, 364),
        };
}
