using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Core.Persistence;

public sealed class BountyStateRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BountyStateRepository(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = paths.BountyStateFile;
    }

    public async Task<BountyProgressState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            BountyProgressState state =
                await JsonFileStore.ReadAsync<BountyProgressState>(
                        _path,
                        cancellationToken)
                    .ConfigureAwait(false) ??
                new BountyProgressState();
            if (state.SchemaVersion == 1)
            {
                state = new BountyProgressState();
                await JsonFileStore.WriteAtomicAsync(
                        _path,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            state.Validate();
            return state.AdvanceDay(
                DateTimeOffset.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        BountyProgressState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await JsonFileStore.WriteAtomicAsync(
                    _path,
                    state with
                    {
                        UpdatedAtUtc =
                            DateTimeOffset.UtcNow,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
