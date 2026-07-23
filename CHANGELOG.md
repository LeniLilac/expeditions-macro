# Changelog

All notable changes to Expeditions Macro are documented here.

## [Unreleased]

## [1.3.0-beta.12] - 2026-07-23

### Added

- Added an inline **Test link** action for the optional Roblox private-server reconnect setting. It validates the saved link and launches the registered `roblox://` protocol without closing an active client.

### Changed

- Removed the Challenge preset's cooldown Expeditions fallback. When a scheduled Challenge rotation becomes unavailable, it returns through the verified game-mode selector and the Macro scheduler immediately chooses the next highest-priority eligible task.
- Reorganized Expeditions, Story, and Raid preset editors into consistent cards for preset selection, route details, models/team, behavior, and advanced tuning.
- Consolidated Discord notification, failure-ping, and Roblox reconnect inputs into compact connection cards with inline test and reveal actions.
- Removed redundant helper descriptions and idle status placeholders from the Macro and preset pages.

### Fixed

- Windows Graphics Capture now discards the queued compositor backlog and waits for a post-barrier frame before scoring camera movement, preventing beta.9 through the first beta.11 build from observing an earlier pose and rejecting or endlessly scanning an otherwise stable model.
- Long-running Expedition, Challenge, Story, Raid, and Infinite monitoring now sends the documented `O` keep-alive every eight minutes; a transient focus/input failure retries after one minute without stopping the run.
- Story and Raid recovery now wait through the AFK Chamber's Return-to-Lobby teleport before testing the configured Play key, preventing loading time from being misreported as a bad keybind.

### Security

- Deep Debug archives now redact the active Windows username and profile-directory segment from event data, exception text, copied logs, and copied text model/configuration files.

### Engineering

- Extracted sanitized archive-text ownership from the Deep Debug session lifecycle and reduced existing Challenge runner/page debt after removing the obsolete cooldown fallback.

## [1.3.0-beta.10] - 2026-07-23

### Added

- Added optional process-level Roblox recovery to Macro plans. After bounded in-client recovery fails, the app can close only the verified Roblox player process, reopen a DPAPI-protected private-server link through the registered `roblox://` protocol, reload saved plan progress, and retry the same incomplete task.
- Added a dedicated rendered-world gate that recognizes the reported blue-void prestart failure before camera input or unit placement and retries the configured route through Play.

### Fixed

- Expedition map verification no longer treats the selected row's bright preview artwork as a missing selector panel, preventing false "Map 1 could not be selected" errors after the game has visibly activated School Grounds.
- Challenge navigation now sends one Back click from an open detail and waits for a stable selector before retrying, preventing a delayed second click from reopening the challenge and timing out.
- Camera setup and runtime refinement now use identical atomic fine-drag gestures in both directions. Setup rejects a non-reversible zero pose, and a failed saved-neighborhood shortcut restores the scan pose and continues the existing turn instead of recursively starting another 360-degree scan.
- Expedition startup recognizes a retained post-match party from a previous task and completes its verified Change Gamemode handoff instead of waiting indefinitely on a screen it does not own.
- Missing or recaptured Roblox sessions now surface as restart-eligible runtime failures while Play-key configuration errors and ordinary low camera confidence continue to fail without restarting the client.

### Security

- Private-server links are excluded from logs, diagnostics, and Deep Debug configuration snapshots. Automatic relaunch requires a supported Roblox process identity and is limited to three restarts within ten minutes.

### Tests

- Added the reported beta.9 bright Map 1 selector frame to the language-independent active-row regression corpus.
- Added a delayed Challenge detail-to-selector regression proving stale transition frames cannot trigger a duplicate Back click.
- Added rendered-map versus blue-void camera fixtures, atomic fine-input, single-turn fallback, private-link validation, secret-redaction, restart-circuit, and same-incomplete-task recovery regressions.

### Engineering

- Split camera fine calibration, stage navigation, Expedition handoff, Challenge navigation, and diagnostic secret redaction into focused modules while lowering the enforced line-debt ceilings.

## [1.3.0-beta.9] - 2026-07-22

### Fixed

