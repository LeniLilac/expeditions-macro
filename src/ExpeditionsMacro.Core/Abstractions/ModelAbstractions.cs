using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Abstractions;

public interface ICameraModelRepository
{
    Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default);

    Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface ICameraSpawnShortcutRepository
{
    Task<CameraSpawnShortcut?> LoadAsync(string cameraModelId, CancellationToken cancellationToken = default);

    Task SaveAsync(CameraSpawnShortcut shortcut, CancellationToken cancellationToken = default);

    Task DeleteAsync(string cameraModelId, CancellationToken cancellationToken = default);
}

public interface IDetectorPack
{
    DetectorPackManifest Manifest { get; }

    bool SupportsChallengeMaps => false;

    IReadOnlyDictionary<string, double> ScoreStates(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    IReadOnlyDictionary<string, double> ScoreStates(
        ExpeditionsMacro.Core.Imaging.ImageFrame clientImage,
        IReadOnlyCollection<string> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        IReadOnlyDictionary<string, double> scores =
            ScoreStates(clientImage);
        return states
            .Where(scores.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                state => state,
                state => scores[state],
                StringComparer.OrdinalIgnoreCase);
    }

    string? Classify(IReadOnlyDictionary<string, double> scores);

    string? RecoveryState(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    string? RootRecoveryState(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage)
    {
        string? state = RecoveryState(clientImage);
        return state is "afk" or "disconnect" or "lobby"
            ? state
            : null;
    }

    string? CurrentNodeType(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    int? SelectedMap(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    int? SelectedDifficulty(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    IReadOnlyList<int> RemainingUnitKeys(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage, IReadOnlySet<int> unitKeys);

    ChallengeMapId? ChallengeMapForType(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage, ChallengeType type) => null;

    (int X, int Y) ActionFor(string state, ExpeditionsMacro.Core.Imaging.ImageFrame? clientImage = null);
}

public interface IDetectorPackRepository
{
    Task<IReadOnlyList<DetectorPackManifest>> ListAsync(CancellationToken cancellationToken = default);

    Task<IDetectorPack?> LoadAsync(string packId, CancellationToken cancellationToken = default);
}
