using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class PlacementModelAutoSaveEventArgs(
    string modelId,
    PlacementModel? model,
    long version) : EventArgs
{
    public string ModelId { get; } = modelId;

    public PlacementModel? Model { get; } = model;

    public long Version { get; } = version;
}

public sealed class PlacementModelAutoSaveFailedEventArgs(
    string modelId,
    long version,
    Exception error) : EventArgs
{
    public string ModelId { get; } = modelId;

    public long Version { get; } = version;

    public Exception Error { get; } = error;
}

public sealed class PlacementModelAutoSaveSession
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _writeGate =
        new(1, 1);
    private readonly Func<
        PlacementModel,
        CancellationToken,
        Task> _save;
    private readonly Func<
        string,
        CancellationToken,
        Task> _delete;
    private readonly TimeSpan _debounce;
    private PendingWrite? _pending;
    private PendingWrite? _inFlight;
    private CancellationTokenSource? _delayCancellation;
    private long _latestVersion;

    public PlacementModelAutoSaveSession(
        PlacementModelRepository repository,
        TimeSpan? debounce = null)
        : this(
            repository.SaveAsync,
            (id, _) =>
            {
                repository.Delete(id);
                return Task.CompletedTask;
            },
            debounce)
    {
    }

    public PlacementModelAutoSaveSession(
        Func<
            PlacementModel,
            CancellationToken,
            Task> save,
        Func<
            string,
            CancellationToken,
            Task> delete,
        TimeSpan? debounce = null)
    {
        _save = save ??
            throw new ArgumentNullException(nameof(save));
        _delete = delete ??
            throw new ArgumentNullException(nameof(delete));
        _debounce = debounce ??
            TimeSpan.FromMilliseconds(250);
        if (_debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debounce));
        }
    }

    public event EventHandler<
        PlacementModelAutoSaveEventArgs>? Saved;

    public event EventHandler<
        PlacementModelAutoSaveFailedEventArgs>? SaveFailed;

    public bool HasPendingChanges
    {
        get
        {
            lock (_sync)
            {
                return _pending is not null ||
                    _inFlight is not null;
            }
        }
    }

    public bool IsLatestVersion(long version)
    {
        lock (_sync)
        {
            return version == _latestVersion;
        }
    }

    public void ScheduleSave(PlacementModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Validate();
        Schedule(model.Id, model);
    }

    public void ScheduleDelete(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            modelId);
        Schedule(modelId, model: null);
    }

    public async Task<bool> FlushAsync(
        CancellationToken cancellationToken = default)
    {
        CancelDelay();
        while (true)
        {
            bool succeeded =
                await PersistPendingAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            if (!succeeded)
            {
                return false;
            }

            lock (_sync)
            {
                if (_pending is null)
                {
                    return true;
                }
            }
        }
    }

    private void Schedule(
        string modelId,
        PlacementModel? model)
    {
        CancellationTokenSource cancellation =
            new();
        lock (_sync)
        {
            PendingWrite write = new(
                modelId,
                model,
                ++_latestVersion);
            _pending = write;
            _delayCancellation?.Cancel();
            _delayCancellation = cancellation;
        }

        _ = PersistAfterDelayAsync(
            cancellation);
    }

    private async Task PersistAfterDelayAsync(
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(
                    _debounce,
                    cancellation.Token)
                .ConfigureAwait(false);
            await PersistPendingAsync(
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(
                        _delayCancellation,
                        cancellation))
                {
                    _delayCancellation = null;
                }
            }
            cancellation.Dispose();
        }
    }

    private async Task<bool> PersistPendingAsync(
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);
        PendingWrite? write;
        try
        {
            lock (_sync)
            {
                write = _pending;
                _pending = null;
                _inFlight = write;
            }
            if (write is null)
            {
                return true;
            }

            try
            {
                if (write.Model is null)
                {
                    await _delete(
                            write.ModelId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _save(
                            write.Model,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                lock (_sync)
                {
                    _inFlight = null;
                    if (_pending is null ||
                        _pending.Version <
                            write.Version)
                    {
                        _pending = write;
                    }
                }
                if (error is OperationCanceledException)
                {
                    throw;
                }
                PublishFailure(write, error);
                return false;
            }

            lock (_sync)
            {
                _inFlight = null;
            }
            PublishSaved(write);
            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void CancelDelay()
    {
        lock (_sync)
        {
            _delayCancellation?.Cancel();
            _delayCancellation = null;
        }
    }

    private void PublishSaved(PendingWrite write)
    {
        try
        {
            Saved?.Invoke(
                this,
                new PlacementModelAutoSaveEventArgs(
                    write.ModelId,
                    write.Model,
                    write.Version));
        }
        catch
        {
            // Persistence must not be broken by a UI status subscriber.
        }
    }

    private void PublishFailure(
        PendingWrite write,
        Exception error)
    {
        try
        {
            SaveFailed?.Invoke(
                this,
                new PlacementModelAutoSaveFailedEventArgs(
                    write.ModelId,
                    write.Version,
                    error));
        }
        catch
        {
            // Persistence must not be broken by a UI status subscriber.
        }
    }

    private sealed record PendingWrite(
        string ModelId,
        PlacementModel? Model,
        long Version);
}