- Windows 10 capture callbacks now only signal frame availability; WinRT frame access and GPU copying run on the serialized capture path, preventing `RPC_E_WRONG_THREAD` failures before the first screenshot.
- Recreates the Windows Graphics Capture frame pool from the actual incoming frame size when Roblox's compositor surface changes, then re-reads live window geometry through bounded retries instead of exposing `CaptureSurfaceChangedException`.

### Tests

- Added cross-thread frame notification, pre-arrived frame, surface recovery, and bounded failure regressions; also verified three changing 808 by 611 frames in a live Roblox capture smoke check.

## [1.3.0-beta.8] - 2026-07-22

### Added

- Added a configurable Shift Lock key under Settings > Controls. It defaults to Left Ctrl and supports distinct left/right Shift and Ctrl keys, letters, numbers, symbols, numpad keys, function keys, and common control keys through physical scan-code input.
- Story, Raid, Expedition, and Challenge reporting now sends a start message with a Roblox screenshot.

### Changed

- Discord Components V2 reports now use bulleted run facts, semantic accents for start, victory, defeat, and error states, and a footer containing the app version plus a localized Discord timestamp.
- Victory and Defeat reports now distinguish the runtime of the completed match from the cumulative macro runtime.

### Fixed

- Story and Raid now start from both lobby-created and retained post-match party previews. The launch boundary requires a detected Start action instead of requiring the preview's rightmost action to be Disband.

### Engineering

- Extracted key-binding UI ownership from the Settings page, physical keyboard emission from the Windows automation adapter, and Story/Raid navigation policy from the shared stage runner, reducing existing monolith debts.

## [1.3.0-beta.7] - 2026-07-22

### Added

- Learns a device-local camera shortcut after two matching normal load-in alignments, then tries one cached relative mouse drag with three-frame goal verification before falling back to the complete yaw atlas. Manual Auto Align remains independent of this cache.

### Fixed

- Cross-mode Story, Raid, and Expedition handoffs now press the configured Play key directly from the terminal screen, then follow the detected post-match party through Change Gamemode instead of dismissing the terminal and becoming stranded on the ordinary HUD.
- Recognizes both lobby and post-match mode-detail action rails. Raid act selection no longer waits forever when the existing party omits **Enter Matchmaking**, and Story accepts the observed horizontally shifted lobby detail panel.

### Documentation

- Added a field-observed game-behavior ledger covering canonical Story, Raid, Expedition, and Challenge entry/exit navigation, the terminal Return to Lobby confirmation/teleport sequence, and the long-idle keepalive policy needed for planned 1.4.x workflows.
- Split contributor details into focused development, testing, and release guides while keeping the root agent policy limited to repository-wide invariants.

### Engineering

- Added a CI and release-time repository policy check that enforces project dependency direction, blocks generated files from source control, caps new production/test/script file sizes, and prevents existing oversized files from growing.

## [1.3.0-beta.6] - 2026-07-22

### Changed

- Exact same-preset Expedition, Story, and Raid tasks now use Repeat Stage between scheduled matches; different presets and modes complete a verified handoff through the shared Play selector. Challenge victories still return through Play, while only configured Challenge defeat retries use Repeat Stage.

### Fixed

- Continuously drains Windows Graphics Capture into the latest FP16 compositor texture, preventing every detector and deep-debug frame from remaining frozen on the first image after startup.
- Falls back to the software WARP Direct3D device when no hardware capture device is available; integrated graphics continues to use the normal hardware path.
- Re-observes the actual camera after coarse and fine probe round trips, and avoids coarse arrow probes once a pose already passes, preventing variable Roblox key timing from discarding a correct yaw and forcing another full-turn attempt.

### Tests

- Added scheduler continuation, terminal-action mapping, and Challenge retry-policy regressions, plus a live changing-frame capture smoke check.

## [1.3.0-beta.5] - 2026-07-22

### Added

- Added a reusable timestamped contact-sheet script for reviewing deep-debug frame transitions together with their detector and automation context.

### Changed

- Captures detector frames from the Roblox window compositor surface in linear FP16, then converts HDR/Auto HDR content to stable SDR pixels before existing vision logic runs. Other applications covering Roblox no longer appear in macro screenshots.

### Fixed

