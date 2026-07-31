using System.Windows;
using ExpeditionsMacro.App.Models;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void RefreshSetupRows(
        IReadOnlyList<PlacementModel> models,
        string? selectedId)
    {
        Dictionary<string, PlacementModel> configured =
            models
                .Where(model =>
                    model.CameraPreparationMode ==
                        CameraPreparationMode.FastNoAlign &&
                    model.Target is not null &&
                    !PlacementSetupCatalog
                        .IsEmptyRouteOverride(model) &&
                    string.Equals(
                        model.Id,
                        PlacementSetupCatalog.IdFor(
                            model.Target),
                        StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    model => model.Id,
                    StringComparer.OrdinalIgnoreCase);

        _setupRows.Clear();
        foreach (PlacementSetupRoute route in
                 PlacementSetupCatalog.All)
        {
            PlacementSetupRoute? inherited =
                configured.ContainsKey(route.ModelId)
                    ? null
                    : PlacementSetupCatalog
                        .CandidatesFor(route.Target)
                        .Skip(1)
                        .FirstOrDefault(candidate =>
                            configured.ContainsKey(
                                candidate.ModelId));
            _setupRows.Add(
                new PlacementSetupRow(
                    route,
                    configured.GetValueOrDefault(
                        route.ModelId),
                    inherited?.Name));
        }

        BuildSetupList();
        PlacementSetupRow? selected =
            selectedId is not null
                ? _setupRows.FirstOrDefault(
                    row => string.Equals(
                        row.Route.ModelId,
                        selectedId,
                        StringComparison.OrdinalIgnoreCase))
                : _selectedSetupTarget is null
                    ? null
                    : _setupRows.FirstOrDefault(
                        row => row.Route.Target.Matches(
                            _selectedSetupTarget));
        SelectSetupNode(
            selected ?? _setupRows.FirstOrDefault());
    }

    private void BuildSetupList()
    {
        _setupRoots.Clear();
        PlacementSetupNode expeditions = AddGroup(
            "Expeditions",
            FindSetup(
                PlacementTargetMode.Expedition,
                PlacementSetupCatalog
                    .SharedExpeditionMapNumber));
        for (int map = 1; map <= 3; map++)
        {
            AddChild(
                expeditions,
                FindSetup(PlacementTargetMode.Expedition, map),
                $"Map {map}");
        }

        for (int map = 1; map <= 5; map++)
        {
            PlacementSetupNode storyMap = AddGroup(
                $"Story map {map} - {MapName(map)}",
                FindSetup(
                    PlacementTargetMode.Story,
                    map,
                    StoryRunKind.Act,
                    PlacementSetupCatalog
                        .SharedStoryActNumber));
            for (int act = 1; act <= 5; act++)
            {
                AddChild(
                    storyMap,
                    FindSetup(
                        PlacementTargetMode.Story,
                        map,
                        StoryRunKind.Act,
                        act),
                    $"Act {act}");
            }
            AddChild(
                storyMap,
                FindSetup(
                    PlacementTargetMode.Story,
                    map,
                    StoryRunKind.Infinite,
                    1),
                "Infinite");
            AddChild(
                storyMap,
                FindSetup(
                    PlacementTargetMode.Story,
                    map,
                    StoryRunKind.Mastery,
                    1),
                "Mastery");
            AddChild(
                storyMap,
                FindSetup(PlacementTargetMode.Challenge, map),
                "Challenge");
        }

        AddRootRoute(
            FindSetup(
                PlacementTargetMode.Raid,
                1,
                StoryRunKind.Act,
                1),
            "Raid Map 1 Act 1");
        AddRootRoute(
            FindSetup(
                PlacementTargetMode.Raid,
                1,
                StoryRunKind.Act,
                2),
            "Raid Map 1 Act 2");
        AddRootRoute(
            FindSetup(
                PlacementTargetMode.Raid,
                1,
                StoryRunKind.Act,
                3),
            "Raid Map 1 Act 3");

        AddRootRoute(
            FindSetup(
                PlacementTargetMode.Event,
                (int)EventModeId.VillainInvasion,
                StoryRunKind.Act,
                (int)EventAct.Act1,
                EventSpawnRoute.Angle1),
            "Event · Villain Invasion · Act 1 · Angle 1");
        AddRootRoute(
            FindSetup(
                PlacementTargetMode.Event,
                (int)EventModeId.VillainInvasion,
                StoryRunKind.Act,
                (int)EventAct.Act1,
                EventSpawnRoute.Angle2),
            "Event · Villain Invasion · Act 1 · Angle 2");
        for (int act = 2; act <= 4; act++)
        {
            AddRootRoute(
                FindSetup(
                    PlacementTargetMode.Event,
                    (int)EventModeId.VillainInvasion,
                    StoryRunKind.Act,
                    act),
                $"Event · Villain Invasion · Act {act}");
        }
        RebuildVisibleSetupNodes();
    }

    private PlacementSetupNode AddGroup(
        string name,
        PlacementSetupRow row)
    {
        PlacementSetupNode node = new(
            name,
            row,
            isGroup: true);
        _setupRoots.Add(node);
        return node;
    }

    private static void AddChild(
        PlacementSetupNode parent,
        PlacementSetupRow row,
        string name) =>
        parent.Children.Add(
            new PlacementSetupNode(
                name,
                row,
                isChild: true));

    private void AddRootRoute(
        PlacementSetupRow row,
        string name) =>
        _setupRoots.Add(
            new PlacementSetupNode(name, row));

    private void RebuildVisibleSetupNodes()
    {
        _setupNodes.Clear();
        foreach (PlacementSetupNode root in
                 _setupRoots)
        {
            _setupNodes.Add(root);
            if (!root.IsExpanded)
            {
                continue;
            }
            foreach (PlacementSetupNode child in
                     root.Children)
            {
                _setupNodes.Add(child);
            }
        }
    }

    private PlacementSetupRow FindSetup(
        PlacementTargetMode mode,
        int map,
        StoryRunKind runKind = StoryRunKind.Act,
        int act = 0,
        EventSpawnRoute spawnRoute =
            EventSpawnRoute.Angle1) =>
        _setupRows.First(row =>
            row.Route.Target.Mode == mode &&
            row.Route.Target.MapNumber == map &&
            (mode != PlacementTargetMode.Story ||
             row.Route.Target.StoryRunKind == runKind) &&
            row.Route.Target.ActNumber == act &&
            row.Route.Target.SpawnRoute == spawnRoute);

    private static string MapName(int map) => map switch
    {
        1 => "School Grounds",
        2 => "Flower Forest",
        3 => "Rose Kingdom",
        4 => "Fairy King Forest",
        5 => "King's Tomb",
        _ => $"Map {map}",
    };

    private void SelectSetupNode(
        PlacementSetupRow? setup)
    {
        PlacementSetupNode? parent = null;
        PlacementSetupNode? node = null;
        if (setup is not null)
        {
            foreach (PlacementSetupNode root in
                     _setupRoots)
            {
                if (ReferenceEquals(root.Row, setup))
                {
                    node = root;
                    break;
                }
                node = root.Children.FirstOrDefault(
                    candidate =>
                        ReferenceEquals(
                            candidate.Row,
                            setup));
                if (node is not null)
                {
                    parent = root;
                    break;
                }
            }
        }
        if (parent is not null)
        {
            parent.IsExpanded = true;
            RebuildVisibleSetupNodes();
        }
        _changingSetupSelection = true;
        try
        {
            FastSetupList.SelectedItem = node;
        }
        finally
        {
            _changingSetupSelection = false;
        }
        if (node is not null)
        {
            FastSetupList.ScrollIntoView(node);
            ApplySelectedSetupNode(node);
        }
    }

    private void FastSetupDisclosure_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement
            {
                DataContext:
                    PlacementSetupNode node,
            } ||
            !node.IsGroup)
        {
            return;
        }

        PlacementSetupNode? selected =
            FastSetupList.SelectedItem as
                PlacementSetupNode;
        node.IsExpanded = !node.IsExpanded;
        if (!node.IsExpanded &&
            selected is not null &&
            node.Children.Contains(selected))
        {
            selected = node;
        }
        RebuildVisibleSetupNodes();
        FastSetupList.SelectedItem =
            selected ?? node;
        FastSetupList.ScrollIntoView(
            selected ?? node);
        e.Handled = true;
    }

    private void ApplySetup(PlacementSetupRow setup)
    {
        using IDisposable suspension =
            SuspendPlacementAutoSave();
        _selectedSetupTarget = setup.Route.Target;
        if (setup.Model is not null)
        {
            ApplyModel(setup.Model);
            return;
        }

        _selectedModel = null;
        _steps.Clear();
        ApplyNewModelDefaults();
        FastStatusText.Text = string.Empty;
        FastDetailText.Text =
            setup.InheritedFrom is null
                ? "Not configured"
                : $"Uses {setup.InheritedFrom}";
    }

    private string? FindInheritedSetupName(
        PlacementSetupRoute route) =>
        PlacementSetupCatalog
            .CandidatesFor(route.Target)
            .Skip(1)
            .Select(candidate =>
                _setupRows.FirstOrDefault(row =>
                    string.Equals(
                        row.Route.ModelId,
                        candidate.ModelId,
                        StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(row =>
                row?.Model is not null &&
                !PlacementSetupCatalog
                    .IsEmptyRouteOverride(row.Model))
            ?.Route.Name;
}
