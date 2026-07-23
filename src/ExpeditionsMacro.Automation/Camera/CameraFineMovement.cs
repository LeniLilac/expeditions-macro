namespace ExpeditionsMacro.Automation.Camera;

internal static class CameraFineMovement
{
    private const int InterGestureDelayMilliseconds = 25;

    public static async Task MoveAsync(
        int horizontalPixels,
        int atomicStepPixels,
        Func<int, CancellationToken, Task> drag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(drag);
        if (atomicStepPixels < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atomicStepPixels));
        }

        int remaining = horizontalPixels;
        while (remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int step = Math.Sign(remaining) *
                Math.Min(Math.Abs(remaining), atomicStepPixels);
            await drag(step, cancellationToken).ConfigureAwait(false);
            remaining -= step;
            if (remaining != 0)
            {
                await Task.Delay(
                    InterGestureDelayMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
