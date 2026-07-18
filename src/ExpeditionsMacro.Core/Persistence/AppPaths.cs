namespace ExpeditionsMacro.Core.Persistence;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExpeditionsMacro");
        CameraModels = Path.Combine(Root, "camera-models");
        PlacementModels = Path.Combine(Root, "placement-models");
        Presets = Path.Combine(Root, "presets");
        DetectorPacks = Path.Combine(Root, "detector-packs");
        Logs = Path.Combine(Root, "logs");
        SettingsFile = Path.Combine(Root, "settings.json");
    }

    public string Root { get; }

    public string CameraModels { get; }

    public string PlacementModels { get; }

    public string Presets { get; }

    public string DetectorPacks { get; }

    public string Logs { get; }

    public string SettingsFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CameraModels);
        Directory.CreateDirectory(PlacementModels);
        Directory.CreateDirectory(Presets);
        Directory.CreateDirectory(DetectorPacks);
        Directory.CreateDirectory(Logs);
    }
}
