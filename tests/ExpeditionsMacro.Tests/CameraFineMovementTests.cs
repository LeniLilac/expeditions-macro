using ExpeditionsMacro.Automation.Camera;

namespace ExpeditionsMacro.Tests;

public sealed class CameraFineMovementTests
{
    [Fact]
    public async Task MoveAsync_UsesIdenticalAtomicGesturesInBothDirections()
    {
        List<int> gestures = [];

        await CameraFineMovement.MoveAsync(
            -4,
            1,
            (pixels, _) =>
            {
                gestures.Add(pixels);
                return Task.CompletedTask;
            },
            CancellationToken.None);
        await CameraFineMovement.MoveAsync(
            4,
            1,
            (pixels, _) =>
            {
                gestures.Add(pixels);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal([-1, -1, -1, -1, 1, 1, 1, 1], gestures);
    }

    [Fact]
    public async Task MoveAsync_RejectsInvalidAtomicStep()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CameraFineMovement.MoveAsync(
                4,
                0,
                (_, _) => Task.CompletedTask,
                CancellationToken.None));
    }
}
