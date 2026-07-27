using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Abstractions;

public sealed record ManualInputCaptureOptions
{
    public required string RecordingId { get; init; }

    public required string RecordingName { get; init; }

    public IReadOnlyCollection<int> IgnoredVirtualKeys { get; init; } =
        [];

    public void Validate()
    {
        ManualInputRecording.ValidateId(RecordingId);
        if (string.IsNullOrWhiteSpace(RecordingName) ||
            RecordingName.Trim().Length > 120)
        {
            throw new ArgumentException(
                "Manual recording name must be between 1 and 120 characters.",
                nameof(RecordingName));
        }
        if (IgnoredVirtualKeys is null ||
            IgnoredVirtualKeys.Any(key => key is < 1 or > 0xFF))
        {
            throw new ArgumentOutOfRangeException(
                nameof(IgnoredVirtualKeys),
                "Ignored recording hotkeys must be valid Windows virtual keys.");
        }
    }
}

public interface IManualInputRecorder
{
    Task<ManualInputRecording> RecordAsync(
        RobloxWindow window,
        ManualInputCaptureOptions options,
        CancellationToken stopToken);
}

public interface IManualInputPlayback
{
    Task PlayAsync(
        RobloxWindow window,
        ManualInputRecording recording,
        CancellationToken cancellationToken,
        Action? playbackStarting = null);
}
