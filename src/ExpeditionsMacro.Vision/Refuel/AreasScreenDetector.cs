using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;

namespace ExpeditionsMacro.Vision.Refuel;

public enum AreasScreenState
{
    None,
    Menu,
    Expeditions,
    Lobby,
}

public sealed record AreasScreenMatch(
    AreasScreenState State,
    double Confidence,
    int? ActionX = null,
    int? ActionY = null,
    int? LobbyTabActionX = null,
    int? LobbyTabActionY = null);

public static class AreasScreenDetector
{
    private const int RailX = 151;
    private const int FirstRowY = 186;
    private const int RowWidth = 92;
    private const int RowHeight = 24;
    private const int RowPitch = 27;
    private const int RowCount = 5;
    private const int LobbyRow = 2;
    private const int ExpeditionsRow = 4;

    private static readonly ScreenRegion CloseSearch =
        new(620, 130, 65, 70);
    private static readonly ScreenRegion HubTop =
        new(250, 200, 140, 15);
    private static readonly ScreenRegion HubLeft =
        new(250, 205, 12, 72);
    private static readonly ScreenRegion HubRight =
        new(376, 205, 14, 72);
    private static readonly ScreenRegion LobbySpawnTop =
        new(250, 350, 140, 15);
    private static readonly ScreenRegion LobbySpawnLeft =
        new(250, 355, 12, 72);
    private static readonly ScreenRegion LobbySpawnRight =
        new(376, 355, 14, 72);

    public static AreasScreenMatch Detect(ImageFrame image)
    {
        RefuelVisionMetrics.ValidateClient(image);
        AreasLayoutMatch? layout = FindLayout(image);
        double hubFrame = layout is AreasLayoutMatch found
            ? CardFrameScore(
                image,
                HubTop,
                HubLeft,
                HubRight,
                found.OffsetX,
                found.OffsetY)
            : 0;
        double lobbySpawnFrame = layout is AreasLayoutMatch lobbyLayout
            ? CardFrameScore(
                image,
                LobbySpawnTop,
                LobbySpawnLeft,
                LobbySpawnRight,
                lobbyLayout.OffsetX,
                lobbyLayout.OffsetY)
            : 0;

        AreasScreenMatch match;
        if (layout is not AreasLayoutMatch candidate)
        {
            match = new AreasScreenMatch(
                AreasScreenState.None,
                0);
        }
        else if (candidate.SelectedRow == ExpeditionsRow)
        {
            double confidence =
                0.65 * candidate.Confidence +
                0.35 * hubFrame;
            match = hubFrame >= 0.74 &&
                    confidence >= 0.76
                ? new AreasScreenMatch(
                    AreasScreenState.Expeditions,
                    confidence,
                    ActionX: 322 + candidate.OffsetX,
                    ActionY: 264 + candidate.OffsetY,
                    LobbyTabActionX:
                        198 + candidate.OffsetX,
                    LobbyTabActionY:
                        252 + candidate.OffsetY)
                : new AreasScreenMatch(
                    AreasScreenState.None,
                    0);
        }
        else if (candidate.SelectedRow == LobbyRow)
        {
            double confidence =
                0.65 * candidate.Confidence +
                0.35 * lobbySpawnFrame;
            match = lobbySpawnFrame >= 0.74 &&
                    confidence >= 0.76
                ? new AreasScreenMatch(
                    AreasScreenState.Lobby,
                    confidence,
                    ActionX: 318 + candidate.OffsetX,
                    ActionY: 388 + candidate.OffsetY,
                    LobbyTabActionX:
                        198 + candidate.OffsetX,
                    LobbyTabActionY:
                        252 + candidate.OffsetY)
                : new AreasScreenMatch(
                    AreasScreenState.None,
                    0);
        }
        else
        {
            match = new AreasScreenMatch(
                AreasScreenState.Menu,
                candidate.Confidence,
                ActionX: 198 + candidate.OffsetX,
                ActionY:
                    FirstRowY +
                    ExpeditionsRow * RowPitch +
                    RowHeight / 2 +
                    candidate.OffsetY,
                LobbyTabActionX:
                    198 + candidate.OffsetX,
                LobbyTabActionY:
                    FirstRowY +
                    LobbyRow * RowPitch +
                    RowHeight / 2 +
                    candidate.OffsetY);
        }

        VisionTrace.Emit(
            "areas_screen",
            match.State.ToString(),
            match.Confidence,
            new
            {
                LayoutConfidence = layout?.Confidence ?? 0,
                SelectedRow = layout?.SelectedRow,
                OffsetX = layout?.OffsetX,
                OffsetY = layout?.OffsetY,
                SelectedAccent = layout?.SelectedAccent ?? 0,
                MinimumNeutralRow =
                    layout?.MinimumNeutralRow ?? 0,
                LowerRailButtonPixels =
                    layout?.LowerRailButtonPixels ?? 0,
                ClosePixels = layout?.ClosePixels ?? 0,
                HubFrame = hubFrame,
                LobbySpawnFrame = lobbySpawnFrame,
                match.ActionX,
                match.ActionY,
                match.LobbyTabActionX,
                match.LobbyTabActionY,
            });
        return match;
    }

