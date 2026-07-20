namespace ExpeditionsMacro.Automation.Expeditions;

public enum ConfirmationDismissalPhase
{
    Ready,
    AwaitingDismissal,
    Completed,
    Exhausted,
}

public sealed class ConfirmationDismissalState
{
    public const int MaximumAttempts = 3;

    public int Attempts { get; private set; }

    public ConfirmationDismissalPhase Phase { get; private set; } = ConfirmationDismissalPhase.Ready;

    public bool TryBeginAttempt()
    {
        if (Phase != ConfirmationDismissalPhase.Ready) return false;
        if (Attempts >= MaximumAttempts)
        {
            Phase = ConfirmationDismissalPhase.Exhausted;
            return false;
        }

        Attempts++;
        Phase = ConfirmationDismissalPhase.AwaitingDismissal;
        return true;
    }

    public bool TryMarkStillVisible()
    {
        if (Phase != ConfirmationDismissalPhase.AwaitingDismissal) return false;
        Phase = Attempts >= MaximumAttempts ? ConfirmationDismissalPhase.Exhausted : ConfirmationDismissalPhase.Ready;
        return true;
    }

    public bool TryComplete()
    {
        if (Phase != ConfirmationDismissalPhase.AwaitingDismissal) return false;
        Phase = ConfirmationDismissalPhase.Completed;
        return true;
    }
}
