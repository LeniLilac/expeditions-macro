using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

internal readonly record struct StageNavigationActionIdentity(
    string State,
    string Action);

internal sealed class StageNavigationTransactionState
{
    internal const int MaximumAttemptsPerAction = 3;

    private StageNavigationActionIdentity? _verified;
    private int _attempts;
    private bool _confirmationPending;

    internal int Attempts => _attempts;

    internal bool ConfirmationPending => _confirmationPending;

    internal static StageNavigationActionIdentity?
        ForVerifiedNavigation(
            StageScreenState? state,
            bool hasChangeModeAction) =>
        state switch
        {
            StageScreenState.PostMatchPreview
                when hasChangeModeAction =>
                    new(
                        StageScreenState.PostMatchPreview.ToString(),
                        "Change Gamemode"),
            StageScreenState.StorySelector or
            StageScreenState.RaidSelector or
            StageScreenState.PreviewReady =>
                new(state.Value.ToString(), "Back"),
            _ => null,
        };

    internal void ObserveVerifiedState(string state)
    {
        if (_verified is not { } current ||
            current.State.Equals(
                state,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _verified = null;
        _attempts = 0;
        _confirmationPending = false;
    }

    internal void ObserveVerified(
        StageNavigationActionIdentity identity)
    {
        if (_verified != identity)
        {
            _verified = identity;
            _attempts = 0;
            _confirmationPending = false;
            return;
        }

        _confirmationPending = false;
    }

    internal int BeginAttempt(
        StageNavigationActionIdentity identity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_verified != identity)
        {
            throw new InvalidOperationException(
                "Stage navigation input requires a verified owner state and action.");
        }
        if (_confirmationPending)
        {
            throw new InvalidOperationException(
                "Stage navigation is still waiting for the previous input to be acknowledged.");
        }
        if (_attempts >= MaximumAttemptsPerAction)
        {
            throw new RobloxUiUnavailableException(
                $"Stage navigation remained on '{identity.State}' after " +
                $"{MaximumAttemptsPerAction} verified '{identity.Action}' attempts. " +
                "Roblox did not acknowledge the action or finish loading the next screen.");
        }

        _attempts++;
        _confirmationPending = true;
        return _attempts;
    }
}
