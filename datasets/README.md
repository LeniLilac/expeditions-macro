# Detector image dataset

This repository includes 336 Roblox client-area captures used to build and regression-test the bundled Anime Expeditions detector pack and the app's specialized UI detectors: 269 Expeditions fixtures and 67 selective Challenge fixtures. The compiled pack in `detector-packs/` is sufficient to run the released application; these images are development and test fixtures.

Captures are 808 by 611 PNG files organized under:

`datasets/anime-expeditions/expeditions/<dataset-name>/`

Challenge-mode fixtures are organized under `datasets/anime-expeditions/challenges/`; see that directory's README for provenance and privacy filtering.

The current builder recognizes these dataset names:

- `Roblox_Disconnect`
- `Lobby_UI`, `Lobby_UI2`, `Play_UI`
- `Expedition_Map_Select_Map1`, `Map2`, and `Map3`
- `Expedition_Map_Select_Difficultly1`, `Difficultly2`, `Difficultly3`, and `Difficultly3_Animation`
- `Expedition_Map_Preview_Map1`, `Expedition_Map1_Prestart`, `Expedition_Midgame_Start`
- `Expedition_Checkpoint`, `Expedition_Checkpoint_Node`, `Expedition_Checkpoint_Extract_Confirm`
- `Expedition_Continue_Button`, `Expedition_Continue_Button_Confirm`
- `Expedition_Reward_Select`, `Select2`, `Select3`, and `Select4`
- `Expedition_Victory_UI`, `Expedition_Defeat_UI`, `Expedition_Empty_Unit_Bar`
- `Expedition_Defense_Node`, `Assault_Node`, `Elite_Node`, `Boss_Node`

The three `Difficultly*_LayoutShift` folders and `AFK_Chamber` are golden-test fixtures for specialized app detectors; they are not reference-builder inputs. The `Expedition_Midgame_Start` dataset includes hovered-button frames from a reported long-running stall.

`Expedition_Reward_Transition` contains purple, gold, and blue reward layouts while one card is still collapsed or moving. `Expedition_Gameplay_Negative` contains ordinary full-match frames that previously resembled the legacy three-region reward template. Both are specialized-detector regression fixtures and are not builder inputs.

`Expedition_Continue_Button_Confirm_006.png` reproduces a reported confirmation stall. The player-name/title area above the modal was replaced with an opaque rectangle before the fixture entered Git history; the Roblox client dimensions and confirmation pixels are unchanged.

`Expedition_Recovery_Navigation_Negative` contains compact Map 2 and Map 3 full-run samples. Some ordinary Map 2 gameplay frames resemble the map selector, so these fixtures verify that navigation-only matches cannot start recovery without a Lobby, Disconnect, or AFK root state.

`Play_UI` includes different avatars, current maps, reward icons, and Roblox UI scale/layout variants. Play-screen detection must use the stable Expedition tile structure rather than those changing details.

When extending the dataset, use several captures per state across lighting and moving-object variations. Crop to the Roblox client area and do not include desktop chrome, other applications, notifications, account names, webhook tokens, or chat content.
