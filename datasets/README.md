# Local detector datasets

Raw Roblox captures are intentionally ignored by Git and are not part of the public repository. The compiled detector pack in `detector-packs/` is sufficient to run the application.

To rebuild or regression-test the Anime Expeditions pack, place 808 by 611 client-area PNG captures under:

`datasets/anime-expeditions/expeditions/<dataset-name>/`

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

Use several captures per state across lighting and moving-object variations. Do not include desktop chrome, other applications, notifications, account names, webhook tokens, or chat content.
