using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Camera;

public sealed class FastNoAlignPreparationSession
{
    private readonly CameraPosePreparationService _pose;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateLock = new();
    private int _preparedProcessId;
    private bool _prepared;

    public FastNoAlignPreparationSession(
        CameraPosePreparationService pose)
    {
        _pose = pose;
    }

    public async Task<bool> EnsurePreparedAsync(
        RobloxWindow window,
        int zoomTicks,
        int pitchDragPixels,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (IsPrepared(window))
            {
                progress?.Report(new MacroProgress(
                    "Fast no align",
                    20,
                    "The camera pose was preserved after the previous match; skipping repeated preparation.",
                    "fast_no_align_reused"));
                return false;
            }

            await _pose.PrepareWithoutYawAsync(
                window,
                zoomTicks,
                pitchDragPixels,
                progress: progress,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            lock (_stateLock)
            {
                _preparedProcessId = window.ProcessId;
                _prepared = true;
            }
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ObserveLobby(RobloxWindow window)
    {
        lock (_stateLock)
        {
            if (_preparedProcessId == 0 ||
                window.ProcessId == 0 ||
                _preparedProcessId == window.ProcessId)
            {
                _prepared = false;
            }
        }
    }

    public void Invalidate()
    {
        lock (_stateLock)
        {
            _prepared = false;
            _preparedProcessId = 0;
        }
    }

    internal bool IsPrepared(RobloxWindow window)
    {
        lock (_stateLock)
        {
            return _prepared &&
                window.ProcessId > 0 &&
                _preparedProcessId == window.ProcessId;
        }
    }
}
