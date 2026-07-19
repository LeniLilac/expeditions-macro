namespace ExpeditionsMacro.Automation.Expeditions;

public enum ExtractionTransactionPhase
{
    Ready,
    AwaitingConfirmation,
    AwaitingDismissal,
    Completed,
}

public sealed class ExtractionTransactionState
{
    public ExtractionTransactionPhase Phase { get; private set; } = ExtractionTransactionPhase.Ready;

    public bool TryBegin()
    {
        if (Phase != ExtractionTransactionPhase.Ready) return false;
        Phase = ExtractionTransactionPhase.AwaitingConfirmation;
        return true;
    }

    public bool TryConfirm()
    {
        if (Phase != ExtractionTransactionPhase.AwaitingConfirmation) return false;
        Phase = ExtractionTransactionPhase.AwaitingDismissal;
        return true;
    }

    public bool TryComplete()
    {
        if (Phase != ExtractionTransactionPhase.AwaitingDismissal) return false;
        Phase = ExtractionTransactionPhase.Completed;
        return true;
    }
}
