using ExpeditionsMacro.Automation.Scheduling;

namespace ExpeditionsMacro.Tests;

public sealed class RepeatedRoutePreparationStateTests
{
    [Fact]
    public void VerifiedRepeatStage_ReusesTeamAndCameraPreparation()
    {
        RepeatedRoutePreparationState state = new(teamSlot: 6);
        state.MarkTeamLoaded();
        state.MarkCameraAligned();
        state.MarkRepeatStageRequested();

        bool arrivedFromRepeatStage = state.ConfirmRepeatStagePrestart();

        Assert.True(arrivedFromRepeatStage);
        Assert.False(state.ShouldLoadTeam);
        Assert.False(state.ShouldAlignCamera(arrivedFromRepeatStage));
    }

    [Fact]
    public void OrdinaryNavigation_RealignsCameraWithoutReloadingTheTeam()
    {
        RepeatedRoutePreparationState state = new(teamSlot: 3);
        state.MarkTeamLoaded();
        state.MarkCameraAligned();

        Assert.False(state.ShouldLoadTeam);
        Assert.True(state.ShouldAlignCamera(arrivedFromRepeatStage: false));
    }

    [Fact]
    public void Recovery_InvalidatesBothReusablePreparations()
    {
        RepeatedRoutePreparationState state = new(teamSlot: 8);
        state.MarkTeamLoaded();
        state.MarkCameraAligned();
        state.MarkRepeatStageRequested();

        state.Invalidate();

        Assert.True(state.ShouldLoadTeam);
        Assert.True(state.ShouldAlignCamera(arrivedFromRepeatStage: true));
        Assert.False(state.ConfirmRepeatStagePrestart());
    }

    [Fact]
    public void NoConfiguredTeam_RemainsReadyAfterRecovery()
    {
        RepeatedRoutePreparationState state = new(teamSlot: 0);
        state.MarkCameraAligned();

        state.Invalidate();

        Assert.False(state.ShouldLoadTeam);
        Assert.True(state.ShouldAlignCamera(arrivedFromRepeatStage: true));
    }
}
