using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Diagnostics;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Vision.Teams;

public enum TeamScreenState
{
    None,
    Units,
    Teams,
    LoadConfirm,
    EquipmentConfirm,
}

public sealed record TeamScreenMatch(TeamScreenState State, double Confidence, int? ActionX = null, int? ActionY = null);

public readonly record struct TeamScrollbarThumb(int X, int CenterY, int Height);

public static class TeamScreenDetector
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    private static readonly ScreenRegion Panel = new(105, 105, 605, 370);
    private static readonly ScreenRegion Header = new(80, 100, 285, 110);
    private static readonly ScreenRegion TeamsPanel = new(140, 135, 530, 330);
    private static readonly ScreenRegion Modal = new(260, 250, 290, 170);
    private static readonly ScreenRegion[] LoadButtonRows =
    [
        new(540, 225, 115, 65),
        new(540, 320, 115, 70),
        new(540, 405, 115, 65),
    ];
    private static readonly ScreenRegion[] AlignedLoadButtonRows =
    [
        new(530, 238, 116, 56),
        new(530, 298, 116, 68),
        new(530, 385, 116, 55),
    ];
    private static readonly int[] ScrollThumbOffsets = [0, 30, 59, 89, 118, 148, 156, 156];

    public static TeamScreenMatch Detect(ImageFrame image)
    {
        Validate(image);
        double modalTeams = DimmedTeamsEvidence(image);
        double modalDark = DarkFraction(image, Modal);
        double include = ActionButtonDetector.Score(image, "team_equipment_include");
        double exclude = ActionButtonDetector.Score(image, "team_equipment_exclude");
        if (modalTeams >= 0.70 && modalDark >= 0.58 && include > 0 && exclude > 0)
        {
            (int X, int Y)? action = ActionButtonDetector.ActionFor(image, "team_equipment_include");
            double confidence = Math.Clamp(0.35 * modalTeams + 0.20 * Ramp(modalDark, 0.58, 0.90) + 0.25 * include + 0.20 * exclude, 0, 1);
            return Trace(new TeamScreenMatch(TeamScreenState.EquipmentConfirm, confidence, action?.X, action?.Y));
        }

        double loadConfirm = ActionButtonDetector.Score(image, "team_load_confirm");
        if (modalTeams >= 0.70 && modalDark >= 0.58 && loadConfirm > 0)
        {
            (int X, int Y)? action = ActionButtonDetector.ActionFor(image, "team_load_confirm");
            double confidence = Math.Clamp(0.45 * modalTeams + 0.20 * Ramp(modalDark, 0.58, 0.90) + 0.35 * loadConfirm, 0, 1);
            return Trace(new TeamScreenMatch(TeamScreenState.LoadConfirm, confidence, action?.X, action?.Y));
        }

        double teams = TeamsBaseScore(image);
        if (teams >= 0.70)
        {
            return Trace(new TeamScreenMatch(TeamScreenState.Teams, teams));
        }

        double units = UnitsScore(image);
        return Trace(units >= 0.70
            ? new TeamScreenMatch(TeamScreenState.Units, units)
            : new TeamScreenMatch(TeamScreenState.None, Math.Max(units, teams)));
    }

    public static (int X, int Y) TeamsTabAction => (305, 427);

    public static int ScrollThumbOffsetY(int teamSlot)
    {
        ValidateTeamSlot(teamSlot);
        return ScrollThumbOffsets[teamSlot - 1];
    }

    public static int ScrollThumbTargetCenterY(int teamSlot, int topCenterY) =>
        topCenterY + ScrollThumbOffsetY(teamSlot);

    public static bool IsScrollbarAtTop(TeamScrollbarThumb thumb) =>
        thumb.CenterY is >= 230 and <= 245;

    public static TeamScrollbarThumb? FindScrollbarThumb(ImageFrame image)
    {
        Validate(image);
        List<(int X, int StartY, int EndY)> columns = [];
        for (int x = 620; x <= 659; x++)
        {
            (int StartY, int EndY) run = LongestThumbRun(image, x);
            if (run.EndY - run.StartY + 1 >= 60)
            {
                columns.Add((x, run.StartY, run.EndY));
            }
        }

        if (columns.Count == 0) return null;
        (int X, int StartY, int EndY) best = columns.MaxBy(column => column.EndY - column.StartY);
        (int X, int StartY, int EndY)[] matching = columns
            .Where(column =>
                Math.Abs(column.StartY - best.StartY) <= 2 &&
                Math.Abs(column.EndY - best.EndY) <= 2)
            .ToArray();
        int xCenter = (int)Math.Round(matching.Average(column => column.X));
        int startY = (int)Math.Round(matching.Average(column => column.StartY));
        int endY = (int)Math.Round(matching.Average(column => column.EndY));
        return new TeamScrollbarThumb(xCenter, (startY + endY) / 2, endY - startY + 1);
    }

    public static (int X, int Y)? AlignedLoadTeamAction(
        ImageFrame image,
        int teamSlot,
        int targetThumbCenterY)
    {
        ValidateTeamSlot(teamSlot);
        TeamScrollbarThumb? thumb = FindScrollbarThumb(image);
        if (thumb is null || Math.Abs(thumb.Value.CenterY - targetThumbCenterY) > 4)
        {
            return null;
        }

        int row = teamSlot <= 6 ? 0 : teamSlot - 6;
        ScreenRegion buttonRegion = AlignedLoadButtonRows[row];
        (int MinX, int MinY, int MaxX, int MaxY, int Count)? bounds = GreenBounds(image, buttonRegion);
        if (bounds is null ||
            bounds.Value.MaxX - bounds.Value.MinX + 1 < 60 ||
            bounds.Value.MaxY - bounds.Value.MinY + 1 < 25 ||
            bounds.Value.Count < 1000)
        {
            return null;
        }

        return (
            (bounds.Value.MinX + bounds.Value.MaxX) / 2,
            (bounds.Value.MinY + bounds.Value.MaxY) / 2);
    }

    public static (int X, int Y) LoadConfirmAction => (345, 331);

    public static (int X, int Y) IncludeEquipmentAction => (319, 376);

    private static double UnitsScore(ImageFrame image)
    {
        double header = ColorFraction(image, Header, IsGold);
        double dark = DarkFraction(image, Panel);
        double close = ActionButtonDetector.Score(image, "team_close");
        double unequip = ActionButtonDetector.Score(image, "units_unequip_all");
        double teams = ActionButtonDetector.Score(image, "units_teams");
        double quickSell = ActionButtonDetector.Score(image, "units_quick_sell");
        if (header < 0.018 || dark < 0.42 || close == 0 || unequip == 0 || teams == 0 || quickSell == 0) return 0;
        return Math.Clamp(
            0.18 * Ramp(header, 0.018, 0.12) +
            0.12 * Ramp(dark, 0.42, 0.82) +
            0.16 * close +
            0.18 * unequip +
            0.18 * teams +
            0.18 * quickSell,
            0,
            1);
    }

    private static double TeamsBaseScore(ImageFrame image)
    {
        double header = ColorFraction(image, Header, IsGold);
        double dark = DarkFraction(image, TeamsPanel);
        double close = ActionButtonDetector.Score(image, "team_close");
        int loadRows = LoadButtonRows.Count(region => ColorFraction(image, region, IsGreenButton) >= 0.075);
        const double minimumHeader = 0.018;
        const double minimumDark = 0.42;
        if (header < minimumHeader || dark < minimumDark || close == 0 || loadRows < 2) return 0;
        return Math.Clamp(
            0.25 * Ramp(header, minimumHeader, 0.12) +
            0.15 * Ramp(dark, minimumDark, 0.86) +
            0.25 * close +
            0.35 * (loadRows / 3d),
            0,
            1);
    }

    private static double DimmedTeamsEvidence(ImageFrame image)
    {
        double dark = DarkFraction(image, TeamsPanel);
        int loadRows = LoadButtonRows.Count(region => ColorFraction(image, region, IsGreenButton) >= 0.10);
        if (dark < 0.80 || loadRows < 2) return 0;
        return Math.Clamp(0.45 * Ramp(dark, 0.80, 0.96) + 0.55 * (loadRows / 3d), 0, 1);
    }

    private static double DarkFraction(ImageFrame image, ScreenRegion region) => ColorFraction(image, region, (red, green, blue) => red + green + blue <= 210);

    private static (int StartY, int EndY) LongestThumbRun(ImageFrame image, int x)
    {
        int bestStart = 0;
        int bestEnd = -1;
        int currentStart = 0;
        bool inside = false;
        for (int y = 190; y <= 440; y++)
        {
            int pixel = (y * image.Width + x) * 3;
            bool matches = IsScrollbarThumb(
                image.Pixels[pixel],
                image.Pixels[pixel + 1],
                image.Pixels[pixel + 2]);
            if (matches && !inside)
            {
                currentStart = y;
                inside = true;
            }
            if ((!matches || y == 440) && inside)
            {
                int currentEnd = matches && y == 440 ? y : y - 1;
                if (currentEnd - currentStart > bestEnd - bestStart)
                {
                    bestStart = currentStart;
                    bestEnd = currentEnd;
                }
                inside = false;
            }
        }
        return (bestStart, bestEnd);
    }

    private static (int MinX, int MinY, int MaxX, int MaxY, int Count)? GreenBounds(
        ImageFrame image,
        ScreenRegion region)
    {
        int minimumX = region.Right;
        int minimumY = region.Bottom;
        int maximumX = -1;
        int maximumY = -1;
        int count = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (!IsGreenButton(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2]))
                {
                    continue;
                }

                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                count++;
            }
        }

        return count == 0 ? null : (minimumX, minimumY, maximumX, maximumY, count);
    }

    private static double ColorFraction(ImageFrame image, ScreenRegion region, Func<byte, byte, byte, bool> predicate)
    {
        int matching = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (predicate(image.Pixels[pixel], image.Pixels[pixel + 1], image.Pixels[pixel + 2])) matching++;
            }
        }
        return (double)matching / (region.Width * region.Height);
    }

    private static bool IsGold(byte red, byte green, byte blue) => red >= 120 && green >= 75 && red - blue >= 45 && green - blue >= 25;
    private static bool IsGreenButton(byte red, byte green, byte blue) => green >= 90 && green - red >= 25 && green - blue >= 20;
    private static bool IsScrollbarThumb(byte red, byte green, byte blue) =>
        red is >= 55 and <= 180 &&
        Math.Abs(red - green) <= 8 &&
        Math.Abs(green - blue) <= 8;
    private static double Ramp(double value, double minimum, double maximum) => Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static void ValidateTeamSlot(int teamSlot)
    {
        if (teamSlot is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(teamSlot));
    }

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != ClientWidth || image.Height != ClientHeight)
        {
            throw new InvalidDataException($"Team detector input must be an RGB {ClientWidth} by {ClientHeight} client image.");
        }
    }

    private static TeamScreenMatch Trace(TeamScreenMatch match)
    {
        VisionTrace.Emit("team_screen", match.State.ToString(), match.Confidence, new { match.ActionX, match.ActionY });
        return match;
    }
}
