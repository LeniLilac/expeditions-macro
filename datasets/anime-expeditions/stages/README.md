# Story, Raid, and team fixtures

This directory contains 27 reviewed 808 by 611 Roblox client-area frames used by the Story, Raid, and saved-team detectors. The retained PNGs contain no account names, chat, notifications, desktop chrome, or secrets. Captured UI pixels remain unscaled and unmodified except for the documented account-name redactions; the three Story wide-button fixtures use documented black padding outside their supplied panel crops.

Sources:

- `story win.zip` and `story loss.zip`: 100 ms captures covering the Story selector, School Grounds Act detail, Victory, and Defeat.
- `mastery win.zip`, `mastery loss.zip`, and `inf loss.zip`: 100 ms captures covering the Mastery and Infinite terminal variants.
- `raid win.zip` and `raid loss.zip`: 100 ms captures covering the Raid selector, Spirit City detail, Victory, and Defeat.
- `team switch.zip`: a 100 ms capture covering Units, Unit Teams, Load Team confirmation, Include Equipment confirmation, and a game-mode selector negative.
- `deep-debug-macro-plan-20260722-094454-1ece431d44f44fafa273ffa1b9e9b5ae.zip`: a reported three-action Raid party preview. The display-name strip at client coordinates `(460, 273)` through `(580, 317)` was replaced with an opaque rectangle; the preview buttons and all detector regions are unchanged.
- `deep-debug-macro-plan-20260722-104319-c1bb43687b4f441cae9ef2a5b6863960.zip`: a reported King's Tomb Mastery detail screen whose purple mode accent must still retain the Story Select Stage action.
- `deep-debug-macro-plan-20260722-104413-7308040c92b046c0a490665eda2abd07.zip`: a reported compact Include Equipment dialog positioned 33 pixels above the original team-switch reference.
- `Screenshot 2026-07-22 105224.png`, `105218.png`, and `105220.png`: privacy-reviewed panel crops showing the full-width Select Stage layout for Act/cyan, Infinite/green, and Mastery/purple. Each crop was placed without scaling on a black 808 by 611 canvas by aligning its red Close control to client coordinate `(673, 155)`. The resulting offsets are `(99, 90)`, `(120, 105)`, and `(106, 98)` respectively; no captured UI pixels were recolored.
- `deep-debug-macro-plan-20260722-120245-c537a052fc414b20bdc3740f4cde6c8b.zip`: a settled King's Tomb Mastery party preview positioned 27 pixels above the original Raid rail. The display-name strip at client coordinates `(465, 292)` through `(590, 334)` was replaced with an opaque rectangle; the three preview buttons and all detector regions are unchanged.
- `deep-debug-macro-plan-20260722-115310-40ec764b9eda41c483c407789764c1a1.zip`: the current compact Raid Victory layout with separate Next Stage, Repeat Stage, and View Party actions. The retained frame contains no account names or other private information and is otherwise unmodified.
- `deep-debug-macro-plan-20260722-130822-f1539b6d301141fbbfaa7162f220fff6.zip`: a current King's Tomb Mastery Victory that the generic Challenge terminal detector confused with Defeat, plus the resulting Story post-match party where Change Gamemode replaces Disband. The party display-name strip at client coordinates `(460, 273)` through `(590, 317)` was replaced with an opaque rectangle; all terminal, navigation, and action-detector pixels are unchanged.
- `deep-debug-macro-plan-20260722-131337-69d254a6ff604443b65c602d39444a2d.zip`: a Spirit City Raid post-match party with a disabled Start action and active Change Gamemode action. The display-name strip at client coordinates `(460, 250)` through `(590, 317)` was replaced with an opaque rectangle; all navigation and action-detector pixels are unchanged.

The full bursts remain local diagnostic artifacts. Only structurally distinct frames needed for detector and cross-state regression are retained here.
