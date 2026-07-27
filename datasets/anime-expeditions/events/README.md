# Villain Invasion Event fixtures

These seventeen reviewed 808 by 611 Roblox client frames cover the
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
