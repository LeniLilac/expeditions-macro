namespace ExpeditionsMacro.Automation.Camera;

public sealed class CameraAlignmentException : InvalidOperationException
{
    public CameraAlignmentException(string message, double bestConfidence, int attempts)
        : base(message)
    {
        BestConfidence = bestConfidence;
        Attempts = attempts;
    }

    public double BestConfidence { get; }

    public int Attempts { get; }
}
