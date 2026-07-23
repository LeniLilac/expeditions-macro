namespace ExpeditionsMacro.Automation.Camera;

public sealed class CameraWorldNotRenderedException : CameraAlignmentException
{
    public CameraWorldNotRenderedException(
        double readiness,
        int attempt)
        : base(
            "The stage world did not render before camera alignment. " +
            "No camera movement or unit placement was attempted.",
            readiness,
            attempt)
    {
    }
}
