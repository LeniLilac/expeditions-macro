using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;

namespace ExpeditionsMacro.Vision.Packs;

internal static class ActionButtonDetector
{
    private enum ButtonColor
    {
        Green,
        ChallengeGreen,
        Purple,
        Red,
        Yellow,
        Cyan,
        Neutral,
    }

    private readonly record struct Profile(
        ScreenRegion SearchRegion,
        ButtonColor Color,
        double ExpectedCenterX,
        double ExpectedCenterY,
        double HorizontalTolerance,
        double VerticalTolerance,
        int MinimumWidth,
        int MaximumWidth,
        int MinimumHeight,
        int MaximumHeight,
        double MinimumFill,
        double MinimumScore = 0.60,
        int MinimumPixels = 80);

    private readonly record struct Component(int Count, int Left, int Top, int Width, int Height)
    {
        public double CenterX => Left + (Width - 1) / 2d;

        public double CenterY => Top + (Height - 1) / 2d;
    }

    private readonly record struct ButtonMatch(double Score, Component Component);

    private static readonly IReadOnlyDictionary<string, Profile> Profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase)
    {
        ["map_select"] = new(new ScreenRegion(590, 455, 218, 135), ButtonColor.Green, 728, 535, 130, 90, 78, 150, 17, 43, 0.42),
        ["select_stage"] = new(new ScreenRegion(590, 455, 218, 135), ButtonColor.Green, 728, 535, 130, 90, 78, 150, 17, 43, 0.42),
        ["map_preview"] = new(new ScreenRegion(350, 295, 280, 150), ButtonColor.Green, 479, 370, 80, 60, 70, 150, 16, 42, 0.42),
        ["confirm"] = new(new ScreenRegion(220, 255, 360, 145), ButtonColor.Green, 345, 331, 75, 55, 75, 155, 15, 40, 0.42),
        ["extract_confirm"] = new(new ScreenRegion(220, 315, 360, 140), ButtonColor.Red, 345, 378, 75, 55, 75, 155, 15, 42, 0.42),
        ["victory"] = new(new ScreenRegion(80, 360, 320, 150), ButtonColor.Yellow, 225, 438, 85, 60, 110, 200, 18, 48, 0.42),
        ["defeat"] = new(new ScreenRegion(80, 360, 320, 150), ButtonColor.Yellow, 225, 438, 85, 60, 110, 200, 18, 48, 0.42),
        ["expedition_terminal_close"] = new(new ScreenRegion(625, 125, 90, 80), ButtonColor.Red, 670, 155, 48, 35, 14, 34, 14, 34, 0.35, 0.40),
        ["disconnect"] = new(new ScreenRegion(320, 320, 330, 145), ButtonColor.Neutral, 496, 394, 95, 60, 120, 230, 22, 58, 0.55),
        ["challenge_select_stage"] = new(new ScreenRegion(300, 395, 250, 80), ButtonColor.ChallengeGreen, 426, 437, 90, 35, 75, 190, 16, 42, 0.28),
        ["challenge_select_stage_wide"] = new(new ScreenRegion(300, 395, 390, 80), ButtonColor.ChallengeGreen, 507, 437, 170, 35, 190, 350, 16, 42, 0.28),
        ["stage_select_stage"] = new(new ScreenRegion(145, 405, 220, 80), ButtonColor.ChallengeGreen, 256, 449, 70, 38, 125, 195, 18, 44, 0.28),
        ["stage_enter_matchmaking"] = new(new ScreenRegion(325, 405, 220, 80), ButtonColor.Purple, 428, 449, 70, 38, 125, 195, 18, 44, 0.25),
        ["challenge_enter_matchmaking"] = new(new ScreenRegion(480, 395, 230, 80), ButtonColor.Purple, 589, 437, 100, 35, 70, 190, 16, 42, 0.25),
        ["challenge_preview_start"] = new(new ScreenRegion(390, 335, 230, 85), ButtonColor.Green, 506, 376, 65, 35, 130, 190, 20, 42, 0.42),
        ["challenge_change_mode"] = new(new ScreenRegion(550, 335, 230, 85), ButtonColor.Yellow, 668, 376, 65, 35, 130, 190, 20, 42, 0.42),
        ["expedition_party_change_map"] = new(new ScreenRegion(500, 315, 170, 90), ButtonColor.Purple, 587, 353, 55, 35, 90, 125, 20, 38, 0.25),
        ["expedition_party_change_mode"] = new(new ScreenRegion(610, 315, 170, 90), ButtonColor.Yellow, 695, 353, 55, 35, 90, 125, 20, 38, 0.42),
        ["challenge_party_start"] = new(new ScreenRegion(390, 370, 230, 65), ButtonColor.Green, 506, 393, 65, 25, 130, 190, 20, 38, 0.42),
        ["challenge_party_disband"] = new(new ScreenRegion(550, 370, 230, 65), ButtonColor.Red, 668, 393, 65, 25, 130, 190, 20, 38, 0.42),
        // Story and Raid parties can expose three narrower actions on a lower rail.
        // Keep these profiles stage-specific so Expedition party UI cannot steal a
        // Challenge-only PreviewReady state.
        ["stage_party_start"] = new(new ScreenRegion(400, 390, 145, 60), ButtonColor.Green, 480, 421, 45, 25, 90, 120, 20, 38, 0.42),
        ["stage_party_change_map"] = new(new ScreenRegion(520, 390, 135, 60), ButtonColor.Purple, 588, 421, 45, 25, 90, 125, 20, 38, 0.25),
        ["stage_party_disband"] = new(new ScreenRegion(630, 390, 150, 60), ButtonColor.Red, 696, 421, 45, 25, 90, 125, 20, 38, 0.42),
        ["challenge_victory_party"] = new(new ScreenRegion(125, 395, 365, 80), ButtonColor.Purple, 304, 437, 100, 35, 250, 350, 16, 42, 0.25),
        ["challenge_victory_close"] = new(new ScreenRegion(625, 125, 90, 80), ButtonColor.Red, 670, 155, 48, 35, 14, 34, 14, 34, 0.35, 0.40),
        ["challenge_post_match_play"] = new(new ScreenRegion(152, 570, 28, 28), ButtonColor.Cyan, 164, 584, 16, 16, 8, 28, 8, 28, 0.08, 0.30, 40),
        ["challenge_game_results"] = new(new ScreenRegion(330, 450, 150, 75), ButtonColor.Yellow, 403, 491, 85, 40, 25, 120, 12, 40, 0.20, 0.38),
        ["team_close"] = new(new ScreenRegion(610, 100, 110, 110), ButtonColor.Red, 660, 150, 55, 65, 14, 36, 14, 36, 0.35, 0.40),
        ["units_unequip_all"] = new(new ScreenRegion(105, 405, 155, 70), ButtonColor.Red, 180, 442, 48, 35, 85, 135, 18, 42, 0.30),
        ["units_teams"] = new(new ScreenRegion(215, 405, 165, 70), ButtonColor.Purple, 294, 442, 48, 35, 85, 135, 18, 42, 0.25),
        ["units_quick_sell"] = new(new ScreenRegion(330, 405, 165, 70), ButtonColor.Yellow, 410, 442, 48, 35, 85, 135, 18, 42, 0.30),
        ["team_load_confirm"] = new(new ScreenRegion(260, 300, 165, 70), ButtonColor.Green, 340, 334, 48, 35, 95, 145, 18, 42, 0.34),
        ["team_equipment_include"] = new(new ScreenRegion(260, 340, 125, 70), ButtonColor.Green, 319, 376, 42, 35, 60, 105, 18, 42, 0.34),
        ["team_equipment_exclude"] = new(new ScreenRegion(345, 340, 125, 70), ButtonColor.Red, 405, 376, 42, 35, 60, 105, 18, 42, 0.30),
    };

    public static double Score(ImageFrame image, string state)
    {
        // Find already requires the expected color, connected geometry, fill, size,
        // and screen neighborhood. Once those independent constraints agree, expose
        // a detector confidence rather than the raw layout-closeness rank. The raw
        // rank deliberately falls as Roblox shifts or scales the UI and should only
        // decide which valid component wins, not whether that component is a state.
        return Find(image, state) is ButtonMatch match ? 0.75 + 0.25 * match.Score : 0;
    }

    public static (int X, int Y)? ActionFor(ImageFrame image, string state)
    {
        if (Find(image, state) is not ButtonMatch match) return null;
        return (
            (int)Math.Round(match.Component.CenterX, MidpointRounding.AwayFromZero),
            (int)Math.Round(match.Component.CenterY, MidpointRounding.AwayFromZero));
    }

    private static ButtonMatch? Find(ImageFrame image, string state)
    {
        if (!Profiles.TryGetValue(state, out Profile profile) ||
            image.Format != PixelFormat.Rgb24 ||
            !profile.SearchRegion.FitsWithin(image.Width, image.Height)) return null;

        Component? best = null;
        double bestScore = 0;
        foreach (Component component in Components(image, profile))
        {
            double score = Score(component, profile);
            if (score <= bestScore) continue;
            best = component;
            bestScore = score;
        }

        return best is Component match && bestScore >= profile.MinimumScore ? new ButtonMatch(bestScore, match) : null;
    }

    private static double Score(Component component, Profile profile)
    {
        if (component.Width < profile.MinimumWidth || component.Width > profile.MaximumWidth ||
            component.Height < profile.MinimumHeight || component.Height > profile.MaximumHeight) return 0;
        double fill = (double)component.Count / (component.Width * component.Height);
        if (fill < profile.MinimumFill || fill > 0.99) return 0;
        double horizontal = 1 - Math.Abs(component.CenterX - profile.ExpectedCenterX) / profile.HorizontalTolerance;
        double vertical = 1 - Math.Abs(component.CenterY - profile.ExpectedCenterY) / profile.VerticalTolerance;
        if (horizontal <= 0 || vertical <= 0) return 0;
        double fillScore = Math.Clamp((fill - profile.MinimumFill) / Math.Max(0.01, 0.78 - profile.MinimumFill), 0, 1);
        return Math.Clamp(0.25 * horizontal + 0.25 * vertical + 0.50 * fillScore, 0, 1);
    }

    private static IReadOnlyList<Component> Components(ImageFrame image, Profile profile)
    {
        int width = profile.SearchRegion.Width;
        int height = profile.SearchRegion.Height;
        bool[] mask = new bool[width * height];
        bool[] visited = new bool[mask.Length];
        int[] queue = new int[mask.Length];
        for (int localY = 0; localY < height; localY++)
        {
            int y = profile.SearchRegion.Y + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int x = profile.SearchRegion.X + localX;
                int pixel = (y * image.Width + x) * 3;
                mask[localY * width + localX] = MatchesColor(
                    image.Pixels[pixel],
                    image.Pixels[pixel + 1],
                    image.Pixels[pixel + 2],
                    profile.Color);
            }
        }

        List<Component> components = [];
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = true;
            int count = 0;
            int minimumX = width;
            int minimumY = height;
            int maximumX = 0;
            int maximumY = 0;
            while (head < tail)
            {
                int current = queue[head++];
                int x = current % width;
                int y = current / width;
                count++;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                if (x > 0) Enqueue(current - 1);
                if (x + 1 < width) Enqueue(current + 1);
                if (y > 0) Enqueue(current - width);
                if (y + 1 < height) Enqueue(current + width);
            }

            if (count >= profile.MinimumPixels)
            {
                components.Add(new Component(
                    count,
                    profile.SearchRegion.X + minimumX,
                    profile.SearchRegion.Y + minimumY,
                    maximumX - minimumX + 1,
                    maximumY - minimumY + 1));
            }

            void Enqueue(int index)
            {
                if (!mask[index] || visited[index]) return;
                visited[index] = true;
                queue[tail++] = index;
            }
        }

        return components;
    }

    private static bool MatchesColor(byte red, byte green, byte blue, ButtonColor color) => color switch
    {
        ButtonColor.Green => green >= 110 && green - red >= 35 && green - blue >= 25 && green * 4 >= red * 5,
        ButtonColor.ChallengeGreen => green >= 65 && green - red >= 22 && green - blue >= 15 && green * 5 >= red * 6,
        ButtonColor.Purple => blue >= 100 && red >= 65 && blue - green >= 35 && red - green >= 25,
        ButtonColor.Red => red >= 105 && red - green >= 30 && red - blue >= 20 && red * 4 >= green * 5,
        ButtonColor.Yellow => red >= 130 && green >= 85 && red - blue >= 60 && green - blue >= 45,
        ButtonColor.Cyan => green >= 85 && blue >= 90 && green - red >= 20 && blue - red >= 25,
        ButtonColor.Neutral => Math.Min(red, Math.Min(green, blue)) >= 160 && Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue)) <= 35,
        _ => false,
    };
}
