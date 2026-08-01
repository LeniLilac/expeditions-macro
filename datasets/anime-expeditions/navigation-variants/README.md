# Navigation variants

These fourteen reviewed 808 by 611 Roblox client captures preserve mode-detail action rails, Roblox chat states, the in-match lobby door, and its exit confirmation.

- `Lobby_*` frames were reached from the lobby. They show **Select Stage** alongside the purple **Enter Matchmaking** action.
- `PostMatch_*` frames were reached by pressing the configured Play key on a completed match and choosing **Change Gamemode**. The existing party persists, so these screens omit **Enter Matchmaking** and retain a wider **Select Stage** action.

The fixtures come from passive deep-debug diagnostic captures supplied on 2026-07-22. The lobby Story frame's join notification was replaced with an opaque rectangle at client coordinates `(55, 0)` through `(625, 90)` before entering Git history. No mode-detail detector or action region intersects that redaction. The other seven retained frames contain no account names, chat, desktop chrome, notifications, or secrets and are otherwise unmodified.

- `ChatClosed.png` and `ChatOpen.png` are privacy-safe derivatives of 72 canonical observations: 33 closed and 39 open. Only the fixed chat-button region remains on a black canvas. The detector owns the shared speech-bubble outline and tail, then distinguishes the filled open body from the outlined closed body without counting notification badges or adjacent controls as state evidence. Full-frame badge, thicker-outline, microphone, and cross-state regressions remain in the other reviewed dataset roots.
- `MatchLobbyDoor_NoVoiceChat.png` and `MatchLobbyDoor_VoiceChat.png` retain the opaque white door at the two field-observed fixed top-bar offsets. Seven supplied 2026-07-27 capture sets contained 228 consistent observations across moving maps. The optional voice slot rendered more than one glyph, so the detector intentionally ignores it and matches the door itself. The player identity/avatar area at client coordinates `(380, 265)` through `(444, 324)` was replaced with an opaque rectangle in both retained frames; the complete top bar and every detector/action pixel are unchanged.
- `MatchLobbyDoor_HighContrastNoVoice.png` is a privacy-safe top-bar-only derivative of a 2026-08-01 automatic Bounty recovery capture. It preserves the thicker opaque door, arrow, and handle that remained visible across 47 rejected observations after School Grounds reached its safe exit wave; every pixel below the Roblox top bar was replaced with black.
- `LobbyExitConfirmation.png` retains the verified red **Return to Lobby** action from the prior field-confirmed transition. The top bar still carries the old accessibility selection outline, but current automation neither requires nor produces it.

These fixtures are specialized cross-mode regression data. They are not inputs to the compiled Expedition detector-pack builder.
