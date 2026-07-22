using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Core.Models;

public sealed record CameraSpawnShortcut
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string CameraModelId { get; init; }

    public required DateTimeOffset CameraModelCreatedAt { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required int FingerprintWidth { get; init; }

    public required int FingerprintHeight { get; init; }

    public required byte[] FingerprintPixels { get; init; }

    public required int SpawnAtlasIndex { get; init; }

    public required int DirectDragPixels { get; init; }

    public required int MousePixelsPerArrowStep { get; init; }

    public int MatchingObservations { get; init; } = 1;

    public int VerifiedUses { get; init; }

    public int ConsecutiveFailures { get; init; }

    public DateTimeOffset? LastVerifiedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public ImageFrame CreateFingerprint()
    {
        Validate();
        return new ImageFrame(
            FingerprintWidth,
            FingerprintHeight,
            PixelFormat.Gray8,
            (byte[])FingerprintPixels.Clone(),
            takeOwnership: true);
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported camera shortcut format.");
        if (string.IsNullOrWhiteSpace(CameraModelId)) throw new InvalidDataException("Camera shortcut model identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0) throw new InvalidDataException("Camera shortcut client size is invalid.");
        if (FingerprintWidth <= 0 || FingerprintHeight <= 0 || FingerprintPixels is null || FingerprintPixels.Length != FingerprintWidth * FingerprintHeight)
        {
            throw new InvalidDataException("Camera shortcut fingerprint is invalid.");
        }
        if (SpawnAtlasIndex < 0 || Math.Abs(DirectDragPixels) > 20000 || MousePixelsPerArrowStep is < -500 or > 500 or 0)
        {
            throw new InvalidDataException("Camera shortcut movement is invalid.");
        }
        if (MatchingObservations < 1 || VerifiedUses < 0 || ConsecutiveFailures < 0)
        {
            throw new InvalidDataException("Camera shortcut learning counters are invalid.");
        }
    }
}
