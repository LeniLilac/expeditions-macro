using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxLobbyReadinessGateTests
{
    [Fact]
    public async Task IntermediateStates_DoNotResumeBeforeStableLobby()
    {
        Queue<string?> states = new(
        [
            null,
            "play",
            "map_select",
            "lobby",
            "teleporting",
            "lobby",
            "lobby",
            "lobby",
        ]);
        int captures = 0;

        await RobloxLobbyReadinessGate.WaitAsync(
            _ =>
            {
                captures++;
                return Task.FromResult(Frame());
            },
            _ => states.Dequeue(),
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(8, captures);
        Assert.Empty(states);
    }

    [Fact]
    public async Task TransientCaptureFailure_ResetsLobbyStability()
    {
        Queue<object> observations = new(
        [
            "lobby",
            new InvalidOperationException("capture is changing"),
            "lobby",
            "lobby",
            "lobby",
        ]);
        string? current = null;

        await RobloxLobbyReadinessGate.WaitAsync(
            _ =>
            {
                object next = observations.Dequeue();
                if (next is Exception error) throw error;
                current = (string?)next;
                return Task.FromResult(Frame());
            },
            _ => current,
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Empty(observations);
    }

    [Fact]
    public async Task NonLobbyStates_TimeOutWithoutBecomingReady()
    {
        DateTimeOffset now =
            new(2026, 7, 25, 0, 0, 0, TimeSpan.Zero);

        RobloxSessionUnavailableException error =
            await Assert.ThrowsAsync<RobloxSessionUnavailableException>(
                () => RobloxLobbyReadinessGate.WaitAsync(
                    _ => Task.FromResult(Frame()),
                    _ => "play",
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(1),
                    CancellationToken.None,
                    () => now,
                    (duration, _) =>
                    {
                        now += duration;
                        return Task.CompletedTask;
                    }));

        Assert.Contains(
            "stable lobby",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TwoLobbyFrames_AreNotEnough()
    {
        DateTimeOffset now =
            new(2026, 7, 25, 0, 0, 0, TimeSpan.Zero);
        int observations = 0;

        await Assert.ThrowsAsync<RobloxSessionUnavailableException>(
            () => RobloxLobbyReadinessGate.WaitAsync(
                _ => Task.FromResult(Frame()),
                _ => ++observations <= 2 ? "lobby" : null,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(1),
                CancellationToken.None,
                () => now,
                (duration, _) =>
                {
                    now += duration;
                    return Task.CompletedTask;
                }));

        Assert.Equal(3, observations);
        Assert.Equal(
            3,
            RobloxLobbyReadinessGate.StableLobbyFrames);
    }

    private static ImageFrame Frame() => new(
        1,
        1,
        PixelFormat.Rgb24,
        new byte[3],
        takeOwnership: true);
}
