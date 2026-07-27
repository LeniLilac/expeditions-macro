using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class ManualInputRouteServiceTests
{
    [Fact]
    public async Task Play_LoadsSelectedRecordingAndUsesExactWindow()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ManualInputRecordingRepository repository =
                new(paths);
            ManualInputRecording recording =
                ManualInputRecordingTests
                    .ValidRecording();
            await repository.SaveAsync(recording);
            FakePlayback playback = new();
            ManualInputRouteService service =
                new(repository, playback);
            RobloxWindow window =
                new(
                    (nint)42,
                    "Roblox",
                    101,
                    "RobloxPlayerBeta");

            await service.PlayAsync(
                window,
                Placement(recording.Id));

            Assert.Equal(1, playback.Calls);
            Assert.Equal(window, playback.Window);
            Assert.Equal(
                recording.Id,
                playback.Recording?.Id);
            Assert.Equal(
                recording.Events.Count,
                playback.Recording?.Events.Count);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task Play_FailsClearlyWhenRecordingWasDeleted()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            ManualInputRouteService service =
                new(
                    new ManualInputRecordingRepository(
                        new AppPaths(root)),
                    new FakePlayback());

            InvalidDataException error =
                await Assert.ThrowsAsync<
                    InvalidDataException>(
                    () => service.PlayAsync(
                        new RobloxWindow(
                            (nint)42,
                            "Roblox"),
                        Placement("missing")));

            Assert.Contains(
                "no longer exists",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task Resolve_FreezesRecordingBeforePlayback()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            ManualInputRecordingRepository repository =
                new(paths);
            ManualInputRecording recording =
                ManualInputRecordingTests
                    .ValidRecording();
            await repository.SaveAsync(recording);
            FakePlayback playback = new();
            ManualInputRouteService service =
                new(repository, playback);

            ManualInputRecording resolved =
                await service.ResolveAsync(
                    Placement(recording.Id));
            repository.Delete(recording.Id);
            await service.PlayAsync(
                new RobloxWindow(
                    (nint)42,
                    "Roblox"),
                resolved);

            Assert.Equal(recording.Id, resolved.Id);
            Assert.Same(resolved, playback.Recording);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task MatchPlayback_StartsClockAtPlaybackBoundary()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            ManualInputRecording recording =
                ManualInputRecordingTests
                    .ValidRecording();
            FakePlayback playback = new();
            ManualInputRouteService service =
                new(
                    new ManualInputRecordingRepository(
                        new AppPaths(root)),
                    playback);

            System.Diagnostics.Stopwatch runtime =
                await ManualInputMatchPlayback.PlayAsync(
                    service,
                    new RobloxWindow(
                        (nint)42,
                        "Roblox"),
                    recording,
                    progress: null,
                    matchStarting: null,
                    CancellationToken.None);

            Assert.True(runtime.IsRunning);
            Assert.True(playback.StartSignaled);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task MatchPlayback_RejectsPlaybackThatNeverSignalsStart()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            ManualInputRecording recording =
                ManualInputRecordingTests
                    .ValidRecording();
            FakePlayback playback = new()
            {
                SignalStart = false,
            };
            ManualInputRouteService service =
                new(
                    new ManualInputRecordingRepository(
                        new AppPaths(root)),
                    playback);

            InvalidOperationException error =
                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => ManualInputMatchPlayback.PlayAsync(
                        service,
                        new RobloxWindow(
                            (nint)42,
                            "Roblox"),
                        recording,
                        progress: null,
                        matchStarting: null,
                        CancellationToken.None));

            Assert.Contains(
                "match clock",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    [Fact]
    public async Task ResolveContract_FailsBeforeInputWhenPlaybackIsUnavailable()
    {
        InvalidOperationException error =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => ManualInputMatchPlayback.ResolveAsync(
                    service: null,
                    Placement("manual-route"),
                    CancellationToken.None));

        Assert.Contains(
            "unavailable",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PlacementModel Placement(
        string recordingId) =>
        new()
        {
            Id = "manual-placement",
            Name = "Manual placement",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode =
                    PlacementTargetMode.Expedition,
                MapNumber = 0,
                ActNumber = 0,
            },
            ManualInputRecordingId =
                recordingId,
            Steps = [],
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private sealed class FakePlayback :
        IManualInputPlayback
    {
        public int Calls { get; private set; }

        public RobloxWindow Window { get; private set; }

        public ManualInputRecording? Recording
        {
            get;
            private set;
        }

        public bool SignalStart { get; init; } = true;

        public bool StartSignaled { get; private set; }

        public Task PlayAsync(
            RobloxWindow window,
            ManualInputRecording recording,
            CancellationToken cancellationToken,
            Action? playbackStarting = null)
        {
            Calls++;
            Window = window;
            Recording = recording;
            if (SignalStart)
            {
                playbackStarting?.Invoke();
                StartSignaled =
                    playbackStarting is not null;
            }
            return Task.CompletedTask;
        }
    }
}
