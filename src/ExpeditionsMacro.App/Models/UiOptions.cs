using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Models;

public sealed record NamedChoice<T>(T Value, string Name);

public sealed record TeamChoice(int Value, string Name);

public sealed record MacroPresetChoice(MacroTaskKind Kind, string Id, string Name)
{
    public string DisplayName => $"{Name} - {Kind}";
}

public sealed class MacroTaskRow
{
    public required MacroTaskDefinition Definition { get; init; }
    public required MacroTaskProgress Progress { get; init; }

    public string Name => string.IsNullOrWhiteSpace(Definition.Name) ? Definition.PresetId : Definition.Name;
    public string Type => Definition.Kind.ToString();
    public string Target => Definition.IsRecurring
        ? "Every reset"
        : Definition.CompleteOnRuntimeDefeat
            ? $"{Definition.TargetRuntimeMinutes / 60d:0.#} h, then defeat"
            : $"{Definition.TargetVictories} victories";
    public string Status => !Definition.Enabled
        ? "Disabled"
        : Progress.Completed
        ? "Complete"
        : Definition.IsRecurring && Progress.NextEligibleAtUtc is DateTimeOffset next
            ? $"Available {next.LocalDateTime:t}"
            : Definition.CompleteOnRuntimeDefeat
                ? $"{TimeSpan.FromSeconds(Progress.RuntimeSeconds):h\\:mm} - {Progress.Defeats}L"
                : $"{Progress.Victories}W / {Progress.Defeats}L";
}
