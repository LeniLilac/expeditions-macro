namespace ExpeditionsMacro.Core.Models;

public enum MatchStepKind
{
    Placement,
    ReconfigureUnit,
    Delay,
    UpgradeUnit,
    StartGame,
}

public enum MatchAutoUpgradeAction
{
    NoChange,
    Disable,
    Priority1,
    Priority2,
    Priority3,
    Priority4,
    Priority5,
    Priority6,
}
