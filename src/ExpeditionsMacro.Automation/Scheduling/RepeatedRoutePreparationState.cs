namespace ExpeditionsMacro.Automation.Scheduling;

internal sealed class RepeatedRoutePreparationState
{
    private readonly bool _teamSelectionRequired;
    private bool _cameraAligned;
    private bool _repeatStageRequested;

    public RepeatedRoutePreparationState(int teamSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(teamSlot);
        _teamSelectionRequired = teamSlot > 0;
        TeamLoaded = !_teamSelectionRequired;
    }

    public bool TeamLoaded { get; private set; }

    public bool ShouldLoadTeam => !TeamLoaded;

    public bool ShouldAlignCamera(bool arrivedFromRepeatStage) =>
        !arrivedFromRepeatStage || !_cameraAligned;

    public void MarkTeamLoaded() => TeamLoaded = true;

    public void MarkCameraAligned() => _cameraAligned = true;

    public void MarkRepeatStageRequested() => _repeatStageRequested = true;

    public bool ConfirmRepeatStagePrestart()
    {
        bool requested = _repeatStageRequested;
        _repeatStageRequested = false;
        return requested;
    }

    public void Invalidate()
    {
        TeamLoaded = !_teamSelectionRequired;
        _cameraAligned = false;
        _repeatStageRequested = false;
    }
}
