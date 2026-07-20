using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Abstractions;

public interface ICameraModelRepository
{
    Task<IReadOnlyList<CameraModelManifest>> ListAsync(CancellationToken cancellationToken = default);

    Task<CameraModel?> LoadAsync(string id, CancellationToken cancellationToken = default);

    Task SaveAsync(CameraModel model, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IDetectorPack
{
    DetectorPackManifest Manifest { get; }

    IReadOnlyDictionary<string, double> ScoreStates(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

    string? Classify(IReadOnlyDictionary<string, double> scores);

    string? RecoveryState(ExpeditionsMacro.Core.Imaging.ImageFrame clientImage);

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

    Task InstallAsync(Stream package, CancellationToken cancellationToken = default);

    Task RollbackAsync(string packId, CancellationToken cancellationToken = default);
}
