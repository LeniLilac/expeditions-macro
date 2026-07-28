using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

internal enum ChallengeNavigationInputOwner
{
    GameModeSelector,
    PostMatchPreview,
    AfkRecovery,
    DisconnectRecovery,
}

internal readonly record struct ChallengeNavigationInputAttempt(
    ChallengeNavigationInputOwner Owner,
    int X,
    int Y,
    int Number);

internal sealed class ChallengeNavigationInputGate
{
    internal const int MaximumAttemptsPerOwner = 3;

    private readonly StableNavigationActionTracker<
        ChallengeNavigationInputOwner> _stability;
    private ChallengeNavigationInputOwner? _attemptOwner;
    private int _attempts;

    public ChallengeNavigationInputGate(int stableDetections)
    {
        _stability = new(
            Math.Max(2, stableDetections));
    }

    public bool HasPendingCandidate =>
        _stability.HasPendingCandidate;

    public RobloxUiUnavailableException? ExhaustedError =>
        _attemptOwner is { } owner &&
        _attempts >= MaximumAttemptsPerOwner
            ? new RobloxUiUnavailableException(
                FailureMessage(owner))
            : null;

    public ChallengeNavigationInputAttempt? Observe(
        ChallengeScreenMatch match,
        string? recovery,
        IDetectorPack detector,
        ImageFrame frame,
        CancellationToken cancellationToken) =>
        Observe(
            ResolveOwner(match.State, recovery),
            ResolveAction(match, recovery, detector, frame),
            cancellationToken);

    internal ChallengeNavigationInputAttempt? Observe(
        ChallengeNavigationInputOwner? owner,
        (int X, int Y)? action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (owner is null || action is null)
        {
            _stability.Reset();
            return null;
        }

        (int X, int Y)? stableAction =
            _stability.Update(owner.Value, action);
        if (stableAction is null)
        {
            return null;
        }

        if (_attemptOwner != owner)
        {
            _attemptOwner = owner;
            _attempts = 0;
        }
        if (_attempts >= MaximumAttemptsPerOwner)
        {
            throw ExhaustedError!;
        }

        _attempts++;
        _stability.Reset();
        return new ChallengeNavigationInputAttempt(
            owner.Value,
            stableAction.Value.X,
            stableAction.Value.Y,
            _attempts);
    }

    private static string FailureMessage(
        ChallengeNavigationInputOwner owner) =>
        owner switch
        {
            ChallengeNavigationInputOwner.GameModeSelector =>
                "The verified game-mode selector remained unchanged after 3 Challenge tile attempts.",
            ChallengeNavigationInputOwner.PostMatchPreview =>
                "The verified Challenge post-match preview remained unchanged after 3 Change Gamemode attempts.",
            ChallengeNavigationInputOwner.AfkRecovery =>
                "The verified AFK Chamber remained unchanged after 3 Return to Lobby attempts.",
            ChallengeNavigationInputOwner.DisconnectRecovery =>
                "The verified Disconnect screen remained unchanged after 3 Reconnect attempts.",
            _ =>
                "Challenge navigation remained unchanged after 3 verified input attempts.",
        };

    private static ChallengeNavigationInputOwner? ResolveOwner(
        ChallengeScreenState state,
        string? recovery)
    {
        if (state == ChallengeScreenState.GameModeSelector)
        {
            return ChallengeNavigationInputOwner.GameModeSelector;
        }
        if (state == ChallengeScreenState.PostMatchPreview)
        {
            return ChallengeNavigationInputOwner.PostMatchPreview;
        }
        if (recovery == "play")
        {
            return ChallengeNavigationInputOwner.GameModeSelector;
        }
        return recovery switch
        {
            "afk" => ChallengeNavigationInputOwner.AfkRecovery,
            "disconnect" =>
                ChallengeNavigationInputOwner.DisconnectRecovery,
            _ => null,
        };
    }

    private static (int X, int Y)? ResolveAction(
        ChallengeScreenMatch match,
        string? recovery,
        IDetectorPack detector,
        ImageFrame frame)
    {
        if (match.State is
            ChallengeScreenState.GameModeSelector or
            ChallengeScreenState.PostMatchPreview)
        {
            return match.ActionX is int x &&
                match.ActionY is int y
                    ? (x, y)
                    : null;
        }
        if (recovery == "play")
        {
            return (480, 205);
        }
        return recovery is "afk" or "disconnect"
            ? detector.ActionFor(recovery, frame)
            : null;
    }
}
