namespace ExpeditionsMacro.Automation.Updates;

public sealed class ApplicationUpdateSession : IDisposable
{
    private readonly ApplicationUpdateService _service;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logWarning;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _activeCancellation;

    public ApplicationUpdateSession(
        ApplicationUpdateService service,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null)
    {
        _service = service;
        _logInfo = logInfo ?? (_ => { });
        _logWarning = logWarning ?? (_ => { });
    }

    public ApplicationUpdateState State { get; private set; } =
        ApplicationUpdateState.Idle;

    public string ChannelDescription =>
        _service.ChannelDescription;

    public event EventHandler? StateChanged;

    public async Task InitializeAsync(
        bool autoCheck,
        CancellationToken cancellationToken = default)
    {
        (StagedApplicationUpdate Stage, string Path)? recovered =
            await _service.RecoverStagedAsync(
                cancellationToken).ConfigureAwait(false);
        if (recovered is not null)
        {
            ApplicationSemanticVersion version =
                ApplicationSemanticVersion.Parse(
                    recovered.Value.Stage.Version);
            Uri releaseUri = new(
                recovered.Value.Stage.ReleaseUri);
            SetState(new ApplicationUpdateState(
                ApplicationUpdatePhase.Ready,
                $"Version {version} is downloaded and ready to install.",
                InstallerPath: recovered.Value.Path,
                Version: version,
                ReleaseUri: releaseUri));
            _logInfo(
                $"Recovered verified application update {version}.");
            return;
        }

        if (autoCheck)
        {
            await CheckAsync(
                automatic: true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task CheckAsync(
        CancellationToken cancellationToken = default) =>
        CheckAsync(
            automatic: false,
            cancellationToken);

    public async Task DownloadAsync(
        CancellationToken cancellationToken = default)
    {
        ApplicationUpdateRelease? release = State.Release;
        if (release is null ||
            State.Phase != ApplicationUpdatePhase.Available)
        {
            throw new InvalidOperationException(
                "Check for an available update before downloading it.");
        }

        await RunExclusiveAsync(
            async token =>
            {
                CallbackProgress progress = new(value =>
                    SetState(new ApplicationUpdateState(
                        ApplicationUpdatePhase.Downloading,
                        $"Downloading version {release.Version}…",
                        release,
                        Progress: value,
                        Version: release.Version,
                        ReleaseUri: release.ReleaseUri)));
                SetState(new ApplicationUpdateState(
                    ApplicationUpdatePhase.Downloading,
                    $"Downloading version {release.Version}…",
                    release,
                    Progress: 0,
                    Version: release.Version,
                    ReleaseUri: release.ReleaseUri));
                string installer =
                    await _service.DownloadInstallerAsync(
                        release,
                        progress,
                        token).ConfigureAwait(false);
                SetState(new ApplicationUpdateState(
                    ApplicationUpdatePhase.Ready,
                    $"Version {release.Version} is downloaded and ready to install.",
                    release,
                    installer,
                    Progress: 1,
                    Version: release.Version,
                    ReleaseUri: release.ReleaseUri));
                _logInfo(
                    $"Verified application update {release.Version} and staged its installer.");
            },
            cancellationToken,
            cancellationState: new ApplicationUpdateState(
                ApplicationUpdatePhase.Available,
                $"Version {release.Version} is available.",
                release,
                Version: release.Version,
                ReleaseUri: release.ReleaseUri)).ConfigureAwait(false);
    }

    public async Task<string> VerifyReadyInstallerAsync(
        CancellationToken cancellationToken = default)
    {
        if (State.Phase != ApplicationUpdatePhase.Ready)
        {
            throw new InvalidOperationException(
                "No application update is ready to install.");
        }
        (StagedApplicationUpdate Stage, string Path)? recovered =
            await _service.RecoverStagedAsync(
                cancellationToken).ConfigureAwait(false);
        if (recovered is null)
        {
            SetState(new ApplicationUpdateState(
                ApplicationUpdatePhase.Error,
                "The staged installer no longer matches the verified release. Download it again."));
            throw new InvalidDataException(
                "The staged installer failed verification.");
        }
        return recovered.Value.Path;
    }

    public void Cancel() => _activeCancellation?.Cancel();

    public void Dispose()
    {
        _activeCancellation?.Cancel();
        if (_operationGate.Wait(0))
        {
            _activeCancellation?.Dispose();
            _operationGate.Dispose();
            _service.Dispose();
        }
    }

    private async Task CheckAsync(
        bool automatic,
        CancellationToken cancellationToken)
    {
        if (State.Phase == ApplicationUpdatePhase.Ready)
        {
            return;
        }

        await RunExclusiveAsync(
            async token =>
            {
                SetState(new ApplicationUpdateState(
                    ApplicationUpdatePhase.Checking,
                    "Checking GitHub for application updates…"));
                ApplicationUpdateRelease? release =
                    await _service.CheckAsync(token)
                        .ConfigureAwait(false);
                if (release is null)
                {
                    SetState(new ApplicationUpdateState(
                        ApplicationUpdatePhase.Current,
                        $"Version {_service.CurrentVersion} is current on the {_service.ChannelDescription.ToLowerInvariant()}."));
                    _logInfo(
                        $"Application update check completed; {_service.CurrentVersion} is current.");
                    return;
                }

                SetState(new ApplicationUpdateState(
                    ApplicationUpdatePhase.Available,
                    $"Version {release.Version} is available.",
                    release,
                    Version: release.Version,
                    ReleaseUri: release.ReleaseUri));
                _logInfo(
                    $"Application update {release.Version} is available.");
            },
            cancellationToken,
            cancellationState: ApplicationUpdateState.Idle,
            automatic).ConfigureAwait(false);
    }

    private async Task RunExclusiveAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken,
        ApplicationUpdateState cancellationState,
        bool automatic = false)
    {
        await _operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            _activeCancellation = linked;
            try
            {
                await action(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (linked.IsCancellationRequested)
            {
                SetState(cancellationState with
                {
                    Message = "Update operation canceled.",
                });
            }
            catch (Exception error)
            {
                _logWarning(
                    $"Application update operation failed ({error.GetType().Name}).");
                SetState(new ApplicationUpdateState(
                    ApplicationUpdatePhase.Error,
                    automatic
                        ? "Automatic update check could not reach or verify GitHub. Use Check now to retry."
                        : UserFailureMessage(error)));
            }
            finally
            {
                if (ReferenceEquals(
                        _activeCancellation,
                        linked))
                {
                    _activeCancellation = null;
                }
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void SetState(ApplicationUpdateState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string UserFailureMessage(Exception error) =>
        error switch
        {
            InvalidDataException or
                System.Text.Json.JsonException or
                FormatException =>
                "GitHub's release metadata or downloaded files could not be verified.",
            HttpRequestException =>
                "GitHub could not be reached or returned an unsuccessful response.",
            IOException or UnauthorizedAccessException =>
                "The update files could not be read or written in local storage.",
            _ =>
                "The update operation could not be completed.",
        };

    private sealed class CallbackProgress(
        Action<double> callback) : IProgress<double>
    {
        public void Report(double value) => callback(value);
    }
}
