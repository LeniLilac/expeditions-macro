namespace ExpeditionsMacro.Core.Persistence;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExpeditionsMacro");
        CameraModels = Path.Combine(Root, "camera-models");
        CameraShortcuts = Path.Combine(Root, "camera-shortcuts");
        PlacementModels = Path.Combine(Root, "placement-models");
        Presets = Path.Combine(Root, "presets");
        ChallengePresets = Path.Combine(Root, "challenge-presets");
        StoryPresets = Path.Combine(Root, "story-presets");
        RaidPresets = Path.Combine(Root, "raid-presets");
        MacroPlans = Path.Combine(Root, "macro-plans");
        DetectorPacks = Path.Combine(Root, "detector-packs");
        Diagnostics = Path.Combine(Root, "diagnostics");
        Logs = Path.Combine(Root, "logs");
        SettingsFile = Path.Combine(Root, "settings.json");
    }

    public string Root { get; }

    public string CameraModels { get; }

    public string CameraShortcuts { get; }

    public string PlacementModels { get; }

    public string Presets { get; }

    public string ChallengePresets { get; }

    public string StoryPresets { get; }

    public string RaidPresets { get; }

    public string MacroPlans { get; }

    public string DetectorPacks { get; }

    public string Diagnostics { get; }

    public string Logs { get; }

    public string SettingsFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CameraModels);
        Directory.CreateDirectory(CameraShortcuts);
        Directory.CreateDirectory(PlacementModels);
        Directory.CreateDirectory(Presets);
        Directory.CreateDirectory(ChallengePresets);
        Directory.CreateDirectory(StoryPresets);
        Directory.CreateDirectory(RaidPresets);
        Directory.CreateDirectory(MacroPlans);
        Directory.CreateDirectory(DetectorPacks);
        Directory.CreateDirectory(Diagnostics);
        Directory.CreateDirectory(Logs);
    }
}
