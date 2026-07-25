namespace ExpeditionsMacro.Core.Models;

public sealed record PlacementSetupRoute(
    PlacementTarget Target,
    string ModelId,
    string Name,
    string ModeName,
    string RouteName);

public static class PlacementSetupCatalog
{
    public const int SharedExpeditionMapNumber = 0;
    public const int SharedStoryActNumber = 0;

    private static readonly string[] MapNames =
    [
        "School Grounds",
        "Flower Forest",
        "Rose Kingdom",
        "Fairy King Forest",
        "King's Tomb",
    ];

    public static IReadOnlyList<PlacementSetupRoute> All { get; } =
        BuildRoutes();

    public static PlacementSetupRoute For(
        PlacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        return All.FirstOrDefault(
            route => route.Target.Matches(target))
            ?? throw new InvalidDataException(
                "The placement route is not supported.");
    }

    public static string IdFor(PlacementTarget target) =>
        For(target).ModelId;

    public static string NameFor(PlacementTarget target) =>
        For(target).Name;

    public static IEnumerable<PlacementSetupRoute>
        CandidatesFor(PlacementTarget target)
    {
        yield return For(target);
        if (target.Mode == PlacementTargetMode.Expedition &&
            target.MapNumber != SharedExpeditionMapNumber)
        {
            yield return For(new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = SharedExpeditionMapNumber,
            });
        }
        if (IsConcreteStoryOrChallenge(target))
        {
            yield return For(SharedStoryTarget(
                target.MapNumber));
        }
    }

    public static bool Covers(
        PlacementTarget setupTarget,
        PlacementTarget requiredTarget)
    {
        setupTarget.Validate();
        requiredTarget.Validate();
        return setupTarget.Matches(requiredTarget) ||
            setupTarget.Mode == PlacementTargetMode.Expedition &&
            requiredTarget.Mode == PlacementTargetMode.Expedition &&
            setupTarget.MapNumber == SharedExpeditionMapNumber &&
            requiredTarget.MapNumber is >= 1 and <= 3 ||
            IsSharedStoryTarget(setupTarget) &&
            IsConcreteStoryOrChallenge(requiredTarget) &&
            setupTarget.MapNumber == requiredTarget.MapNumber;
    }

    private static IReadOnlyList<PlacementSetupRoute>
        BuildRoutes()
    {
        List<PlacementSetupRoute> routes = [];
        Add(
            routes,
            new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = SharedExpeditionMapNumber,
            },
            "Expeditions",
            "Shared setup",
            "setup-expeditions-shared");
        for (int map = 1; map <= 3; map++)
        {
            Add(
                routes,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Expedition,
                    MapNumber = map,
                },
                "Expedition",
                MapNames[map - 1],
                $"setup-expedition-map-{map}");
        }

        for (int map = 1; map <= 5; map++)
        {
            Add(
                routes,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Challenge,
                    MapNumber = map,
                },
                "Challenge",
                MapNames[map - 1],
                $"setup-challenge-map-{map}");
        }

        for (int map = 1; map <= 5; map++)
        {
            Add(
                routes,
                SharedStoryTarget(map),
                "Story",
                $"Map {map} · {MapNames[map - 1]}",
                $"setup-story-map-{map}-shared");
            for (int act = 1; act <= 5; act++)
            {
                Add(
                    routes,
                    new PlacementTarget
                    {
                        Mode = PlacementTargetMode.Story,
                        MapNumber = map,
                        StoryRunKind = StoryRunKind.Act,
                        ActNumber = act,
                    },
                    "Story",
                    $"{MapNames[map - 1]} · Act {act}",
                    $"setup-story-map-{map}-act-{act}");
            }

            Add(
                routes,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber = map,
                    StoryRunKind = StoryRunKind.Mastery,
                    ActNumber = 1,
                },
                "Story",
                $"{MapNames[map - 1]} · Mastery",
                $"setup-story-map-{map}-mastery");
            Add(
                routes,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber = map,
                    StoryRunKind = StoryRunKind.Infinite,
                    ActNumber = 1,
                },
                "Story",
                $"{MapNames[map - 1]} · Infinite",
                $"setup-story-map-{map}-infinite");
        }

        for (int act = 1; act <= 3; act++)
        {
            Add(
                routes,
                new PlacementTarget
                {
                    Mode = PlacementTargetMode.Raid,
                    MapNumber = 1,
                    ActNumber = act,
                },
                "Raid",
                $"Spirit City · Act {act}",
                $"setup-raid-map-1-act-{act}");
        }

        return routes;
    }

    public static bool IsSharedStoryTarget(
        PlacementTarget target) =>
        target.Mode == PlacementTargetMode.Story &&
        target.StoryRunKind == StoryRunKind.Act &&
        target.ActNumber == SharedStoryActNumber;

    private static bool IsConcreteStoryOrChallenge(
        PlacementTarget target) =>
        target.Mode == PlacementTargetMode.Challenge ||
        target.Mode == PlacementTargetMode.Story &&
        !IsSharedStoryTarget(target);

    private static PlacementTarget SharedStoryTarget(
        int mapNumber) =>
        new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = mapNumber,
            StoryRunKind = StoryRunKind.Act,
            ActNumber = SharedStoryActNumber,
        };

    private static void Add(
        ICollection<PlacementSetupRoute> routes,
        PlacementTarget target,
        string modeName,
        string routeName,
        string modelId)
    {
        target.Validate();
        routes.Add(
            new PlacementSetupRoute(
                target,
                modelId,
                $"{modeName} · {routeName}",
                modeName,
                routeName));
    }
}
