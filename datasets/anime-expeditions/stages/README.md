# Story, Raid, and team fixtures

This directory contains 17 reviewed 808 by 611 Roblox client-area frames used by the Story, Raid, and saved-team detectors. The retained PNGs contain no account names, chat, notifications, desktop chrome, or secrets. All preserve their original pixels except the documented account-name redaction below.

Sources:

- `story win.zip` and `story loss.zip`: 100 ms captures covering the Story selector, School Grounds Act detail, Victory, and Defeat.
- `mastery win.zip`, `mastery loss.zip`, and `inf loss.zip`: 100 ms captures covering the Mastery and Infinite terminal variants.
- `raid win.zip` and `raid loss.zip`: 100 ms captures covering the Raid selector, Spirit City detail, Victory, and Defeat.
- `team switch.zip`: a 100 ms capture covering Units, Unit Teams, Load Team confirmation, Include Equipment confirmation, and a game-mode selector negative.
- `deep-debug-macro-plan-20260722-094454-1ece431d44f44fafa273ffa1b9e9b5ae.zip`: a reported three-action Raid party preview. The display-name strip at client coordinates `(460, 273)` through `(580, 317)` was replaced with an opaque rectangle; the preview buttons and all detector regions are unchanged.

The full bursts remain local diagnostic artifacts. Only structurally distinct frames needed for detector and cross-state regression are retained here.
