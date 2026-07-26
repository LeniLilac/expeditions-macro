# Startup game-settings fixtures

These fourteen 808 by 611 Roblox client captures reproduce the startup
settings-normalization flow:

- a fully loaded Lobby with Settings closed;
- an in-progress Settings opening animation;
- the settled panel at UI Scale 0.80, 1.00, and 1.20;
- the Gameplay, Graphics, Units, and two observed Misc page layouts;
- both the top and bottom Units scrollbar positions.

The fixtures were selected from reviewed local diagnostic archives on
2026-07-25. Only representative client frames were retained. The source
archives, repeated frames, and timestamped review contact sheets are not
tracked.

Every retained image was visually reviewed. The images contain only
the canonical Anime Expeditions client—no desktop chrome, chat,
account name, notification, webhook, private-server link, or Windows
profile path.

`SettingsOpeningTransition.png` is intentionally a negative fixture:
the workflow must wait for the panel geometry to settle before it
uses any coordinate-based page or toggle action.

`MiscellaneousPageEventUpdate.png` preserves the compact control rows
from a beta.23 startup failure. The player/title strip above Settings was
replaced with an opaque rectangle; no settings detector or action region
intersects that redaction.
