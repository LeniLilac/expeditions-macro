using System.Windows;
using System.Windows.Media;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.App.Pages;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Services;

internal static class PlacementSnapshotPreparer
{
    public static void Prepare(
        FrameworkElement root,
        string file)
    {
        if (!RequiresPreparation(file))
        {
            return;
        }

        PlacementModelsPage? page =
            FindVisualChild<PlacementModelsPage>(
                root);
        if (page is null)
        {
            throw new InvalidOperationException(
                "The Placement Setup snapshot did not contain its page.");
        }
        if (MatchesState(
                file,
                "placement-setup-timing"))
        {
            page.SetSnapshotMatchSettings(
                placementIntervalMilliseconds: 900,
                placementAttempts: 4,
                defaultTargetingPriority:
                    UnitTargetingPriority.Strongest,
                defaultAutoUpgradePriority:
                    UnitAutoUpgradePriority.Priority4,
                defaultAfterStartDelayMilliseconds: 30_000,
                impossibilityThresholdMinutes: 0,
                recordingMode: false,
                new PlacementAdvancedSettings());
            root.UpdateLayout();
            return;
        }
        if (MatchesState(
                file,
                "placement-setup-match-step-editor"))
        {
            page.SetSnapshotStepSettings(
                new PlacementStepRow
                {
                    Kind =
                        MatchStepKind.ReconfigureUnit,
                    UnitKey = 1,
                    X = 390,
                    Y = 352,
                    Phase =
                        PlacementPhase.BeforeStart,
                    ChangeTargetingPriority = true,
                    TargetingPriority =
                        UnitTargetingPriority.Strongest,
                    AutoUpgradeAction =
                        MatchAutoUpgradeAction.Priority2,
                });
            root.UpdateLayout();
            return;
        }
        if (file.StartsWith(
                "placement-setup-advanced-",
                StringComparison.OrdinalIgnoreCase))
        {
            PrepareAdvanced(root, page, file);
            return;
        }
        if (file.Contains(
                "recording",
                StringComparison.OrdinalIgnoreCase))
        {
            page.ClearSnapshotMatchSettings();
        }
        page.SetCompactSnapshotViewport(
            file.Contains(
                "steps",
                StringComparison.OrdinalIgnoreCase));
        root.UpdateLayout();
    }

    private static void PrepareAdvanced(
        FrameworkElement root,
        PlacementModelsPage page,
        string file)
    {
        bool recording = file.EndsWith(
            "-recording",
            StringComparison.OrdinalIgnoreCase);
        page.SetSnapshotMatchSettings(
            placementIntervalMilliseconds: 900,
            placementAttempts: 2,
            defaultTargetingPriority:
                UnitTargetingPriority.First,
            defaultAutoUpgradePriority:
                UnitAutoUpgradePriority.Priority1,
            defaultAfterStartDelayMilliseconds: 30_000,
            impossibilityThresholdMinutes:
                recording ? 18 : 0,
            recording,
            new PlacementAdvancedSettings
            {
                Enabled = true,
                UnitSelectionDelayMilliseconds = 180,
                PlacementBurstDurationMilliseconds = 35,
                BeforeSelectionClickMilliseconds = 120,
                BeforeSelectedUnitProofMilliseconds = 250,
                ActionKeyIntervalMilliseconds = 80,
                VerifySelectedUnitPanelBeforeActions = true,
                VerifyPrestartBeforeManualPlayback = false,
                ManualPlaybackStartDelayMilliseconds = 4200,
            });
        root.UpdateLayout();
        page.ScrollSnapshotAdvancedSettingsIntoView();
        root.UpdateLayout();
    }

    private static bool RequiresPreparation(
        string file) =>
        file.Contains(
            "placement-setup",
            StringComparison.OrdinalIgnoreCase) &&
        (file.Contains(
             "-small",
             StringComparison.OrdinalIgnoreCase) ||
         file.Contains(
             "-medium",
             StringComparison.OrdinalIgnoreCase) ||
         file.Contains(
             "-collapsed",
             StringComparison.OrdinalIgnoreCase) ||
         file.StartsWith(
             "placement-setup-advanced-",
             StringComparison.OrdinalIgnoreCase) ||
         string.Equals(
             file,
             "placement-setup-timing",
             StringComparison.OrdinalIgnoreCase) ||
         string.Equals(
             file,
             "placement-setup-match-step-editor",
             StringComparison.OrdinalIgnoreCase));

    private static bool MatchesState(
        string file,
        string state) =>
        string.Equals(
            file,
            state,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            file,
            $"{state}-small",
            StringComparison.OrdinalIgnoreCase);

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0;
             index < VisualTreeHelper.GetChildrenCount(
                 parent);
             index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);
            if (child is T match)
            {
                return match;
            }
            T? nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }
}
