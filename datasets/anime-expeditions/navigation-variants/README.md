# Mode-detail navigation variants

These eight reviewed 808 by 611 Roblox client captures preserve the two observed action-rail variants for Story, Raid, Challenge, and Expedition detail screens.

- `Lobby_*` frames were reached from the lobby. They show **Select Stage** alongside the purple **Enter Matchmaking** action.
- `PostMatch_*` frames were reached by pressing the configured Play key on a completed match and choosing **Change Gamemode**. The existing party persists, so these screens omit **Enter Matchmaking** and retain a wider **Select Stage** action.

The fixtures come from passive deep-debug diagnostic captures supplied on 2026-07-22. The lobby Story frame's join notification was replaced with an opaque rectangle at client coordinates `(55, 0)` through `(625, 90)` before entering Git history. No detector or action region intersects that redaction. The other seven retained frames contain no account names, chat, desktop chrome, notifications, or secrets and are otherwise unmodified.

These fixtures are specialized cross-mode regression data. They are not inputs to the compiled Expedition detector-pack builder.
