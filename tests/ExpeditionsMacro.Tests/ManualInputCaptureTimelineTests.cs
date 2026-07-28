using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows;

namespace ExpeditionsMacro.Tests;

public sealed class ManualInputCaptureTimelineTests
{
    [Fact]
    public void DelayedButtonCallback_UsesNativeOrderBeforeLaterMoves()
    {
        ManualInputCaptureTimeline timeline = StartedAt(1_000);
        timeline.Add(Move(457, 306), 1_010);
        timeline.Add(Move(459, 303), 1_030);
        timeline.Add(Move(463, 298), 1_040);
        timeline.Add(ButtonDown(457, 306), 1_010);

        ManualInputEvent[] snapshot =
            timeline.BuildSnapshot(
                initialClientX: 450,
                initialClientY: 318,
                durationMicroseconds: 50_000);

        Assert.Collection(
            snapshot,
            input => AssertEvent(
                input,
                ManualInputEventKind.MouseMove,
                10_000),
            input => AssertEvent(
                input,
                ManualInputEventKind.MouseButtonDown,
                10_000),
            input => AssertEvent(
                input,
                ManualInputEventKind.MouseMove,
                30_000),
            input => AssertEvent(
                input,
                ManualInputEventKind.MouseMove,
                40_000));
    }

    [Fact]
    public void SameMillisecondEvents_RetainCallbackSequence()
    {
        ManualInputCaptureTimeline timeline = StartedAt(2_000);
        timeline.Add(Key(ManualInputEventKind.KeyDown), 2_010);
        timeline.Add(Key(ManualInputEventKind.KeyUp), 2_010);
        timeline.Add(Move(100, 100), 2_010);

        ManualInputEvent[] snapshot =
            timeline.BuildSnapshot(
                initialClientX: 100,
                initialClientY: 100,
                durationMicroseconds: 20_000);

        Assert.Equal(
            [
                ManualInputEventKind.KeyDown,
                ManualInputEventKind.KeyUp,
                ManualInputEventKind.MouseMove,
            ],
            snapshot.Select(input => input.Kind));
        Assert.All(
            snapshot,
            input => Assert.Equal(
                10_000,
                input.OffsetMicroseconds));
    }

    [Fact]
    public void NativeTimestampMapping_IsWrapSafe()
    {
        ManualInputCaptureTimeline timeline =
            StartedAt(uint.MaxValue - 1);
        timeline.Add(Key(ManualInputEventKind.KeyDown), uint.MaxValue);
        timeline.Add(Key(ManualInputEventKind.KeyUp), 0);
        timeline.Add(Move(100, 100), 1);

        ManualInputEvent[] snapshot =
            timeline.BuildSnapshot(
                initialClientX: 100,
                initialClientY: 100,
                durationMicroseconds: 4_000);

        Assert.Equal(
            [1_000L, 2_000L, 3_000L],
            snapshot.Select(input =>
                input.OffsetMicroseconds));
    }

    [Fact]
    public void MixedKeyboardAndMouseEvents_ShareOneNativeTimeline()
    {
        ManualInputCaptureTimeline timeline = StartedAt(4_000);
        timeline.Add(Move(200, 220), 4_030);
        timeline.Add(Key(ManualInputEventKind.KeyDown), 4_010);
        timeline.Add(ButtonDown(200, 220), 4_040);
        timeline.Add(Key(ManualInputEventKind.KeyUp), 4_020);

        ManualInputEvent[] snapshot =
            timeline.BuildSnapshot(
                initialClientX: 100,
                initialClientY: 100,
                durationMicroseconds: 50_000);

        Assert.Equal(
            [
                ManualInputEventKind.KeyDown,
                ManualInputEventKind.KeyUp,
                ManualInputEventKind.MouseMove,
                ManualInputEventKind.MouseButtonDown,
            ],
            snapshot.Select(input => input.Kind));
    }

