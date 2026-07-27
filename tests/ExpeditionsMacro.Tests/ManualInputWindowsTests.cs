using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Tests;

public sealed class ManualInputWindowsTests
{
    [Fact]
    public void EventFactory_RecordsPhysicalScanData()
    {
        NativeMethods.KeyboardHookData data = new()
        {
            VirtualKey = 0x27,
            ScanCode = 0x4D,
            Flags = 0x01,
        };

        bool recorded =
            ManualInputEventFactory.TryCreateKeyboard(
                NativeMethods.WmKeyDown,
                data,
                1_234,
                new HashSet<int>(),
                out ManualInputEvent? input);

        Assert.True(recorded);
        Assert.NotNull(input);
        Assert.Equal(1_234, input.OffsetMicroseconds);
        Assert.Equal(
            ManualInputEventKind.KeyDown,
            input.Kind);
        Assert.Equal(0x27, input.VirtualKey);
        Assert.Equal(0x4D, input.ScanCode);
        Assert.True(input.ExtendedKey);
    }

    [Theory]
    [InlineData(0x10)]
    [InlineData(0x02)]
    public void EventFactory_IgnoresInjectedKeyboard(
        uint flags)
    {
        NativeMethods.KeyboardHookData data = new()
        {
            VirtualKey = 0x41,
            ScanCode = 0x1E,
            Flags = flags,
        };

        bool recorded =
            ManualInputEventFactory.TryCreateKeyboard(
                NativeMethods.WmKeyDown,
                data,
                0,
                new HashSet<int>(),
                out ManualInputEvent? input);

        Assert.False(recorded);
        Assert.Null(input);
    }

    [Theory]
    [InlineData(NativeMethods.WmKeyDown)]
    [InlineData(NativeMethods.WmKeyUp)]
    public void EventFactory_IgnoresConfiguredControlHotkey(
        uint message)
    {
        NativeMethods.KeyboardHookData data = new()
        {
            VirtualKey = 0x76,
            ScanCode = 0x41,
        };

        bool recorded =
            ManualInputEventFactory.TryCreateKeyboard(
                message,
                data,
                0,
                new HashSet<int> { 0x76 },
                out ManualInputEvent? input);

        Assert.False(recorded);
        Assert.Null(input);
    }

    [Theory]
    [InlineData(
        0x0201,
        ManualInputEventKind.MouseButtonDown,
        ManualMouseButton.Left,
        0u)]
    [InlineData(
        0x0202,
        ManualInputEventKind.MouseButtonUp,
        ManualMouseButton.Left,
        0u)]
    [InlineData(
        0x0204,
        ManualInputEventKind.MouseButtonDown,
        ManualMouseButton.Right,
        0u)]
    [InlineData(
        0x0205,
        ManualInputEventKind.MouseButtonUp,
        ManualMouseButton.Right,
        0u)]
    [InlineData(
        0x0207,
        ManualInputEventKind.MouseButtonDown,
        ManualMouseButton.Middle,
        0u)]
    [InlineData(
        0x0208,
        ManualInputEventKind.MouseButtonUp,
        ManualMouseButton.Middle,
        0u)]
    [InlineData(
        0x020B,
        ManualInputEventKind.MouseButtonDown,
        ManualMouseButton.X1,
        0x00010000u)]
    [InlineData(
        0x020C,
        ManualInputEventKind.MouseButtonUp,
        ManualMouseButton.X2,
        0x00020000u)]
    public void EventFactory_RecordsEveryMouseButton(
        uint message,
        ManualInputEventKind kind,
        ManualMouseButton button,
        uint mouseData)
    {
        NativeMethods.MouseHookData data = new()
        {
            Position = new NativeMethods.Point
            {
                X = 240,
                Y = 300,
            },
            MouseData = mouseData,
        };

        MouseObservation observation =
            ManualInputEventFactory.CreateMouse(
                message,
                data,
                new ClientBounds(
                    0,
                    0,
                    808,
                    611),
                10_000);

        Assert.NotNull(observation.Input);
        Assert.Equal(
            kind,
            observation.Input.Kind);
        Assert.Equal(
            button,
            observation.Input.MouseButton);
    }

