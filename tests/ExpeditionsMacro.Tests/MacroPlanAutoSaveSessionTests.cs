using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class MacroPlanAutoSaveSessionTests
{
    [Fact]
    public async Task RapidChanges_SaveOnlyTheFinalPlan()
    {
        List<MacroPlan> saved = [];
        await using MacroPlanAutoSaveSession session =
            new(
                (plan, _) =>
                {
                    saved.Add(plan);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(40));

        session.Schedule(Plan("First", 1));
        session.Schedule(Plan("Second", 2));
        session.Schedule(Plan("Final", 3));
        await session.FlushAsync();

        MacroPlan actual = Assert.Single(saved);
        Assert.Equal("Final", actual.Name);
        Assert.Equal(
            3,
            actual.Tasks[0].TargetVictories);
    }

    [Fact]
    public async Task SaveInProgress_IsSerializedBeforeNewerState()
    {
        TaskCompletionSource releaseFirst = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        TaskCompletionSource firstStarted = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        List<string> saved = [];
        int active = 0;
        int maximumActive = 0;
        await using MacroPlanAutoSaveSession session =
            new(
                async (plan, _) =>
                {
                    int current =
                        Interlocked.Increment(
                            ref active);
                    maximumActive = Math.Max(
                        maximumActive,
                        current);
                    saved.Add(plan.Name);
                    if (plan.Name == "First")
                    {
                        firstStarted.TrySetResult();
                        await releaseFirst.Task;
                    }
                    Interlocked.Decrement(
                        ref active);
                },
                TimeSpan.Zero);

        Task first =
            session.SaveNowAsync(
                Plan("First", 1));
        await firstStarted.Task
            .WaitAsync(TimeSpan.FromSeconds(2));
        session.Schedule(Plan("Final", 2));
        releaseFirst.SetResult();
        await first;
        await session.FlushAsync();

        Assert.Equal(
            ["First", "Final"],
            saved);
        Assert.Equal(1, maximumActive);
    }

    [Fact]
    public async Task Flush_DrainsNewerRequestQueuedDuringActiveSave()
    {
        TaskCompletionSource firstStarted = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        List<string> saved = [];
        await using MacroPlanAutoSaveSession session =
            new(
                async (plan, _) =>
                {
                    saved.Add(plan.Name);
                    if (plan.Name == "First")
                    {
                        firstStarted.TrySetResult();
                        await releaseFirst.Task;
                    }
                },
                TimeSpan.Zero);

        session.Schedule(Plan("First", 1));
        await firstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        Task flush = session.FlushAsync();
        session.Schedule(Plan("Final", 2));
        releaseFirst.SetResult();
        await flush.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(["First", "Final"], saved);
        Assert.False(session.HasPendingChanges);
    }

    [Fact]
    public async Task SaveFailure_IsReportedAndANewerSaveCanRecover()
    {
        int attempts = 0;
        TaskCompletionSource<Exception> failure = new(
            TaskCreationOptions
                .RunContinuationsAsynchronously);
        List<MacroPlanAutoSaveState> states = [];
        await using MacroPlanAutoSaveSession session =
            new(
                (plan, _) =>
                {
                    if (Interlocked.Increment(
                            ref attempts) == 1)
                    {
                        throw new IOException(
                            "Disk unavailable.");
                    }
                    return Task.CompletedTask;
                },
                TimeSpan.Zero);
        session.StatusChanged += (_, status) =>
        {
            states.Add(status.State);
            if (status.Error is not null)
            {
                failure.TrySetResult(
                    status.Error);
            }
        };

        session.Schedule(Plan("First", 1));
        Exception error = await failure.Task
            .WaitAsync(TimeSpan.FromSeconds(2));
        await session.SaveNowAsync(
            Plan("Recovered", 2));

        Assert.Equal(
            "Disk unavailable.",
            error.Message);
        Assert.Contains(
            MacroPlanAutoSaveState.Failed,
            states);
        Assert.Equal(
            MacroPlanAutoSaveState.Saved,
            states[^1]);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task SaveDelegate_HasNoDependencyOnDashboardValidation()
    {
        bool planSaved = false;
        bool unrelatedValidationCalled = false;
        await using MacroPlanAutoSaveSession session =
            new(
                (plan, _) =>
                {
                    planSaved = true;
                    return Task.CompletedTask;
                },
                TimeSpan.Zero);

        await session.SaveNowAsync(
            Plan("Independent", 1));

        Assert.True(planSaved);
        Assert.False(
            unrelatedValidationCalled);
    }

    [Fact]
    public async Task RenameQueuedDuringSave_ReplacesTheWholeAncestry()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"macro-plan-autosave-{Guid.NewGuid():N}");
        try
        {
            AppPaths paths = new(root);
            paths.EnsureCreated();
            MacroPlanRepository repository =
                new(paths);
            MacroPlan original =
                Plan("Original", 1);
            await repository.SaveAsync(original);
            TaskCompletionSource firstStarted =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);
            TaskCompletionSource releaseFirst =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);
            List<MacroPlanAutoSaveStatusEventArgs>
                savedEvents = [];
            await using
                MacroPlanAutoSaveSession session =
                    new(
                        async (
                            plan,
                            sourcePlanId,
                            token) =>
                        {
                            if (plan.Name == "Intermediate")
                            {
                                firstStarted.TrySetResult();
                                await releaseFirst.Task;
                            }
                            await repository
                                .SaveReplacingAsync(
                                    plan,
                                    sourcePlanId,
                                    token);
                        },
                        TimeSpan.Zero);
            session.StatusChanged += (_, status) =>
            {
                if (status.State ==
                    MacroPlanAutoSaveState.Saved)
                {
                    savedEvents.Add(status);
                }
            };

            session.Schedule(
                Plan("Intermediate", 1),
                original.Id);
            await firstStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));
            session.Schedule(
                Plan("Final name", 1),
                original.Id);
            releaseFirst.SetResult();
            await session.FlushAsync();

            MacroPlan saved = Assert.Single(
                await repository.ListAsync());
            Assert.Equal("Final name", saved.Name);
            Assert.Null(
                await repository.LoadAsync(
                    original.Id));
            Assert.Null(
                await repository.LoadAsync(
                    ModelId.FromName(
                        "Intermediate")));
            MacroPlanAutoSaveStatusEventArgs
                savedStatus =
                    Assert.Single(savedEvents);
            Assert.Equal(
                original.Id,
                savedStatus.SourcePlanId);
            Assert.Equal(
                "Final name",
                savedStatus.Plan.Name);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private static MacroPlan Plan(
        string name,
        int victories) =>
        new()
        {
            Id = ModelId.FromName(name),
            Name = name,
            Tasks =
            [
                new MacroTaskDefinition
                {
                    Id = "challenge",
                    Kind =
                        MacroTaskKind.Challenge,
                    Name =
                        "Challenge rotation",
                    TargetVictories = victories,
                },
            ],
        };

}
