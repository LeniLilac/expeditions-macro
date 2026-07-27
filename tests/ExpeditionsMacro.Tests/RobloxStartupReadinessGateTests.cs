using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxStartupReadinessGateTests
{
    private const double LobbyThreshold = 0.80;

    [Fact]
    public async Task
        Scale120FieldScores_ReachPreflightWithoutStrictLobbyClassification()
    {
        Queue<RobloxStartupReadinessObservation> observations = new(
        [
            Unknown(lobby: 0.287649, other: 0.220539),
            Unknown(lobby: 0.345898, other: 0.341489),
            Unknown(lobby: 0.361553, other: 0.336520),
            Unknown(lobby: 0.072789, other: 0.086106),
            Unknown(lobby: 0.149630, other: 0.235624),
            Unknown(lobby: 0.112311, other: 0.340852),
            Unknown(lobby: 0.782338, other: 0.468555),
            Unknown(lobby: 0.782338, other: 0.470346),
            Unknown(lobby: 0.781705, other: 0.465290),
        ]);
        int observed = 0;

        await RobloxStartupReadinessGate.WaitAsync(
            _ =>
            {
                observed++;
                return Task.FromResult(observations.Dequeue());
            },
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(9, observed);
        Assert.Empty(observations);
    }

    [Fact]
    public async Task RecognizedNonLobbyState_ResetsStartupStability()
    {
        Queue<RobloxStartupReadinessObservation> observations = new(
        [
            Unknown(lobby: 0.781, other: 0.46),
            new("play", 0.781, 0.46, LobbyThreshold),
            Unknown(lobby: 0.781, other: 0.46),
            Unknown(lobby: 0.781, other: 0.46),
            Unknown(lobby: 0.781, other: 0.46),
        ]);

        await RobloxStartupReadinessGate.WaitAsync(
            _ => Task.FromResult(observations.Dequeue()),
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Empty(observations);
    }

    [Fact]
    public async Task AmbiguousNearLobbyScore_TimesOutWithoutStartingPreflight()
    {
        DateTimeOffset now =
            new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

        RobloxSessionUnavailableException error =
            await Assert.ThrowsAsync<RobloxSessionUnavailableException>(
                () => RobloxStartupReadinessGate.WaitAsync(
                    _ => Task.FromResult(
                        Unknown(
                            lobby: 0.781,
                            other: 0.700)),
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
            "startup-check",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactLobby_RemainsReadyWithoutNearThresholdFallback()
    {
        Assert.True(
            RobloxStartupReadinessGate.IsReady(
                new RobloxStartupReadinessObservation(
                    "lobby",
                    0,
                    1,
                    LobbyThreshold)));
    }

    private static RobloxStartupReadinessObservation Unknown(
        double lobby,
        double other) =>
        new(
            ClassifiedState: null,
            LobbyScore: lobby,
            StrongestOtherScore: other,
            LobbyThreshold: LobbyThreshold);
}
