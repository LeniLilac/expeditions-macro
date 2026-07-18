using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Core.Models;

public sealed record CameraCalibrationSettings
{
    public string Name { get; init; } = "Camera model";

    public int CaptureCount { get; init; } = 12;

    public TimeSpan CaptureDuration { get; init; } = TimeSpan.FromSeconds(3);

    public int CoarseStepPixels { get; init; } = 16;

    public int FineStepPixels { get; init; } = 1;

    public int SettleMilliseconds { get; init; } = 350;

    public int MaximumSamples { get; init; } = 300;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Enter a camera model name.");
        if (CaptureCount is < 2 or > 100) throw new ArgumentOutOfRangeException(nameof(CaptureCount));
        if (CaptureDuration < TimeSpan.Zero || CaptureDuration > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(CaptureDuration));
        if (CoarseStepPixels is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(CoarseStepPixels));
        if (FineStepPixels is < 1 or > 25) throw new ArgumentOutOfRangeException(nameof(FineStepPixels));
        if (SettleMilliseconds is < 25 or > 5000) throw new ArgumentOutOfRangeException(nameof(SettleMilliseconds));
        if (MaximumSamples is < 12 or > 2000) throw new ArgumentOutOfRangeException(nameof(MaximumSamples));
    }
}

public sealed record CameraModelManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ScreenRegion Region { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required double BaselineScore { get; init; }

    public required double SuccessThreshold { get; init; }

    public required int CoarseStepPixels { get; init; }

    public required int FineStepPixels { get; init; }

    public required int FullYawPixels { get; init; }

    public required int SettleMilliseconds { get; init; }

    public required int AtlasSampleCount { get; init; }

    public required IReadOnlyList<double> ScanScores { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported camera model format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Camera model identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0 || !Region.FitsWithin(ClientWidth, ClientHeight)) throw new InvalidDataException("Camera model region is invalid.");
        if (FullYawPixels <= 0 || AtlasSampleCount < 3) throw new InvalidDataException("Camera model does not contain a complete yaw atlas.");
        if (SuccessThreshold is < 0 or > 1 || BaselineScore is < 0 or > 1) throw new InvalidDataException("Camera model scores are invalid.");
    }
}

public sealed record CameraModel(
    CameraModelManifest Manifest,
    ImageFrame Reference,
    ImageFrame GoalOverlay,
    IReadOnlyList<ImageFrame> YawAtlas);
