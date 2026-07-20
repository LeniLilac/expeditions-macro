# Challenge mode fixtures

This directory contains selective 808 by 611 Roblox client-area frames used by the Challenge-mode detectors. Fixtures are not rescaled or cropped; the privacy redactions documented below are the only intentional pixel changes.

Sources:

- `Challenge Run 1.zip`: the initial full manual run used to establish selector, preview, prestart, gameplay, Victory, Defeat, and Expeditions-handoff states.
- `challenge run 2.zip`: a 2,366-frame manual run from a different player and PC, captured by app version 1.0.13 at 300 ms intervals. Nineteen representative frames were retained for cross-PC selector permutations, four map prestarts, ordinary gameplay, cooldown, Victory, Defeat, and handoff coverage.
- `chal run 4.zip`: a 698-frame app-version 1.0.14 run with a roughly 5% larger in-game Challenges panel. Seven privacy-safe frames were retained for the larger list, available/cooldown details, tooltip/loading states, and game-mode selector.
- `Challenge run 3.zip`: a 1,197-frame app-version 1.0.14 run captured at 500 ms intervals. Nine representative frames were retained for the School/Rose/Flower selector set, Flower Forest detail/prestart/gameplay, two wider Victory panels, the hovered Victory close control, and a Rose Kingdom defeat.
- `diagnostic-capture.zip`: an 80-frame app-version 1.1.0 capture at 100 ms intervals. One representative frame was retained for the private-party preview variant with green Start and red Disband controls.
- `diagnostic-capture.zip` (2026-07-20 12:22 UTC): a seven-frame app-version 1.1.0 capture at one-second intervals. One frame was retained for a selector timeout where hovering one entry dimmed the other two row emblems from cyan to gray.
- `diagnostic-capture.zip` (2026-07-20 12:56 UTC): a six-frame app-version 1.1.0 capture at one-second intervals. One frame was retained for the gray Regular Challenge selector shown after all three entries were completed in the current 30-minute window.
- `diagnostic-capture.zip` (2026-07-20 13:23 UTC): a five-frame app-version 1.1.0 capture at one-second intervals. One frame was retained for a game-mode selector whose bright Challenge artwork invalidated the former dark-tile-content check.

The full bursts remain local diagnostic artifacts. Frames containing a visible player or display name in party previews are excluded unless the identifying text can be isolated from detector evidence. The display-name strip in `Prestart_FlowerForest_01.png`, `GameplayNegative_08.png`, and `PreviewReady_03.png` was replaced with an opaque rectangle; every other retained fixture preserves its original pixels.

`ChallengeDetailTooltipNegative` covers an available Challenge whose reward tooltip obscures Select Stage. It must never be classified as Victory or Defeat. `GameModeSelector_05.png` verifies that selector recognition uses the four mode headings and fixed row/column dividers rather than variable tile artwork. `ChallengeList_11.png` preserves the reported dimmed-emblem selector state and verifies that list recognition uses the repeated three-row structure instead of emblem color alone. `ChallengeListUnavailable_01.png` covers the gray selector that means the current 30-minute Regular Challenge window is complete; one observation is not treated as proof of the daily limit.