- Saving a Macro plan now also persists the DPAPI-protected Discord webhook and failure-alert user ID. A successful webhook test persists the same fields, so replacing or updating the app no longer appears to reset them.
- Story/Raid handoffs now leave post-match party and map-selector screens through their verified live actions before the scheduler starts another mode.
- Distinguished the current compact cyan Victory rail from the visually similar red Defeat panel without broadening terminal recognition across unrelated UI.

### Tests

- Added FP16 scRGB conversion, HDR highlight compression, window-surface crop mapping, settings restart, compact terminal, and scheduled handoff regressions.

## [1.3.0-beta.4] - 2026-07-22

### Fixed

- Recognized the settled upper Story party-preview rail after its entrance animation, preserving two-frame stability while mapping the live Start action.
- Recognized the current compact Story/Raid Victory action rail so terminal results complete instead of polling indefinitely.
- Closed and verified the Challenge selector before a cooldown task returns to the Macro scheduler, preventing the next Expedition, Story, or Raid task from waiting behind a Challenge-owned panel.
- Restored shared game-mode navigation before a recoverable Story/Raid camera-alignment skip is handed to the scheduler; an unverifiable handoff now stops safely.
- Snapshotted Placement recording and playback timing switches before background execution, preventing WPF thread-affinity errors when either operation starts.
- Accepted coherent cross-session vertical projection drift during full-resolution camera verification only when at least three saved world regions agree, while retaining the tight thumbnail atlas and wrong-yaw rejection.
- Re-observed the live camera after a complete arrow-key turn and return before applying any fine mouse correction, preventing a stale first-visit offset from sending the camera away from an already strong match.
- Tested strong fine-neighborhood candidates earlier when session rendering lowers them below the calibration baseline; unchanged direct verification still rejects false candidates.

### Tests

- Added privacy-reviewed fixtures for the reported King's Tomb Mastery preview and compact Spirit City Victory screen, including full cross-state regression coverage.
- Added Challenge cooldown handoff retries, fail-closed navigation coverage, and a scheduler ownership regression proving lower-priority modes run only after Challenge becomes ineligible.
- Added privacy-safe composites from a reported camera failure plus regressions for coherent projection drift, nearby and wrong yaw, and full-turn return re-observation.

## [1.3.0-beta.3] - 2026-07-22

### Added

- Added a source-only Deep Debug Viewer with ZIP browsing, video-like frame playback and scrubbing, synchronized event/input/state context, adjustable speed, and a configurable decoded-frame cache.

### Changed

- Sharded the five longest full-corpus golden checks and the remaining golden suite across six independent GitHub Actions jobs, reducing validation wall time without skipping coverage.

### Fixed

- Recognized Act, Infinite, and Mastery Story detail panels across their cyan, green, and purple semantic accents and both reviewed Select Stage layouts, with the click mapped from the live button.
- Detected the compact Include Equipment dialog and clicked its live Include action instead of a stale fixed vertical coordinate.
- Refined strong saved fine-yaw neighborhood candidates as soon as they appear during camera fallback, avoiding an unnecessary full rotation while preserving the direct-score threshold and three-frame verification.

### Tests

- Added reviewed Story detail and compact team-dialog fixtures plus focused camera regressions for successful early refinement and false-candidate full-scan fallback.

## [1.3.0-beta.2] - 2026-07-22

### Changed

- Camera preparation now enables shift lock before every pitch or yaw mouse drag and restores it afterward; it no longer probes the unsafe unlocked state by dragging the visible pointer across the HUD.
- Split fast tests, golden-image regressions, and UI snapshots into parallel GitHub Actions jobs. Silent prerelease packaging now runs independently of validation so beta artifacts can be tested sooner while failures remain visible on the tagged commit.

### Fixed

- Moved the Story game-mode click from the reward-icon strip to the stable map-copy area so item tooltip controls cannot absorb navigation.
- Recognized the current three-action Story/Raid party preview and mapped its live Start button without broadening Challenge preview detection into ordinary Expedition screens.
- Marshaled Camera and Placement model-list refreshes onto the WPF Dispatcher after background setup completes, preventing a successfully saved model from ending with a false CollectionView thread error.

### Tests

- Added a privacy-reviewed three-action Raid party-preview fixture and cross-state regression coverage for its Start action.
- Extended UI snapshot validation to refresh Camera and Placement model collections from a worker-thread completion context before rendering every page.

