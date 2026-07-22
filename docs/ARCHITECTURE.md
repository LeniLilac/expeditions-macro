# Architecture

The application separates reusable automation behavior from the WPF shell:

- `ExpeditionsMacro.Core` contains immutable geometry, image, model, preset, persistence, and workflow contracts.
- `ExpeditionsMacro.Windows` owns Win32 window discovery, exact client sizing, GDI capture, DPAPI, the configurable global macro hotkey, keyboard input, Roblox-compatible mouse nudging, Roblox `O`-key zoom with a wheel fallback, extended scan-code camera arrows, and relative right-drag fine camera input. The global hotkey accepts safe single-key letters, digits, punctuation, numpad keys, and supported function keys.
- `ExpeditionsMacro.Vision` owns lighting-normalized scoring, temporal medians, detector-pack compilation, validation, classification, map and difficulty selection, node hues, and hotbar checks.
- `ExpeditionsMacro.Automation` owns camera calibration/alignment, placement recording/playback, the Expeditions state machine, recovery, Discord Components V2, and detector updates.
- `ExpeditionsMacro.App` is the single WPF interface and exclusive input-owner coordinator.
- `ExpeditionsMacro.DatasetBuilder` compiles a local capture corpus into a small, hashed detector pack.

All saved coordinates are relative to the Roblox client. Camera calibration and placement recording use the canonical 808 by 611 detector size. Camera setup standardizes zoom and pitch, selects stable map regions automatically while preserving left/center/right evidence, learns and persists a signed fine mouse-controlled neighborhood around the goal, then recognizes a full yaw turn with Right-arrow pulses against those nearby views before performing final mouse refinement. Runtime alignment uses the saved full-turn atlas for coarse correction and the saved fine atlas before its live micro-search. Playback and alignment apply the model's recorded client size. When the app or a workflow resizes Roblox, it leaves Roblox at that standardized client size so later detection and placement steps share the same geometry. Window discovery verifies the owning Roblox player process and carries its PID into run diagnostics. A stale handle is rediscovered after focus failure and transparently aliased for later capture and input. Normal framed sizing remains the first path; if Windows clamps it above the canonical client size, the Windows layer removes minimum-tracking frame styles, applies a verified borderless size, and retains the original styles for lifecycle cleanup.

The workflow never reads Roblox memory or injects into the process. It observes screen pixels and emits ordinary Windows keyboard and mouse input.

Play navigation uses a separately configured A-Z key matching Anime Expeditions' **Toggle Play Menu** binding. The runner presses it from the lobby, terminal, or prestart state, verifies the expected selector or party preview, and retries boundedly; it does not depend on the position or visibility of either on-screen Play button.
