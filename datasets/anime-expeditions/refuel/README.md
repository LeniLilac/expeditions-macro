# Resource-refuel fixtures

This directory contains eleven reviewed 808 by 611 Roblox client captures for
the Areas, Gold Mine, and Resource Drill detectors.

The original six frames come from the passive 419-frame diagnostic capture
supplied on 2026-07-24. The four fuel-state variants come from canonical
passive captures reviewed on 2026-07-29. `AreasLobby_01.png` comes from the
same-day passive 92-frame `lobby.zip` sequence. The original active player-name
area was replaced with opaque black rectangles before those images entered Git
history. The Lobby-category frame has an opaque black rectangle over client
coordinates `(470, 105)` through `(655, 150)`; its category and Spawn detector
regions remain unchanged. The later captures contain no visible account
identity. No fixture contains desktop chrome, chat, notifications, webhook
data, private-server links, or other secrets.

- `AreasMenu_01.png`: Areas opened on its default Upgrade section.
- `AreasExpeditions_01.png`: Expeditions selected with Expeditions Hub ready.
- `AreasLobby_01.png`: Lobby category selected with its live Spawn card.
- `GoldMine_01.png`: Gold Mine station panel with Add Fuel available.
- `GoldMine_MissingFuel_01.png`: current Gold Mine panel with Missing Fuel.
- `GoldMine_FuelPresent_01.png`: current Gold Mine panel with fuel present.
- `GoldMine_AddFuel_01.png`: Gold Mine Add Fuel quantity dialog.
- `ResourceDrill_01.png`: Resource Drill station panel with Add Fuel available.
- `ResourceDrill_MissingFuel_01.png`: current Resource Drill panel with Missing Fuel.
- `ResourceDrill_FuelPresent_01.png`: current Resource Drill panel with fuel present.
- `ResourceDrill_AddFuel_01.png`: Resource Drill Add Fuel quantity dialog.

Detector regression coverage also derives in-memory variants from these reviewed
captures. Areas ownership follows the five-row category rail and the live target
card frame; decorative panel borders and mutable page contents are not
authorization. Ordinary station ownership requires the station accent, red
Close component, both Building Stats bars, the Rewards frame, and a live Add
Fuel control. The Add Fuel dialog is independently owned by live Max, Confirm,
and Cancel controls plus both Stats bars and the Rewards frame. Click
coordinates follow those live controls when the owned layout moves within its
supported tolerance.
