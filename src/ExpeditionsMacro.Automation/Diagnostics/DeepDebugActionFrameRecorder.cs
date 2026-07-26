using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Diagnostics;

internal sealed class DeepDebugActionFrameRecorder
{
    private readonly IRobloxAutomation _automation;
    private readonly DeepDebugSessionService _debug;

    public DeepDebugActionFrameRecorder(
        IRobloxAutomation automation,
        DeepDebugSessionService debug)
    {
        _automation = automation;
        _debug = debug;
    }

    public void Record(
        RobloxWindow window,
        string action,
        string phase)
    {
        if (!_debug.IsActive) return;
        try
        {
            ImageFrame frame =
                _automation.CaptureClient(window);
            _debug.RecordFrame(
                frame,
                "automation_action",
                new
                {
                    Action = action,
                    Phase = phase,
                    Window = WindowData(window),
                });
        }
        catch (Exception error)
        {
            _debug.RecordEvent(
                "diagnostic",
                "automation_action_frame_failed",
                new
                {
                    Action = action,
                    Phase = phase,
                    Error = error.Message,
                });
        }
    }

    private static object WindowData(
        RobloxWindow window) => new
        {
            Handle = window.Handle.ToInt64(),
            window.Title,
            window.ProcessId,
            window.ProcessName,
        };
}
