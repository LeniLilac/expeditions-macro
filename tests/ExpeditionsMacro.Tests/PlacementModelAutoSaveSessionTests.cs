using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementModelAutoSaveSessionTests
{
    [Fact]
    public async Task FlushCoalescesRapidChangesIntoLatestModel()
    {
        List<PlacementModel> saved = [];
        PlacementModelAutoSaveSession session =
            CreateSession(
                (model, _) =>
                {
                    saved.Add(model);
                    return Task.CompletedTask;
                });

        session.ScheduleSave(Model(team: 1));
        session.ScheduleSave(Model(team: 4));

        Assert.True(await session.FlushAsync());
        PlacementModel model =
            Assert.Single(saved);
        Assert.Equal(4, model.TeamSlot);
        Assert.False(session.HasPendingChanges);
    }

    [Fact]
    public async Task SavesRemainSerializedWhenChangeArrivesDuringWrite()
    {
        TaskCompletionSource firstStarted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        List<int> savedTeams = [];
        int active = 0;
        int maximumActive = 0;
        int call = 0;
        PlacementModelAutoSaveSession session =
            CreateSession(
                async (model, _) =>
                {
                    int current =
                        Interlocked.Increment(
                            ref active);
                    maximumActive = Math.Max(
                        maximumActive,
                        current);
                    int index =
                        Interlocked.Increment(
                            ref call);
                    if (index == 1)
                    {
                        firstStarted.SetResult();
                        await releaseFirst.Task;
                    }
                    savedTeams.Add(model.TeamSlot);
                    Interlocked.Decrement(ref active);
                },
                TimeSpan.Zero);

        session.ScheduleSave(Model(team: 2));
        await firstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        session.ScheduleSave(Model(team: 7));
        releaseFirst.SetResult();

        Assert.True(await session.FlushAsync());
        Assert.Equal(1, maximumActive);
        Assert.Equal([2, 7], savedTeams);
    }

    [Fact]
    public async Task ActiveWrite_RemainsPendingAndFlushWaitsForIt()
    {
        TaskCompletionSource started = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        TaskCompletionSource release = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        PlacementModelAutoSaveSession session =
            CreateSession(
                async (_, _) =>
                {
                    started.TrySetResult();
                    await release.Task;
                },
                TimeSpan.Zero);

        session.ScheduleSave(Model(team: 2));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        Assert.True(session.HasPendingChanges);

        Task<bool> flush = session.FlushAsync();
        Assert.False(flush.IsCompleted);
        release.SetResult();

        Assert.True(await flush.WaitAsync(
            TimeSpan.FromSeconds(2)));
        Assert.False(session.HasPendingChanges);
    }

    [Fact]
    public async Task OlderCompletion_IsMarkedStaleAfterNewerSchedule()
    {
        TaskCompletionSource started = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        TaskCompletionSource release = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        PlacementModelAutoSaveSession session =
            CreateSession(
                async (model, _) =>
                {
                    if (model.TeamSlot == 2)
                    {
                        started.TrySetResult();
                        await release.Task;
                    }
                },
                TimeSpan.Zero);
        List<(long Version, bool Latest)> events = [];
        session.Saved += (_, status) =>
            events.Add((
                status.Version,
                session.IsLatestVersion(
                    status.Version)));

        session.ScheduleSave(Model(team: 2));
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        session.ScheduleSave(Model(team: 7));
        release.SetResult();
        Assert.True(await session.FlushAsync());

        Assert.Equal(2, events.Count);
        Assert.False(events[0].Latest);
        Assert.True(events[1].Latest);
        Assert.True(
            events[1].Version >
            events[0].Version);
    }

    [Fact]
    public async Task FailedWriteRemainsPendingForExplicitFlushRetry()
    {
        int attempts = 0;
        PlacementModelAutoSaveSession session =
            CreateSession(
                (_, _) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new IOException(
                            "Storage unavailable.");
                    }
                    return Task.CompletedTask;
                });
        List<string> failures = [];
        session.SaveFailed +=
            (_, e) => failures.Add(
                e.Error.Message);

        session.ScheduleSave(Model(team: 3));

        Assert.False(await session.FlushAsync());
        Assert.True(session.HasPendingChanges);
        Assert.Equal(
            ["Storage unavailable."],
            failures);

        Assert.True(await session.FlushAsync());
        Assert.False(session.HasPendingChanges);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task LatestSaveSupersedesPendingSetupClear()
    {
        List<string> deleted = [];
        List<PlacementModel> saved = [];
        PlacementModelAutoSaveSession session =
            new(
                (model, _) =>
                {
                    saved.Add(model);
                    return Task.CompletedTask;
                },
                (id, _) =>
                {
                    deleted.Add(id);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMinutes(1));

        session.ScheduleDelete("placement-test");
        session.ScheduleSave(Model(team: 8));

        Assert.True(await session.FlushAsync());
        Assert.Empty(deleted);
        Assert.Equal(
            8,
            Assert.Single(saved).TeamSlot);
    }

    [Fact]
    public async Task FlushPersistsPendingDeleteBeforeShutdown()
    {
        List<string> deleted = [];
        PlacementModelAutoSaveSession session =
            new(
                (_, _) => Task.CompletedTask,
                (id, _) =>
                {
                    deleted.Add(id);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMinutes(1));

        session.ScheduleDelete("placement-test");

        Assert.True(await session.FlushAsync());
        Assert.Equal(
            ["placement-test"],
            deleted);
    }

    [Fact]
    public async Task
        PlaybackModeChangesPersistOnlyTheRecordingAssignment()
    {
        string root =
            TestPaths.NewTemporaryDirectory();
        try
        {
            PlacementModelRepository repository =
                new(new AppPaths(root));
            PlacementModelAutoSaveSession session =
                new(
                    repository.SaveAsync,
                    (id, _) =>
                    {
                        repository.Delete(id);
                        return Task.CompletedTask;
                    },
                    TimeSpan.Zero);
            PlacementModel stepMode =
                Model(team: 3);
            PlacementModel recordingMode =
                stepMode with
                {
                    ManualInputRecordingId =
                        "recording-one",
                };

            session.ScheduleSave(recordingMode);
            Assert.True(await session.FlushAsync());
            PlacementModel savedRecording =
                Assert.IsType<PlacementModel>(
                    await repository.LoadAsync(
                        stepMode.Id));
            Assert.Equal(
                "recording-one",
                savedRecording
                    .ManualInputRecordingId);
            Assert.Equal(
                stepMode.Steps,
                savedRecording.Steps);

            session.ScheduleSave(stepMode);
            Assert.True(await session.FlushAsync());
            PlacementModel savedSteps =
                Assert.IsType<PlacementModel>(
                    await repository.LoadAsync(
                        stepMode.Id));
            Assert.Null(
                savedSteps.ManualInputRecordingId);
            Assert.Equal(
                stepMode.Steps,
                savedSteps.Steps);
            Assert.Equal(
                stepMode.TeamSlot,
                savedSteps.TeamSlot);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(
                root);
        }
    }

    private static PlacementModelAutoSaveSession
        CreateSession(
            Func<
                PlacementModel,
                CancellationToken,
                Task> save,
            TimeSpan? debounce = null) =>
        new(
            save,
            (_, _) => Task.CompletedTask,
            debounce ?? TimeSpan.FromMinutes(1));

    private static PlacementModel Model(int team) =>
        new()
        {
            Id = "placement-test",
            Name = "Placement test",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode =
                    PlacementTargetMode.Expedition,
                MapNumber =
                    PlacementSetupCatalog
                        .SharedExpeditionMapNumber,
            },
            TeamSlot = team,
            Steps =
            [
                new PlacementStep
                {
                    UnitKey = 1,
                    X = 200,
                    Y = 300,
                    DelayAfterMilliseconds = 900,
                },
            ],
            CreatedAt =
                DateTimeOffset.UnixEpoch,
            UpdatedAt =
                DateTimeOffset.UnixEpoch,
        };
}
