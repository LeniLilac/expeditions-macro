namespace ExpeditionsMacro.Core.Runtime;

public enum MacroEventLevel
{
    Information,
    Success,
    Warning,
    Error,
}

public sealed record MacroEvent(
    DateTimeOffset Timestamp,
    MacroEventLevel Level,
    string Message,
    string? State = null,
    double? Confidence = null);

public sealed record MacroProgress(
    string Phase,
    int Percent,
    string Message,
    string? DetectedState = null,
    double? Confidence = null);

public sealed record ExpeditionRunSummary(
    DateTimeOffset StartedAt,
    TimeSpan Runtime,
    int Repeats,
    int Victories,
    int Defeats,
    int Recoveries,
    int BossesSeen);
