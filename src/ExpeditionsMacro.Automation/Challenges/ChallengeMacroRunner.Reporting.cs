using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
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
            // Recovery must continue even if its optional screenshot cannot be captured.
            return null;
        }
    }
}
