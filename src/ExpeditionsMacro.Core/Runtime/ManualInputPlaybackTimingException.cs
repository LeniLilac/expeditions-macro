using System.Globalization;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Runtime;

public sealed class ManualInputPlaybackTimingException :
    InvalidOperationException
{
    public ManualInputPlaybackTimingException(
        long scheduledMicroseconds,
        long actualMicroseconds,
        ManualInputEventKind eventKind,
        bool inputWasSent)
        : base(BuildMessage(
            scheduledMicroseconds,
            actualMicroseconds,
            eventKind,
            inputWasSent))
    {
        ScheduledMicroseconds = scheduledMicroseconds;
        ActualMicroseconds = actualMicroseconds;
        EventKind = eventKind;
        InputWasSent = inputWasSent;
    }

    public long ScheduledMicroseconds { get; }

    public long ActualMicroseconds { get; }

    public long DriftMicroseconds =>
        ActualMicroseconds - ScheduledMicroseconds;

    public ManualInputEventKind EventKind { get; }

    public bool InputWasSent { get; }

    private static string BuildMessage(
        long scheduledMicroseconds,
        long actualMicroseconds,
        ManualInputEventKind eventKind,
        bool inputWasSent)
    {
        long driftMicroseconds =
            actualMicroseconds - scheduledMicroseconds;
        string direction =
            driftMicroseconds < 0
                ? "early"
                : "late";
        double absoluteMilliseconds =
            Math.Abs(driftMicroseconds) / 1_000d;
        string boundary =
            inputWasSent
                ? "after sending"
                : "before sending";
        string formattedMilliseconds =
            absoluteMilliseconds.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        return
            $"Manual playback was {formattedMilliseconds} ms " +
            $"{direction} {boundary} {eventKind}; required timing is " +
            "within +/- 50 ms.";
    }
}
