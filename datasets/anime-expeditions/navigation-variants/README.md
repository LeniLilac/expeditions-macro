# Navigation variants

These eleven reviewed 808 by 611 Roblox client captures preserve mode-detail action rails, the in-match lobby door, and its exit confirmation.

- `Lobby_*` frames were reached from the lobby. They show **Select Stage** alongside the purple **Enter Matchmaking** action.
- `PostMatch_*` frames were reached by pressing the configured Play key on a completed match and choosing **Change Gamemode**. The existing party persists, so these screens omit **Enter Matchmaking** and retain a wider **Select Stage** action.

The fixtures come from passive deep-debug diagnostic captures supplied on 2026-07-22. The lobby Story frame's join notification was replaced with an opaque rectangle at client coordinates `(55, 0)` through `(625, 90)` before entering Git history. No detector or action region intersects that redaction. The other seven retained frames contain no account names, chat, desktop chrome, notifications, or secrets and are otherwise unmodified.

- `MatchLobbyDoor_NoVoiceChat.png` and `MatchLobbyDoor_VoiceChat.png` retain the opaque white door at the two field-observed fixed top-bar offsets. Seven supplied 2026-07-27 capture sets contained 228 consistent observations across moving maps. The optional voice slot rendered more than one glyph, so the detector intentionally ignores it and matches the door itself. The player identity/avatar area at client coordinates `(380, 265)` through `(444, 324)` was replaced with an opaque rectangle in both retained frames; the complete top bar and every detector/action pixel are unchanged.
- `LobbyExitConfirmation.png` retains the verified red **Return to Lobby** action from the prior field-confirmed transition. The top bar still carries the old accessibility selection outline, but current automation neither requires nor produces it.

These fixtures are specialized cross-mode regression data. They are not inputs to the compiled Expedition detector-pack builder.
