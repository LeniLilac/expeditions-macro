namespace ExpeditionsMacro.DetectorViewer.Models;

public sealed class ViewerSessionModel
{
    public const double MinimumZoom = 0.50;
    public const double MaximumZoom = 4.00;

    public int FrameCount { get; private set; }

    public int FrameIndex { get; private set; }

    public double Zoom { get; private set; } = 1;

    public bool ShowGeometry { get; set; } = true;

    public bool ShowLabels { get; set; } = true;

    public bool CanMovePrevious =>
        FrameCount > 0 &&
        FrameIndex > 0;

    public bool CanMoveNext =>
        FrameCount > 0 &&
        FrameIndex < FrameCount - 1;

    public void ResetFrames(int frameCount)
    {
        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameCount));
        }
        FrameCount = frameCount;
        FrameIndex = 0;
    }

    public int SelectFrame(int index)
    {
        if (FrameCount == 0)
        {
            FrameIndex = 0;
            return 0;
        }
        FrameIndex = Math.Clamp(
            index,
            0,
            FrameCount - 1);
        return FrameIndex;
    }

    public int MoveFrame(int delta) =>
        SelectFrame(
            checked(FrameIndex + delta));

    public double SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom))
        {
            throw new ArgumentOutOfRangeException(
                nameof(zoom));
        }
        Zoom = Math.Clamp(
            zoom,
            MinimumZoom,
            MaximumZoom);
        return Zoom;
    }
}
