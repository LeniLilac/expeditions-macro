using ExpeditionsMacro.Automation.Activity;

namespace ExpeditionsMacro.Tests;

public sealed class InactivityKeepAliveTests
{
    [Fact]
    public async Task Pulse_IsNotSentBeforeEightMinutes()
    {
        MutableTimeProvider time = new();
        InactivityKeepAlive keepAlive = new(time);
        List<char> keys = [];

        time.Advance(InactivityKeepAlive.Interval - TimeSpan.FromMilliseconds(1));
        InactivityPulseResult result = await keepAlive.TryPulseAsync(
            (key, _) =>
            {
                keys.Add(key);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(InactivityPulseResult.NotDue, result);
        Assert.Empty(keys);
    }

    [Fact]
    public async Task Pulse_SendsOEveryEightMinutes()
    {
        MutableTimeProvider time = new();
        InactivityKeepAlive keepAlive = new(time);
        List<char> keys = [];
        Func<char, CancellationToken, Task> pulse = (key, _) =>
        {
            keys.Add(key);
            return Task.CompletedTask;
        };

        time.Advance(InactivityKeepAlive.Interval);
        Assert.Equal(
            InactivityPulseResult.Sent,
            await keepAlive.TryPulseAsync(
                pulse,
                CancellationToken.None));
        Assert.Equal([InactivityKeepAlive.PulseKey], keys);

        time.Advance(InactivityKeepAlive.Interval);
        Assert.Equal(
            InactivityPulseResult.Sent,
            await keepAlive.TryPulseAsync(
                pulse,
                CancellationToken.None));
        Assert.Equal(['O', 'O'], keys);
    }

    [Fact]
    public async Task Pulse_RetriesTransientFailureWithoutStoppingWorkflow()
    {
        MutableTimeProvider time = new();
        InactivityKeepAlive keepAlive = new(time);
        int attempts = 0;

        time.Advance(InactivityKeepAlive.Interval);
        Assert.Equal(
            InactivityPulseResult.Deferred,
            await keepAlive.TryPulseAsync(
                (_, _) =>
                {
                    attempts++;
                    throw new InvalidOperationException("Focus changed.");
                },
                CancellationToken.None));

        time.Advance(InactivityKeepAlive.FailureRetryInterval);
        Assert.Equal(
            InactivityPulseResult.Sent,
            await keepAlive.TryPulseAsync(
                (_, _) =>
                {
                    attempts++;
                    return Task.CompletedTask;
                },
                CancellationToken.None));
        Assert.Equal(2, attempts);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now =
            new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