    private static AreasLayoutMatch? FindLayout(
        ImageFrame image)
    {
        RefuelColorBounds? close =
            RefuelVisionMetrics.FindColorBounds(
                image,
                CloseSearch,
                RefuelVisionMetrics.IsRed);
        if (close is not RefuelColorBounds closeAction ||
            closeAction.Width is < 18 or > 32 ||
            closeAction.Height is < 14 or > 32 ||
            closeAction.Count < 100)
        {
            return null;
        }

        int offsetX =
            (int)Math.Round(closeAction.CenterX) - 652;
        if (Math.Abs(offsetX) > 10)
        {
            return null;
        }

        AreasLayoutMatch? best = null;
        for (int offsetY = -12;
             offsetY <= 12;
             offsetY++)
        {
            double[] neutral = new double[RowCount];
            double[] accent = new double[RowCount];
            for (int row = 0; row < RowCount; row++)
            {
                ScreenRegion region = new(
                    RailX + offsetX,
                    FirstRowY +
                    row * RowPitch +
                    offsetY,
                    RowWidth,
                    RowHeight);
                neutral[row] =
                    RefuelVisionMetrics.ColorFraction(
                        image,
                        region,
                        RefuelVisionMetrics.IsNeutralGray);
                accent[row] = Math.Max(
                    RefuelVisionMetrics.ColorFraction(
                        image,
                        region,
                        RefuelVisionMetrics.IsTeal),
                    RefuelVisionMetrics.ColorFraction(
                        image,
                        region,
                        RefuelVisionMetrics.IsPurple));
            }

            int selectedRow = Array.IndexOf(
                accent,
                accent.Max());
            double selectedAccent = accent[selectedRow];
            double secondAccent = accent
                .Where((_, index) => index != selectedRow)
                .Max();
            double minimumNeutral = neutral
                .Where((_, index) => index != selectedRow)
                .Min();
            double averageNeutral = neutral
                .Where((_, index) => index != selectedRow)
                .Average();
            ScreenRegion lowerRail = new(
                RailX + offsetX,
                328 + offsetY,
                RowWidth,
                102);
            RefuelColorComponent? lowerRailButton =
                RefuelVisionMetrics.FindComponent(
                    image,
                    lowerRail,
                    RefuelVisionMetrics.IsNeutralGray,
                    component =>
                        component.Width >= 60 &&
                        component.Height >= 8 &&
                        component.Count >= 350);
            if (selectedAccent < 0.30 ||
                secondAccent > 0.20 ||
                minimumNeutral < 0.65 ||
                lowerRailButton is not null)
            {
                continue;
            }

            double confidence = Math.Clamp(
                0.55 +
                0.20 * RefuelVisionMetrics.Ramp(
                    averageNeutral,
                    0.65,
                    0.86) +
                0.15 * RefuelVisionMetrics.Ramp(
                    selectedAccent,
                    0.30,
                    0.70) +
                0.10 * RefuelVisionMetrics.Ramp(
                    closeAction.Count,
                    100,
                    260),
                0,
                1);
            AreasLayoutMatch current = new(
                offsetX,
                offsetY,
                selectedRow,
                confidence,
                selectedAccent,
                minimumNeutral,
                lowerRailButton?.Count ?? 0,
                closeAction.Count);
            if (best is null ||
                current.Confidence > best.Value.Confidence)
            {
                best = current;
            }
        }

        return best;
    }

    private static double CardFrameScore(
        ImageFrame image,
        ScreenRegion top,
        ScreenRegion left,
        ScreenRegion right,
        int offsetX,
        int offsetY)
    {
        RefuelColorComponent? topBorder =
            RefuelVisionMetrics.FindComponent(
                image,
                top.Translate(offsetX, offsetY),
                RefuelVisionMetrics.IsTeal,
                component =>
                    component.Width >= 115 &&
                    component.Height >= 2 &&
                    component.Count >= 100);
        RefuelColorComponent? leftBorder =
            RefuelVisionMetrics.FindComponent(
                image,
                left.Translate(offsetX, offsetY),
                RefuelVisionMetrics.IsTeal,
                component =>
                    component.Width >= 3 &&
                    component.Height >= 28 &&
                    component.Count >= 25);
        RefuelColorComponent? rightBorder =
            RefuelVisionMetrics.FindComponent(
                image,
                right.Translate(offsetX, offsetY),
                RefuelVisionMetrics.IsTeal,
                component =>
                    component.Width >= 3 &&
                    component.Height >= 28 &&
                    component.Count >= 25);
        return topBorder is not RefuelColorComponent topMatch ||
               leftBorder is not RefuelColorComponent leftMatch ||
               rightBorder is not RefuelColorComponent rightMatch
            ? 0
            : Math.Clamp(
                0.58 +
                0.18 * RefuelVisionMetrics.Ramp(
                    topMatch.Width,
                    115,
                    135) +
                0.12 * RefuelVisionMetrics.Ramp(
                    leftMatch.Height,
                    28,
                    50) +
                0.12 * RefuelVisionMetrics.Ramp(
                    rightMatch.Height,
                    28,
                    50),
                0,
                1);
    }

    private readonly record struct AreasLayoutMatch(
        int OffsetX,
        int OffsetY,
        int SelectedRow,
        double Confidence,
        double SelectedAccent,
        double MinimumNeutralRow,
        int LowerRailButtonPixels,
        int ClosePixels);
}