## [1.3.0-beta.1] - 2026-07-22

### Added

- Added saved Macro plans that prioritize and sequence Challenge, Expedition, Story, and Raid presets without interrupting an active match. Per-task victories, defeats, runtime, completion, and Challenge reset eligibility persist locally.
- Added Story presets for five maps with Act 1-5, Infinite, Mastery, Normal/Hard selection, two placement phases, defeat retries, recovery, and optional saved-team loading.
- Added Raid presets for Spirit City Acts 1-3 with two placement phases, defeat retries, recovery, and optional saved-team loading.
- Added a separately configured Unit-menu key and automated Team 1-8 loading through the in-game Units interface.
- Added Fredoka as the embedded application typeface and native Lucide vector icons for navigation and actions.
- Added an explicitly confirmed, disabled-by-default deep debug mode that archives every detector frame, state/action trace, generated input, placement-recording input, sanitized settings, selected plans/presets, detector pack, and referenced camera/placement models after successful, canceled, and failed operations.
- Added a dedicated silent prerelease workflow for alpha, beta, and release-candidate tags. It publishes verified GitHub prerelease assets without reading or sending the Discord release webhook.

### Changed

- Replaced small checkbox controls with larger switch controls and separated task state from task actions in the Macro editor.
- Camera preparation now retries the complete alignment search in the alternate shift-lock state before declaring alignment failure.
- New camera-model setup uses a 200 ms settle interval by default.

### Fixed

- Deferred Challenge placement points covered by the Start Game dialog until immediately after the deliberate Start click. This prevents a placement coordinate from starting the match early or being swallowed by the dialog.
- Kept Challenge, Story, Raid, and Expedition task handoffs inside the Play interface so the Macro scheduler can switch modes deterministically.
- Stopped lobby recovery with clear in-game Toggle Play Menu setup instructions when the configured key fails to open Play, instead of waiting silently or continuing through the visible Play button.
- Extended Challenge prestart waiting to three minutes only after the stable Roblox teleport transition is recognized, instead of failing after 35 seconds while the stage is still loading.
- Moved saved-team loading for Story, Raid, and Challenge runs behind a verified prestart state.
- Added safe preset deletion with clear blocking details when a Macro plan or fallback preset still references the selection.
- Fixed dark-theme tooltips, Debug capture status overlap, and compact action/status rails.

### Tests

- Added reviewed Story, Raid, team-selection, scheduler, alternate-shift camera, Start-dialog occlusion, stage-teleporting, and saved-team regression coverage.
- Expanded dark and light snapshot coverage to every application page, including long-running Macro status views.

## [1.2.1] - 2026-07-22

### Changed

- Camera preparation now repeatedly clamps zoom and pitch, evaluates both shift-lock states, and verifies final alignment across three independently rendered frames.
- Camera matching now tolerates small translation and scale differences and uses hue as a weak tie-breaker when geometry scores are close.
- Zoom-out now presses Roblox's `O` key first and falls back to mouse-wheel input if key injection fails.
- Automatic failure diagnostics now default to enabled, retain the latest ten action-state frames, capture ten additional half-second frames after a failure, include the run log by default, and keep only the ten newest automatic error archives.

### Fixed

- Replaced language- and artwork-dependent Expedition map-selection verification with the stable cyan active-row marker, fixing false Map 1 and Map 2 lock errors across current, alternate, and French layouts.
- Prioritized valid centered **Start Game** dialogs over post-match HUD lookalikes, fixing reported King's Tomb prestart timeouts.
- Re-detected extraction confirmation controls after every attempt and retried focused clicks up to three times instead of waiting indefinitely on a stale modal.
- Verified Roblox windows by process name and PID, refreshed stale handles after teleports or focus failures, and retried focus against the newly discovered client.
- Added a temporary borderless sizing fallback when Windows clamps Roblox's framed window, allowing the automation client area to reach exactly 808 by 611 pixels.

### Tests

- Added map-selection regressions for Map 1 and Map 2 across localized and alternate layouts, plus an active-gameplay negative.
- Added a privacy-reviewed King's Tomb prestart regression and coverage for extraction retries, registered camera scoring, diagnostic retention, verified Roblox window discovery, forced sizing, and `O`-key zoom behavior.

