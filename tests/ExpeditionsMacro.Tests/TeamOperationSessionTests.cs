using ExpeditionsMacro.Automation.Teams;
using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Tests;

public sealed class TeamOperationSessionTests
{
    [Fact]
    public void SameProcessAndTeam_AreReusedWithinOneOperation()
    {
        TeamOperationSession session = new();
        RobloxWindow first = Window(
            handle: 10,
            processId: 42);
        session.MarkLoaded(first, teamSlot: 3);

        Assert.True(
            session.IsLoaded(
                Window(handle: 11, processId: 42),
                teamSlot: 3));
        Assert.False(
            session.IsLoaded(first, teamSlot: 4));
    }

    [Fact]
    public void ReplacedRobloxProcess_InvalidatesTheKnownTeam()
    {
        TeamOperationSession session = new();
        session.MarkLoaded(
            Window(handle: 10, processId: 42),
            teamSlot: 6);

        Assert.False(
            session.IsLoaded(
                Window(handle: 10, processId: 43),
                teamSlot: 6));
        Assert.False(
            session.IsLoaded(
                Window(handle: 10, processId: 42),
                teamSlot: 6));
    }

    [Fact]
    public void DifferentVerifiedTeam_ReplacesTheKnownSlot()
    {
        RobloxWindow window = Window(
            handle: 10,
            processId: 42);
        TeamOperationSession session = new();
        session.MarkLoaded(window, teamSlot: 3);

        session.MarkLoaded(window, teamSlot: 7);

        Assert.False(
            session.IsLoaded(window, teamSlot: 3));
        Assert.True(
            session.IsLoaded(window, teamSlot: 7));
    }

    [Fact]
    public void FailedTeamSwitch_LeavesTheOperationTeamUnknown()
    {
        RobloxWindow window = Window(
            handle: 10,
            processId: 42);
        TeamOperationSession session = new();
        session.MarkLoaded(window, teamSlot: 3);

        session.BeginSelection(teamSlot: 7);

        Assert.False(
            session.IsLoaded(window, teamSlot: 3));
        Assert.False(
            session.IsLoaded(window, teamSlot: 7));
    }

    [Fact]
    public void MissingProcessIdentity_FallsBackToTheWindowHandle()
    {
        TeamOperationSession session = new();
        session.MarkLoaded(
            Window(handle: 10, processId: 0),
            teamSlot: 2);

        Assert.True(
            session.IsLoaded(
                Window(handle: 10, processId: 0),
                teamSlot: 2));
        Assert.False(
            session.IsLoaded(
                Window(handle: 11, processId: 0),
                teamSlot: 2));
    }

    [Fact]
    public void NewOperation_DoesNotReuseThePreviousTeam()
    {
        RobloxWindow window = Window(
            handle: 10,
            processId: 42);
        TeamOperationSession previous = new();
        previous.MarkLoaded(window, teamSlot: 5);

        TeamOperationSession next = new();

        Assert.False(
            next.IsLoaded(window, teamSlot: 5));
        Assert.True(
            next.IsLoaded(window, teamSlot: 0));
    }

    private static RobloxWindow Window(
        nint handle,
        int processId) =>
        new(
            handle,
            "Roblox",
            processId,
            "RobloxPlayerBeta");
}
