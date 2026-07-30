using System.Windows.Controls;

namespace ExpeditionsMacro.App.Controls;

public partial class SettingsKeyBindingsPanel
{
    private static string BindingLabel(
        BindingTarget target) => target switch
        {
            BindingTarget.Play => "Toggle Play Menu",
            BindingTarget.Unit => "Toggle Unit Inventory",
            BindingTarget.Areas => "Toggle Areas Menu",
            BindingTarget.CancelPlacement =>
                "Toggle Cancel Unit Placement",
            BindingTarget.QuickPlacement => "Quick Placement",
            BindingTarget.Targeting => "Change Unit Targeting",
            BindingTarget.Upgrade => "Upgrade Unit",
            BindingTarget.Sell => "Sell Unit",
            BindingTarget.AutoUpgradeUnit => "Auto Upgrade Unit",
            BindingTarget.ToggleAutoUpgradePlacedUnits =>
                "Toggle Auto Upgrade Placed Units",
            BindingTarget.ShiftLock => "Toggle Shift Lock",
            _ => throw new ArgumentOutOfRangeException(
                nameof(target)),
        };

    private TextBlock StatusFor(
        BindingTarget target) => target switch
        {
            BindingTarget.Macro => MacroStatusText,
            BindingTarget.Play => PlayStatusText,
            BindingTarget.Unit => UnitStatusText,
            BindingTarget.Areas => AreasStatusText,
            BindingTarget.CancelPlacement =>
                CancelPlacementStatusText,
            BindingTarget.QuickPlacement =>
                QuickPlacementStatusText,
            BindingTarget.Targeting => TargetingStatusText,
            BindingTarget.Upgrade => UpgradeStatusText,
            BindingTarget.Sell => SellStatusText,
            BindingTarget.AutoUpgradeUnit =>
                AutoUpgradeUnitStatusText,
            BindingTarget.ToggleAutoUpgradePlacedUnits =>
                ToggleAutoUpgradePlacedUnitsStatusText,
            BindingTarget.ShiftLock => ShiftLockStatusText,
            _ => throw new ArgumentOutOfRangeException(
                nameof(target)),
        };

    private enum BindingTarget
    {
        None,
        Macro,
        Play,
        Unit,
        Areas,
        CancelPlacement,
        QuickPlacement,
        Targeting,
        Upgrade,
        Sell,
        AutoUpgradeUnit,
        ToggleAutoUpgradePlacedUnits,
        ShiftLock,
    }
}
