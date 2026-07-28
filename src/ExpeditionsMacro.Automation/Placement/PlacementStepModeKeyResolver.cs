using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed record PlacementStepModeKeys(
    int QuickPlacement,
    char CancelPlacement,
    char Targeting,
    char AutoUpgrade);

internal sealed class PlacementStepModeKeyResolver(
    Func<char> targetingKey,
    Func<char> autoUpgradeKey,
    Func<int> quickPlacementKey)
{
    public PlacementStepModeKeys Resolve(
        IReadOnlyList<PlacementStep> steps,
        char cancelPlacementKey)
    {
        int quickPlacement =
            quickPlacementKey();
        if (!KeyboardKey.IsSupportedQuickPlacementKey(
                quickPlacement))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Quick Placement key to the same physical key assigned in Anime Expeditions.");
        }
        if (!char.IsAsciiLetter(
                cancelPlacementKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard, then set Toggle Cancel Unit Placement key to the same letter assigned in Anime Expeditions.");
        }

        char targeting = default;
        if (steps.Any(step =>
                (int)step.TargetingPriority > 0))
        {
            targeting = targetingKey();
            if (!char.IsAsciiLetter(targeting))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Change Unit Targeting key to the same A-Z letter assigned in Anime Expeditions.");
            }
            targeting =
                char.ToUpperInvariant(targeting);
        }

        char autoUpgrade = default;
        if (steps.Any(step =>
                step.AutoUpgradePriority !=
                    UnitAutoUpgradePriority.Off))
        {
            autoUpgrade = autoUpgradeKey();
            if (!char.IsAsciiLetter(autoUpgrade))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Auto Upgrade Unit key to the same A-Z letter assigned in Anime Expeditions.");
            }
            autoUpgrade =
                char.ToUpperInvariant(autoUpgrade);
        }

        return new PlacementStepModeKeys(
            quickPlacement,
            char.ToUpperInvariant(
                cancelPlacementKey),
            targeting,
            autoUpgrade);
    }
}
