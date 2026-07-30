using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Stages;

public sealed record StageRuntimeModels(
    PlacementModel? Placement);

public enum StageRunOutcome
{
    Victory,
    Defeat,
    ObjectiveComplete,
}

public sealed record StageWaveObjective
{
    public required int QuestWave { get; init; }

    public int SafeExitWave => QuestWave + 2;

    public void Validate()
    {
        if (QuestWave is not (15 or 30 or 45 or 60))
        {
            throw new InvalidDataException(
                "A Bounty wave objective must target wave 15, 30, 45, or 60.");
        }
    }
}

public sealed record StageRunResult(
    StageRunOutcome Outcome,
    TimeSpan Runtime,
    int Attempts,
    int Victories,
    int Defeats,
    ImageFrame TerminalFrame);
