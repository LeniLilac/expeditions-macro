using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Automation.Teams;

internal sealed class TeamListOpeningTransaction
{
    private const int StableOwnerObservations = 2;
    private const int TeamsTabClickAttempts = 2;
    private static readonly TimeSpan OwnerTimeout =
        TimeSpan.FromSeconds(6);
    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public TeamListOpeningTransaction(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
    }

    public async Task OpenAsync(
        RobloxWindow window,
        char unitMenuKey,
        CancellationToken cancellationToken)
    {
        EnsureFocus(window);
        await _automation.TapLetterKeyAsync(
            window,
            char.ToUpperInvariant(unitMenuKey),
            cancellationToken).ConfigureAwait(false);

        OwnerObservation owner =
            await WaitForOwnerAsync(
                window,
                cancellationToken)
                .ConfigureAwait(false);
        int clickAttempts = 0;
        while (owner.Match.State == TeamScreenState.Units &&
               clickAttempts < TeamsTabClickAttempts)
        {
            if (owner.Match.ActionX is not int x ||
                owner.Match.ActionY is not int y)
            {
                throw new RobloxUiUnavailableException(
                    "The live Teams action could not be located on the verified Unit Inventory.");
            }

            EnsureFocus(window);
            await _automation.ClickClientAsync(
                window,
                x,
                y,
                cancellationToken).ConfigureAwait(false);
            clickAttempts++;
            owner = await WaitForOwnerAsync(
                window,
                cancellationToken).ConfigureAwait(false);
        }

        if (owner.Match.State != TeamScreenState.Teams)
        {
            throw new RobloxUiUnavailableException(
                $"The Teams tab did not open after {clickAttempts} verified click attempts; Unit Inventory remained open.");
        }
    }

    private async Task<OwnerObservation> WaitForOwnerAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        OwnerObservation? candidate = null;
        TeamScreenMatch last =
            new(TeamScreenState.None, 0);
        int consecutive = 0;
        ObservationWaitBudget budget = new(
            OwnerTimeout,
            StableOwnerObservations,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending:
                       consecutive > 0 &&
                       consecutive <
                           StableOwnerObservations))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame image = CaptureClient(window);
            last = TeamScreenDetector.Detect(image);
            OwnerObservation? observed =
                ObserveOwnedState(image, last);
            if (observed is OwnerObservation current)
            {
                if (candidate is OwnerObservation prior &&
                    SameEvidence(prior, current))
                {
                    consecutive++;
                }
                else
                {
                    candidate = current;
                    consecutive = 1;
                }

                if (consecutive >= StableOwnerObservations)
                {
                    return current;
                }
            }
            else
            {
                candidate = null;
                consecutive = 0;
            }

            budget.MarkObserved();
            await _delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            $"The Unit Inventory-to-Teams transition did not expose a stable owned screen. Last state: {last.State} ({last.Confidence:P0}).");
    }

    private static OwnerObservation? ObserveOwnedState(
        ImageFrame image,
        TeamScreenMatch match)
    {
        if (match.State == TeamScreenState.Units &&
            match.ActionX is not null &&
            match.ActionY is not null)
        {
            return new OwnerObservation(match, null);
        }

        if (match.State == TeamScreenState.Teams &&
            TeamScreenDetector.FindScrollbarThumb(image) is
                TeamScrollbarThumb thumb)
        {
            return new OwnerObservation(match, thumb);
        }

        return null;
    }

    private static bool SameEvidence(
        OwnerObservation left,
        OwnerObservation right)
    {
        if (left.Match.State != right.Match.State)
        {
            return false;
        }

        if (left.Match.State == TeamScreenState.Units)
        {
            return left.Match.ActionX == right.Match.ActionX &&
                left.Match.ActionY == right.Match.ActionY;
        }

        return left.Thumb is TeamScrollbarThumb leftThumb &&
            right.Thumb is TeamScrollbarThumb rightThumb &&
            Math.Abs(leftThumb.X - rightThumb.X) <= 2 &&
            Math.Abs(leftThumb.CenterY - rightThumb.CenterY) <= 2 &&
            Math.Abs(leftThumb.Height - rightThumb.Height) <= 3;
    }

    private ImageFrame CaptureClient(
        RobloxWindow window)
    {
        EnsureFocus(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != TeamScreenDetector.ClientWidth ||
            bounds.Height != TeamScreenDetector.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox no longer matches the required {TeamScreenDetector.ClientWidth} by {TeamScreenDetector.ClientHeight} client size while opening Teams.");
        }

        return _automation.CaptureClient(window);
    }

    private void EnsureFocus(RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while opening Teams.");
        }
    }

    private readonly record struct OwnerObservation(
        TeamScreenMatch Match,
        TeamScrollbarThumb? Thumb);
}
