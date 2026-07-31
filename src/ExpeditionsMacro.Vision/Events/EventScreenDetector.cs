using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Vision.Events;

public enum EventScreenState
{
    None,
    EventCatalog,
    EventHome,
    ActSelector,
    ActDetail,
    PreviewReady,
    Prestart,
    Victory,
    Defeat,
    GameModeSelector,
}

public readonly record struct EventScreenMatch(
    EventScreenState State,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null);

public static class EventScreenDetector
{
    private static readonly ScreenRegion EventHeader =
        new(0, 55, 180, 55);
    private static readonly ScreenRegion ActScrollRail =
        new(190, 548, 610, 25);

    public static EventScreenMatch Detect(
        ImageFrame image)
    {
        ValidateClient(image);
        StageScreenMatch shared =
            StageScreenDetector.Detect(image);
        EventScreenMatch? sharedState =
            shared.State switch
            {
                StageScreenState.PreviewReady =>
                    new(
                        EventScreenState.PreviewReady,
                        shared.Confidence,
                        shared.ActionX,
                        shared.ActionY),
                StageScreenState.Prestart =>
                    new(
                        EventScreenState.Prestart,
                        shared.Confidence,
                        shared.ActionX,
                        shared.ActionY),
                StageScreenState.Victory =>
                    new(
                        EventScreenState.Victory,
                        shared.Confidence,
                        shared.ActionX,
                        shared.ActionY),
                StageScreenState.Defeat =>
                    new(
                        EventScreenState.Defeat,
                        shared.Confidence,
                        shared.ActionX,
                        shared.ActionY),
                StageScreenState.GameModeSelector =>
                    new(
                        EventScreenState.GameModeSelector,
                        shared.Confidence),
                _ => null,
            };
        if (sharedState is not null)
        {
            return Trace(sharedState.Value);
        }

        double eventContext = EventContextScore(image);
        if (shared.State == StageScreenState.RaidDetail &&
            eventContext >= 0.72)
        {
            return Trace(
                new EventScreenMatch(
                    EventScreenState.ActDetail,
                    Math.Min(
                        shared.Confidence,
                        eventContext),
                    shared.ActionX,
                    shared.ActionY));
        }

        double actSelector = ActSelectorScore(
            image,
            eventContext);
        if (actSelector >= 0.72)
        {
            return Trace(
                new EventScreenMatch(
                    EventScreenState.ActSelector,
                    actSelector));
        }

        double eventCatalog =
            EventEntryDetector.CatalogScore(image);
        if (eventCatalog >= 0.72)
        {
            return Trace(
                new EventScreenMatch(
                    EventScreenState.EventCatalog,
                    eventCatalog,
                    94,
                    183));
        }

        // Decorative Event chrome can finish rendering after both live owned
        // controls. Do not block navigation once the selected Villain tab and
        // Event Gamemode action independently agree.
        double eventHome =
            EventEntryDetector.HomeScore(image);
        return Trace(
            eventHome >= 0.72
                ? new EventScreenMatch(
                    EventScreenState.EventHome,
                    eventHome,
                    499,
                    571)
                : new EventScreenMatch(
                    EventScreenState.None,
                    Math.Max(
                        Math.Max(
                            eventHome,
                            eventCatalog),
                        actSelector)));
    }

    public static EventScreenMatch DetectMatchState(
        ImageFrame image)
    {
        ValidateClient(image);
        StageScreenMatch shared =
            StageScreenDetector.DetectMatchState(image);
        EventScreenMatch result =
            shared.State switch
            {
                StageScreenState.Victory =>
                    new EventScreenMatch(
                        EventScreenState.Victory,
                        shared.Confidence),
                StageScreenState.Defeat =>
                    new EventScreenMatch(
                        EventScreenState.Defeat,
                        shared.Confidence),
                StageScreenState.GameModeSelector =>
                    new EventScreenMatch(
                        EventScreenState.GameModeSelector,
                        shared.Confidence),
                _ => new EventScreenMatch(
                    EventScreenState.None,
                    shared.Confidence),
            };
        VisionTrace.Emit(
            "event_match_screen",
            result.State.ToString(),
            result.Confidence,
            new
            {
                SharedState = shared.State,
                shared.Confidence,
            });
        return result;
    }

    public static (int X, int Y)
        LobbyEventAction => (50, 410);

    public static (int X, int Y)
        EventGameModeAction => (499, 571);

    public static (int X, int Y)
        SelectStageAction => (238, 437);

