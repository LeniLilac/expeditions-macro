using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
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
            return null;
        }
    }
}
