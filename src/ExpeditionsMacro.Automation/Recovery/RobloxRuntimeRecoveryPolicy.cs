using System.ComponentModel;
using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Recovery;

public static class RobloxRuntimeRecoveryPolicy
{
    public const int MaximumRestartsPerWindow = 3;

    public static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(10);

    public static bool IsRestartCandidate(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is OperationCanceledException or PlayMenuBindingException) return false;
        if (error is RobloxSessionUnavailableException or
            CameraWorldNotRenderedException or
            TimeoutException or
            Win32Exception)
        {
            return true;
        }

        return error is AggregateException aggregate
            ? aggregate.InnerExceptions.Any(IsRestartCandidate)
            : error.InnerException is not null && IsRestartCandidate(error.InnerException);
    }
}

internal sealed class RobloxRestartCircuitBreaker
{
    private readonly Queue<DateTimeOffset> _restarts = [];

    public bool TryReserve(DateTimeOffset now)
    {
        while (_restarts.TryPeek(out DateTimeOffset oldest) &&
            now - oldest >= RobloxRuntimeRecoveryPolicy.RestartWindow)
        {
            _restarts.Dequeue();
        }
        if (_restarts.Count >= RobloxRuntimeRecoveryPolicy.MaximumRestartsPerWindow) return false;
        _restarts.Enqueue(now);
        return true;
    }
}
