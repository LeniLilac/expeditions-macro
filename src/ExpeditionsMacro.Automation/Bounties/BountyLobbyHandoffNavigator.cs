using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Bounties;

internal sealed class BountyLobbyHandoffNavigator
{
    private readonly IRobloxAutomation _automation;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;

    public BountyLobbyHandoffNavigator(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset> utcNow)
    {
        _automation = automation;
        _delay = delay;
        _utcNow = utcNow;
    }

    public async Task EnsureAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ImageFrame current =
            Capture(window, detector);
        if (IsLobby(current, detector))
        {
            return;
        }

        if (PlayInterfaceCloser.DetectLayer(current) !=
            PlayInterfaceLayer.Closed)
        {
            await PlayInterfaceCloser.CloseAsync(
                () => PlayInterfaceCloser.DetectLayer(
                    Capture(window, detector)),
                token =>
                {
                    (int X, int Y) back =
                        StageScreenDetector
                            .SelectorBackAction;
                    return ClickAsync(
                        window,
                        back.X,
                        back.Y,
                        token);
                },
                cancellationToken,
                _delay,
                _utcNow).ConfigureAwait(false);
            current = Capture(window, detector);
            if (IsLobby(current, detector))
            {
                return;
            }
        }

        await new MatchLobbyNavigator(
                _automation,
                _utcNow,
                _delay)
            .ReturnAsync(
                window,
                detector,
                cancellationToken)
            .ConfigureAwait(false);
        current = Capture(window, detector);
        if (!IsLobby(current, detector))
        {
            throw new RobloxUiUnavailableException(
                "Bounty navigation could not prove a stable Lobby after the verified in-match return.");
        }
    }

    private static bool IsLobby(
        ImageFrame frame,
        IDetectorPack detector) =>
        string.Equals(
            detector.RecoveryState(frame),
            "lobby",
            StringComparison.OrdinalIgnoreCase);

    private async Task ClickAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.ClickClientAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame Capture(
        RobloxWindow window,
        IDetectorPack detector)
    {
        Focus(window);
        var bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                detector.Manifest.ClientWidth ||
            bounds.Height !=
                detector.Manifest.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox no longer matches the detector client size during the Bounty Lobby handoff.");
        }
        return _automation.CaptureClient(window);
    }

    private void Focus(RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox during the Bounty Lobby handoff.");
        }
    }
}
