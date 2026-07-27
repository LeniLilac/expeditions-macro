namespace ExpeditionsMacro.Core.Persistence;

internal sealed class PersistedPlanReplacementLineage
{
    private readonly object _sync = new();
    private readonly Dictionary<string, string>
        _successors =
            new(StringComparer.OrdinalIgnoreCase);

    public string? Resolve(string? sourcePlanId)
    {
        if (string.IsNullOrWhiteSpace(
                sourcePlanId))
        {
            return null;
        }

        lock (_sync)
        {
            return ResolveNoLock(sourcePlanId);
        }
    }

    public void Register(
        string? originalSourcePlanId,
        string? persistedSourcePlanId,
        string savedPlanId)
    {
        if (string.IsNullOrWhiteSpace(
                persistedSourcePlanId) ||
            string.Equals(
                persistedSourcePlanId,
                savedPlanId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_sync)
        {
            string[] affected =
                _successors.Keys
                    .Where(key => string.Equals(
                        ResolveNoLock(key),
                        persistedSourcePlanId,
                        StringComparison
                            .OrdinalIgnoreCase))
                    .ToArray();
            _successors.Remove(savedPlanId);
            foreach (string key in affected)
            {
                if (!string.Equals(
                        key,
                        savedPlanId,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    _successors[key] =
                        savedPlanId;
                }
            }
            _successors[persistedSourcePlanId] =
                savedPlanId;
            if (!string.IsNullOrWhiteSpace(
                    originalSourcePlanId) &&
                !string.Equals(
                    originalSourcePlanId,
                    savedPlanId,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                _successors[originalSourcePlanId] =
                    savedPlanId;
            }
        }
    }

    private string ResolveNoLock(
        string sourcePlanId)
    {
        string current = sourcePlanId;
        HashSet<string> visited =
            new(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(current) &&
               _successors.TryGetValue(
                   current,
                   out string? successor))
        {
            current = successor;
        }
        return current;
    }
}
