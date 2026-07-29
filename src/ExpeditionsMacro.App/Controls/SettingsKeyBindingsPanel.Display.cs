using System.IO;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class SettingsKeyBindingsPanel
{
    internal void ShowConfiguredQuickPlacementForSnapshot()
    {
        QuickPlacementButton.Content = "Left Shift";
        QuickPlacementStatusText.Text =
            "Left Shift must match Anime Expeditions' Quick Placement binding.";
        ClearQuickPlacementButton.Visibility =
            System.Windows.Visibility.Visible;
        ClearQuickPlacementButton.IsEnabled = true;
    }

    private void UpdateMacroDisplay()
    {
        AppServices services = Services;
        string hotkey = services.Hotkey.DisplayName;
        if (_captureTarget != BindingTarget.Macro)
        {
            MacroButton.Content = hotkey;
        }
        MacroDiagnostic = services.Hotkey.IsRegistered
            ? $"{hotkey} registered"
            : "Unavailable";
        if (_captureTarget != BindingTarget.Macro &&
            !_saving)
        {
            MacroStatusText.Text =
                $"{hotkey} is registered globally for every macro workflow.";
        }
    }

    private void UpdatePlayDisplay()
    {
        AppServices services = Services;
        try
        {
            char key = AppSettings.ParsePlayMenuKey(
                services.Settings.PlayMenuKey,
                services.Hotkey.VirtualKey);
            AppSettings.ValidateControlKeySet(
                services.Settings,
                requireUnitActionKeys: false);
            if (_captureTarget != BindingTarget.Play)
            {
                PlayButton.Content = key.ToString();
            }
            PlayStatusText.Text =
                $"{key} must match Anime Expeditions' Toggle Play Menu binding.";
            PlayDiagnostic = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(
                services.Settings.PlayMenuKey);
            if (_captureTarget != BindingTarget.Play)
            {
                PlayButton.Content = empty
                    ? "Not set"
                    : services.Settings.PlayMenuKey;
            }
            PlayStatusText.Text = empty
                ? "Required before a macro can start."
                : error.Message;
            PlayDiagnostic = empty
                ? "Not set"
                : "Conflict";
        }
    }

    private void UpdateUnitDisplay()
    {
        AppServices services = Services;
        try
        {
            char key = AppSettings.ParseUnitMenuKey(
                services.Settings.UnitMenuKey,
                services.Hotkey.VirtualKey,
                services.Settings.PlayMenuKey,
                services.Settings.AreasMenuKey);
            AppSettings.ValidateControlKeySet(
                services.Settings,
                requireUnitActionKeys: false);
            if (_captureTarget != BindingTarget.Unit)
            {
                UnitButton.Content = key.ToString();
            }
            UnitStatusText.Text =
                $"{key} must match Anime Expeditions' Toggle Unit Inventory binding.";
            UnitDiagnostic = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(
                services.Settings.UnitMenuKey);
            if (_captureTarget != BindingTarget.Unit)
            {
                UnitButton.Content = empty
                    ? "Not set"
                    : services.Settings.UnitMenuKey;
            }
            UnitStatusText.Text = empty
                ? "Required only when a preset changes the active team."
                : error.Message;
            UnitDiagnostic = empty
                ? "Not set"
                : "Conflict";
        }
    }

    private void UpdateAreasDisplay()
    {
        AppServices services = Services;
        try
        {
            char key = AppSettings.ParseAreasMenuKey(
                services.Settings.AreasMenuKey,
                services.Hotkey.VirtualKey,
                services.Settings.PlayMenuKey,
                services.Settings.UnitMenuKey);
            AppSettings.ValidateControlKeySet(
                services.Settings,
                requireUnitActionKeys: false);
            if (_captureTarget != BindingTarget.Areas)
            {
                AreasButton.Content = key.ToString();
            }
            AreasStatusText.Text =
                $"{key} must match Anime Expeditions' Toggle Areas Menu binding.";
            AreasDiagnostic = key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(
                services.Settings.AreasMenuKey);
            if (_captureTarget != BindingTarget.Areas)
            {
                AreasButton.Content = empty
                    ? "Not set"
                    : services.Settings.AreasMenuKey;
            }
            AreasStatusText.Text = empty
                ? "Required by refuel Utilities and the resource-refuel Debug tool."
                : error.Message;
            AreasDiagnostic = empty
                ? "Not set"
                : "Conflict";
        }
    }

    private void UpdateCancelPlacementDisplay()
    {
        AppServices services = Services;
        try
        {
            char key =
                AppSettings.ParseCancelPlacementKey(
                    services.Settings.CancelPlacementKey,
                    services.Hotkey.VirtualKey,
                    services.Settings.PlayMenuKey,
                    services.Settings.UnitMenuKey,
                    services.Settings.AreasMenuKey,
                    services.Settings
                        .ShiftLockVirtualKey);
            AppSettings.ValidateControlKeySet(
                services.Settings,
                requireUnitActionKeys: false);
            if (_captureTarget !=
                BindingTarget.CancelPlacement)
            {
                CancelPlacementButton.Content =
                    key.ToString();
            }
            CancelPlacementStatusText.Text =
                $"{key} must match Anime Expeditions' Toggle Cancel Unit Placement binding.";
            CancelPlacementDiagnostic =
                key.ToString();
        }
        catch (InvalidDataException error)
        {
            bool empty = string.IsNullOrWhiteSpace(
                services.Settings.CancelPlacementKey);
            if (_captureTarget !=
                BindingTarget.CancelPlacement)
            {
                CancelPlacementButton.Content =
                    empty
                        ? "Not set"
                        : services.Settings
                            .CancelPlacementKey;
            }
            CancelPlacementStatusText.Text =
                empty
                    ? "Required only when Match Steps contain placement actions."
                    : error.Message;
            CancelPlacementDiagnostic =
                empty
                    ? "Not set"
                    : "Conflict";
        }
    }

    private void UpdateQuickPlacementDisplay()
    {
        int virtualKey =
            Services.Settings
                .QuickPlacementVirtualKey;
        if (virtualKey == 0)
        {
            if (_captureTarget !=
                BindingTarget.QuickPlacement)
            {
                QuickPlacementButton.Content =
                    "Not set";
            }
            QuickPlacementStatusText.Text =
                "Required before starting a plan whose Match Steps contain placement actions.";
            QuickPlacementDiagnostic = "Not set";
            return;
        }

        string display =
            KeyboardKey.GetDisplayName(virtualKey);
        try
        {
            _ = AppSettings.ParseQuickPlacementKey(
                Services.Settings);
            if (_captureTarget !=
                BindingTarget.QuickPlacement)
            {
                QuickPlacementButton.Content =
                    display;
            }
            QuickPlacementStatusText.Text =
                $"{display} must match Anime Expeditions' Quick Placement binding.";
            QuickPlacementDiagnostic = display;
        }
        catch (InvalidDataException error)
        {
            if (_captureTarget !=
                BindingTarget.QuickPlacement)
            {
                QuickPlacementButton.Content =
                    display;
            }
            QuickPlacementStatusText.Text =
                error.Message;
            QuickPlacementDiagnostic = "Conflict";
        }
    }

    private void UpdateTargetingDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.Targeting,
            Services.Settings.ChangeUnitTargetingKey,
            "Change Unit Targeting",
            TargetingButton,
            TargetingStatusText,
            "Required only when a placement step changes targeting from First.",
            value => TargetingDiagnostic = value);

    private void UpdateUpgradeDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.Upgrade,
            Services.Settings.UpgradeUnitKey,
            "Upgrade Unit",
            UpgradeButton,
            UpgradeStatusText,
            "Required only when a macro workflow uses Upgrade Unit.",
            value => UpgradeDiagnostic = value);

    private void UpdateAutoUpgradeDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.AutoUpgradeUnit,
            Services.Settings.AutoUpgradeUnitKey,
            "Auto Upgrade Unit",
            AutoUpgradeUnitButton,
            AutoUpgradeUnitStatusText,
            "Required only when a placement step selects Auto Upgrade Priority 1 through 6.",
            value => AutoUpgradeDiagnostic = value);

    private void UpdateToggleAutoUpgradePlacedUnitsDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.ToggleAutoUpgradePlacedUnits,
            Services.Settings
                .ToggleAutoUpgradePlacedUnitsKey,
            "Toggle Auto Upgrade Placed Units",
            ToggleAutoUpgradePlacedUnitsButton,
            ToggleAutoUpgradePlacedUnitsStatusText,
            "Required only when a macro workflow toggles Auto Upgrade for placed units.",
            value =>
                ToggleAutoUpgradePlacedUnitsDiagnostic =
                    value);

    private void UpdateRequiredUnitActionDisplay(
        BindingTarget target,
        string configuredValue,
        string bindingName,
        System.Windows.Controls.Button button,
        System.Windows.Controls.TextBlock status,
        string unsetStatus,
        Action<string> setDiagnostic)
    {
        string candidate = configuredValue.Trim();
        try
        {
            if (candidate.Length != 1 ||
                !char.IsAsciiLetter(candidate[0]))
            {
                throw new InvalidDataException(
                    $"Scroll down to Controls on the Dashboard and set the {bindingName} key.");
            }
            AppSettings.ValidateControlKeySet(
                Services.Settings,
                requireUnitActionKeys: false);
            string key =
                char.ToUpperInvariant(candidate[0])
                    .ToString();
            if (_captureTarget != target)
            {
                button.Content = key;
            }
            status.Text =
                $"{key} must match Anime Expeditions' {bindingName} binding.";
            setDiagnostic(key);
        }
        catch (InvalidDataException error)
        {
            bool empty = candidate.Length == 0;
            if (_captureTarget != target)
            {
                button.Content = empty
                    ? "Not set"
                    : configuredValue;
            }
            status.Text = empty
                ? unsetStatus
                : error.Message;
            setDiagnostic(empty
                ? "Not set"
                : "Conflict");
        }
    }

    private void UpdateShiftLockDisplay()
    {
        AppServices services = Services;
        if (services.Settings.ShiftLockVirtualKey == 0)
        {
            if (_captureTarget != BindingTarget.ShiftLock)
            {
                ShiftLockButton.Content = "Not set";
            }
            ShiftLockStatusText.Text =
                "Required only when camera preparation uses Toggle Shift Lock.";
            ShiftLockDiagnostic = "Not set";
            return;
        }
        string display = KeyboardKey.GetDisplayName(
            services.Settings.ShiftLockVirtualKey);
        try
        {
            _ = AppSettings.ParseShiftLockKey(
                services.Settings.ShiftLockVirtualKey,
                services.Hotkey.VirtualKey,
                services.Settings.PlayMenuKey,
                services.Settings.UnitMenuKey,
                services.Settings.AreasMenuKey,
                services.Settings.CancelPlacementKey,
                services.Settings
                    .QuickPlacementVirtualKey);
            AppSettings.ValidateControlKeySet(
                services.Settings,
                requireUnitActionKeys: false);
            if (_captureTarget != BindingTarget.ShiftLock)
            {
                ShiftLockButton.Content = display;
            }
            ShiftLockStatusText.Text =
                $"{display} must match Anime Expeditions' Toggle Shift Lock binding.";
            ShiftLockDiagnostic = display;
        }
        catch (InvalidDataException error)
        {
            if (_captureTarget != BindingTarget.ShiftLock)
            {
                ShiftLockButton.Content = display;
            }
            ShiftLockStatusText.Text = error.Message;
            ShiftLockDiagnostic = "Conflict";
        }
    }
}
