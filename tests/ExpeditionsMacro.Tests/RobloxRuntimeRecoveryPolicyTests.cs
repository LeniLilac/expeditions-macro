using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Automation.Recovery;
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
                new CameraWorldNotRenderedException(0.02, 1)));
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
                new CameraAlignmentException(
                    "ordinary low confidence",
                    0.4,
                    3)));
        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new PlayMenuBindingException('P')));
        Assert.False(
            RobloxRuntimeRecoveryPolicy.IsRestartCandidate(
                new InvalidDataException("invalid preset")));
    }

    [Fact]
    public void CircuitBreaker_AllowsThreeRestartsPerTenMinutes()
    {
        RobloxRestartCircuitBreaker circuit = new();
        DateTimeOffset start =
            DateTimeOffset.Parse("2026-07-23T00:00:00Z");

        Assert.True(circuit.TryReserve(start));
        Assert.True(circuit.TryReserve(start.AddMinutes(1)));
        Assert.True(circuit.TryReserve(start.AddMinutes(2)));
        Assert.False(circuit.TryReserve(start.AddMinutes(3)));
        Assert.True(circuit.TryReserve(start.AddMinutes(10)));
    }
}
