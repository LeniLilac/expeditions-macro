namespace ExpeditionsMacro.Core.Models;

public enum CameraPreparationMode
{
    // Retained so public-beta JSON remains readable. Runtime supports
    // FastNoAlign only.
    CameraModel,
    FastNoAlign,
}

public enum PlacementPhase
{
    BeforeStart,
    AfterStart,
}

public enum PlacementTargetMode
{
    Expedition,
    Challenge,
    Story,
    Raid,
    Event,
}

public sealed record PlacementPhaseChange(
    IReadOnlyList<PlacementStep> Steps,
    int ChangedIndex,
    bool Changed);

public static class PlacementAuthoringRules
{
    public const int MinimumPlacementSpacingPixels = 7;

    public const int DefaultStepDelayMilliseconds = 900;

    public const int DefaultAfterStartDelayMilliseconds = 30_000;

    public const UnitAutoUpgradePriority
        DefaultAutoUpgradePriority =
            UnitAutoUpgradePriority.Priority1;

    private const int StartDialogLeft = 310;
    private const int StartDialogTop = 92;
    private const int StartDialogRight = 498;
    private const int StartDialogBottom = 208;

    public static bool AreSeparated(
        int firstX,
        int firstY,
        int secondX,
        int secondY)
    {
        long deltaX = firstX - secondX;
        long deltaY = firstY - secondY;
        long minimum = MinimumPlacementSpacingPixels;
        return deltaX * deltaX + deltaY * deltaY >=
            minimum * minimum;
    }

    public static IReadOnlyList<PlacementStep>
        OrderForAuthoring(
            IReadOnlyList<PlacementStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps
            .Select(
                (step, index) =>
                    new
                    {
                        Step = step,
                        Index = index,
                    })
            .OrderBy(item =>
                item.Step.Phase ==
                    PlacementPhase.BeforeStart
                    ? 0
                    : 1)
            .ThenBy(item => item.Index)
            .Select(item => item.Step)
            .ToArray();
    }

    public static PlacementPhaseChange
        ChangePhaseForAuthoring(
            IReadOnlyList<PlacementStep> steps,
            int sourceIndex,
            PlacementPhase destination)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (sourceIndex < 0 ||
            sourceIndex >= steps.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceIndex));
        }
        if (!Enum.IsDefined(destination))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination));
        }

        foreach (PlacementStep step in steps)
        {
            if (!Enum.IsDefined(step.Phase))
            {
                throw new InvalidDataException(
                    "Placement phase is invalid.");
            }
        }
        PlacementStep source = steps[sourceIndex];
        if (source.Phase == destination)
        {
            return new PlacementPhaseChange(
                steps.ToArray(),
                sourceIndex,
                Changed: false);
        }

        PlacementStep changed =
            source with { Phase = destination };
        IReadOnlyList<PlacementStep> ordered =
            OrderForAuthoring(
                steps.Select(
                        (step, index) =>
                            index == sourceIndex
                                ? changed
                                : step)
                    .ToArray());
        int beforeCount = ordered.Count(
            step =>
                step.Phase ==
                PlacementPhase.BeforeStart);
        return new PlacementPhaseChange(
            ordered,
            destination == PlacementPhase.BeforeStart
                ? beforeCount - 1
                : beforeCount,
            Changed: true);
    }

    public static void ValidateMinimumSpacing(
        IReadOnlyList<PlacementStep> steps)
    {
        for (int first = 0; first < steps.Count; first++)
        {
            for (int second = first + 1;
                 second < steps.Count;
                 second++)
            {
                if (AreSeparated(
                    steps[first].X,
                    steps[first].Y,
                    steps[second].X,
                    steps[second].Y))
                {
                    continue;
                }

                throw new InvalidDataException(
                    $"Fast no align placements must be at least {MinimumPlacementSpacingPixels} client pixels apart.");
            }
        }
    }

    public static bool IsCoveredByStartDialog(
        int x,
        int y) =>
        x >= StartDialogLeft &&
        x <= StartDialogRight &&
        y >= StartDialogTop &&
        y <= StartDialogBottom;

    public static void ValidateBeforeStartSafety(
        IReadOnlyList<PlacementStep> steps)
    {
        PlacementStep? covered = steps.FirstOrDefault(
            step =>
                step.Phase == PlacementPhase.BeforeStart &&
                IsCoveredByStartDialog(step.X, step.Y));
        if (covered is not null)
        {
            throw new InvalidDataException(
                $"Before-start placement ({covered.X}, {covered.Y}) is covered by the Start Game dialog. Move it outside the centered dialog or mark it After Start.");
        }
    }
}