    [Theory]
    [InlineData(0x020A, 120,
        ManualInputEventKind.MouseWheel)]
    [InlineData(0x020A, -120,
        ManualInputEventKind.MouseWheel)]
    [InlineData(0x020E, 120,
        ManualInputEventKind.MouseHorizontalWheel)]
    [InlineData(0x020E, -120,
        ManualInputEventKind.MouseHorizontalWheel)]
    public void EventFactory_RecordsBothWheelDirections(
        uint message,
        int delta,
        ManualInputEventKind kind)
    {
        NativeMethods.MouseHookData data = new()
        {
            Position = new NativeMethods.Point
            {
                X = 240,
                Y = 300,
            },
            MouseData = unchecked(
                (uint)(delta << 16)),
        };

        MouseObservation observation =
            ManualInputEventFactory.CreateMouse(
                message,
                data,
                new ClientBounds(
                    0,
                    0,
                    808,
                    611),
                10_000);

        Assert.NotNull(observation.Input);
        Assert.Equal(
            kind,
            observation.Input.Kind);
        Assert.Equal(
            delta,
            observation.Input.WheelDelta);
    }

    [Fact]
    public void EventFactory_ConvertsMouseToClientCoordinates()
    {
        NativeMethods.MouseHookData data = new()
        {
            Position = new NativeMethods.Point
            {
                X = 310,
                Y = 420,
            },
            MouseData = unchecked((uint)(120 << 16)),
        };

        MouseObservation observation =
            ManualInputEventFactory.CreateMouse(
                0x020A,
                data,
                new ClientBounds(100, 200, 808, 611),
                22_000);

        Assert.False(observation.IsOutsideClient);
        Assert.NotNull(observation.Input);
        Assert.Equal(210, observation.Input.ClientX);
        Assert.Equal(220, observation.Input.ClientY);
        Assert.Equal(120, observation.Input.WheelDelta);
        Assert.Equal(
            ManualInputEventKind.MouseWheel,
            observation.Input.Kind);
    }

    [Fact]
    public void EventFactory_RejectsMouseOutsideClient()
    {
        NativeMethods.MouseHookData data = new()
        {
            Position = new NativeMethods.Point
            {
                X = 99,
                Y = 420,
            },
        };

        MouseObservation observation =
            ManualInputEventFactory.CreateMouse(
                NativeMethods.WmMouseMove,
                data,
                new ClientBounds(100, 200, 808, 611),
                0);

        Assert.True(observation.IsOutsideClient);
        Assert.Null(observation.Input);
    }

    [Fact]
    public void EventFactory_IgnoresInjectedMouse()
    {
        NativeMethods.MouseHookData data = new()
        {
            Position = new NativeMethods.Point
            {
                X = 240,
                Y = 300,
            },
            Flags = 0x01,
        };

        MouseObservation observation =
            ManualInputEventFactory.CreateMouse(
                NativeMethods.WmMouseMove,
                data,
                new ClientBounds(0, 0, 808, 611),
                0);

        Assert.False(observation.IsOutsideClient);
        Assert.Null(observation.Input);
    }

    [Fact]
    public void Recorder_StopFocusGraceAcceptsOnlyStopSignal()
    {
        Assert.True(
            WindowsManualInputRecorder
                .WaitForStopTransition(
                    new CancellationToken(
                        canceled: true),
                    graceMilliseconds: 0));
        Assert.False(
            WindowsManualInputRecorder
                .WaitForStopTransition(
                    CancellationToken.None,
                    graceMilliseconds: 0));
    }

    [Fact]
    public void StopwatchPlaybackClock_RequiresExplicitSingleStart()
    {
        StopwatchManualInputClock clock = new();

        Assert.Throws<InvalidOperationException>(
            () => _ = clock.ElapsedMicroseconds);

        clock.Start();

        Assert.True(clock.ElapsedMicroseconds >= 0);
        Assert.Throws<InvalidOperationException>(
            clock.Start);
    }

