using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private ImageFrame? TryCaptureClient(
        RobloxWindow window,
        IDetectorPack detector)
    {
        try
        {
            return CaptureClient(window, detector);
        }
        catch
        {
            // Recovery can proceed even if its optional screenshot cannot be captured.
            return null;
        }
    }
}
