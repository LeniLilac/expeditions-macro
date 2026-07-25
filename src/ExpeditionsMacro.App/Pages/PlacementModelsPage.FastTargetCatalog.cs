using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private static readonly IReadOnlyList<
        PlacementEditorChoice<PlacementTargetMode>>
        TargetModes =
        [
            new(
                PlacementTargetMode.Expedition,
                "Expedition"),
            new(
                PlacementTargetMode.Challenge,
                "Challenge"),
            new(
                PlacementTargetMode.Story,
                "Story"),
            new(
                PlacementTargetMode.Raid,
                "Raid"),
        ];

    private static readonly IReadOnlyList<
        PlacementStoryVariant>
        StoryVariants =
        [
            new(
                StoryRunKind.Act,
                PlacementSetupCatalog
                    .SharedStoryActNumber,
                "Shared map setup"),
            .. Enumerable.Range(1, 5)
                .Select(act => new PlacementStoryVariant(
                    StoryRunKind.Act,
                    act,
                    $"Act {act}")),
            new(
                StoryRunKind.Mastery,
                1,
                "Mastery"),
            new(
                StoryRunKind.Infinite,
                1,
                "Infinite"),
        ];

    private static IReadOnlyList<
        PlacementEditorChoice<int>>
        MapChoices(PlacementTargetMode mode) =>
        mode switch
        {
            PlacementTargetMode.Expedition =>
            [
                new(
                    PlacementSetupCatalog
                        .SharedExpeditionMapNumber,
                    "Shared setup"),
                new(1, "School Grounds"),
                new(2, "Flower Forest"),
                new(3, "Rose Kingdom"),
            ],
            PlacementTargetMode.Challenge or
            PlacementTargetMode.Story =>
            [
                new(1, "School Grounds"),
                new(2, "Flower Forest"),
                new(3, "Rose Kingdom"),
                new(4, "Fairy King Forest"),
                new(5, "King's Tomb"),
            ],
            PlacementTargetMode.Raid =>
            [
                new(1, "Spirit City"),
            ],
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode)),
        };

    private static string StoryVariantLabel(
        PlacementTarget target) =>
        target.StoryRunKind switch
        {
            StoryRunKind.Act
                when target.ActNumber ==
                    PlacementSetupCatalog
                        .SharedStoryActNumber =>
                "Shared map setup",
            StoryRunKind.Act =>
                $"Act {target.ActNumber}",
            StoryRunKind.Mastery => "Mastery",
            StoryRunKind.Infinite => "Infinite",
            _ => target.StoryRunKind.ToString(),
        };
}
