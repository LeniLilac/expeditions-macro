using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Windows;

internal sealed class ManualInputCaptureTimeline
{
    private const long NativeTimestampToleranceMicroseconds = 1_000;
    private readonly object _gate = new();
    private readonly List<CapturedInput> _inputs = [];
    private long _nextSequence;
    private uint _startNativeMilliseconds;
    private bool _started;

    public void Start(uint nativeMilliseconds)
    {
        lock (_gate)
        {
            if (_started)
            {
                throw new InvalidOperationException(
                    "The manual input capture timeline has already started.");
            }
            _startNativeMilliseconds = nativeMilliseconds;
            _started = true;
        }
    }

    public void Add(
        ManualInputEvent input,
        uint nativeMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(input);
        long sequence =
            Interlocked.Increment(ref _nextSequence);
        lock (_gate)
        {
            if (!_started)
            {
                throw new InvalidOperationException(
                    "The manual input capture timeline has not started.");
            }
            _inputs.Add(
                new CapturedInput(
                    input,
                    nativeMilliseconds,
                    sequence));
        }
    }

    public ManualInputEvent[] BuildSnapshot(
        int initialClientX,
        int initialClientY,
        long durationMicroseconds)
    {
        if (durationMicroseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMicroseconds));
        }

        CapturedInput[] captured;
        uint startNativeMilliseconds;
        lock (_gate)
        {
            if (!_started)
            {
                throw new InvalidOperationException(
                    "The manual input capture timeline has not started.");
            }
            captured = _inputs.ToArray();
            startNativeMilliseconds =
                _startNativeMilliseconds;
        }

        ManualInputEvent[] ordered =
            captured
                .Select(input =>
                    Project(
                        input,
                        startNativeMilliseconds,
                        durationMicroseconds))
                .OrderBy(input =>
                    input.ElapsedNativeMilliseconds)
                .ThenBy(input =>
                    input.Sequence)
                .Select(input =>
                    input.Input)
                .ToArray();
        return NormalizePointerPath(
            ordered,
            initialClientX,
            initialClientY);
    }

    private static ProjectedInput Project(
        CapturedInput captured,
        uint startNativeMilliseconds,
        long durationMicroseconds)
    {
        uint elapsedNativeMilliseconds =
            ElapsedMilliseconds(
                startNativeMilliseconds,
                captured.NativeMilliseconds);
        long offsetMicroseconds = checked(
            (long)elapsedNativeMilliseconds *
            1_000);
        if (offsetMicroseconds >
            durationMicroseconds)
        {
            long excessMicroseconds =
                offsetMicroseconds -
                durationMicroseconds;
            if (excessMicroseconds >
                NativeTimestampToleranceMicroseconds)
            {
                throw new InvalidDataException(
                    "Windows reported manual input outside the recording timeline.");
            }
            offsetMicroseconds =
                durationMicroseconds;
        }

        return new ProjectedInput(
            captured.Input with
            {
                OffsetMicroseconds =
                    offsetMicroseconds,
            },
            elapsedNativeMilliseconds,
            captured.Sequence);
    }

    internal static uint ElapsedMilliseconds(
        uint startNativeMilliseconds,
        uint eventNativeMilliseconds) =>
        unchecked(
            eventNativeMilliseconds -
            startNativeMilliseconds);

    private static ManualInputEvent[] NormalizePointerPath(
        IReadOnlyList<ManualInputEvent> inputs,
        int initialClientX,
        int initialClientY)
    {
        List<ManualInputEvent> normalized =
            new(inputs.Count);
        int pointerX = initialClientX;
        int pointerY = initialClientY;
        foreach (ManualInputEvent input in inputs)
        {
            if (input.Kind ==
                ManualInputEventKind.MouseMove)
            {
                pointerX =
                    RequireCoordinate(
                        input.ClientX);
                pointerY =
                    RequireCoordinate(
                        input.ClientY);
                normalized.Add(input);
                continue;
            }
            if (input.Kind is not (
                    ManualInputEventKind.MouseButtonDown or
                    ManualInputEventKind.MouseButtonUp or
                    ManualInputEventKind.MouseWheel or
                    ManualInputEventKind.MouseHorizontalWheel))
            {
                normalized.Add(input);
                continue;
            }

            int actionX =
                RequireCoordinate(
                    input.ClientX);
            int actionY =
                RequireCoordinate(
                    input.ClientY);
            if (Math.Abs(actionX - pointerX) > 1 ||
                Math.Abs(actionY - pointerY) > 1)
            {
                throw new InvalidDataException(
                    "Windows reported an incomplete pointer path while recording. Record this route again.");
            }
            if (actionX != pointerX ||
                actionY != pointerY)
            {
                normalized.Add(
                    new ManualInputEvent
                    {
                        OffsetMicroseconds =
                            input.OffsetMicroseconds,
                        Kind =
                            ManualInputEventKind.MouseMove,
                        ClientX = actionX,
                        ClientY = actionY,
                    });
                pointerX = actionX;
                pointerY = actionY;
            }
            normalized.Add(input);
        }
        return normalized.ToArray();
    }

    private static int RequireCoordinate(int? coordinate) =>
        coordinate ??
        throw new InvalidDataException(
            "Recorded mouse input is missing its client position.");

    private sealed record CapturedInput(
        ManualInputEvent Input,
        uint NativeMilliseconds,
        long Sequence);

    private sealed record ProjectedInput(
        ManualInputEvent Input,
        uint ElapsedNativeMilliseconds,
        long Sequence);
}
