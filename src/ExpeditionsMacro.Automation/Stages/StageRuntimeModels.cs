using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Stages;

public sealed record StageRuntimeModels(
    CameraModel Camera,
    PlacementModel? PrestartPlacement,
    PlacementModel? DelayedPlacement);

public enum StageRunOutcome
{
    Victory,
    Defeat,
}

public sealed record StageRunResult(
    StageRunOutcome Outcome,
    TimeSpan Runtime,
    int Attempts,
    int Victories,
    int Defeats,
    ImageFrame TerminalFrame);
