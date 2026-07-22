using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Discord;

public sealed record DiscordRunTarget(int MapNumber, int Difficulty, string Route);

public sealed class DiscordRunReporter
{
    private readonly IDiscordNotifier _notifier;
    private readonly string _webhookUrl;
    private readonly string _macroName;
    private readonly string _attachmentPrefix;
    private readonly Action<string, MacroEventLevel, string?, double?> _log;
    private readonly object _pendingGate = new();
    private readonly HashSet<Task> _pending = [];

    public DiscordRunReporter(
        IDiscordNotifier notifier,
        string webhookUrl,
        string macroName,
        string attachmentPrefix,
        Action<string, MacroEventLevel, string?, double?> log)
    {
        _notifier = notifier;
        _webhookUrl = webhookUrl;
        _macroName = macroName;
        _attachmentPrefix = attachmentPrefix;
        _log = log;
    }

    public void Queue(
        string eventName,
        string detail,
        ImageFrame? screenshot,
        TimeSpan runtime,
        int victories,
        int defeats,
        DiscordRunTarget target,
        TimeSpan? matchRuntime = null)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl)) return;
        _log($"Queued Discord {eventName} notification.", MacroEventLevel.Information, null, null);
        DiscordNotification notification = CreateNotification(
            eventName,
            detail,
            screenshot,
            runtime,
            victories,
            defeats,
            target,
            matchRuntime);
        Task sendTask = Task.Run(() => SendCoreAsync(notification, eventName, CancellationToken.None));
        lock (_pendingGate) _pending.Add(sendTask);
        _ = sendTask.ContinueWith(
            completed =>
            {
                lock (_pendingGate) _pending.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async Task SendAsync(
        string eventName,
        string detail,
        ImageFrame? screenshot,
        TimeSpan runtime,
        int victories,
        int defeats,
        DiscordRunTarget target,
        CancellationToken cancellationToken,
        TimeSpan? matchRuntime = null)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl)) return;
        DiscordNotification notification = CreateNotification(
            eventName,
            detail,
            screenshot,
            runtime,
            victories,
            defeats,
            target,
            matchRuntime);
        await SendCoreAsync(notification, eventName, cancellationToken).ConfigureAwait(false);
    }

    public async Task FlushAsync()
    {
        Task[] pending;
        lock (_pendingGate) pending = _pending.ToArray();
        if (pending.Length == 0) return;

        Task all = Task.WhenAll(pending);
        Task completed = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);
        if (completed != all)
        {
            _log($"Stopped with {pending.Count(task => !task.IsCompleted)} Discord notification(s) still in flight.", MacroEventLevel.Warning, null, null);
            return;
        }
        await all.ConfigureAwait(false);
    }

    private DiscordNotification CreateNotification(
        string eventName,
        string detail,
        ImageFrame? screenshot,
        TimeSpan runtime,
        int victories,
        int defeats,
        DiscordRunTarget target,
        TimeSpan? matchRuntime) => new()
        {
            WebhookUrl = _webhookUrl,
            Event = eventName,
            Runtime = runtime,
            MatchRuntime = matchRuntime,
            Victories = victories,
            Defeats = defeats,
            MapNumber = target.MapNumber,
            Difficulty = target.Difficulty,
            Detail = detail,
            MacroName = _macroName,
            Route = target.Route,
            AttachmentPrefix = _attachmentPrefix,
            Screenshot = screenshot?.Clone(),
        };

    private async Task SendCoreAsync(
        DiscordNotification notification,
        string eventName,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.SendAsync(notification, cancellationToken).ConfigureAwait(false);
            _log($"Discord {eventName} notification sent.", MacroEventLevel.Success, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            _log($"Discord {eventName} notification failed: {error.Message}", MacroEventLevel.Warning, "discord", null);
        }
    }
}
