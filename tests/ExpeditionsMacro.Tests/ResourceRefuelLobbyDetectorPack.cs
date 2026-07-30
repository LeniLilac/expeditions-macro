using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Infrastructure;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Tests;

internal sealed class LobbyDetectorPack : IDetectorPack
{
    public DetectorPackManifest Manifest =>
        throw new NotSupportedException();

    public IReadOnlyDictionary<string, double> ScoreStates(
        ImageFrame clientImage) =>
        new Dictionary<string, double>();

    public string? Classify(
        IReadOnlyDictionary<string, double> scores) =>
        null;

    public string? RecoveryState(ImageFrame clientImage) =>
        AreasScreenDetector.Detect(clientImage).State !=
            AreasScreenState.None ||
        ChallengeScreenDetector
            .Detect(clientImage).State !=
            ChallengeScreenState.None
            ? "unknown"
            : "lobby";

    public string? CurrentNodeType(
        ImageFrame clientImage) =>
        null;

    public int? SelectedMap(
        ImageFrame clientImage) =>
        null;

    public int? SelectedDifficulty(
        ImageFrame clientImage) =>
        null;

    public IReadOnlyList<int> RemainingUnitKeys(
        ImageFrame clientImage,
        IReadOnlySet<int> unitKeys) =>
        [];

    public (int X, int Y) ActionFor(
        string state,
        ImageFrame? clientImage = null) =>
        throw new NotSupportedException();
}
