# Changelog

All notable changes to Expeditions Macro are documented here.

## [Unreleased]

## [1.0.11] - 2026-07-19

### Added

- Added an unlimited diagnostic screenshot capture under Settings that temporarily uses the standard Roblox client size, restores the original window, and writes same-name ZIPs containing PNG frames and a local manifest.

### Fixed

- Prevented Play, map selector, and map preview lookalikes from initiating recovery during an active match unless a stable AFK, disconnect, or lobby root state is present.
- Prioritized active Start, reward, checkpoint, continue, confirmation, victory, and defeat states over navigation-only visual collisions.
- Replaced opaque `VisionScorer` type-initializer failures with actionable computer-vision startup errors.
- Bundled the Microsoft Visual C++ 2015-2022 x64 runtime required by OpenCvSharp in portable and installer releases.

### Tests

- Added compact Map 2 and Map 3 gameplay regressions selected from two complete manual runs and scanned both full timelines for recovery-state collisions.
- Extended release verification to require OpenCV and Visual C++ native dependencies in the portable archive.

## [1.0.10] - 2026-07-19

### Fixed

- Recognized reward selection while one card is still collapsed or moving by combining the stable blue reward overlay, segmented progress header, and the remaining Select Upgrade controls.
- Removed the legacy three-region reward fallback that could classify ordinary gameplay as a reward at high confidence.
- Prevented reward animations from matching the Play screen and starting an unnecessary lobby rejoin.
- Required recovery states to remain present across consecutive captures before abandoning a run, preserving boss-node progress and checkpoint extraction when one transient frame resembles a recovery screen.

### Tests

- Added reviewed purple, gold, and blue reward-transition captures plus ordinary-gameplay negatives selected from a complete manual match run.

## [1.0.9] - 2026-07-19

### Added

- Added automatic Components V2 release announcements to the public Discord release channel, including the Release Ping role and direct release downloads.
- Added a repository-wide `AGENTS.md` development guide covering architecture boundaries, Roblox input invariants, detector fixtures, testing, privacy, and releases.

### Fixed

- Recognized the Play screen from the stable Expedition title and footer structure across changing map names, artwork, reward icons, avatars, and UI scale, then mapped the click through the detected layout.
- Prevented the adaptive Play detector from stealing scaled or translated lobby frames.
- Allowed camera setup to verify and fine-sweep a provisional full-turn peak as low as roughly 75% when the following yaw view confirms the wraparound, while preserving the strong refined acceptance threshold.
- Retained the best continuation-verified camera candidate for one final refinement attempt instead of discarding it when the coarse scan reaches its sample limit.

## [1.0.8] - 2026-07-18

### Added

- Added a persistent **Join Discord** button that opens the public Expeditions Macro community invite in the default browser.

### Fixed

- Followed automation cursor moves with a relative motion pulse so Roblox acknowledges the parked cursor location and reliably clears button hover styling.
- Waited for the non-hovered button render after parking before the macro resumes visual detection.

## [1.0.7] - 2026-07-18

### Added

- Added inactivity recovery from the AFK Chamber through Return to Lobby and the existing configured-route rejoin flow.
- Added public AFK Chamber and hovered Start-button regression captures from the reported long-running session.

### Fixed

- Moved the cursor to a neutral client edge after every simulated click so hover styling cannot poison later button detection.
- Recognized the Start Game button while hovered or during its transition animation without matching unrelated captured UI states.

## [1.0.4] - 2026-07-18

### Fixed

- Allowed camera-model setup to recognize a degraded full-turn return when the following yaw view also repeats the start of the scan, while rejecting isolated lookalike landmarks.
- Added a fine-drag sweep around the detected wraparound so camera models store the measured full-yaw circumference and finish setup at the highest-confidence goal position.

## [1.0.3] - 2026-07-18

### Added

- Added a measured full-turn camera scan when the fast yaw-atlas alignment finishes below the model target.

### Changed

- Standardized camera calibration and placement recording on the detector pack's 808 × 611 Roblox client size, with original window bounds restored afterward.
- Reflowed camera and placement model guidance above their controls to give model inputs and previews the full content width.

### Fixed

- Prevented unit placement from starting when camera alignment remains below the model confidence target after fallback scanning.
- Verified that Roblox accepted the required client size before the Expeditions macro continues.

## [1.0.2] - 2026-07-18

### Added

- Added shifted-layout difficulty captures to the public golden-image regression dataset.
- Added detector pack 1.0.1 with explicit difficulty hue metadata.

### Fixed

- Replaced fragile grayscale-only difficulty verification with fast green, red, and magenta active-state detection that tolerates the game's six-pixel UI shift.
- Displayed only saved names in preset, camera-model, and placement-model selectors.
- Prevented placement status text and Expeditions status content from overlapping nearby action controls at constrained widths.

## [1.0.1] - 2026-07-18

### Fixed

- Prevented camera-region details from overlapping the region and overlay controls.
- Centered single-line input text so focused values are no longer vertically clipped at Windows display scaling.
- Made camera-model setup and standalone alignment temporarily enable shift lock and restore it after success, cancellation, or failure.
- Allowed the UI snapshot renderer to run while the normal single app instance is open, without changing normal single-instance behavior.

## [1.0.0] - 2026-07-18

### Added

- Unified native Windows app for Expeditions runs, camera-model calibration, and placement-model recording, editing, and testing.
- Roblox-relative capture regions and placement coordinates with temporary client-size restoration.
- Full-turn yaw learning with coarse shortest-path alignment and fine mouse-drag correction.
- Detector-pack-driven lobby, map, difficulty, node, reward, checkpoint, victory, defeat, and disconnect handling.
- Configurable checkpoint extraction, including the first checkpoint or the first checkpoint after a chosen number of boss nodes.
- Lobby and disconnect recovery, including direct starts from the lobby.
- Optional Discord Components V2 reports with protected webhook storage.
- Dark, light, and system themes; F6 start/stop; local logs; detector-pack updates; portable and installer releases.
- Reproducible detector fixtures with full golden-image regression coverage in public CI.

[Unreleased]: https://github.com/LeniLilac/expeditions-macro/compare/v1.0.11...HEAD
[1.0.11]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.11
[1.0.10]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.10
[1.0.9]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.9
[1.0.8]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.8
[1.0.7]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.7
[1.0.4]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.4
[1.0.3]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.3
[1.0.2]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.2
[1.0.1]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.1
[1.0.0]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.0