    [Fact]
    public async Task PlaybackEngine_UsesAbsoluteMicrosecondDeadlines()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new(
            driftMicroseconds: 4_000);
        FakeInputSink sink = new();
        List<ManualInputPlaybackTiming> timings = [];
        ManualInputPlaybackEngine engine = new();

        await engine.PlayAsync(
            recording,
            clock,
            sink,
            playbackStarting: null,
            timings.Add,
            CancellationToken.None);

        Assert.Equal(
            recording.Events
                .Select(input => input.OffsetMicroseconds)
                .Append(recording.DurationMicroseconds),
            clock.Deadlines);
        Assert.Equal(recording.Events, sink.Sent);
        Assert.Same(recording, sink.PreparedRecording);
        Assert.Equal(1, clock.StartCalls);
        Assert.All(
            timings,
            timing => Assert.InRange(
                timing.DriftMicroseconds,
                0,
                10_000));
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Fact]
    public async Task PlaybackEngine_PreparesBeforeStartingClock()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        List<string> order = [];
        FakePlaybackClock clock = new()
        {
            Starting = () => order.Add("clock"),
        };
        FakeInputSink sink = new()
        {
            Preparing = _ => order.Add("prepare"),
        };
        ManualInputPlaybackEngine engine = new();

        await engine.PlayAsync(
            recording,
            clock,
            sink,
            () => order.Add("callback"),
            timing: null,
            CancellationToken.None);

        Assert.Equal(
            new[] { "prepare", "callback", "clock" },
            order);
        Assert.Same(recording, sink.PreparedRecording);
    }

    [Fact]
    public async Task PlaybackEngine_DoesNotStartClockForInvalidRecording()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording() with
            {
                Events = [],
            };
        FakePlaybackClock clock = new();
        FakeInputSink sink = new();
        ManualInputPlaybackEngine engine = new();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => engine.PlayAsync(
                recording,
                clock,
                sink,
                playbackStarting: null,
                timing: null,
                CancellationToken.None));

        Assert.Equal(0, clock.StartCalls);
        Assert.Equal(0, sink.PrepareCalls);
    }

    [Fact]
    public async Task PlaybackEngine_DoesNotStartClockWhenPreflightFails()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new();
        FakeInputSink sink = new()
        {
            PreparationFailure =
                new InvalidOperationException(
                    "simulated preflight failure"),
        };
        ManualInputPlaybackEngine engine = new();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.PlayAsync(
                recording,
                clock,
                sink,
                playbackStarting: null,
                timing: null,
                CancellationToken.None));

        Assert.Equal(0, clock.StartCalls);
        Assert.Equal(1, sink.PrepareCalls);
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Fact]
    public async Task PlaybackEngine_ReleasesHeldInputsOnCancellation()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new(
            cancelAtDeadline: 10_000);
        FakeInputSink sink = new();
        ManualInputPlaybackEngine engine = new();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.PlayAsync(
                recording,
                clock,
                sink,
                playbackStarting: null,
                timing: null,
                new CancellationToken(canceled: false)));

        Assert.Single(sink.Sent);
        Assert.Equal(
            ManualInputEventKind.KeyDown,
            sink.Sent[0].Kind);
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Fact]
    public async Task PlaybackEngine_ReleasesHeldInputsWhenSendFails()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new();
        FakeInputSink sink = new()
        {
            FailAfterSendCount = 1,
        };
        ManualInputPlaybackEngine engine = new();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.PlayAsync(
                recording,
                clock,
                sink,
                playbackStarting: null,
                timing: null,
                CancellationToken.None));

        Assert.Single(sink.Sent);
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Fact]
    public async Task PlaybackEngine_RejectsTimingDriftOverTenMilliseconds()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new(
            driftMicroseconds: 10_001);
        FakeInputSink sink = new();
        ManualInputPlaybackEngine engine = new();

        TimeoutException error =
            await Assert.ThrowsAsync<TimeoutException>(
                () => engine.PlayAsync(
                    recording,
                    clock,
                    sink,
                    playbackStarting: null,
                    timing: null,
                    CancellationToken.None));

        Assert.Contains(
            "10 ms",
            error.Message,
            StringComparison.Ordinal);
        Assert.Empty(sink.Sent);
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Fact]
    public async Task PlaybackEngine_RejectsSendThatFinishesOverTenMillisecondsLate()
    {
        ManualInputRecording recording =
            ManualInputRecordingTests.ValidRecording();
        FakePlaybackClock clock = new();
        FakeInputSink sink = new()
        {
            AfterSend = () =>
                clock.AdvanceBy(10_001),
        };
        ManualInputPlaybackEngine engine = new();

        TimeoutException error =
            await Assert.ThrowsAsync<TimeoutException>(
                () => engine.PlayAsync(
                    recording,
                    clock,
                    sink,
                    playbackStarting: null,
                    timing: null,
                    CancellationToken.None));

        Assert.Contains(
            "10 ms",
            error.Message,
            StringComparison.Ordinal);
        Assert.Single(sink.Sent);
        Assert.Equal(1, sink.ReleaseCalls);
    }

    [Theory]
    [InlineData(-1920, -1920, 3840, 0)]
    [InlineData(1919, -1920, 3840, 65535)]
    [InlineData(0, -1920, 3840, 32776)]
    public void VirtualDesktop_NormalizesAcrossAllMonitors(
        int coordinate,
        int origin,
        int extent,
        int expected)
    {
        VirtualDesktop desktop =
            new(origin, -1080, extent, 2160);

        Assert.Equal(
            expected,
            desktop.NormalizeX(coordinate));
    }

    private sealed class FakePlaybackClock :
        IManualInputPlaybackClock
    {
        private readonly long _driftMicroseconds;
        private readonly long? _cancelAtDeadline;

        public FakePlaybackClock(
            long driftMicroseconds = 0,
            long? cancelAtDeadline = null)
        {
            _driftMicroseconds = driftMicroseconds;
            _cancelAtDeadline = cancelAtDeadline;
        }

        public List<long> Deadlines { get; } = [];

        public Action? Starting { get; init; }

        public int StartCalls { get; private set; }

        public long ElapsedMicroseconds { get; private set; }

        public void Start()
        {
            StartCalls++;
            Starting?.Invoke();
        }

        public ValueTask WaitUntilAsync(
            long deadlineMicroseconds,
            CancellationToken cancellationToken)
        {
            if (StartCalls != 1)
            {
                throw new InvalidOperationException(
                    "Playback waited before starting its clock.");
            }
            Deadlines.Add(deadlineMicroseconds);
            if (_cancelAtDeadline is not null &&
                deadlineMicroseconds >= _cancelAtDeadline)
            {
                return ValueTask.FromException(
                    new OperationCanceledException(
                        cancellationToken));
            }
            ElapsedMicroseconds =
                deadlineMicroseconds +
                _driftMicroseconds;
            return ValueTask.CompletedTask;
        }

        public void AdvanceBy(
            long microseconds) =>
            ElapsedMicroseconds += microseconds;
    }

    private sealed class FakeInputSink : IManualInputSink
    {
        public List<ManualInputEvent> Sent { get; } = [];

        public Action<ManualInputRecording>? Preparing { get; init; }

        public ManualInputRecording? PreparedRecording { get; private set; }

        public int PrepareCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public int? FailAfterSendCount { get; init; }

        public Exception? PreparationFailure { get; init; }

        public Action? AfterSend { get; init; }

        public void Prepare(ManualInputRecording recording)
        {
            PrepareCalls++;
            PreparedRecording = recording;
            Preparing?.Invoke(recording);
            if (PreparationFailure is not null)
            {
                throw PreparationFailure;
            }
        }

        public void Send(ManualInputEvent input)
        {
            if (FailAfterSendCount is not null &&
                Sent.Count >= FailAfterSendCount)
            {
                throw new InvalidOperationException("simulated send failure");
            }
            Sent.Add(input);
            AfterSend?.Invoke();
        }

        public void ReleaseHeldInputs() =>
            ReleaseCalls++;
    }
}
