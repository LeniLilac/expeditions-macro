using System.IO;
using ExpeditionsMacro.App.Services;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Controls;

public partial class SettingsKeyBindingsPanel
{
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
                    ? "Set key"
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
                    ? "Set key"
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
                    ? "Set key"
                    : services.Settings.AreasMenuKey;
            }
            AreasStatusText.Text = empty
                ? "Required only by the experimental resource-refuel Debug tools."
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
            if (_captureTarget !=
                BindingTarget.CancelPlacement)
            {
                CancelPlacementButton.Content =
                    services.Settings
                        .CancelPlacementKey;
            }
            CancelPlacementStatusText.Text =
                error.Message;
            CancelPlacementDiagnostic =
                "Conflict";
        }
    }

    private void UpdateTargetingDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.Targeting,
            Services.Settings.ChangeUnitTargetingKey,
            "Change Unit Targeting",
            TargetingButton,
            TargetingStatusText,
            value => TargetingDiagnostic = value);

    private void UpdateUpgradeDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.Upgrade,
            Services.Settings.UpgradeUnitKey,
            "Upgrade Unit",
            UpgradeButton,
            UpgradeStatusText,
            value => UpgradeDiagnostic = value);

    private void UpdateAutoUpgradeDisplay() =>
        UpdateRequiredUnitActionDisplay(
            BindingTarget.AutoUpgrade,
            Services.Settings.ToggleAutoUpgradeUnitKey,
            "Toggle Auto Upgrade Unit",
            AutoUpgradeButton,
            AutoUpgradeStatusText,
            value => AutoUpgradeDiagnostic = value);

    private void UpdateRequiredUnitActionDisplay(
        BindingTarget target,
        string configuredValue,
        string bindingName,
        System.Windows.Controls.Button button,
        System.Windows.Controls.TextBlock status,
        Action<string> setDiagnostic)
    {
        string candidate = configuredValue.Trim();
        try
        {
            if (candidate.Length != 1 ||
                !char.IsAsciiLetter(candidate[0]))
            {
                throw new InvalidDataException(
                    $"Set the {bindingName} key under Settings > Controls.");
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
                    ? "Set key"
                    : configuredValue;
            }
            status.Text = empty
                ? "Required before a macro can start."
                : error.Message;
            setDiagnostic(empty
                ? "Not set"
                : "Conflict");
        }
    }

    private void UpdateShiftLockDisplay()
    {
        AppServices services = Services;
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
                services.Settings.CancelPlacementKey);
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
