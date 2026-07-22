using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExpeditionsMacro.DeepDebugViewer.Services;

public sealed class FrameBitmapCache
{
    public const long DefaultBudgetBytes = 10L * 1024 * 1024 * 1024;

    private readonly DeepDebugArchive _archive;
    private readonly object _gate = new();
    private readonly Dictionary<int, CacheItem> _items = [];
    private readonly LinkedList<int> _leastRecentlyUsed = [];
    private long _budgetBytes;
    private long _currentBytes;

    public FrameBitmapCache(DeepDebugArchive archive, long budgetBytes = DefaultBudgetBytes)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        if (budgetBytes < 4) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
        _budgetBytes = budgetBytes;
    }

    public long BudgetBytes
    {
        get
        {
            lock (_gate) return _budgetBytes;
        }
    }

    public long CurrentBytes
    {
        get
        {
            lock (_gate) return _currentBytes;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate) return _items.Count;
        }
    }

    public async Task<BitmapSource> GetAsync(int frameIndex, CancellationToken cancellationToken = default)
    {
        if (frameIndex < 0 || frameIndex >= _archive.Frames.Count) throw new ArgumentOutOfRangeException(nameof(frameIndex));
        lock (_gate)
        {
            if (_items.TryGetValue(frameIndex, out CacheItem? cached))
            {
                Touch(cached);
                return cached.Bitmap;
            }
        }

        byte[] bytes = await _archive.ReadFrameBytesAsync(_archive.Frames[frameIndex], cancellationToken).ConfigureAwait(false);
        DecodedFrame decoded = await Task.Run(() => Decode(bytes), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_items.TryGetValue(frameIndex, out CacheItem? raced))
            {
                Touch(raced);
                return raced.Bitmap;
            }

            LinkedListNode<int> node = _leastRecentlyUsed.AddFirst(frameIndex);
            _items[frameIndex] = new CacheItem(decoded.Bitmap, decoded.DecodedBytes, node);
            _currentBytes += decoded.DecodedBytes;
            EvictToBudget(frameIndex);
        }
        return decoded.Bitmap;
    }

    public void SetBudget(long budgetBytes)
    {
        if (budgetBytes < 4) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
        lock (_gate)
        {
            _budgetBytes = budgetBytes;
            EvictToBudget(protectedFrameIndex: null);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _leastRecentlyUsed.Clear();
            _currentBytes = 0;
        }
    }

    private static DecodedFrame Decode(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        FormatConvertedBitmap converted = new(image, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        long decodedBytes = checked((long)converted.PixelWidth * converted.PixelHeight * 4);
        return new DecodedFrame(converted, decodedBytes);
    }

    private void Touch(CacheItem item)
    {
        _leastRecentlyUsed.Remove(item.Node);
        _leastRecentlyUsed.AddFirst(item.Node);
    }

    private void EvictToBudget(int? protectedFrameIndex)
    {
        while (_currentBytes > _budgetBytes && _items.Count > 1)
        {
            LinkedListNode<int>? candidate = _leastRecentlyUsed.Last;
            if (candidate is null) break;
            if (candidate.Value == protectedFrameIndex)
            {
                candidate = candidate.Previous;
                if (candidate is null) break;
            }

            _leastRecentlyUsed.Remove(candidate);
            if (_items.Remove(candidate.Value, out CacheItem? evicted)) _currentBytes -= evicted.DecodedBytes;
        }
    }

    private sealed record CacheItem(BitmapSource Bitmap, long DecodedBytes, LinkedListNode<int> Node);

    private sealed record DecodedFrame(BitmapSource Bitmap, long DecodedBytes);
}
