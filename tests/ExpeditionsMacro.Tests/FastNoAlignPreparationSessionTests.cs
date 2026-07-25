using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class FastNoAlignPreparationSessionTests
{
    [Fact]
    public async Task EnsurePrepared_ReusesPoseUntilLobbyIsObserved()
    {
        FakeAutomation automation = Automation();
        FastNoAlignPreparationSession session =
            Session(automation);
        RobloxWindow window =
            Window(processId: 41);

        bool first = await session.EnsurePreparedAsync(
            window,
            zoomTicks: 5,
            pitchDragPixels: 300,
            progress: null,
            CancellationToken.None);
        bool reused = await session.EnsurePreparedAsync(
            window,
            zoomTicks: 5,
            pitchDragPixels: 300,
            progress: null,
            CancellationToken.None);

        Assert.True(first);
        Assert.False(reused);
        Assert.Equal(2, automation.ShiftLockKeys.Count);

        session.ObserveLobby(window);
        bool afterLobby = await session.EnsurePreparedAsync(
            window,
            zoomTicks: 5,
            pitchDragPixels: 300,
            progress: null,
            CancellationToken.None);

        Assert.True(afterLobby);
        Assert.Equal(4, automation.ShiftLockKeys.Count);
    }

    [Fact]
    public async Task EnsurePrepared_DoesNotReuseAcrossRobloxProcesses()
    {
        FakeAutomation automation = Automation();
        FastNoAlignPreparationSession session =
            Session(automation);

        await session.EnsurePreparedAsync(
            Window(processId: 41),
            zoomTicks: 5,
            pitchDragPixels: 300,
            progress: null,
            CancellationToken.None);

        Assert.True(session.IsPrepared(
            Window(processId: 41)));
        Assert.False(session.IsPrepared(
            Window(processId: 42)));
    }

    [Fact]
    public async Task FailedPreparation_IsNeverCached()
    {
        FakeAutomation automation = new(
            VisionScorerTests.Pattern(
                RobloxClientProfile.Width,
                RobloxClientProfile.Height))
        {
            DragFailure =
                new InvalidOperationException("pitch failed"),
        };
        FastNoAlignPreparationSession session =
            Session(automation);
        RobloxWindow window =
            Window(processId: 41);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.EnsurePreparedAsync(
                window,
                zoomTicks: 5,
                pitchDragPixels: 300,
                progress: null,
                CancellationToken.None));

        Assert.False(session.IsPrepared(window));
    }

    private static FakeAutomation Automation() =>
        new(
            VisionScorerTests.Pattern(
                RobloxClientProfile.Width,
                RobloxClientProfile.Height));

    private static FastNoAlignPreparationSession Session(
        FakeAutomation automation) =>
        new(new CameraPosePreparationService(automation));

    private static RobloxWindow Window(int processId) =>
        new(
            (nint)42,
            "Roblox",
            processId,
            "RobloxPlayerBeta");
}
