using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed record PlacementStepModeKeys(
    int QuickPlacement,
    char CancelPlacement,
    char Targeting,
    char AutoUpgrade,
    char Upgrade,
    char Sell);

internal sealed class PlacementStepModeKeyResolver(
    Func<char> targetingKey,
    Func<char> autoUpgradeKey,
    Func<int> quickPlacementKey,
    Func<char> upgradeKey,
    Func<char> sellKey)
{
    public PlacementStepModeKeys Resolve(
        IReadOnlyList<PlacementStep> steps,
        char cancelPlacementKey)
    {
        bool placesUnits = steps.Any(step =>
            step.Kind == MatchStepKind.Placement);
        int quickPlacement = default;
        char normalizedCancel = default;
        if (placesUnits)
        {
            quickPlacement = quickPlacementKey();
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
            normalizedCancel =
                char.ToUpperInvariant(
                    cancelPlacementKey);
        }

        char targeting = default;
        if (steps.Any(step =>
                (step.Kind ==
                     MatchStepKind.Placement &&
                 (int)step.TargetingPriority > 0) ||
                (step.Kind ==
                     MatchStepKind.ReconfigureUnit &&
                 step.ChangeTargetingPriority)))
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
                (step.Kind ==
                     MatchStepKind.Placement &&
                 step.AutoUpgradePriority !=
                     UnitAutoUpgradePriority.Off) ||
                (step.Kind ==
                     MatchStepKind.ReconfigureUnit &&
                 step.AutoUpgradeAction !=
                     MatchAutoUpgradeAction.NoChange)))
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

        char upgrade = default;
        if (steps.Any(step =>
                step.Kind ==
                    MatchStepKind.UpgradeUnit))
        {
            upgrade = upgradeKey();
            if (!char.IsAsciiLetter(upgrade))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Upgrade Unit key to the same A-Z letter assigned in Anime Expeditions.");
            }
            upgrade =
                char.ToUpperInvariant(upgrade);
        }

        char sell = default;
        if (steps.Any(step =>
                step.Kind == MatchStepKind.SellUnit))
        {
            sell = sellKey();
            if (!char.IsAsciiLetter(sell))
            {
                throw new InvalidDataException(
                    "Scroll down to Controls on the Dashboard and set Sell Unit key to the same A-Z letter assigned in Anime Expeditions.");
            }
            sell = char.ToUpperInvariant(sell);
        }

        return new PlacementStepModeKeys(
            quickPlacement,
            normalizedCancel,
            targeting,
            autoUpgrade,
            upgrade,
            sell);
    }
}
