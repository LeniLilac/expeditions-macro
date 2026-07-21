using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Vision.Packs;

public static class DetectorPackCapabilities
{
    internal static IReadOnlyDictionary<ChallengeMapId, string> ChallengeMapReferences { get; } =
        new Dictionary<ChallengeMapId, string>
        {
            [ChallengeMapId.SchoolGrounds] = "challenge-maps/school-grounds.png",
            [ChallengeMapId.FlowerForest] = "challenge-maps/flower-forest.png",
            [ChallengeMapId.RoseKingdom] = "challenge-maps/rose-kingdom.png",
            [ChallengeMapId.FairyKingForest] = "challenge-maps/fairy-king-forest.png",
            [ChallengeMapId.KingsTomb] = "challenge-maps/kings-tomb.png",
        };

    public static bool SupportsChallengeMaps(DetectorPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        HashSet<string> files = manifest.Files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ChallengeMapReferences.Values.All(files.Contains);
    }

    public static bool SupportsChallengeMaps(string directory, DetectorPackManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!SupportsChallengeMaps(manifest)) return false;
        return ChallengeMapReferences.Values.All(relative => File.Exists(Resolve(directory, relative)));
    }

    public static string ChallengeMapsUnavailableMessage(DetectorPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return $"Detector pack '{manifest.PackId}' version {manifest.Version} does not include the Challenge map references. " +
            "Update the detector pack in Settings or reinstall Expeditions Macro before running Challenge automation.";
    }

    private static string Resolve(string directory, string relative) =>
        Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
}
