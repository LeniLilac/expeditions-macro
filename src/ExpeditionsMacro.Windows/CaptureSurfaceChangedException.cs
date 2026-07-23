namespace ExpeditionsMacro.Windows;

internal sealed class CaptureSurfaceChangedException(
    int expectedWidth,
    int expectedHeight,
    int actualWidth,
    int actualHeight,
    Exception? innerException = null)
    : Exception(
        $"The Windows capture surface changed from {expectedWidth} by {expectedHeight} to {actualWidth} by {actualHeight} before its window geometry stabilized.",
        innerException)
{
    public int ExpectedWidth { get; } = expectedWidth;

    public int ExpectedHeight { get; } = expectedHeight;

    public int ActualWidth { get; } = actualWidth;

    public int ActualHeight { get; } = actualHeight;
}
