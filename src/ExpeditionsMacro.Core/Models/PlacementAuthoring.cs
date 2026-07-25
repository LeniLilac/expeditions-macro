namespace ExpeditionsMacro.Core.Models;

public enum CameraPreparationMode
{
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
}

public static class PlacementAuthoringRules
{
    public const int MinimumPlacementSpacingPixels = 7;

    public const int DefaultStepDelayMilliseconds = 900;

    public const int DefaultAfterStartDelayMilliseconds = 30_000;

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
}

public sealed record PlacementTarget
{
    public required PlacementTargetMode Mode { get; init; }

    public required int MapNumber { get; init; }

    public StoryRunKind StoryRunKind { get; init; } = StoryRunKind.Act;

    public int ActNumber { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) ||
            !Enum.IsDefined(StoryRunKind))
        {
            throw new InvalidDataException(
                "The placement route is invalid.");
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
            default:
                throw new InvalidDataException(
                    "The placement route is invalid.");
        }
    }

    public bool Matches(PlacementTarget other) =>
        Mode == other.Mode &&
        MapNumber == other.MapNumber &&
        StoryRunKind == other.StoryRunKind &&
        ActNumber == other.ActNumber;

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
