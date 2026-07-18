namespace ExpeditionsMacro.Core.Runtime;

public sealed class StableStateTracker<T> where T : notnull
{
    private readonly int _required;
    private T? _candidate;
    private int _count;

    public StableStateTracker(int required = 2)
    {
        _required = Math.Max(1, required);
    }

    public T? Update(T? state)
    {
        if (state is null)
        {
            Reset();
            return default;
        }

        if (EqualityComparer<T>.Default.Equals(state, _candidate))
        {
            _count++;
        }
        else
        {
            _candidate = state;
            _count = 1;
        }

        return _count >= _required ? state : default;
    }

    public void Reset()
    {
        _candidate = default;
        _count = 0;
    }
}
