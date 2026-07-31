namespace ExpeditionsMacro.Automation.Bounties;

public sealed class BountyGoldUnavailableException
    : Exception
{
    public BountyGoldUnavailableException()
        : base(
            "Bounty rerolling stopped because this account has less than 1,000 Gold.")
    {
    }
}