    [Fact]
    public void OnePixelActionGap_BecomesExplicitRecordedMovement()
    {
        ManualInputCaptureTimeline timeline = StartedAt(4_500);
        timeline.Add(Move(431, 180), 4_510);
        timeline.Add(ButtonUp(431, 181), 4_520);

        ManualInputEvent[] snapshot =
            timeline.BuildSnapshot(
                initialClientX: 430,
                initialClientY: 180,
                durationMicroseconds: 30_000);

        Assert.Collection(
            snapshot,
            input =>
            {
                AssertEvent(
                    input,
                    ManualInputEventKind.MouseMove,
                    10_000);
                Assert.Equal(431, input.ClientX);
                Assert.Equal(180, input.ClientY);
            },
            input =>
            {
                AssertEvent(
                    input,
                    ManualInputEventKind.MouseMove,
                    20_000);
                Assert.Equal(431, input.ClientX);
                Assert.Equal(181, input.ClientY);
            },
            input =>
            {
                AssertEvent(
                    input,
                    ManualInputEventKind.MouseButtonUp,
                    20_000);
                Assert.Equal(431, input.ClientX);
                Assert.Equal(181, input.ClientY);
            });
    }

    [Fact]
    public void UnreconciledPointerAction_StopsBeforeSavingSnapshot()
    {
        ManualInputCaptureTimeline timeline = StartedAt(5_000);
        timeline.Add(Move(200, 220), 5_010);
        timeline.Add(ButtonDown(240, 260), 5_020);

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => timeline.BuildSnapshot(
                    initialClientX: 100,
                    initialClientY: 100,
                    durationMicroseconds: 30_000));

        Assert.Contains(
            "incomplete pointer path",
            error.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Record this route again",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EventBeforeCaptureStart_IsRejectedInsteadOfWrappedForward()
    {
        ManualInputCaptureTimeline timeline = StartedAt(10_000);
        timeline.Add(
            Key(ManualInputEventKind.KeyDown),
            9_999);

        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => timeline.BuildSnapshot(
                    initialClientX: 100,
                    initialClientY: 100,
                    durationMicroseconds: 30_000));

        Assert.Contains(
            "outside the recording timeline",
            error.Message,
            StringComparison.Ordinal);
    }

    private static ManualInputCaptureTimeline StartedAt(
        uint nativeMilliseconds)
    {
        ManualInputCaptureTimeline timeline = new();
        timeline.Start(nativeMilliseconds);
        return timeline;
    }

    private static ManualInputEvent Move(
        int clientX,
        int clientY) =>
        new()
        {
            OffsetMicroseconds = 0,
            Kind = ManualInputEventKind.MouseMove,
            ClientX = clientX,
            ClientY = clientY,
        };

    private static ManualInputEvent ButtonDown(
        int clientX,
        int clientY) =>
        new()
        {
            OffsetMicroseconds = 0,
            Kind = ManualInputEventKind.MouseButtonDown,
            ClientX = clientX,
            ClientY = clientY,
            MouseButton = ManualMouseButton.Left,
        };

    private static ManualInputEvent ButtonUp(
        int clientX,
        int clientY) =>
        new()
        {
            OffsetMicroseconds = 0,
            Kind = ManualInputEventKind.MouseButtonUp,
            ClientX = clientX,
            ClientY = clientY,
            MouseButton = ManualMouseButton.Right,
        };

    private static ManualInputEvent Key(
        ManualInputEventKind kind) =>
        new()
        {
            OffsetMicroseconds = 0,
            Kind = kind,
            VirtualKey = 0x41,
            ScanCode = 0x1E,
        };

    private static void AssertEvent(
        ManualInputEvent input,
        ManualInputEventKind kind,
        long offsetMicroseconds)
    {
        Assert.Equal(kind, input.Kind);
        Assert.Equal(
            offsetMicroseconds,
            input.OffsetMicroseconds);
    }
}
