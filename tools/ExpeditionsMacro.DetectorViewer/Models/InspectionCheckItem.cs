using ExpeditionsMacro.Vision.Inspection;

namespace ExpeditionsMacro.DetectorViewer.Models;

public sealed class InspectionCheckItem
{
    public InspectionCheckItem(
        DetectorInspectionCheck check)
    {
        Check = check;
        StatusText = check.Status switch
        {
            DetectorInspectionCheckStatus.Passed =>
                "Pass",
            DetectorInspectionCheckStatus.Failed =>
                "Fail",
            DetectorInspectionCheckStatus.NotExposed =>
                "Not exposed",
            _ => "Observed",
        };
    }

    public DetectorInspectionCheck Check { get; }

    public string Id => Check.Id;

    public string Label => Check.Label;

    public string Expected => Check.Expected;

    public string Measured => Check.Measured;

    public string Threshold => Check.Threshold;

    public IReadOnlyList<string> RegionIds =>
        Check.RegionIds;

    public string StatusText { get; }

    public bool IsPassed =>
        Check.Status ==
        DetectorInspectionCheckStatus.Passed;

    public bool IsFailed =>
        Check.Status ==
        DetectorInspectionCheckStatus.Failed;

    public bool IsNotExposed =>
        Check.Status ==
        DetectorInspectionCheckStatus.NotExposed;
}
