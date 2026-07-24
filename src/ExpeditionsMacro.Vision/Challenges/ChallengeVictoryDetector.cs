using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Challenges;

internal static class ChallengeVictoryDetector
{
    private static readonly ScreenRegion RosterRegion = new(510, 180, 150, 275);

    public static double Score(
        ImageFrame image,
        double closeAction,
        double partyAction)
    {
        double roster = ScoreRoster(image);
        if (closeAction == 0 ||
            partyAction == 0 ||
            roster == 0)
        {
            return 0;
        }

        // The cyan reward artwork animates independently after Victory opens.
        // Terminal stability comes from the Victory-only close, View Party,
        // and repeated roster-reward structures instead.
        return Math.Clamp(
            0.30 * closeAction +
            0.40 * partyAction +
            0.30 * roster,
            0,
            1);
    }

    private static double ScoreRoster(ImageFrame image)
    {
        int supportedRows = 0;
        const int rowHeight = 50;
        for (int row = 0; row < 5; row++)
        {
            ScreenRegion region = new(
                RosterRegion.X,
                RosterRegion.Y + row * rowHeight,
                RosterRegion.Width,
                rowHeight);
            if (ColorFraction(image, region) >= 0.008) supportedRows++;
        }

        return supportedRows < 3
            ? 0
            : 0.62 + 0.38 * ((supportedRows - 3) / 2d);
    }

    private static double ColorFraction(
        ImageFrame image,
        ScreenRegion region)
    {
        if (!region.FitsWithin(image.Width, image.Height)) return 0;
        int matching = 0;
        for (int y = region.Y; y < region.Bottom; y++)
        {
            for (int x = region.X; x < region.Right; x++)
            {
                int pixel = (y * image.Width + x) * 3;
                if (IsRewardYellow(
                        image.Pixels[pixel],
                        image.Pixels[pixel + 1],
                        image.Pixels[pixel + 2]))
                {
                    matching++;
                }
            }
        }

        return (double)matching / (region.Width * region.Height);
    }

    private static bool IsRewardYellow(
        byte red,
        byte green,
        byte blue) =>
        red >= 120 &&
        green >= 80 &&
        blue <= 95 &&
        red - blue >= 45;
}
