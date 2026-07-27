using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class ManualInputRouteService
{
    private readonly ManualInputRecordingRepository _recordings;
    private readonly IManualInputPlayback _playback;

    public ManualInputRouteService(
        ManualInputRecordingRepository recordings,
        IManualInputPlayback playback)
    {
        _recordings = recordings;
        _playback = playback;
    }

    public static bool IsConfigured(
        PlacementModel placement) =>
        !string.IsNullOrWhiteSpace(
            placement.ManualInputRecordingId);

    public async Task PlayAsync(
        RobloxWindow window,
        PlacementModel placement,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ManualInputRecording recording =
            await ResolveAsync(
                    placement,
                    cancellationToken)
                .ConfigureAwait(false);
        await PlayAsync(
                window,
                recording,
                progress,
                playbackStarting: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ManualInputRecording> ResolveAsync(
        PlacementModel placement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        placement.Validate();
        string recordingId =
            placement.ManualInputRecordingId ??
            throw new InvalidOperationException(
                "This placement setup does not select a manual recording.");
        return await _recordings.LoadAsync(
                    recordingId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidDataException(
                $"Manual recording '{recordingId}' no longer exists. Choose another recording in Placement Setup.");
    }

    public async Task PlayAsync(
        RobloxWindow window,
        ManualInputRecording recording,
        IProgress<MacroProgress>? progress = null,
        Action? playbackStarting = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        recording.Validate();
        progress?.Report(
            new MacroProgress(
                "Manual recording",
                48,
                $"Playing {recording.Name}."));
        await _playback.PlayAsync(
                window,
                recording,
                cancellationToken,
                playbackStarting)
            .ConfigureAwait(false);
        progress?.Report(
            new MacroProgress(
                "Manual recording",
                62,
                $"{recording.Name} finished. Waiting for Victory or Defeat."));
    }
}
