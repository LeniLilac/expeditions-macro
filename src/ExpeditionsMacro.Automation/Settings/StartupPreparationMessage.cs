namespace ExpeditionsMacro.Automation.Settings;

internal static class StartupPreparationMessage
{
    public static string Progress(
        bool normalizeUiScale,
        bool normalizeGameSettings) =>
        (normalizeUiScale, normalizeGameSettings) switch
        {
            (true, true) =>
                "Checking UI Scale and required Anime Expeditions settings.",
            (true, false) =>
                "Checking Anime Expeditions UI Scale.",
            (false, true) =>
                "Waiting for a stable lobby before checking required Anime Expeditions settings.",
            _ =>
                "Waiting for a stable lobby. Startup preparation is disabled.",
        };

    public static string Result(
        bool normalizeUiScale,
        bool normalizeGameSettings,
        int changes,
        bool scaleChanged)
    {
        if (!normalizeGameSettings)
        {
            return scaleChanged
                ? "Anime Expeditions UI Scale is set to 1.00. Required game-settings checks are disabled."
                : "Anime Expeditions UI Scale already matches 1.00. Required game-settings checks are disabled.";
        }

        string scale = normalizeUiScale
            ? scaleChanged
                ? " Rendered UI Scale was calibrated."
                : " Rendered UI Scale already matched 1.00."
            : " UI Scale input was not changed.";
        return changes == 0
            ? "Anime Expeditions settings already match the required profile." +
              scale
            : $"Anime Expeditions settings ready: {changes} toggle(s) corrected." +
              scale;
    }
}
