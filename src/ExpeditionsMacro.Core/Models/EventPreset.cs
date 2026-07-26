namespace ExpeditionsMacro.Core.Models;

public enum EventModeId
{
    VillainInvasion = 1,
}

public enum EventAct
{
    Act1 = 1,
    Act2 = 2,
    Act3 = 3,
    Act4 = 4,
}

public sealed record EventPreset
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public EventModeId Mode { get; init; } =
        EventModeId.VillainInvasion;

    public required EventAct Act { get; init; }

    public required string PlacementModelId { get; init; }

    public int TeamSlot { get; init; }

    public int DefeatRetries { get; init; }

    public bool AutoRecover { get; init; } = true;

    public int ZoomTicks { get; init; } = 30;

    public int PitchDragPixels { get; init; } = 1800;

    public int PollMilliseconds { get; init; } = 450;

    public int StableDetections { get; init; } = 2;

    public int UnitKeyHoldMilliseconds { get; init; } = 110;

    public int UnitSelectDelayMilliseconds { get; init; } = 250;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(PlacementModelId))
        {
            throw new InvalidDataException(
                "Event setup identity is incomplete.");
        }
        if (!Enum.IsDefined(Mode) ||
            !Enum.IsDefined(Act))
        {
            throw new InvalidDataException(
                "The Event mode or act is invalid.");
        }
        if (Act == EventAct.Act4)
        {
            throw new InvalidDataException(
                "Villain Invasion Act 4 is not supported until its placement dataset is available.");
        }
        if (TeamSlot is < 0 or > 8 ||
            DefeatRetries is < 0 or > 20)
        {
            throw new InvalidDataException(
                "Event team or retry settings are out of range.");
        }
        if (ZoomTicks is < 5 or > 80 ||
            PitchDragPixels is < 300 or > 5000)
        {
            throw new InvalidDataException(
                "Event camera preparation settings are out of range.");
        }
        if (PollMilliseconds is < 150 or > 5000 ||
            StableDetections is < 1 or > 5)
        {
            throw new InvalidDataException(
                "Event detection timing is out of range.");
        }
        if (UnitKeyHoldMilliseconds is < 30 or > 1000 ||
            UnitSelectDelayMilliseconds is < 25 or > 5000)
        {
            throw new InvalidDataException(
                "Event placement timing is out of range.");
        }
    }
}
