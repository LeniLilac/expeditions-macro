# Architecture

The application separates reusable automation behavior from the WPF shell:

- `ExpeditionsMacro.Core` contains immutable geometry, image, model, preset, persistence, and workflow contracts.
- `ExpeditionsMacro.Windows` owns Win32 window discovery, exact client sizing, GDI capture, DPAPI, F6, keyboard input, Roblox-compatible mouse nudging, and relative right-drag camera input.
- `ExpeditionsMacro.Vision` owns lighting-normalized scoring, temporal medians, detector-pack compilation, validation, classification, map and difficulty selection, node hues, and hotbar checks.
- `ExpeditionsMacro.Automation` owns camera calibration/alignment, placement recording/playback, the Expeditions state machine, recovery, Discord Components V2, and detector updates.
- `ExpeditionsMacro.App` is the single WPF interface and exclusive input-owner coordinator.
- `ExpeditionsMacro.DatasetBuilder` compiles a local capture corpus into a small, hashed detector pack.

All saved coordinates are relative to the Roblox client. Camera and placement workflows temporarily apply the model's recorded client size, then restore the original outer window bounds in a `finally` block.

The workflow never reads Roblox memory or injects into the process. It observes screen pixels and emits ordinary Windows keyboard and mouse input.
