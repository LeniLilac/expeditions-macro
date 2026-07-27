using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class SettingsKeyBindingsPanel
{
    private async Task ApplyMacroAsync(int virtualKey)
    {
        AppServices services = Services;
        AppSettings candidate = services.Settings with
        {
            MacroHotkeyVirtualKey = virtualKey,
        };
        ValidateBindings(candidate);

        int previous = services.Hotkey.VirtualKey;
        string display = KeyboardKey.GetDisplayName(virtualKey);
        MacroButton.Content = display;
        MacroStatusText.Text =
            $"Registering {display} globally...";
        try
        {
            await Task.Run(
                () => services.Hotkey.Rebind(virtualKey));
            await services.UpdateSettingsAsync(
                _ => candidate);
            MacroStatusText.Text =
                $"{display} is now the macro start and stop key.";
        }
        catch
        {
            if (services.Hotkey.VirtualKey != previous)
            {
                try
                {
                    await Task.Run(
                        () => services.Hotkey.Rebind(
                            previous));
                }
                catch
                {
                }
            }
            throw;
        }
    }

    private Task ApplyPlayAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                PlayMenuKey = key.ToString(),
            });

    private Task ApplyUnitAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                UnitMenuKey = key.ToString(),
            });

    private Task ApplyAreasAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                AreasMenuKey = key.ToString(),
            });

    private Task ApplyCancelPlacementAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                CancelPlacementKey = key.ToString(),
            });

    private Task ApplyTargetingAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                ChangeUnitTargetingKey = key.ToString(),
            });

    private Task ApplyUpgradeAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                UpgradeUnitKey = key.ToString(),
            });

    private Task ApplyAutoUpgradeUnitAsync(char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                AutoUpgradeUnitKey = key.ToString(),
            });

    private Task ApplyToggleAutoUpgradePlacedUnitsAsync(
        char key) =>
        ApplySettingsAsync(
            settings => settings with
            {
                ToggleAutoUpgradePlacedUnitsKey =
                    key.ToString(),
            });

    private Task ApplyShiftLockAsync(int virtualKey) =>
        ApplySettingsAsync(
            settings => settings with
            {
                ShiftLockVirtualKey = virtualKey,
            });

    private Task ClearBindingAsync(
        BindingTarget target) =>
        Services.UpdateSettingsAsync(
            settings => target switch
            {
                BindingTarget.Play =>
                    settings with
                    {
                        PlayMenuKey = string.Empty,
                    },
                BindingTarget.Unit =>
                    settings with
                    {
                        UnitMenuKey = string.Empty,
                    },
                BindingTarget.Areas =>
                    settings with
                    {
                        AreasMenuKey = string.Empty,
                    },
                BindingTarget.CancelPlacement =>
                    settings with
                    {
                        CancelPlacementKey = string.Empty,
                    },
                BindingTarget.Targeting =>
                    settings with
                    {
                        ChangeUnitTargetingKey = string.Empty,
                    },
                BindingTarget.Upgrade =>
                    settings with
                    {
                        UpgradeUnitKey = string.Empty,
                    },
                BindingTarget.AutoUpgradeUnit =>
                    settings with
                    {
                        AutoUpgradeUnitKey = string.Empty,
                    },
                BindingTarget.ToggleAutoUpgradePlacedUnits =>
                    settings with
                    {
                        ToggleAutoUpgradePlacedUnitsKey =
                            string.Empty,
                    },
                BindingTarget.ShiftLock =>
                    settings with
                    {
                        ShiftLockVirtualKey = 0,
                    },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(target)),
            });

    private async Task ApplySettingsAsync(
        Func<AppSettings, AppSettings> update)
    {
        AppServices services = Services;
        AppSettings candidate = update(services.Settings);
        ValidateBindings(candidate);
        await services.UpdateSettingsAsync(_ => candidate);
    }

    private static void ValidateBindings(
        AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(
                settings.PlayMenuKey))
        {
            _ = AppSettings.ParsePlayMenuKey(
                settings.PlayMenuKey,
                settings.MacroHotkeyVirtualKey);
        }
        if (!string.IsNullOrWhiteSpace(
                settings.UnitMenuKey))
        {
            _ = AppSettings.ParseUnitMenuKey(
                settings.UnitMenuKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.AreasMenuKey);
        }
        if (!string.IsNullOrWhiteSpace(
                settings.AreasMenuKey))
        {
            _ = AppSettings.ParseAreasMenuKey(
                settings.AreasMenuKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey);
        }

        if (settings.ShiftLockVirtualKey != 0)
        {
            _ = AppSettings.ParseShiftLockKey(
                settings.ShiftLockVirtualKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey,
                settings.AreasMenuKey,
                settings.CancelPlacementKey);
        }
        if (!string.IsNullOrWhiteSpace(
                settings.CancelPlacementKey))
        {
            _ = AppSettings.ParseCancelPlacementKey(
                settings.CancelPlacementKey,
                settings.MacroHotkeyVirtualKey,
                settings.PlayMenuKey,
                settings.UnitMenuKey,
                settings.AreasMenuKey,
                settings.ShiftLockVirtualKey);
        }
        AppSettings.ValidateControlKeySet(
            settings,
            requireUnitActionKeys: false);
    }
}
