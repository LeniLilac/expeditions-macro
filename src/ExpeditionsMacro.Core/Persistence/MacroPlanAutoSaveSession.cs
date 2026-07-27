using System.Threading.Channels;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public enum MacroPlanAutoSaveState
{
    Pending,
    Saving,
    Saved,
    Failed,
}

public sealed class MacroPlanAutoSaveStatusEventArgs(
    MacroPlanAutoSaveState state,
    MacroPlan plan,
    string? sourcePlanId,
    long version,
    Exception? error = null) : EventArgs
{
    public MacroPlanAutoSaveState State { get; } = state;

    public MacroPlan Plan { get; } = plan;

    public string? SourcePlanId { get; } = sourcePlanId;

    public long Version { get; } = version;

    public Exception? Error { get; } = error;
}

public sealed class MacroPlanAutoSaveSession :
    IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Func<
        MacroPlan,
        string?,
        CancellationToken,
        Task> _saveAsync;
    private readonly TimeSpan _debounceDelay;
    private readonly Channel<SaveRequest> _requests;
    private readonly Task _worker;
    private MacroPlan? _latestPlan;
    private string? _latestSourcePlanId;
    private long _latestVersion;
    private long _savedVersion;
    private readonly PersistedPlanReplacementLineage
        _replacementLineage = new();
    private bool _disposed;

    public MacroPlanAutoSaveSession(
        Func<
            MacroPlan,
            CancellationToken,
            Task> saveAsync,
        TimeSpan? debounceDelay = null)
        : this(
            (plan, _, token) =>
                saveAsync(plan, token),
            debounceDelay)
    {
    }

    public MacroPlanAutoSaveSession(
        Func<
            MacroPlan,
            string?,
            CancellationToken,
            Task> saveAsync,
        TimeSpan? debounceDelay = null)
    {
        _saveAsync = saveAsync ??
            throw new ArgumentNullException(
                nameof(saveAsync));
        _debounceDelay =
            debounceDelay ??
            TimeSpan.FromMilliseconds(350);
        if (_debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debounceDelay));
        }
        _requests =
            Channel.CreateUnbounded<SaveRequest>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                });
        _worker = RunWorkerAsync();
    }

    public event EventHandler<
        MacroPlanAutoSaveStatusEventArgs>?
        StatusChanged;

    public bool HasPendingChanges
    {
        get
        {
            lock (_sync)
            {
                return _latestVersion >
                    _savedVersion;
            }
        }
    }

    public void Schedule(MacroPlan plan)
        => Schedule(plan, sourcePlanId: null);

    public void Schedule(
        MacroPlan plan,
        string? sourcePlanId)
    {
        SaveRequest request =
            CreateRequest(
                plan,
                sourcePlanId,
                immediate: false,
                completion: null);
        Publish(
            MacroPlanAutoSaveState.Pending,
            request);
        Write(request);
    }

    public Task SaveNowAsync(
        MacroPlan plan,
        CancellationToken cancellationToken =
            default) =>
        SaveNowAsync(
            plan,
            sourcePlanId: null,
            cancellationToken);

    public Task SaveNowAsync(
        MacroPlan plan,
        string? sourcePlanId,
        CancellationToken cancellationToken =
            default)
    {
        TaskCompletionSource completion = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        SaveRequest request =
            CreateRequest(
                plan,
                sourcePlanId,
                immediate: true,
                completion);
        Publish(
            MacroPlanAutoSaveState.Pending,
            request);
        Write(request);
        return completion.Task.WaitAsync(
            cancellationToken);
    }

    public async Task FlushAsync(
        CancellationToken cancellationToken =
            default)
    {
        while (true)
        {
            MacroPlan? plan;
            string? sourcePlanId;
            long version;
            lock (_sync)
            {
                ThrowIfDisposed();
                plan = _latestPlan;
                sourcePlanId =
                    _latestSourcePlanId;
                version = _latestVersion;
                if (version <= _savedVersion)
                {
                    return;
                }
            }
            if (plan is null)
            {
                return;
            }

            TaskCompletionSource completion = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
            Write(new SaveRequest(
                plan,
                sourcePlanId,
                version,
                Immediate: true,
                completion));
            await completion.Task.WaitAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _requests.Writer.TryComplete();
        }
        await _worker.ConfigureAwait(false);
    }

    private SaveRequest CreateRequest(
        MacroPlan plan,
        string? sourcePlanId,
        bool immediate,
        TaskCompletionSource? completion)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        lock (_sync)
        {
            ThrowIfDisposed();
            _latestPlan = plan;
            _latestSourcePlanId =
                sourcePlanId;
            _latestVersion++;
            return new SaveRequest(
                plan,
                sourcePlanId,
                _latestVersion,
                immediate,
                completion);
        }
    }

    private void Write(SaveRequest request)
    {
        if (!_requests.Writer.TryWrite(request))
        {
            request.Completion?.TrySetException(
                new ObjectDisposedException(
                    nameof(
                        MacroPlanAutoSaveSession)));
            throw new ObjectDisposedException(
                nameof(MacroPlanAutoSaveSession));
        }
    }

    private async Task RunWorkerAsync()
    {
        List<SaveRequest> batch = [];
        try
        {
            while (await _requests.Reader
                       .WaitToReadAsync()
                       .ConfigureAwait(false))
            {
                Drain(batch);
                bool immediate =
                    batch.Any(request =>
                        request.Immediate);
                while (!immediate &&
                       await WaitForMoreAsync()
                           .ConfigureAwait(false))
                {
                    Drain(batch);
                    immediate =
                        batch.Any(request =>
                            request.Immediate);
                }

                SaveRequest latest =
                    batch.MaxBy(request =>
                        request.Version)!;
                long savedVersion = SavedVersion();
                if (latest.Version <= savedVersion)
                {
                    CompleteThrough(
                        batch,
                        savedVersion);
                    batch.Clear();
                    continue;
                }

                Publish(
                    MacroPlanAutoSaveState.Saving,
                    latest);
                try
                {
                    string? persistedSourceId =
                        _replacementLineage.Resolve(
                            latest.SourcePlanId);
                    await _saveAsync(
                            latest.Plan,
                            persistedSourceId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    _replacementLineage.Register(
                        latest.SourcePlanId,
                        persistedSourceId,
                        latest.Plan.Id);
                    MarkSaved(latest.Version);
                    savedVersion = latest.Version;
                    CompleteThrough(
                        batch,
                        savedVersion);
                    if (IsLatest(savedVersion))
                    {
                        Publish(
                            MacroPlanAutoSaveState
                                .Saved,
                            latest);
                    }
                }
                catch (Exception error)
                {
                    FailThrough(
                        batch,
                        latest.Version,
                        error);
                    if (IsLatest(latest.Version))
                    {
                        Publish(
                            MacroPlanAutoSaveState
                                .Failed,
                            latest,
                            error);
                    }
                }
                batch.Clear();
            }
        }
        catch (Exception error)
        {
            FailThrough(
                batch,
                long.MaxValue,
                error);
            while (_requests.Reader.TryRead(
                       out SaveRequest? request))
            {
                request?.Completion
                    ?.TrySetException(error);
            }
        }
    }

    private async Task<bool> WaitForMoreAsync()
    {
        using CancellationTokenSource wait =
            new();
        Task<bool> available = _requests.Reader
            .WaitToReadAsync(wait.Token)
            .AsTask();
        Task delay = Task.Delay(_debounceDelay);
        Task completed = await Task.WhenAny(
                available,
                delay)
            .ConfigureAwait(false);
        if (ReferenceEquals(completed, available))
        {
            return await available
                .ConfigureAwait(false);
        }
        wait.Cancel();
        try
        {
            await available.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        return false;
    }

    private void Drain(List<SaveRequest> batch)
    {
        while (_requests.Reader.TryRead(
                   out SaveRequest? request))
        {
            if (request is not null)
            {
                batch.Add(request);
            }
        }
    }

    private bool IsLatest(long version)
    {
        lock (_sync)
        {
            return version == _latestVersion;
        }
    }

    private long SavedVersion()
    {
        lock (_sync)
        {
            return _savedVersion;
        }
    }

    private void MarkSaved(long version)
    {
        lock (_sync)
        {
            _savedVersion = Math.Max(
                _savedVersion,
                version);
        }
    }

    private static void CompleteThrough(
        IEnumerable<SaveRequest> requests,
        long version)
    {
        foreach (SaveRequest request in requests
                     .Where(request =>
                         request.Version <= version))
        {
            request.Completion
                ?.TrySetResult();
        }
    }

    private static void FailThrough(
        IEnumerable<SaveRequest> requests,
        long version,
        Exception error)
    {
        foreach (SaveRequest request in requests
                     .Where(request =>
                         request.Version <= version))
        {
            request.Completion
                ?.TrySetException(error);
        }
    }

    private void Publish(
        MacroPlanAutoSaveState state,
        SaveRequest request,
        Exception? error = null)
    {
        try
        {
            StatusChanged?.Invoke(
                this,
                new MacroPlanAutoSaveStatusEventArgs(
                    state,
                    request.Plan,
                    request.SourcePlanId,
                    request.Version,
                    error));
        }
        catch
        {
            // Persistence must not be broken by a UI status subscriber.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(MacroPlanAutoSaveSession));
        }
    }

    private sealed record SaveRequest(
        MacroPlan Plan,
        string? SourcePlanId,
        long Version,
        bool Immediate,
        TaskCompletionSource? Completion);
}
