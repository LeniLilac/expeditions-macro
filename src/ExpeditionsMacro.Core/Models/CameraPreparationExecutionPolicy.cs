namespace ExpeditionsMacro.Core.Models;

public static class CameraPreparationExecutionPolicy
{
    public const string RetiredCameraModelGuidance =
        "Open Placement Setup and choose or create a Fast no align setup before running the macro.";

    public static bool IsSupportedForExecution(
        CameraPreparationMode mode) =>
        mode == CameraPreparationMode.FastNoAlign;

    public static void ValidateForExecution(
        CameraPreparationMode mode,
        string subject = "This setup")
    {
        if (!Enum.IsDefined(mode))
        {
            throw new InvalidDataException(
                "Camera preparation mode is invalid.");
        }
        if (IsSupportedForExecution(mode))
        {
            return;
        }

        string label = string.IsNullOrWhiteSpace(subject)
            ? "This setup"
            : subject.Trim();
        throw new InvalidDataException(
            $"{label} uses the retired Camera Model workflow. " +
            RetiredCameraModelGuidance);
    }
}
