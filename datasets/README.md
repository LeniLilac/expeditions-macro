# Detector image dataset

This repository includes 475 image fixtures used to build and regression-test the bundled Anime Expeditions detector pack, specialized UI detectors, and automatic camera-region selection. Of these, 472 are 808 by 611 Roblox client captures: 286 Expeditions fixtures, 81 selective Challenge fixtures, 50 Story/Raid/team/placement fixtures, 9 cross-mode navigation variants, 15 camera fixtures, 6 experimental resource-refuel fixtures, 14 game-settings fixtures, and 11 Event fixtures. The remaining three are privacy-safe 304 by 192 grayscale camera composites derived from a reported runtime-alignment failure. The compiled pack in `detector-packs/` is sufficient to run the released application; these images are development and test fixtures.

Captures are 808 by 611 PNG files organized under:

`datasets/anime-expeditions/expeditions/<dataset-name>/`

Challenge-mode fixtures are organized under `datasets/anime-expeditions/challenges/`; see that directory's README for provenance and privacy filtering.

Story, Raid, and saved-team fixtures are organized under `datasets/anime-expeditions/stages/`; see that directory's README for provenance and retained states.

Lobby-entry and post-match Story, Raid, Challenge, and Expedition detail variants are organized under `datasets/anime-expeditions/navigation-variants/`. These fixtures prove that **Enter Matchmaking** is optional and that navigation must use the live **Select Stage** action in either party context.

Camera-alignment fixtures are organized under `datasets/anime-expeditions/camera-rotations/`. Each of the three Expedition and four Story/Challenge maps contributes a goal frame plus a clearly incorrect yaw from a continuous right-arrow rotation. These fixtures verify that automatic setup selects four textured regions spanning the left, center, and right of the client and keeps an incorrect yaw well below the alignment threshold. The `RuntimeProjectionDrift` composites additionally verify that final alignment accepts a coherent cross-session vertical projection shift without broadening the thumbnail atlas or accepting a nearby wrong yaw. `UnrenderedWorld` preserves the textureless blue load failure that must be rejected before the macro moves the camera or places units.

Experimental Areas, Gold Mine, and Resource Drill fixtures are organized under `datasets/anime-expeditions/refuel/`. They support only the Debug-page route calibration tool; no released Macro task or automatic refuel schedule consumes them.

Startup settings fixtures are organized under `datasets/anime-expeditions/settings/`. They cover the closed Lobby, the Settings opening animation, supported UI Scale values, each required settings page, and the Units page at both scroll boundaries.

Villain Invasion Event fixtures are organized under `datasets/anime-expeditions/events/`. They cover the Event catalog when another Event is initially selected, Villain Invasion home, act selection/detail, the horizontally scrolled Act 4 selector and detail, prestart, Defeat, and reviewed Victory action rails—with and without **Next Stage**, including the final Act 4 rail. Event navigation uses these specialized fixtures and remains separate from the shared Play-interface detector because Event is available only from Lobby.

The current builder recognizes these dataset names:

- `Roblox_Disconnect`
- `Lobby_UI`, `Lobby_UI2`, `Play_UI`
- `Expedition_Map_Select_Map1`, `Map2`, and `Map3`
- `Expedition_Map_Select_Difficultly1`, `Difficultly2`, `Difficultly3`, and `Difficultly3_Animation`
- `Expedition_Map_Preview_Map1`, `Expedition_Map1_Prestart`, `Expedition_Midgame_Start`
- `Expedition_Checkpoint`, `Expedition_Checkpoint_Node`, `Expedition_Checkpoint_Extract_Confirm`
- `Expedition_Continue_Button`, `Expedition_Continue_Button_Confirm`
- `Expedition_Reward_Select`, `Select2`, `Select3`, `Select4`, and `Select5`
- `Expedition_Victory_UI`, `Expedition_Defeat_UI`, `Expedition_Empty_Unit_Bar`
- `Expedition_Defense_Node`, `Assault_Node`, `Elite_Node`, `Boss_Node`

The three `Difficultly*_LayoutShift` folders and `AFK_Chamber` are golden-test fixtures for specialized app detectors; they are not reference-builder inputs. The `Expedition_Midgame_Start` dataset includes hovered-button frames from a reported long-running stall.

`Expedition_Reward_Transition` contains purple, gold, and blue reward layouts while one card is still collapsed or moving. `Expedition_Reward_Select5` preserves three consecutive high-saturation blue-overlay frames from a beta.16 run where the old broad header scan rejected the live seven-pixel progress bar as a thick cyan band. `Expedition_Gameplay_Negative` contains ordinary full-match frames that previously resembled the legacy three-region reward template. The transition and gameplay-negative folders are specialized-detector regression fixtures and are not builder inputs.

`Expedition_Continue_Button_Confirm_006.png` reproduces a reported confirmation stall. The player-name/title area above the modal was replaced with an opaque rectangle before the fixture entered Git history; the Roblox client dimensions and confirmation pixels are unchanged.

`Expedition_Recovery_Navigation_Negative` contains compact Map 2 and Map 3 full-run samples. Some ordinary Map 2 gameplay frames resemble the map selector, so these fixtures verify that navigation-only matches cannot start recovery without a Lobby, Disconnect, or AFK root state.

`Play_UI` includes different avatars, current maps, reward icons, and Roblox UI scale/layout variants. Play-screen detection must use the stable Expedition tile structure rather than those changing details.

`Expedition_Map_Select_Selection_Regression` contains English and French selector screens that reproduced false "map could not be selected" errors. The beta.9 Map 1 fixture also preserves a bright selected-row preview that made the former all-rows-dark structural gate reject an otherwise unambiguous active marker. A 2026-07-27 current-layout fixture adds green environmental lighting and a two-pixel map-card-rail phase shift that made fixed perimeter regions prefer inactive card artwork. Map selection must use the isolated cyan active-row marker rather than localized map-name text, search only a small shared vertical offset for the complete rail, and must not score the selected row's changing artwork as an inactive dark panel.

The `CurrentUI*.png` files in the three map and difficulty folders preserve the 2026-07-25 full-screen Expedition selector. Its active map is owned by the cyan perimeter on the left-side card rail, while difficulty is owned by the current lower-left green, red, or purple control. The old compact selector remains in the same folders as a supported legacy layout.

`LobbyEventTheme.png` preserves the red Event-theme Lobby whose bright railing resembles the Settings close circle. Settings detection must require independent dark panel geometry before accepting that red component. `GraphicsPageCurrent.png` includes the new **Event Theme Enabled** control, which the startup profile disables. `MiscellaneousPageCurrent.png` and `MiscellaneousPageEventUpdate.png` preserve both observed vertical layouts for the required Misc controls. `LobbyExitConfirmation.png` covers the accessibility-owned Back-to-Lobby confirmation.

When extending the dataset, use several captures per state across lighting and moving-object variations. Crop to the Roblox client area and do not include desktop chrome, other applications, notifications, account names, webhook tokens, or chat content.
