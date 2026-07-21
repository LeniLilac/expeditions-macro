# Changelog

All notable changes to Expeditions Macro are documented here.

## [Unreleased]

## [1.2.0] - 2026-07-21

### Added

- Added automatic camera-region selection that chooses four stable, detailed map areas across the standard Roblox client while avoiding common HUD zones.
- Added a persisted signed fine-yaw neighborhood atlas so runtime alignment can reuse the micro-adjustment evidence learned during setup.
- Added a Settings calibration overlay for matching Roblox's rendered UI scale to the standard detector scale.
- Added **Test webhook** actions for validating optional Discord reporting without sending a screenshot or user mention.
- Added seven-map camera-rotation fixtures covering all Expedition maps and Story/Challenge map layouts.

### Changed

- Rebuilt camera setup around sensitivity-independent Left/Right-arrow pulses for coarse yaw and right-drag mouse movement only for fine refinement.
- Camera setup now standardizes zoom, top-down pitch, and temporary shift lock automatically; manual comparison-region selection is no longer required.
- Runtime alignment now makes up to three fresh attempts with alternating scan direction and sampling phase, using both the saved coarse atlas and fine neighborhood before each full-turn fallback.
- Changed new camera defaults to a 30 ms arrow hold and 100 ms settle time, with calibration controls hidden behind **Show tuning**.
- Camera models now use schema version 3. Existing camera models must be recorded again; placement models and presets remain reusable after selecting replacement camera models.

### Fixed

- Prevented low-confidence alignment from placing units or starting a match. After all three attempts fail, the macro exits the unstarted match safely, records diagnostics, and reports the skipped task; the Challenge scheduler advances to its next configured task while standalone Expeditions stops at the party preview.
- Parked the pointer inside the Roblox client with spaced acknowledged motion pulses so unit hover cards and highlighted selector rows clear before Start Game or map detection.
- Re-detected live Start Game and Play controls after parking instead of trusting a stale or partially covered action.
- Made camera-model replacement transactional with bounded retries and backup restoration when another process temporarily holds model files.
- Preserved the v1.1.6 live post-match Play detection and detector-pack 1.0.2 migration behavior while integrating the new camera workflow.

### Tests

- Added camera calibration, automatic-region, persisted-neighborhood, alternating-retry, safe-skip, hover-clear, and UI-scale overlay coverage.
- Added golden rotation checks proving incorrect yaw remains below the alignment threshold across three Expedition and four Story/Challenge maps.

## [1.1.6] - 2026-07-21

### Changed

- Upgraded the bundled detector pack to 1.0.2 with the five Challenge map references required by Challenge automation.
- Replaced older bundled detector packs automatically at startup, repaired corrupted same-version installations, and preserved genuinely newer user-installed packs.
- Included detector-pack identity, manifest hash, and Challenge-map capability in diagnostic capture manifests.

### Fixed

- Replaced the fixed post-match Play click with live detection of the bottom-left Play control, a safe detected-center click, and verified retries when Roblox does not transition.
- Stopped Challenge runs early with an actionable detector-pack update message when the active pack cannot recognize Challenge maps.

### Tests

- Added the reported post-match HUD and Challenge selector frames as privacy-reviewed regression fixtures.
- Added navigation retry, detector-pack migration, payload-integrity, capability, and reported-map recognition coverage.

## [1.1.5] - 2026-07-20

### Fixed

- Recognized the compact three-button Expedition party preview during the Challenge cooldown handoff, then clicked its detected **Change Gamemode** button instead of repeatedly clicking blank HUD space.

### Tests

- Added a privacy-redacted reproduction of the reported v1.1.4 stall and verified its 105-pixel-wide action at `(695, 352)` without accepting an unrelated narrow yellow control.

## [1.1.4] - 2026-07-20

### Added

- Added rename support for existing camera and placement models while preserving their stable model IDs and preset links.

### Changed

