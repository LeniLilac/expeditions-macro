namespace ExpeditionsMacro.Core.Models;

public readonly record struct UnitActionKeys(
    char ChangeTargeting,
    char Upgrade,
    char AutoUpgrade,
    char ToggleAutoUpgradePlacedUnits);
