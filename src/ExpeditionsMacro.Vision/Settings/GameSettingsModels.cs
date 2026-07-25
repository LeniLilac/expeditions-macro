using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Vision.Settings;

public enum GameSettingsPage
{
    None,
    All,
    Audio,
    Gameplay,
    Graphics,
    Units,
    Enemies,
    Miscellaneous,
    Keybinds,
    Testing,
}

public enum GameSettingToggleState
{
    Unknown,
    Disabled,
    Enabled,
}

public readonly record struct GameSettingsPanelMatch(
    bool Visible,
    bool Settled,
    double Confidence,
    double UiScale,
    int CloseX,
    int CloseY);

public readonly record struct GameSettingsPageMatch(
    GameSettingsPage Page,
    double Confidence);

public readonly record struct GameSettingToggleMatch(
    RequiredGameSetting Setting,
    GameSettingToggleState State,
    double Confidence,
    int ActionX,
    int ActionY);

public readonly record struct GameSettingsScrollbarThumb(
    int X,
    int StartY,
    int EndY)
{
    public int CenterY => (StartY + EndY) / 2;

    public bool IsAtTop => StartY <= 190;

    public bool IsAtBottom => EndY >= 448;
}
