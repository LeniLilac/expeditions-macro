namespace ExpeditionsMacro.Automation.Navigation;

internal sealed class StableNavigationActionTracker<TState>
    where TState : notnull
{
    // GB-017: an actionable state may be recognized before its panel finishes
    // sliding. Keep the original action center as the stability anchor.
    private readonly int _required;
    private readonly int _coordinateTolerance;
    private TState? _candidateState;
    private (int X, int Y)? _candidateAction;
    private int _count;

    public StableNavigationActionTracker(
        int required = 2,
        int coordinateTolerance = 3)
    {
        _required = Math.Max(1, required);
        _coordinateTolerance = Math.Max(0, coordinateTolerance);
    }

    public (int X, int Y)? Update(
        TState? state,
        (int X, int Y)? action)
    {
        if (state is null || action is null)
        {
            Reset();
            return null;
        }

        if (EqualityComparer<TState>.Default.Equals(
                state,
                _candidateState) &&
            _candidateAction is { } candidate &&
            Math.Abs(candidate.X - action.Value.X) <=
                _coordinateTolerance &&
            Math.Abs(candidate.Y - action.Value.Y) <=
                _coordinateTolerance)
        {
            _count++;
        }
        else
        {
            _candidateState = state;
            _candidateAction = action;
            _count = 1;
        }

        return _count >= _required ? action : null;
    }

    public void Reset()
    {
        _candidateState = default;
        _candidateAction = null;
        _count = 0;
    }
}
