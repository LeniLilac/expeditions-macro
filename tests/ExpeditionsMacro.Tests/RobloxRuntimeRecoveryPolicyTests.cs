using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class RobloxRuntimeRecoveryPolicyTests
{
    [Fact]
    public void RestartCandidates_AreLimitedToRuntimeAndSessionFailures()
    {
        Assert.True(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new RobloxSessionUnavailableException("capture failed")));
        Assert.True(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new RobloxUiUnavailableException(
                    "verified team UI stopped responding")));
        Assert.True(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new TimeoutException("navigation stalled")));
        Assert.True(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new InvalidOperationException(
                    "wrapper",
                    new TimeoutException("nested stall"))));

        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new PlayMenuBindingException('P')));
        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new RobloxDisplayScaleException(125)));
        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new ManualInputPlaybackTimingException(
                    scheduledMicroseconds: 100_000,
                    actualMicroseconds: 2_100_000,
                    ManualInputEventKind.MouseButtonDown,
                    inputWasSent: true)));
        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new InvalidDataException("invalid preset")));
    }

    [Fact]
    public void CircuitBreaker_AllowsTenRestartsPerTenMinutes()
    {
        RobloxRestartCircuitBreaker circuit = new();
        DateTimeOffset start =
            DateTimeOffset.Parse("2026-07-23T00:00:00Z");

        for (int restart = 0; restart < 10; restart++)
        {
            Assert.True(
                circuit.TryReserve(
                    start.AddSeconds(
                        restart)));
        }
        Assert.False(
            circuit.TryReserve(
                start.AddMinutes(9)));
        Assert.True(circuit.TryReserve(start.AddMinutes(10)));
    }
}
