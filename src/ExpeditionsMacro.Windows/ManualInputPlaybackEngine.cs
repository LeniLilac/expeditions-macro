using System.Diagnostics;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Windows;

internal interface IManualInputPlaybackClock
{
    void Start();

    long ElapsedMicroseconds { get; }

    ValueTask WaitUntilAsync(
        long deadlineMicroseconds,
        CancellationToken cancellationToken);
}

internal interface IManualInputSink
{
    void Prepare(ManualInputRecording recording);

    void Send(ManualInputEvent input);

    void ReleaseHeldInputs();
}

internal sealed class ManualInputPlaybackEngine
{
    internal const long MaximumDriftMicroseconds =
        50_000;

    public async Task PlayAsync(
        ManualInputRecording recording,
        IManualInputPlaybackClock clock,
        IManualInputSink sink,
        Action? playbackStarting,
        Action<ManualInputPlaybackTiming>? timing,
        CancellationToken cancellationToken)
    {
        recording.Validate();
        Exception? playbackFailure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            sink.Prepare(recording);
            cancellationToken.ThrowIfCancellationRequested();
            playbackStarting?.Invoke();
            clock.Start();
            foreach (ManualInputEvent input in recording.Events)
            {
                await clock.WaitUntilAsync(
                        input.OffsetMicroseconds,
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                long actualMicroseconds =
                    clock.ElapsedMicroseconds;
                long driftMicroseconds =
                    actualMicroseconds -
                    input.OffsetMicroseconds;
                if (Math.Abs(driftMicroseconds) >
                    MaximumDriftMicroseconds)
                {
                    throw new TimeoutException(
                        "Manual playback could not maintain the required +/- 50 ms timing accuracy.");
                }
                sink.Send(input);
                actualMicroseconds =
                    clock.ElapsedMicroseconds;
                driftMicroseconds =
                    actualMicroseconds -
                    input.OffsetMicroseconds;
                timing?.Invoke(
                    new ManualInputPlaybackTiming(
                        input.OffsetMicroseconds,
                        actualMicroseconds,
                        input.Kind));
                if (Math.Abs(driftMicroseconds) >
                    MaximumDriftMicroseconds)
                {
                    throw new TimeoutException(
                        "Manual playback could not maintain the required +/- 50 ms timing accuracy.");
                }
            }

            await clock.WaitUntilAsync(
                    recording.DurationMicroseconds,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            playbackFailure = error;
            throw;
        }
        finally
        {
            try
            {
                sink.ReleaseHeldInputs();
            }
            catch when (playbackFailure is not null)
            {
                // Preserve the original failure after making every release attempt.
            }
        }
    }
}

internal sealed class StopwatchManualInputClock :
    IManualInputPlaybackClock
{
    private const long SpinWindowMicroseconds = 15_000;
    private readonly Stopwatch _stopwatch = new();
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException(
                "The manual playback clock has already started.");
        }
        _started = true;
        _stopwatch.Start();
    }

    public long ElapsedMicroseconds
    {
        get
        {
            if (!_started)
            {
                throw new InvalidOperationException(
                    "The manual playback clock has not started.");
            }
            long ticks = _stopwatch.ElapsedTicks;
            long seconds = ticks / Stopwatch.Frequency;
            long remainder = ticks % Stopwatch.Frequency;
            return checked(
                seconds * 1_000_000 +
                remainder * 1_000_000 /
                Stopwatch.Frequency);
        }
    }

    public async ValueTask WaitUntilAsync(
        long deadlineMicroseconds,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long remaining =
                deadlineMicroseconds -
                ElapsedMicroseconds;
            if (remaining <= 0)
            {
                return;
            }
            if (remaining > SpinWindowMicroseconds)
            {
                long delayMicroseconds =
                    remaining -
                    SpinWindowMicroseconds;
                await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            delayMicroseconds / 1_000d),
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            Thread.SpinWait(128);
        }
    }
}

public readonly record struct ManualInputPlaybackTiming(
    long ScheduledMicroseconds,
    long ActualMicroseconds,
    ManualInputEventKind Kind)
{
    public long DriftMicroseconds =>
        ActualMicroseconds -
        ScheduledMicroseconds;
}
