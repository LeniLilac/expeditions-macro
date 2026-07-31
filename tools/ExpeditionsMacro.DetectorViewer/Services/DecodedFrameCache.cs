namespace ExpeditionsMacro.DetectorViewer.Services;

public sealed class DecodedFrameCache
{
    public const long DefaultBudgetBytes =
        96L * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<int, Entry> _entries = [];
    private long _stamp;

    public DecodedFrameCache(
        long budgetBytes = DefaultBudgetBytes)
    {
        if (budgetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(budgetBytes));
        }
        BudgetBytes = budgetBytes;
    }

    public long BudgetBytes { get; }

    public long CurrentBytes
    {
        get
        {
            lock (_gate)
            {
                return _entries.Values
                    .Where(entry =>
                        entry.Frame is not null)
                    .Sum(entry =>
                        entry.Frame!.DecodedBytes);
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public async Task<DecodedViewerFrame> GetAsync(
        int index,
        Func<CancellationToken, Task<byte[]>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        Entry entry;
        lock (_gate)
        {
            if (_entries.TryGetValue(
                    index,
                    out Entry? existing))
            {
                existing.Stamp = ++_stamp;
                entry = existing;
            }
            else
            {
                entry = new Entry(
                    ++_stamp,
                    LoadAsync(
                        read,
                        cancellationToken));
                _entries[index] = entry;
            }
        }
        try
        {
            DecodedViewerFrame frame =
                await entry.Task.ConfigureAwait(false);
            lock (_gate)
            {
                entry.Frame = frame;
                entry.Stamp = ++_stamp;
                Trim(index);
            }
            return frame;
        }
        catch
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(
                        index,
                        out Entry? current) &&
                    ReferenceEquals(current, entry))
                {
                    _entries.Remove(index);
                }
            }
            throw;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private static async Task<DecodedViewerFrame>
        LoadAsync(
        Func<CancellationToken, Task<byte[]>> read,
        CancellationToken cancellationToken)
    {
        byte[] bytes =
            await read(cancellationToken)
                .ConfigureAwait(false);
        return ViewerFrameDecoder.Decode(bytes);
    }

    private void Trim(int pinnedIndex)
    {
        long current = _entries.Values
            .Where(entry =>
                entry.Frame is not null)
            .Sum(entry =>
                entry.Frame!.DecodedBytes);
        foreach ((int key, Entry value) in _entries
                     .Where(pair =>
                         pair.Key != pinnedIndex &&
                         pair.Value.Frame is not null)
                     .OrderBy(pair =>
                         pair.Value.Stamp)
                     .ToArray())
        {
            if (current <= BudgetBytes)
            {
                break;
            }
            current -= value.Frame!.DecodedBytes;
            _entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public Entry(
            long stamp,
            Task<DecodedViewerFrame> task)
        {
            Stamp = stamp;
            Task = task;
        }

        public long Stamp { get; set; }

        public Task<DecodedViewerFrame> Task { get; }

        public DecodedViewerFrame? Frame { get; set; }
    }
}
