namespace ExpeditionsMacro.Vision.Diagnostics;

public sealed record VisionDetectionTrace(
    DateTimeOffset TimestampUtc,
    string Detector,
    string State,
    double? Confidence = null,
    object? Data = null);

public static class VisionTrace
{
    public static event Action<VisionDetectionTrace>? Detected;

    public static void Emit(string detector, string state, double? confidence = null, object? data = null)
    {
        try
        {
            Detected?.Invoke(new VisionDetectionTrace(DateTimeOffset.UtcNow, detector, state, confidence, data));
        }
        catch
        {
            // Diagnostic observers must never alter detector behavior.
        }
    }
}
