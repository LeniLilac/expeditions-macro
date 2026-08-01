namespace ExpeditionsMacro.Core.Models;

public sealed record ChallengeRotationProgress
{
    public DateTimeOffset? Epoch { get; init; }

    public IReadOnlyList<ChallengeType> Attempted { get; init; } = [];

    public DateTimeOffset? PreviousAllCooldownEpoch { get; init; }

    public DateTimeOffset? DailyLimitUntilUtc { get; init; }

    public void Validate()
    {
        if (Attempted.Any(type => !Enum.IsDefined(type)) ||
            Attempted.Distinct().Count() != Attempted.Count)
        {
            throw new InvalidDataException(
                "Saved Challenge rotation attempts are invalid.");
        }
        if (Attempted.Count > 0 && Epoch is null)
        {
            throw new InvalidDataException(
                "Saved Challenge attempts require their reset epoch.");
        }
        if (PreviousAllCooldownEpoch is not null &&
            Epoch is null)
        {
            throw new InvalidDataException(
                "Saved Challenge cooldown evidence requires its reset epoch.");
        }
    }
}
