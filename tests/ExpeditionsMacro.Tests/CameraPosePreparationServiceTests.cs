using ExpeditionsMacro.Automation.Camera;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Tests;

public sealed class CameraPosePreparationServiceTests
{
    [Fact]
    public async Task PrepareWithoutYaw_ClampsPoseWithoutYawInput()
    {
        CameraPoseTestAutomation automation = new(
            VisionScorerTests.Pattern(
                RobloxClientProfile.Width,
                RobloxClientProfile.Height));
        CameraPosePreparationService service = new(
            automation,
            () => KeyboardKey.RightControl);

        await service.PrepareWithoutYawAsync();

        Assert.Equal(
            (RobloxClientProfile.Width, RobloxClientProfile.Height),
            automation.ResizeRequest);
        Assert.Equal(30, automation.ZoomTicks);
        Assert.Equal(
            [KeyboardKey.RightControl, KeyboardKey.RightControl],
            automation.ShiftLockKeys);
        Assert.Equal(1, automation.MoveToCenterCount);
        Assert.NotEmpty(automation.Drags);
        Assert.All(
            automation.Drags,
            drag =>
            {
                Assert.Equal(0, drag.X);
                Assert.True(drag.Y > 0);
            });
        Assert.All(
            automation.DragShiftLockStates,
            state => Assert.True(state));
        Assert.False(automation.ShiftLockState);
    }

    [Fact]
    public async Task PrepareWithoutYaw_WhenPitchFails_RestoresShiftLock()
    {
        CameraPoseTestAutomation automation = new(
            VisionScorerTests.Pattern(
                RobloxClientProfile.Width,
                RobloxClientProfile.Height))
        {
            DragFailure =
                new InvalidOperationException("pitch failed"),
        };
        CameraPosePreparationService service = new(
            automation);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareWithoutYawAsync(
                zoomTicks: 5,
                pitchDragPixels: 300,
                settleMilliseconds: 25));

        Assert.Equal(2, automation.ShiftLockKeys.Count);
        Assert.False(automation.ShiftLockState);
    }

    [Fact]
    public async Task PreparePitchOnly_PreservesZoomAndYaw()
    {
        CameraPoseTestAutomation automation = new(
            VisionScorerTests.Pattern(
                RobloxClientProfile.Width,
                RobloxClientProfile.Height));
        CameraPosePreparationService service = new(
            automation,
            () => KeyboardKey.RightControl);
        RobloxWindow window = automation.FindWindow()!.Value;

        await service.PreparePitchOnlyAsync(window);

        Assert.Null(automation.ResizeRequest);
        Assert.Equal(0, automation.ZoomTicks);
        Assert.Equal(
            [KeyboardKey.RightControl, KeyboardKey.RightControl],
            automation.ShiftLockKeys);
        Assert.NotEmpty(automation.Drags);
        Assert.All(
            automation.Drags,
            drag =>
            {
                Assert.Equal(0, drag.X);
                Assert.True(drag.Y > 0);
            });
        Assert.All(
            automation.DragShiftLockStates,
            state => Assert.True(state));
        Assert.False(automation.ShiftLockState);
    }
}
