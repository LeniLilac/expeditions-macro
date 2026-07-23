using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Camera;
using ExpeditionsMacro.Vision.Infrastructure;

namespace ExpeditionsMacro.Tests;

public sealed class CameraWorldReadinessTests
{
    private static readonly ScreenRegion[] Regions =
    [
        new(96, 110, 166, 108),
        new(546, 110, 166, 108),
        new(96, 250, 166, 108),
        new(321, 250, 166, 108),
    ];

    [Fact]
    public void BlueVoid_IsRejectedWhileRenderedMapRemainsReady()
    {
        string fixtures = Path.Combine(
            TestPaths.RepositoryRoot,
            "datasets",
            "anime-expeditions",
            "camera-rotations");
        ImageFrame goal = ImageCodec.Load(
            Path.Combine(
                fixtures,
                "Expedition_Map1",
                "goal.png"));
        ImageFrame blueVoid = ImageCodec.Load(
            Path.Combine(
                fixtures,
                "UnrenderedWorld",
                "blue-void.png"));
        ImageFrame wrongYaw = ImageCodec.Load(
            Path.Combine(
                fixtures,
                "Expedition_Map1",
                "wrong-yaw.png"));
        ImageFrame reference =
            CameraRegionAnalyzer.BuildComposite(goal, Regions);

        CameraWorldReadinessResult rendered =
            CameraWorldReadiness.Evaluate(reference, reference);
        CameraWorldReadinessResult renderedWrongYaw =
            CameraWorldReadiness.Evaluate(
                reference,
                CameraRegionAnalyzer.BuildComposite(
                    wrongYaw,
                    Regions));
        CameraWorldReadinessResult missing =
            CameraWorldReadiness.Evaluate(
                reference,
                CameraRegionAnalyzer.BuildComposite(
                    blueVoid,
                    Regions));

        Assert.True(rendered.IsReady);
        Assert.True(renderedWrongYaw.IsReady);
        Assert.False(missing.IsReady);
        Assert.True(
            missing.CurrentTexture < rendered.CurrentTexture * 0.20,
            $"Missing texture {missing.CurrentTexture:P1}, rendered {rendered.CurrentTexture:P1}.");
    }
}
