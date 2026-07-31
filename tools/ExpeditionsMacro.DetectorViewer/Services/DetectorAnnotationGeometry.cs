using ExpeditionsMacro.DetectorViewer.Models;

namespace ExpeditionsMacro.DetectorViewer.Services;

public static class DetectorAnnotationGeometry
{
    public static DetectorAnnotationRegion? CreateRegion(
        double startX,
        double startY,
        double endX,
        double endY,
        int imageWidth,
        int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(imageWidth));
        }
        double left = Math.Clamp(
            Math.Min(startX, endX),
            0,
            imageWidth);
        double top = Math.Clamp(
            Math.Min(startY, endY),
            0,
            imageHeight);
        double rightValue = Math.Clamp(
            Math.Max(startX, endX),
            0,
            imageWidth);
        double bottomValue = Math.Clamp(
            Math.Max(startY, endY),
            0,
            imageHeight);
        int x = (int)Math.Floor(left);
        int y = (int)Math.Floor(top);
        int right = (int)Math.Ceiling(rightValue);
        int bottom = (int)Math.Ceiling(bottomValue);
        return right - x < 3 || bottom - y < 3
            ? null
            : new DetectorAnnotationRegion
            {
                X = x,
                Y = y,
                Width = right - x,
                Height = bottom - y,
            };
    }
}
