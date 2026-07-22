# Deep Debug Viewer

Deep Debug Viewer is a local developer utility for replaying the screenshots and event stream inside an Expeditions Macro Deep Debug ZIP. It is part of the source tree for diagnosis and is not included in the installer or portable app.

## Run

Install the .NET 10 SDK, then double-click `Run Deep Debug Viewer.cmd` in this directory. You can also run:

```powershell
dotnet run --project tools/ExpeditionsMacro.DeepDebugViewer -- "C:\path\to\deep-debug.zip"
```

Choose **Open archive** or drag a ZIP onto the window. The file picker starts in `%LocalAppData%\ExpeditionsMacro\diagnostics` when that directory exists.

## Inspect a run

- Use **Play**, the timeline slider, the Left/Right arrow keys, or the previous/next buttons to move through captures.
- Choose 0.25x through 4x playback. Playback follows recorded capture timing, with long gaps capped at two seconds.
- Read the synchronized event rail for detector states, workflow decisions, generated keyboard/mouse input, and frame call sites around the selected screenshot.
- Choose a decoded-frame cache budget from 2 GB through 20 GB. The default is 10 GB. The viewer budgets actual Pbgra32 pixel memory, reads ahead around the current frame, and reports cache usage live.
- Choose **Clear cache** to release every cached frame. The frame currently displayed by WPF remains visible until another frame replaces it.

The viewer indexes `events.jsonl` once but streams PNG entries from the ZIP as needed. It does not extract the archive or load every screenshot into memory. Missing and corrupt frames remain in the timeline and display an inline error without stopping playback. A malformed event line is skipped and reported in the status area.

Deep Debug archives are expected to be sanitized by the app. The viewer does not read application settings, webhooks, or models from outside the selected archive, and it defensively redacts Discord webhook URLs and Discord-style user IDs if either appears in an event payload.
