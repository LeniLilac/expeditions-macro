namespace ExpeditionsMacro.DetectorViewer.Models;

public enum DetectorExpectedResult
{
    Review,
    Match,
    NoMatch,
}

public sealed class DetectorAnnotationDocument
{
    public int Schema { get; set; } = 1;

    public List<DetectorImageAnnotation> Images { get; set; } = [];
}

public sealed class DetectorImageAnnotation
{
    public string ImagePath { get; set; } = string.Empty;

    public string DetectorId { get; set; } = string.Empty;

    public DetectorExpectedResult Expected { get; set; } =
        DetectorExpectedResult.Review;

    public string Notes { get; set; } = string.Empty;

    public List<DetectorAnnotationRegion> Regions { get; set; } = [];
}

public sealed class DetectorAnnotationRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = "Detection area";

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string CoordinateSummary =>
        $"X {X}, Y {Y}, W {Width}, H {Height}";
}

public sealed record AnnotationExpectedOption(
    DetectorExpectedResult Value,
    string Label);

public sealed record AnnotationRegionListItem(
    Guid Id,
    string Label,
    string CoordinateSummary);