- Removed redundant page subtitles from the main app pages for a denser, cleaner layout.
- Removed detector-pack selectors from Expeditions and Challenges. The app now uses the installed current detector pack automatically, with detector details remaining in Settings.
- Reworked Expeditions recovery, extraction, model, and Discord controls into clearer grouped sections.
- Reworked Challenge rotation and reporting controls to reduce cross-page clutter.
- Made diagnostic log inclusion opt-in by default for new installs, matching automatic error screenshot capture.

### Fixed

- Restored vertical scrolling on the Expeditions page when advanced tuning or smaller windows require it.
- Let Challenge cooldown fallback return from an already-open Expedition party preview instead of repeatedly clicking the bottom-left Play button.

## [1.1.3] - 2026-07-20

### Added

- Added a Settings toggle to include the current macro run log in manual and automatic diagnostic ZIPs.

### Changed

- Resized Roblox to the standard 808 by 611 client size when the app opens if Roblox is already running.
- Kept Roblox at the standardized client size after startup, camera setup, placement recording, debug capture, and macro runs instead of restoring earlier bounds.

### Fixed

- Let the Challenge cooldown Expeditions fallback finish its active Expedition run before returning to Challenges when a global reset arrives.
- Closed the Expedition victory or defeat screen before switching back to Challenge selection, avoiding the unreachable post-match preview timeout.

### Tests

- Updated camera-region, alignment, placement, settings, and Expedition deadline tests for persistent standard sizing.
- Added golden coverage for the Expedition terminal close action used during Challenge fallback handoff.

## [1.1.2] - 2026-07-20

### Added

- Published the first public release of Challenge mode, covering Trait, Stat, and Sprite rotations across five maps, split placement phases, retry policy, reset tracking, Discord monitoring, and automatic diagnostics.

### Fixed

- Closed and verified the Challenge selector before starting the cooldown Expeditions fallback, then handed control to the existing Play-to-Expeditions recovery route.
- Retried the selector close action up to three times when Roblox did not acknowledge the click, and stopped with an actionable error instead of waiting forever for an unreachable prestart screen.
- Restored Discord release-announcement highlights for release-note documents that use descriptive headings such as **Fixed**, **Reliability**, or **Setup** instead of the legacy **Changes** heading.

### Tests

- Replayed the supplied 149-frame manual Challenge-selector-to-Expeditions route and retained privacy-safe cooldown-selector and game-mode-selector frames as regression fixtures.
- Verified that both active and gray cooldown selectors expose their detected close action for the handoff.

## [1.1.1] - 2026-07-20

### Added

- Added a persistent **Setup guide** button in the app sidebar that opens the public visual walkthrough in the default browser.

### Fixed

- Marshalled Challenge cooldown-handoff logging and fallback status updates through the WPF dispatcher, preventing the app from stopping when Expeditions starts on a worker thread.
- Kept fallback log messages durable even if the window dispatcher is already shutting down.

### Tests

- Replayed the reported cooldown selector diagnostics and verified that the detector correctly reaches the Expeditions handoff state.
- Rendered the complete dark/light UI snapshot set with the new sidebar action.

## [1.1.0] - 2026-07-20

### Added

- Added a complete regular-Challenge workflow for Trait, Stat, and Sprite rotations across five maps.
- Added per-map camera selection, before-start placement, delayed after-start placement, configurable defeat retries, half-hour reset tracking, and daily-limit waiting.
- Added an optional Expeditions handoff while Challenges are on cooldown and Components V2 reporting for Challenge attempts, results, recovery, and waiting states.
- Added an optional Discord user ID for five mention-restricted alerts after unexpected Expeditions or Challenge errors; manual Stop does not alert.
- Added an opt-in automatic failure capture that saves 10 one-second Roblox-client screenshots to a timestamped diagnostic ZIP.

### Fixed

