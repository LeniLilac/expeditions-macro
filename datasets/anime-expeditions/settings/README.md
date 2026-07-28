# Startup game-settings fixtures

These fifteen 808 by 611 Roblox client captures reproduce the startup
settings-normalization flow:

- a fully loaded Lobby with the no-voice Settings gear closed;
- the shifted Roblox Settings gear when the top bar includes a voice control;
- an in-progress Settings opening animation;
- the settled panel at UI Scale 0.80, 1.00, and 1.20;
- the Gameplay, Graphics, Units, and two observed Misc page layouts;
- both the top and bottom Units scrollbar positions.

The fixtures were selected from reviewed local diagnostic archives on
2026-07-25 and 2026-07-27. Only representative client frames were retained. The source
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

`SettingsButtonVoiceClosed.png` retains only the privacy-safe top-bar
pixels from a field capture and replaces the rest of the canonical client
frame with opaque black. It preserves the exact opaque gear glyph at
`(276, 34)` and the variable translucent pill behind it. No player,
account, chat, desktop, notification, or secret-bearing pixels remain.

`LobbyClosed.png` and `SettingsButtonVoiceClosed.png` are the two fixed
gear-offset anchors. Focused detector tests derive brightened variants
from both images to preserve the field-observed high-contrast opaque
outline without adding duplicate corpus files. The detector still
requires the gear structure and distinguishes its outline from the
filled selected state; the optional microphone/headset pixels are never
used as evidence.
