# Story, Raid, and team fixtures

This directory contains 22 reviewed 808 by 611 Roblox client-area frames used by the Story, Raid, and saved-team detectors. The retained PNGs contain no account names, chat, notifications, desktop chrome, or secrets. Captured UI pixels remain unscaled and unmodified except for the documented account-name redaction; the three Story wide-button fixtures use documented black padding outside their supplied panel crops.

Sources:

- `story win.zip` and `story loss.zip`: 100 ms captures covering the Story selector, School Grounds Act detail, Victory, and Defeat.
- `mastery win.zip`, `mastery loss.zip`, and `inf loss.zip`: 100 ms captures covering the Mastery and Infinite terminal variants.
- `raid win.zip` and `raid loss.zip`: 100 ms captures covering the Raid selector, Spirit City detail, Victory, and Defeat.
- `team switch.zip`: a 100 ms capture covering Units, Unit Teams, Load Team confirmation, Include Equipment confirmation, and a game-mode selector negative.
- `deep-debug-macro-plan-20260722-094454-1ece431d44f44fafa273ffa1b9e9b5ae.zip`: a reported three-action Raid party preview. The display-name strip at client coordinates `(460, 273)` through `(580, 317)` was replaced with an opaque rectangle; the preview buttons and all detector regions are unchanged.
- `deep-debug-macro-plan-20260722-104319-c1bb43687b4f441cae9ef2a5b6863960.zip`: a reported King's Tomb Mastery detail screen whose purple mode accent must still retain the Story Select Stage action.
- `deep-debug-macro-plan-20260722-104413-7308040c92b046c0a490665eda2abd07.zip`: a reported compact Include Equipment dialog positioned 33 pixels above the original team-switch reference.
- `Screenshot 2026-07-22 105224.png`, `105218.png`, and `105220.png`: privacy-reviewed panel crops showing the full-width Select Stage layout for Act/cyan, Infinite/green, and Mastery/purple. Each crop was placed without scaling on a black 808 by 611 canvas by aligning its red Close control to client coordinate `(673, 155)`. The resulting offsets are `(99, 90)`, `(120, 105)`, and `(106, 98)` respectively; no captured UI pixels were recolored.

The full bursts remain local diagnostic artifacts. Only structurally distinct frames needed for detector and cross-state regression are retained here.
