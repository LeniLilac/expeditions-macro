namespace ExpeditionsMacro.Core.Models;

public sealed record PlacementAdvancedSettings
{
    public const int MaximumActionDelayMilliseconds =
        60_000;

    public const int MaximumPlaybackStartDelayMilliseconds =
        300_000;

    public bool Enabled { get; init; }

    public int UnitSelectionDelayMilliseconds { get; init; } =
        250;

    public int PlacementBurstDurationMilliseconds { get; init; } =
        50;

    public int BeforeSelectionClickMilliseconds { get; init; }

    public int BeforeSelectedUnitProofMilliseconds { get; init; }

    public int ActionKeyIntervalMilliseconds { get; init; } =
        100;

    public bool VerifySelectedUnitPanelBeforeActions
    {
        get;
        init;
    } = true;

    public bool VerifyPrestartBeforeManualPlayback
    {
        get;
        init;
    } = true;

    public int ManualPlaybackStartDelayMilliseconds
    {
        get;
        init;
    }

    public void Validate()
    {
        ValidateActionDelay(
            UnitSelectionDelayMilliseconds,
            "Unit selection delay");
        ValidateActionDelay(
            PlacementBurstDurationMilliseconds,
            "Placement burst duration");
        ValidateActionDelay(
            BeforeSelectionClickMilliseconds,
            "Before-selection delay");
        ValidateActionDelay(
            BeforeSelectedUnitProofMilliseconds,
            "Selected-unit proof delay");
        ValidateActionDelay(
            ActionKeyIntervalMilliseconds,
            "Action key interval");
        if (ManualPlaybackStartDelayMilliseconds is < 0 or
            > MaximumPlaybackStartDelayMilliseconds)
        {
            throw new InvalidDataException(
                $"Manual playback start delay must be 0 through {MaximumPlaybackStartDelayMilliseconds} ms.");
        }
    }

    private static void ValidateActionDelay(
        int value,
        string label)
    {
        if (value is < 0 or
            > MaximumActionDelayMilliseconds)
        {
            throw new InvalidDataException(
                $"{label} must be 0 through {MaximumActionDelayMilliseconds} ms.");
        }
    }
}