- Hardened Challenge navigation across both observed selector scales, unavailable and dimmed rows, animated thumbnails, private-party previews, reward tooltips, hovered controls, and bright game-mode artwork.
- Rejected broad blue scenery as a reward header so a valid Flower Forest Start Game dialog remains actionable.
- Clicked Challenge map artwork instead of reward icons and tied all shared-screen detections to their expected transition context.
- Included the v1.0.14 post-teleport recovery and v1.0.15 confirmation-dismissal fixes in the Challenge-capable build.

### Tests

- Added 68 selective Challenge fixtures covering multiple players, PCs, all five maps, gameplay, terminal screens, cooldowns, and Expeditions handoff states.
- Passed 231 automated tests across 337 checked-in Roblox client captures.

## [1.0.15] - 2026-07-20

### Fixed

- Waited for Roblox to acknowledge a registered cursor move before pressing a UI button, improving clicks on slower or low-frame-rate clients.
- Verified that the Continue Expedition confirmation actually closes and retried its detected button up to three times when Roblox ignores an input event, instead of remaining on the modal indefinitely.
- Stopped with an actionable error after the bounded retries if the confirmation never clears, avoiding an unobservable infinite stall.

### Tests

- Replayed all 21 frames from the reported v1.0.14 diagnostic capture at 98.6% confirmation confidence with an action at `(340, 340)`.
- Added a privacy-redacted 808 by 611 fixture proving the modal action cannot fall through to the underlying checkpoint Continue button.
- Added confirmation-transaction regressions proving dismissal ends the transaction and a persistent dialog permits exactly three verified attempts.

## [1.0.14] - 2026-07-19

### Changed

- Updated the in-app and README community link to the current Expeditions Macro Discord invite.

### Fixed

- Continued automatic lobby recovery through the initial in-map checkpoint that can appear after the teleport preview, instead of retaining a stale preview state and waiting indefinitely.
- Restricted that standalone Continue transition to the post-teleport recovery step so ordinary node pauses and the map-preview action cannot be mistaken for it.

### Tests

- Reproduced the reported 808 by 611 diagnostic frame locally at 100% Continue confidence and verified its Roblox-relative action at `(404, 490)`.
- Added recovery-policy regressions for the post-preview Continue transition, its normal disabled state, and map-preview priority when both signals are present.

## [1.0.13] - 2026-07-19

### Added

- Added a persistent global macro-hotkey setting under Settings > Controls. F6 remains the default, and users can record F1-F11 or F13-F24 directly from the interface.
- Added the running app version to the bottom of the left navigation footer.

### Fixed

- Standardized Roblox to the canonical 808 by 611 client size before the camera-region selector appears, so users cannot choose an area that later falls outside the calibration size.
- Stored a camera selection as client-relative coordinates immediately and restored the original Roblox window after selection, cancellation, or failure, so moving Roblox before setup no longer shifts the comparison region.
- Updated workflow prompts, buttons, diagnostics, recording instructions, and the sidebar footer whenever the macro hotkey changes.

### Tests

- Added camera-region regressions for standard-size selection, relative conversion, cancellation, invalid bounds, preview capture, and window restoration.
- Added global-hotkey regressions for the F6 default, supported rebinding, display names, and the Windows-reserved F12 rejection.

## [1.0.12] - 2026-07-19

### Fixed

- Made checkpoint extraction a single guarded transaction so lag cannot return the macro to generic checkpoint handling and issue another Extract click.
- Waited for the extraction confirmation to appear and then disappear before resuming gameplay monitoring, without repeatedly clicking Confirm while the dialog remains visible.
- Extended both extraction transition windows to 30 seconds and stopped safely with an actionable error instead of sending a delayed duplicate action when the UI never acknowledges a click.

### Tests

- Added extraction-transaction state regressions proving repeated observations cannot authorize duplicate Extract or Confirm clicks.

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

[Unreleased]: https://github.com/LeniLilac/expeditions-macro/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.6...v1.2.0
[1.1.6]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/LeniLilac/expeditions-macro/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.1.0
[1.0.15]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.15
[1.0.14]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.14
[1.0.13]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.13
[1.0.12]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.12
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
