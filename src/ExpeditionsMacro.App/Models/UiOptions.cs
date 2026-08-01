using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record NamedChoice<T>(T Value, string Name);

public sealed record TeamChoice(int Value, string Name);

public sealed class MacroTaskRow
{
    public required MacroTaskDefinition Definition { get; init; }
    public required MacroTaskProgress Progress { get; init; }

    public string Name => string.IsNullOrWhiteSpace(Definition.Name) ? Definition.PresetId : Definition.Name;
    public string Type => Definition.Kind switch
    {
        MacroTaskKind.Utility => "Utilities",
        MacroTaskKind.Bounty => "Bounty",
        _ => Definition.Kind.ToString(),
    };
    public string LoopLabel =>
        $"#{Definition.Priority}  {Type} · {Name}";
    public string Target => Definition.Kind switch
    {
        MacroTaskKind.Utility =>
            $"Every {Definition.RefuelIntervalMinutes} min",
        MacroTaskKind.Bounty => string.Empty,
        _ when Definition.IsRecurring => "Every reset",
        _ =>
            Definition.CompleteOnRuntimeDefeat
                ? $"{Definition.TargetRuntimeMinutes / 60d:0.#} h, then defeat"
                : $"{Definition.TargetVictories} victories",
    };
    public string Status => Definition.Kind is
            MacroTaskKind.Utility or
            MacroTaskKind.Bounty
        ? Progress.NextEligibleAtUtc is DateTimeOffset utilityNext
            ? $"Due {utilityNext.LocalDateTime:t}"
            : "Ready"
        : Progress.Completed
        ? "Complete"
        : Definition.IsRecurring && Progress.NextEligibleAtUtc is DateTimeOffset next
            ? $"Available {next.LocalDateTime:t}"
            : Definition.CompleteOnRuntimeDefeat
                ? $"{TimeSpan.FromSeconds(Math.Max(0, Progress.RuntimeSeconds - Progress.TargetRuntimeBaselineSeconds)):h\\:mm} - {Progress.Defeats}L"
                : $"{Math.Max(0, Progress.Victories - Progress.TargetVictoryBaseline)}W / {Progress.Defeats}L";
}