    public static bool RequiresLaterActScroll(
        EventAct act) =>
        act is EventAct.Act3 or
            EventAct.Act4;

    public static (
        int StartX,
        int StartY,
        int EndX,
        int EndY) LaterActScroll =>
        (402, 560, 628, 560);

    public static (int X, int Y)? ActAction(
        ImageFrame image,
        EventAct act)
    {
        EventActAnchorMatch? anchor =
            EventActAnchorDetector.Find(
                image,
                act);
        return anchor is EventActAnchorMatch match
            ? (match.ActionX, match.ActionY)
            : null;
    }

    private static double EventContextScore(
        ImageFrame image)
    {
        double headerRed = ColorFraction(
            image,
            EventHeader,
            IsEventRed);
        double selectedVillainTab = Math.Max(
            SelectedVillainTabScore(
                image,
                top: 109),
            SelectedVillainTabScore(
                image,
                top: 160));
        double dark = ColorFraction(
            image,
            new ScreenRegion(0, 0, 808, 611),
            IsDark);
        if ((headerRed < 0.08 &&
             selectedVillainTab == 0) ||
            dark < 0.33)
        {
            return 0;
        }
        double ownedContext = Math.Max(
            Ramp(
                headerRed,
                0.08,
                0.45),
            selectedVillainTab);
        return Math.Clamp(
            0.68 +
            0.20 * ownedContext +
            0.12 * Ramp(
                dark,
                0.33,
                0.72),
            0,
            1);
    }

    private static double SelectedVillainTabScore(
        ImageFrame image,
        int top)
    {
        // The selected Villain tab persists across Event Home, Act
        // selection, and Act detail while the decorative Events header can
        // finish later. Its wide body distinguishes it from an unselected
        // card's thin rail.
        double railRed = ColorFraction(
            image,
            new ScreenRegion(
                13,
                top,
                4,
                44),
            IsEventRed);
        double bodyRed = ColorFraction(
            image,
            new ScreenRegion(
                17,
                top,
                11,
                44),
            IsEventRed);
        if (railRed < 0.55 ||
            bodyRed < 0.55)
        {
            return 0;
        }
        return (
            Ramp(
                railRed,
                0.55,
                0.80) +
            Ramp(
                bodyRed,
                0.55,
                0.90)) /
            2;
    }

    private static double ActSelectorScore(
        ImageFrame image,
        double eventContext)
    {
        if (eventContext == 0) return 0;
        double heading =
            EventActSelectorHeadingDetector.Score(
                image);
        double scrollRed =
            BestHorizontalLineFraction(
                image,
                ActScrollRail,
                IsEventRed);
        if (heading == 0 ||
            scrollRed < 0.55)
        {
            return 0;
        }
        return Math.Clamp(
            0.68 +
            0.12 * heading +
            0.12 * Ramp(
                scrollRed,
                0.55,
                0.95) +
            0.08 * eventContext,
            0,
            1);
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        int matches = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matches++;
                }
            }
        }
        return (double)matches /
            (region.Width * region.Height);
    }

    private static double BestHorizontalLineFraction(
        ImageFrame image,
        ScreenRegion region,
        Func<byte, byte, byte, bool> predicate)
    {
        double best = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            int matches = 0;
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matches++;
                }
            }
            best = Math.Max(
                best,
                (double)matches / region.Width);
        }
        return best;
    }

    private static bool IsEventRed(
        byte red,
        byte green,
        byte blue) =>
        red >= 95 &&
        red - green >= 38 &&
        red - blue >= 25;

    private static bool IsDark(
        byte red,
        byte green,
        byte blue) =>
        red + green + blue <= 175;

    private static bool IsNeutralWhite(
        byte red,
        byte green,
        byte blue) =>
        Math.Min(red, Math.Min(green, blue)) >= 170 &&
        Math.Max(red, Math.Max(green, blue)) -
        Math.Min(red, Math.Min(green, blue)) <= 45;

    private static double Ramp(
        double value,
        double minimum,
        double maximum) =>
        Math.Clamp(
            (value - minimum) /
            (maximum - minimum),
            0,
            1);

    private static void ValidateClient(
        ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 ||
            image.Width != 808 ||
            image.Height != 611)
        {
            throw new InvalidDataException(
                "Event detector input must be an RGB 808 by 611 client image.");
        }
    }

    private static EventScreenMatch Trace(
        EventScreenMatch match)
    {
        VisionTrace.Emit(
            "event_screen",
            match.State.ToString(),
            match.Confidence,
            new
            {
                match.ActionX,
                match.ActionY,
            });
        return match;
    }
}
