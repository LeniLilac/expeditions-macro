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

public sealed record TeamScreenMatch(TeamScreenState State, double Confidence);

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

    public static TeamScreenMatch Detect(ImageFrame image)
    {
        Validate(image);
        double modalTeams = DimmedTeamsEvidence(image);
        double modalDark = DarkFraction(image, Modal);
        double include = ActionButtonDetector.Score(image, "team_equipment_include");
        double exclude = ActionButtonDetector.Score(image, "team_equipment_exclude");
        if (modalTeams >= 0.70 && modalDark >= 0.58 && include > 0 && exclude > 0)
        {
            double confidence = Math.Clamp(0.35 * modalTeams + 0.20 * Ramp(modalDark, 0.58, 0.90) + 0.25 * include + 0.20 * exclude, 0, 1);
            return Trace(new TeamScreenMatch(TeamScreenState.EquipmentConfirm, confidence));
        }

        double loadConfirm = ActionButtonDetector.Score(image, "team_load_confirm");
        if (modalTeams >= 0.70 && modalDark >= 0.58 && loadConfirm > 0)
        {
            double confidence = Math.Clamp(0.45 * modalTeams + 0.20 * Ramp(modalDark, 0.58, 0.90) + 0.35 * loadConfirm, 0, 1);
            return Trace(new TeamScreenMatch(TeamScreenState.LoadConfirm, confidence));
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

    public static (int X, int Y) LoadTeamAction(int teamSlot) => teamSlot switch
    {
        1 => (580, 267),
        2 => (580, 355),
        3 => (580, 447),
        4 => (580, 250),
        5 => (580, 337),
        6 => (580, 425),
        7 => (580, 328),
        8 => (580, 416),
        _ => throw new ArgumentOutOfRangeException(nameof(teamSlot)),
    };

    public static int ScrollNotchesForTeam(int teamSlot) => teamSlot switch
    {
        <= 3 => 20,
        <= 6 => -8,
        _ => -20,
    };

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
    private static double Ramp(double value, double minimum, double maximum) => Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static void Validate(ImageFrame image)
    {
        if (image.Format != PixelFormat.Rgb24 || image.Width != ClientWidth || image.Height != ClientHeight)
        {
            throw new InvalidDataException($"Team detector input must be an RGB {ClientWidth} by {ClientHeight} client image.");
        }
    }

    private static TeamScreenMatch Trace(TeamScreenMatch match)
    {
        VisionTrace.Emit("team_screen", match.State.ToString(), match.Confidence);
        return match;
    }
}
