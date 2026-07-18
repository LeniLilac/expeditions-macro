using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.Vision.Packs;

public sealed record DatasetStateSpec(
    string Name,
    IReadOnlyList<string> Datasets,
    IReadOnlyList<ScreenRegion> Regions,
    int ActionX,
    int ActionY,
    double Threshold);

public static class AnimeExpeditionsDetectorSpec
{
    public const string PackId = "anime-expeditions-expeditions";
    public const string BundledPackVersion = "1.0.1";
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    public static ScreenRegion NodeBarRegion { get; } = new(236, 63, 62, 9);

    // Covers the colored center control at both the original position and the
    // six-pixel-up layout introduced by the July 2026 game UI revision.
    public static ScreenRegion DifficultyHueRegion { get; } = new(672, 376, 80, 59);

    // OpenCV hue values use a circular 0..179 scale.
    public static IReadOnlyDictionary<int, double> DifficultyHuePrototypes { get; } = new Dictionary<int, double>
    {
        [1] = 45,
        [2] = 0,
        [3] = 140,
    };

    public static IReadOnlyList<DatasetStateSpec> States { get; } =
    [
        new("disconnect", ["Roblox_Disconnect"], [new(204, 181, 400, 251), new(224, 374, 360, 38)], 496, 394, 0.86),
        new("lobby", ["Lobby_UI", "Lobby_UI2"], [new(10, 236, 80, 31), new(10, 269, 80, 40), new(10, 310, 80, 40), new(10, 352, 80, 40), new(10, 396, 80, 30)], 70, 389, 0.80),
        new("play", ["Play_UI"], [new(608, 158, 176, 90), new(617, 162, 92, 23)], 690, 205, 0.82),
        new("map_select", ["Expedition_Map_Select_Map1", "Expedition_Map_Select_Map2", "Expedition_Map_Select_Map3", "Expedition_Map_Select_Difficultly1", "Expedition_Map_Select_Difficultly2", "Expedition_Map_Select_Difficultly3"], [new(669, 518, 119, 33), new(723, 18, 72, 16)], 728, 535, 0.82),
        new("map_preview", ["Expedition_Map_Preview_Map1"], [new(425, 354, 324, 33)], 479, 370, 0.82),
        new("victory", ["Expedition_Victory_UI"], [new(125, 150, 95, 27)], 225, 438, 0.90),
        new("defeat", ["Expedition_Defeat_UI"], [new(125, 150, 95, 27)], 225, 438, 0.90),
        new("extract_confirm", ["Expedition_Checkpoint_Extract_Confirm"], [new(283, 217, 241, 176)], 345, 378, 0.82),
        new("reward", ["Expedition_Reward_Select", "Expedition_Reward_Select2", "Expedition_Reward_Select3", "Expedition_Reward_Select4"], [new(136, 383, 116, 19), new(345, 383, 117, 19), new(554, 383, 118, 19)], 194, 391, 0.76),
        new("confirm", ["Expedition_Continue_Button_Confirm"], [new(280, 255, 248, 93)], 345, 331, 0.80),
        new("checkpoint", ["Expedition_Checkpoint", "Expedition_Checkpoint_Node"], [new(310, 482, 185, 36)], 448, 500, 0.78),
        new("start", ["Expedition_Map1_Prestart", "Expedition_Midgame_Start"], [new(315, 100, 180, 92)], 404, 177, 0.82),
        new("continue", ["Expedition_Continue_Button"], [new(355, 482, 97, 36)], 404, 500, 0.88),
    ];

    public static IReadOnlyDictionary<int, string> MapDatasets { get; } = new Dictionary<int, string>
    {
        [1] = "Expedition_Map_Select_Map1",
        [2] = "Expedition_Map_Select_Map2",
        [3] = "Expedition_Map_Select_Map3",
    };

    public static IReadOnlyDictionary<int, string> DifficultyDatasets { get; } = new Dictionary<int, string>
    {
        [1] = "Expedition_Map_Select_Difficultly1",
        [2] = "Expedition_Map_Select_Difficultly2",
        [3] = "Expedition_Map_Select_Difficultly3",
    };

    public static IReadOnlyDictionary<string, string> NodeDatasets { get; } = new Dictionary<string, string>
    {
        ["defense"] = "Expedition_Defense_Node",
        ["assault"] = "Expedition_Assault_Node",
        ["elite"] = "Expedition_Elite_Node",
        ["boss"] = "Expedition_Boss_Node",
        ["checkpoint"] = "Expedition_Checkpoint_Node",
    };

    public static IReadOnlyDictionary<string, int[]> ExtraActions { get; } = new Dictionary<string, int[]>
    {
        ["extract"] = [360, 500],
        ["map_1"] = [98, 230],
        ["map_2"] = [98, 282],
        ["map_3"] = [98, 333],
        ["difficulty_minus"] = [657, 406],
        ["difficulty_plus"] = [768, 406],
        ["select_stage"] = [728, 535],
    };
}