## [1.2.0] - 2026-07-21

### Added

- Added automatic camera-region selection that chooses four stable, detailed map areas across the standard Roblox client while avoiding common HUD zones.
- Added a persisted signed fine-yaw neighborhood atlas so runtime alignment can reuse the micro-adjustment evidence learned during setup.
- Added a Settings calibration overlay for matching Roblox's rendered UI scale to the standard detector scale.
- Added **Test webhook** actions for validating optional Discord reporting without sending a screenshot or user mention.
- Added seven-map camera-rotation fixtures covering all Expedition maps and Story/Challenge map layouts.
- Added a required, separately configured Anime Expeditions **Toggle Play Menu** letter under Settings. It starts empty, is captured by clicking its button and pressing a letter, and shows a five-step popup if either macro is started before setup is complete.
- Expanded the global macro start/stop hotkey to letters, digits, punctuation, numpad keys, and the existing supported function-key range.

### Changed

- Rebuilt camera setup around sensitivity-independent Left/Right-arrow pulses for coarse yaw and right-drag mouse movement only for fine refinement.
- Camera setup now standardizes zoom, top-down pitch, and temporary shift lock automatically; manual comparison-region selection is no longer required.
- Runtime alignment now makes up to three fresh attempts with alternating scan direction and sampling phase, using both the saved coarse atlas and fine neighborhood before each full-turn fallback.
- Changed new camera defaults to a 30 ms arrow hold and 100 ms settle time, with calibration controls hidden behind **Show tuning**.
- Camera models now use schema version 3. Existing camera models must be recorded again; placement models and presets remain reusable after selecting replacement camera models.
- Challenge and Expedition handoffs now open Play directly with the configured game key while terminal UI is still visible, then verify the party preview with up to three bounded attempts.

### Fixed

- Prevented low-confidence alignment from placing units or starting a match. After all three attempts fail, the macro exits the unstarted match safely, records diagnostics, and reports the skipped task; the Challenge scheduler advances to its next configured task while standalone Expeditions stops at the party preview.
- Parked the pointer inside the Roblox client with spaced acknowledged motion pulses so unit hover cards and highlighted selector rows clear before Start Game or map detection.
- Re-detected live Start Game controls after parking instead of trusting a stale or partially covered action.
- Made camera-model replacement transactional with bounded retries and backup restoration when another process temporarily holds model files.
- Removed fragile Play clicks from lobby recovery, post-match, fallback, and alignment-skip navigation, so hotbar overlap, UI scale, and shifted Play icons cannot block mode changes.
- Prevented assigning the same letter to the macro hotkey and Play-menu key, which would otherwise let the macro's own navigation input stop the run.
- Preserved detector-pack 1.0.2 migration behavior while integrating the new camera workflow.

### Tests

- Added camera calibration, automatic-region, persisted-neighborhood, alternating-retry, safe-skip, hover-clear, and UI-scale overlay coverage.
- Added golden rotation checks proving incorrect yaw remains below the alignment threshold across three Expedition and four Story/Challenge maps.
- Added key-driven Play navigation retries, required-key validation, hotkey-conflict validation, and global letter/number/punctuation hotkey coverage.

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

[Unreleased]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.12...HEAD
[1.3.0-beta.12]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.10...v1.3.0-beta.12
[1.3.0-beta.10]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.9...v1.3.0-beta.10
[1.3.0-beta.9]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.8...v1.3.0-beta.9
[1.3.0-beta.8]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.7...v1.3.0-beta.8
[1.3.0-beta.7]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.6...v1.3.0-beta.7
[1.3.0-beta.6]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.5...v1.3.0-beta.6
[1.3.0-beta.5]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.4...v1.3.0-beta.5
[1.3.0-beta.4]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.3...v1.3.0-beta.4
[1.3.0-beta.3]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.2...v1.3.0-beta.3
[1.3.0-beta.2]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.1...v1.3.0-beta.2
[1.3.0-beta.1]: https://github.com/LeniLilac/expeditions-macro/compare/v1.2.1...v1.3.0-beta.1
[1.2.1]: https://github.com/LeniLilac/expeditions-macro/compare/v1.2.0...v1.2.1
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
