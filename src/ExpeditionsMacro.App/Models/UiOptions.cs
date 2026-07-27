using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record NamedChoice<T>(T Value, string Name);

public sealed record TeamChoice(int Value, string Name);

public sealed class MacroTaskRow
{
    public required MacroTaskDefinition Definition { get; init; }
    public required MacroTaskProgress Progress { get; init; }

    public string Name => string.IsNullOrWhiteSpace(Definition.Name) ? Definition.PresetId : Definition.Name;
    public string Type => Definition.Kind.ToString();
    public string LoopLabel =>
        $"#{Definition.Priority}  {Type} · {Name}";
    public string Target => Definition.IsRecurring
        ? "Every reset"
        : Definition.CompleteOnRuntimeDefeat
            ? $"{Definition.TargetRuntimeMinutes / 60d:0.#} h, then defeat"
            : $"{Definition.TargetVictories} victories";
    public string Status => Progress.Completed
        ? "Complete"
        : Definition.IsRecurring && Progress.NextEligibleAtUtc is DateTimeOffset next
            ? $"Available {next.LocalDateTime:t}"
            : Definition.CompleteOnRuntimeDefeat
                ? $"{TimeSpan.FromSeconds(Math.Max(0, Progress.RuntimeSeconds - Progress.TargetRuntimeBaselineSeconds)):h\\:mm} - {Progress.Defeats}L"
                : $"{Math.Max(0, Progress.Victories - Progress.TargetVictoryBaseline)}W / {Progress.Defeats}L";
}
