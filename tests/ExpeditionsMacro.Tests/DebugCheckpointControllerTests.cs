using ExpeditionsMacro.Automation.Diagnostics;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Tests;

public sealed class DebugCheckpointControllerTests
{
    [Fact]
    public async Task BeforeActions_WaitsUntilOneStepIsAuthorized()
    {
        DebugCheckpointController controller = new();
        TaskCompletionSource<DebugCheckpoint> observed =
            NewCheckpointSource();
        controller.CheckpointAdded += checkpoint =>
            observed.TrySetResult(checkpoint);
        controller.Begin(
            DebugStepMode.BeforeActions,
            CancellationToken.None);
        try
        {
            Task action = controller.BeforeActionAsync(
                "Click Roblox",
                "Click (100, 200).",
                CancellationToken.None);

            DebugCheckpoint checkpoint =
                await observed.Task.WaitAsync(
                    TimeSpan.FromSeconds(2));
            Assert.Equal(
                DebugCheckpointKind.Action,
                checkpoint.Kind);
            Assert.True(controller.IsWaiting);
            Assert.False(action.IsCompleted);

            controller.Step();
            await action.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(controller.IsWaiting);
        }
        finally
        {
            controller.Complete();
        }
    }

    [Fact]
    public async Task EveryDetection_WaitsAndRetainsLatestFrame()
    {
        DebugCheckpointController controller = new();
        TaskCompletionSource<DebugCheckpoint> observed =
            NewCheckpointSource();
        controller.CheckpointAdded += checkpoint =>
            observed.TrySetResult(checkpoint);
        controller.Begin(
            DebugStepMode.EveryDetectionAndAction,
            CancellationToken.None);
        try
        {
            ImageFrame frame = new(
                2,
                2,
                PixelFormat.Rgb24,
                Enumerable.Range(0, 12)
                    .Select(value => (byte)value)
                    .ToArray());
            controller.RecordFrame(frame);
            Task detection = Task.Run(() =>
                controller.RecordDetection(
                    new VisionDetectionTrace(
                        DateTimeOffset.UtcNow,
                        "stage_screen",
                        "Prestart",
                        0.96)));

            DebugCheckpoint checkpoint =
                await observed.Task.WaitAsync(
                    TimeSpan.FromSeconds(2));
            Assert.Equal(
                DebugCheckpointKind.Detection,
                checkpoint.Kind);
            Assert.Equal("Prestart", checkpoint.State);
            Assert.NotNull(checkpoint.Frame);
            Assert.Equal(frame.Pixels, checkpoint.Frame!.Pixels);
            Assert.False(detection.IsCompleted);

            controller.Step();
            await detection.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            controller.Complete();
        }
    }

    [Fact]
    public async Task Resume_ReleasesCurrentGateAndRunsFutureActions()
    {
        DebugCheckpointController controller = new();
        TaskCompletionSource<DebugCheckpoint> observed =
            NewCheckpointSource();
        controller.CheckpointAdded += checkpoint =>
            observed.TrySetResult(checkpoint);
        controller.Begin(
            DebugStepMode.BeforeActions,
            CancellationToken.None);
        try
        {
            Task first = controller.BeforeActionAsync(
                "Press P",
                "Open Play.",
                CancellationToken.None);
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            controller.Resume();
            await first.WaitAsync(TimeSpan.FromSeconds(2));
            Task second = controller.BeforeActionAsync(
                "Click Story",
                "Open Story.",
                CancellationToken.None);
            await second.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(
                DebugStepMode.Continuous,
                controller.Mode);
            Assert.False(controller.IsWaiting);
        }
        finally
        {
            controller.Complete();
        }
    }

    [Fact]
    public async Task Cancellation_ReleasesPausedCheckpoint()
    {
        DebugCheckpointController controller = new();
        using CancellationTokenSource cancellation = new();
        controller.Begin(
            DebugStepMode.BeforeActions,
            cancellation.Token);
        Task action = controller.BeforeActionAsync(
            "Click",
            "Pending action.",
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await action);
        controller.Complete();
        Assert.False(controller.IsActive);
    }

    [Fact]
    public async Task Cancellation_DoesNotPauseCleanupActions()
    {
        DebugCheckpointController controller = new();
        using CancellationTokenSource cancellation = new();
        controller.Begin(
            DebugStepMode.BeforeActions,
            cancellation.Token);
        Task cleanup = controller.BeforeActionAsync(
            "Restore Roblox window",
            "Cleanup after cancellation.",
            CancellationToken.None);
        Assert.True(controller.IsWaiting);
        cancellation.Cancel();

        await cleanup.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(controller.IsWaiting);
        controller.Complete();
    }

    private static TaskCompletionSource<DebugCheckpoint>
        NewCheckpointSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