public sealed record PlacementTarget
{
    public required PlacementTargetMode Mode { get; init; }

    public required int MapNumber { get; init; }

    public StoryRunKind StoryRunKind { get; init; } = StoryRunKind.Act;

    public int ActNumber { get; init; }

    public EventSpawnRoute SpawnRoute { get; init; } =
        EventSpawnRoute.Angle1;

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) ||
            !Enum.IsDefined(StoryRunKind) ||
            !Enum.IsDefined(SpawnRoute))
        {
            throw new InvalidDataException(
                "The placement route is invalid.");
        }
        if (Mode != PlacementTargetMode.Event &&
            SpawnRoute != EventSpawnRoute.Angle1)
        {
            throw new InvalidDataException(
                "Alternate spawn routes are only valid for Event placements.");
        }

        switch (Mode)
        {
            case PlacementTargetMode.Expedition:
                RequireRange(MapNumber, 0, 3, "Expedition map");
                RequireExact(ActNumber, 0, "Expedition act");
                break;
            case PlacementTargetMode.Challenge:
                RequireRange(MapNumber, 1, 5, "Challenge map");
                RequireExact(ActNumber, 0, "Challenge act");
                break;
            case PlacementTargetMode.Story:
                RequireRange(MapNumber, 1, 5, "Story map");
                if (StoryRunKind == StoryRunKind.Act)
                {
                    RequireRange(
                        ActNumber,
                        PlacementSetupCatalog.SharedStoryActNumber,
                        5,
                        "Story act");
                }
                else
                {
                    RequireExact(
                        ActNumber,
                        1,
                        "Story run variant");
                }
                break;
            case PlacementTargetMode.Raid:
                RequireExact(MapNumber, 1, "Raid map");
                RequireRange(ActNumber, 1, 3, "Raid act");
                break;
            case PlacementTargetMode.Event:
                RequireExact(
                    MapNumber,
                    (int)EventModeId.VillainInvasion,
                    "Event mode");
                RequireRange(ActNumber, 1, 4, "Event act");
                if (ActNumber != (int)EventAct.Act1 &&
                    SpawnRoute != EventSpawnRoute.Angle1)
                {
                    throw new InvalidDataException(
                        "Only Villain Invasion Act 1 supports alternate spawn routes.");
                }
                break;
            default:
                throw new InvalidDataException(
                    "The placement route is invalid.");
        }
    }

    public bool Matches(PlacementTarget other) =>
        Mode == other.Mode &&
        MapNumber == other.MapNumber &&
        StoryRunKind == other.StoryRunKind &&
        ActNumber == other.ActNumber &&
        SpawnRoute == other.SpawnRoute;

    public static PlacementTarget ForExpedition(
        ExpeditionPreset preset) =>
        new()
        {
            Mode = PlacementTargetMode.Expedition,
            MapNumber = preset.MapNumber,
            ActNumber = 0,
        };

    public static PlacementTarget ForChallenge(
        ChallengeMapId map) =>
        new()
        {
            Mode = PlacementTargetMode.Challenge,
            MapNumber = (int)map,
            ActNumber = 0,
        };

    public static PlacementTarget ForStory(
        StoryPreset preset) =>
        new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = (int)preset.Map,
            StoryRunKind = preset.RunKind,
            ActNumber = preset.RunKind == StoryRunKind.Act
                ? preset.ActNumber
                : 1,
        };

    public static PlacementTarget ForRaid(
        RaidPreset preset) =>
        new()
        {
            Mode = PlacementTargetMode.Raid,
            MapNumber = 1,
            ActNumber = (int)preset.Act,
        };

    public static PlacementTarget ForEvent(
        EventPreset preset) =>
        new()
        {
            Mode = PlacementTargetMode.Event,
            MapNumber = (int)preset.Mode,
            ActNumber = (int)preset.Act,
            SpawnRoute = preset.SpawnRoute,
        };

    private static void RequireRange(
        int value,
        int minimum,
        int maximum,
        string label)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                $"{label} must be {minimum} through {maximum}.");
        }
    }

    private static void RequireExact(
        int value,
        int expected,
        string label)
    {
        if (value != expected)
        {
            throw new InvalidDataException(
                $"{label} is not valid for this placement route.");
        }
    }
}
