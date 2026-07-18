using ExpeditionsMacro.Core.Geometry;

namespace ExpeditionsMacro.Core.Models;

public sealed record DetectorRegionReference(ScreenRegion Region, string File);

public sealed record DetectorStateDefinition
{
    public required string Name { get; init; }

    public required IReadOnlyList<DetectorRegionReference> Regions { get; init; }

    public required int ActionX { get; init; }

    public required int ActionY { get; init; }

    public required double Threshold { get; init; }
}

public sealed record SelectionDetectorDefinition
{
    public required int Value { get; init; }

    public required ScreenRegion Region { get; init; }

    public required string File { get; init; }

    public required double MinimumScore { get; init; }
}

public sealed record DetectorPackFile(string Path, string Sha256, long Bytes);

public sealed record DetectorPackManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string PackId { get; init; }

    public required string Version { get; init; }

    public required string GameId { get; init; }

    public required string ModeId { get; init; }

    public required string MinimumAppVersion { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required IReadOnlyList<DetectorStateDefinition> States { get; init; }

    public required IReadOnlyList<SelectionDetectorDefinition> MapSelections { get; init; }

    public required IReadOnlyList<SelectionDetectorDefinition> DifficultySelections { get; init; }

    public IReadOnlyDictionary<int, double>? DifficultyHuePrototypes { get; init; }

    public ScreenRegion? DifficultyHueRegion { get; init; }

    public required IReadOnlyDictionary<string, double> NodeHuePrototypes { get; init; }

    public required ScreenRegion NodeHueRegion { get; init; }

    public required string EmptyHotbarReferenceFile { get; init; }

    public required IReadOnlyDictionary<string, int[]> ExtraActions { get; init; }

    public required IReadOnlyList<DetectorPackFile> Files { get; init; }

    public required DateTimeOffset BuiltAt { get; init; }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported detector pack format.");
        if (string.IsNullOrWhiteSpace(PackId) || string.IsNullOrWhiteSpace(Version)) throw new InvalidDataException("Detector pack identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0) throw new InvalidDataException("Detector pack client size is invalid.");
        if (States.Count == 0 || Files.Count == 0) throw new InvalidDataException("Detector pack is incomplete.");
        if ((DifficultyHuePrototypes is null) != (DifficultyHueRegion is null)) throw new InvalidDataException("Difficulty color detection is incomplete.");
        if (DifficultyHuePrototypes is not null && DifficultyHuePrototypes.Count != DifficultySelections.Count) throw new InvalidDataException("Difficulty color detection does not cover every selection.");
    }
}
