using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Core.Models;

public sealed record CameraCalibrationSettings
{
    public string Name { get; init; } = "Camera model";

    public int CaptureCount { get; init; } = 12;

    public TimeSpan CaptureDuration { get; init; } = TimeSpan.FromSeconds(3);

    public int ArrowHoldMilliseconds { get; init; } = 30;

    public int FineStepPixels { get; init; } = 1;

    public int FineSearchPixels { get; init; } = 16;

    public int SettleMilliseconds { get; init; } = 200;

    public int MaximumSamples { get; init; } = 300;

    public int ZoomTicks { get; init; } = 30;

    public int PitchDragPixels { get; init; } = 1800;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Enter a camera model name.");
        if (CaptureCount is < 2 or > 100) throw new ArgumentOutOfRangeException(nameof(CaptureCount));
        if (CaptureDuration < TimeSpan.Zero || CaptureDuration > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(CaptureDuration));
        if (ArrowHoldMilliseconds is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(ArrowHoldMilliseconds));
        if (FineStepPixels is < 1 or > 25) throw new ArgumentOutOfRangeException(nameof(FineStepPixels));
        if (FineSearchPixels is < 4 or > 100) throw new ArgumentOutOfRangeException(nameof(FineSearchPixels));
        if (SettleMilliseconds is < 25 or > 5000) throw new ArgumentOutOfRangeException(nameof(SettleMilliseconds));
        if (MaximumSamples is < 12 or > 2000) throw new ArgumentOutOfRangeException(nameof(MaximumSamples));
        if (ZoomTicks is < 5 or > 80) throw new ArgumentOutOfRangeException(nameof(ZoomTicks));
        if (PitchDragPixels is < 300 or > 5000) throw new ArgumentOutOfRangeException(nameof(PitchDragPixels));
    }
}

public sealed record CameraModelManifest
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ScreenRegion> Regions { get; init; }

    public required int ClientWidth { get; init; }

    public required int ClientHeight { get; init; }

    public required double BaselineScore { get; init; }

    public required double SuccessThreshold { get; init; }

    public required int ArrowHoldMilliseconds { get; init; }

    public required int FineStepPixels { get; init; }

    public required int FineSearchPixels { get; init; }

    public required IReadOnlyList<int> FineYawOffsets { get; init; }

    public required int FullYawSteps { get; init; }

    public required int SettleMilliseconds { get; init; }

    // Schema 3 models created before camera-pose normalization omitted these
    // values. Property defaults keep those models loadable while new models
    // persist the exact preparation settings used during setup.
    public int ZoomTicks { get; init; } = 30;

    public int PitchDragPixels { get; init; } = 1800;

    public required int AtlasSampleCount { get; init; }

    public required IReadOnlyList<double> ScanScores { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported camera model format.");
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Camera model identity is missing.");
        if (ClientWidth <= 0 || ClientHeight <= 0 || Regions is null || Regions.Count is < 2 or > 8 || Regions.Any(region => !region.FitsWithin(ClientWidth, ClientHeight)))
        {
            throw new InvalidDataException("Camera model regions are invalid.");
        }
        if (Regions.Distinct().Count() != Regions.Count) throw new InvalidDataException("Camera model regions must be unique.");
        if (ArrowHoldMilliseconds is < 1 or > 1000 || FineStepPixels is < 1 or > 25 || FineSearchPixels is < 4 or > 100)
        {
            throw new InvalidDataException("Camera model movement settings are invalid.");
        }
        if (ZoomTicks is < 5 or > 80 || PitchDragPixels is < 300 or > 5000)
        {
            throw new InvalidDataException("Camera model preparation settings are invalid.");
        }
        if (FineYawOffsets is null
            || FineYawOffsets.Count < 3
            || FineYawOffsets.Distinct().Count() != FineYawOffsets.Count
            || !FineYawOffsets.Contains(0)
            || FineYawOffsets.Any(offset => Math.Abs(offset) > Math.Max(FineSearchPixels, FineStepPixels * 2))
            || !FineYawOffsets.SequenceEqual(FineYawOffsets.Order()))
        {
            throw new InvalidDataException("Camera model fine yaw atlas is invalid.");
        }
        if (FullYawSteps < 3 || AtlasSampleCount != FullYawSteps + 1 || ScanScores.Count != AtlasSampleCount)
        {
            throw new InvalidDataException("Camera model does not contain a complete yaw atlas.");
        }
        if (SuccessThreshold is < 0 or > 1 || BaselineScore is < 0 or > 1) throw new InvalidDataException("Camera model scores are invalid.");
    }
}

public sealed record CameraModel(
    CameraModelManifest Manifest,
    ImageFrame Reference,
    ImageFrame GoalOverlay,
    IReadOnlyList<ImageFrame> FineYawAtlas,
    IReadOnlyList<ImageFrame> YawAtlas);
