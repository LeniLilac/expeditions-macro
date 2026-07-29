using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Placement;

public interface IPlacementMatchStartPlayback
{
    Task StartAsync(
        RobloxWindow window,
        CancellationToken cancellationToken);
}

public sealed class PlacementMatchStartPlayback :
    IPlacementMatchStartPlayback
{
    private const int StableDetections = 2;
    private readonly IRobloxAutomation _automation;

    public PlacementMatchStartPlayback(
        IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public async Task StartAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        EnsureClient(window);
        await _automation.ParkCursorAsync(
                window,
                cancellationToken)
            .ConfigureAwait(false);
        StableScreenAction<ChallengeScreenMatch>? action =
            await StableScreenActionWaiter.WaitAsync(
                    ChallengeScreenState.Prestart,
                    StableDetections,
                    () => ChallengeScreenDetector.Detect(
                        CaptureClient(window)),
                    static match => match.State,
                    static match =>
                        match.ActionX is int x &&
                        match.ActionY is int y
                            ? (x, y)
                            : null,
                    TimeSpan.FromSeconds(12),
                    TimeSpan.FromMilliseconds(200),
                    cancellationToken)
                .ConfigureAwait(false);
        if (action is null)
        {
            throw new RobloxUiUnavailableException(
                "The Start Game button disappeared before placement test playback could continue.");
        }

        EnsureClient(window);
        await _automation.ClickClientAsync(
                window,
                action.Value.X,
                action.Value.Y,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private ExpeditionsMacro.Core.Imaging.ImageFrame
        CaptureClient(
        RobloxWindow window)
    {
        EnsureClient(window);
        return _automation.CaptureClient(window);
    }

    private void EnsureClient(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                RobloxClientProfile.Width ||
            bounds.Height !=
                RobloxClientProfile.Height)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox no longer matches the required {RobloxClientProfile.Width} by {RobloxClientProfile.Height} client size.");
        }
    }
}
