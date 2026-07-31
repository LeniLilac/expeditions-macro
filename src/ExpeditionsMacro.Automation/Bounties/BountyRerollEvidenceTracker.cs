namespace ExpeditionsMacro.Automation.Bounties;

internal sealed class BountyRerollEvidenceTracker
{
    internal const int OrdinaryRerollLimit = 100;
    internal const int UnchangedMythicLimit = 4;

    private int _ordinaryRerolls;
    private int? _rerolledMythic;
    private int _unchangedMythicRerolls;

    public bool ObserveOrdinaryReroll()
    {
        _rerolledMythic = null;
        _unchangedMythicRerolls = 0;
        _ordinaryRerolls++;
        return _ordinaryRerolls >=
            OrdinaryRerollLimit;
    }

    public bool ObserveConfirmedMythic(int number)
    {
        _ordinaryRerolls = 0;
        if (_rerolledMythic == number)
        {
            _unchangedMythicRerolls++;
        }
        else
        {
            _unchangedMythicRerolls = 0;
        }
        return _unchangedMythicRerolls >=
            UnchangedMythicLimit;
    }

    public void MarkMythicRerolled(int number)
    {
        _rerolledMythic = number;
    }
}
