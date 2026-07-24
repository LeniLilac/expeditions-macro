using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Automation.Diagnostics;

public enum DebugStepMode
{
    Continuous,
    BeforeActions,
    EveryDetectionAndAction,
}

public enum DebugCheckpointKind
{
    Detection,
    Action,
    Status,
}

public sealed record DebugCheckpoint(
    long Sequence,
    DateTimeOffset TimestampUtc,
    DebugCheckpointKind Kind,
    string Title,
    string Detail,
    string? State,
    double? Confidence,
    ImageFrame? Frame);

public sealed class DebugCheckpointController
{
    private readonly object _gate = new();
    private CancellationToken _operationToken;
    private TaskCompletionSource<bool>? _continueSignal;
    private ImageFrame? _latestFrame;
    private DebugStepMode _mode;
    private bool _active;
    private long _sequence;

    public event Action<DebugCheckpoint>? CheckpointAdded;

    public event EventHandler? StateChanged;

    public bool IsActive
    {
        get
        {
            lock (_gate) return _active;
        }
    }

    public bool IsWaiting
    {
        get
        {
            lock (_gate) return _continueSignal is not null;
        }
    }

    public DebugStepMode Mode
    {
        get
        {
            lock (_gate) return _mode;
        }
    }

    public void Begin(DebugStepMode mode, CancellationToken operationToken)
    {
        lock (_gate)
        {
            if (_active) throw new InvalidOperationException("A debug checkpoint session is already active.");
            _active = true;
            _mode = mode;
            _operationToken = operationToken;
            _sequence = 0;
            _latestFrame = null;
            _continueSignal = null;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Complete()
    {
        TaskCompletionSource<bool>? signal;
        lock (_gate)
        {
            signal = _continueSignal;
            _continueSignal = null;
            _latestFrame = null;
            _active = false;
            _mode = DebugStepMode.Continuous;
            _operationToken = default;
        }
        signal?.TrySetResult(true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordFrame(ImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_gate)
        {
            if (_active) _latestFrame = frame.Clone();
        }
    }

    public void RecordDetection(VisionDetectionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        DebugStepMode mode;
        lock (_gate)
        {
            if (!_active) return;
            mode = _mode;
        }

        PublishAsync(
                DebugCheckpointKind.Detection,
                $"{trace.Detector}: {trace.State}",
                trace.Confidence is double confidence
                    ? $"Detected at {confidence:P1} confidence."
                    : "Detector observation recorded.",
                trace.State,
                trace.Confidence,
                pause: mode == DebugStepMode.EveryDetectionAndAction)
            .GetAwaiter()
            .GetResult();
    }

    public Task BeforeActionAsync(
        string action,
        string detail,
        CancellationToken cancellationToken) =>
        PublishAsync(
            DebugCheckpointKind.Action,
            action,
            detail,
            state: null,
            confidence: null,
            pause: Mode is not DebugStepMode.Continuous,
            cancellationToken,
            allowAfterOperationCancellation:
                !cancellationToken.CanBeCanceled);

    public void RecordStatus(
        string title,
        string detail,
        string? state = null,
        double? confidence = null) =>
        PublishAsync(
                DebugCheckpointKind.Status,
                title,
                detail,
                state,
                confidence,
                pause: false)
            .GetAwaiter()
            .GetResult();

    public void Step()
    {
        TaskCompletionSource<bool>? signal;
        lock (_gate)
        {
            signal = _continueSignal;
            _continueSignal = null;
        }
        signal?.TrySetResult(true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PauseAtNext(DebugStepMode mode)
    {
        if (mode == DebugStepMode.Continuous)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                "Choose an action or detection checkpoint mode.");
        }
        lock (_gate)
        {
            if (!_active) return;
            _mode = mode;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        TaskCompletionSource<bool>? signal;
        lock (_gate)
        {
            if (!_active) return;
            _mode = DebugStepMode.Continuous;
            signal = _continueSignal;
            _continueSignal = null;
        }
        signal?.TrySetResult(true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task PublishAsync(
        DebugCheckpointKind kind,
        string title,
        string detail,
        string? state,
        double? confidence,
        bool pause,
        CancellationToken cancellationToken = default,
        bool allowAfterOperationCancellation = false)
    {
        DebugCheckpoint? checkpoint;
        TaskCompletionSource<bool>? signal = null;
        CancellationToken operationToken;
        lock (_gate)
        {
            if (!_active) return;
            operationToken = _operationToken;
            pause &= !operationToken.IsCancellationRequested;
            checkpoint = new DebugCheckpoint(
                ++_sequence,
                DateTimeOffset.UtcNow,
                kind,
                title,
                detail,
                state,
                confidence,
                _latestFrame?.Clone());
            if (pause)
            {
                if (_continueSignal is not null)
                {
                    throw new InvalidOperationException(
                        "A debug checkpoint is already waiting for user input.");
                }
                signal = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _continueSignal = signal;
            }
        }

        CheckpointAdded?.Invoke(checkpoint);
        if (signal is null) return;
        StateChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            if (allowAfterOperationCancellation)
            {
                using CancellationTokenRegistration registration =
                    operationToken.Register(
                        () => signal.TrySetResult(true));
                await signal.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                using CancellationTokenSource linked =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        operationToken,
                        cancellationToken);
                await signal.Task
                    .WaitAsync(linked.Token)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_continueSignal, signal))
                {
                    _continueSignal = null;
                }
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
