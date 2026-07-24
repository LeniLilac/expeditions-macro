using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class MacroRunTotalsTests
{
    [Fact]
    public async Task ReportersForDifferentModesShareMacroRuntimeAndOutcomes()
    {
        DateTimeOffset now =
            new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        MacroRunTotals totals = new(now, () => now);
        RecordingNotifier notifier = new();
        DiscordRunReporter expedition = Reporter(
            notifier,
            "Expeditions Macro",
            totals);

        now += TimeSpan.FromMinutes(10);
        await expedition.SendAsync(
            "victory",
            "Expedition completed.",
            screenshot: null,
            runtime: TimeSpan.FromMinutes(2),
            victories: 1,
            defeats: 0,
            new DiscordRunTarget(1, 3, string.Empty),
            CancellationToken.None,
            matchRuntime: TimeSpan.FromMinutes(2));

        now += TimeSpan.FromMinutes(5);
        DiscordRunReporter story = Reporter(
            notifier,
            "Story Macro",
            totals);
        await story.SendAsync(
            "started",
            "Story is starting.",
            screenshot: null,
            runtime: TimeSpan.Zero,
            victories: 0,
            defeats: 0,
            new DiscordRunTarget(0, 0, "Act 2"),
            CancellationToken.None);
        await story.SendAsync(
            "defeat",
            "Story ended in Defeat.",
            screenshot: null,
            runtime: TimeSpan.FromSeconds(30),
            victories: 0,
            defeats: 1,
            new DiscordRunTarget(0, 0, "Act 2"),
            CancellationToken.None,
            matchRuntime: TimeSpan.FromSeconds(30));

        Assert.Collection(
            notifier.Notifications,
            notification =>
            {
                Assert.Equal(TimeSpan.FromMinutes(10), notification.Runtime);
                Assert.Equal(1, notification.Victories);
                Assert.Equal(0, notification.Defeats);
                Assert.Equal(TimeSpan.FromMinutes(2), notification.MatchRuntime);
            },
            notification =>
            {
                Assert.Equal(TimeSpan.FromMinutes(15), notification.Runtime);
                Assert.Equal(1, notification.Victories);
                Assert.Equal(0, notification.Defeats);
            },
            notification =>
            {
                Assert.Equal(TimeSpan.FromMinutes(15), notification.Runtime);
                Assert.Equal(1, notification.Victories);
                Assert.Equal(1, notification.Defeats);
                Assert.Equal(TimeSpan.FromSeconds(30), notification.MatchRuntime);
            });
    }

    private static DiscordRunReporter Reporter(
        IDiscordNotifier notifier,
        string name,
        MacroRunTotals totals) =>
        new(
            notifier,
            "https://discord.com/api/webhooks/123/token",
            name,
            name,
            (_, _, _, _) => { },
            totals);

    private sealed class RecordingNotifier : IDiscordNotifier
    {
        public List<DiscordNotification> Notifications { get; } = [];

        public Task SendAsync(
            DiscordNotification notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
