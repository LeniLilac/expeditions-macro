# Villain Invasion Event fixtures

These eighteen reviewed 808 by 611 Roblox client frames cover the
Villain Invasion Event catalog, home, act selection, prestart, and
terminal states. They are specialized detector regressions and are not
inputs to the compiled Expedition detector-pack builder.

The six `*_Current_*.png` and `EventHome_BeginnerPathPresent_*.png`
fixtures were derived from the paired `diagnostic-capture.zip` and
`diagnostic-capture(1).zip` field captures supplied on 2026-07-27.
Each source contained three settled frames. The source archives and logs
remain local and are not tracked.

- `EventCatalog_BeginnerPathSelected_Current_*.png` preserves the cyan
  selected Beginner's Path tab and the thin red, unselected Villain
  Invasion rail. The card action remains at client coordinate
  `(94, 183)`.
- `EventHome_BeginnerPathPresent_*.png` preserves the wide red Villain
  Invasion tab, its opaque white chevron, and the red Event Gamemode
  action. These fixtures prove that an already-selected Event does not
  receive another card click.

Only the left Event header/card rail and, for the home fixtures, the
Event Gamemode action remain from the supplied frames. All unrelated
pixels to their right were replaced with opaque black. The retained
pixels contain no account names, chat, desktop chrome, notifications,
or secrets. Detector evidence does not use character or map artwork.

`ActSelector_CurrentShifted.png` was derived from a beta.32 Deep Debug
session supplied on 2026-07-27. The field layout keeps the selected
Villain Invasion sidebar and a full-width red selector rail, which made
the older detector report Event Home after the Event Gamemode click.
Only the Event header and selected tabs, the Act Selection title and
opaque subtitle, and the bottom selector rail remain; all character
artwork and unrelated pixels are opaque black.

The specialized detector evaluates this shifted Act selector before
Event Home. The selected Villain Invasion sidebar is shared evidence,
so the full-width selector rail and opaque subtitle are required before
selector ownership can win. Event Home remains independently owned by
the selected Villain Invasion card plus the live Event Gamemode action;
its decorative red header may still be loading. A deterministic
privacy-safe transform of the retained Home fixture removes the second
lower-border row and reproduces the one-pixel border thickness observed
in a beta.34 field failure without retaining that private capture.
