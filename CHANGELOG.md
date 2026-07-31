# Changelog

All notable changes to Expeditions Macro are documented here.

## [Unreleased]

## [1.3.0-beta.52] - 2026-07-31

### Fixed

- Sell Unit Match Steps now give Roblox one second to acknowledge the configured key and retry no more than twice when the same selected-unit panel remains freshly verified. Normal sales still send one keypress; cancellation or lost panel ownership sends no additional input.

### Tests

- Added field-derived coverage for an ignored first Sell Unit keypress, delayed panel closure, cancellation before retry, and the three-attempt hard cap.
- Passed 1,624 application and detector tests, 5 Deep Debug Viewer tests, 14 Detector Viewer tests, all 88 main-app dark/light UI states, and all 10 Detector Viewer dark/light UI states.

## [1.3.0-beta.51] - 2026-07-31

### Changed

- New Bounty tasks now default to zero parked non-viable Bounties. The redundant visible parking label was removed while the compact zero-to-four Gold/time guidance and accessible control name remain.

### Fixed

- Mythic Bounty suffix recognition now keeps its exact `#1` through `#10` path first, then accepts only a high-similarity, action-anchored raster variant with a clear runner-up margin and reviewed title position. Ambiguous glyph evidence remains unknown and sends no input.
- Villain Invasion Event Home is no longer misclassified as Act Selection when Roblox's performance overlay adds red and white pixels across the broad title area. Act Selection now requires the compact owned heading/subtitle spacing plus the selector rail.

### Tests

- Added privacy-reviewed field regressions for the distinct Mythic `#4`, `#5`, and `#6` suffix rasterizations rejected after correctly canceling reroll confirmation, plus the performance-overlay Event Home misclassification.
- Passed 1,620 application and detector tests, 5 Deep Debug Viewer tests, 14 Detector Viewer tests, all 88 main-app dark/light UI states, and all 10 Detector Viewer dark/light UI states.

## [1.3.0-beta.50] - 2026-07-31

### Added

- Macro Plan now exposes a confirmed Delete action for the selected saved plan and safely opens the next saved plan, or a clean unsaved plan when none remain.

### Fixed

- Removing the final user-authored action from an act or map-specific Step Mode Placement Setup now clears that override and restores its shared category setup; the required Start Game timeline row no longer keeps an otherwise empty override active.
- Newly added Match Steps now start below Start Game at the bottom of the timeline, reducing manual reordering for the common After Start workflow.
- Legacy placement rows saved on the canonical client edge, including the default `(0,0)` coordinate, are skipped before any unit-selection key or mouse input can open an unrelated HUD panel.
- Bounty Challenge objectives now adapt the required Story Infinite Placement Setup to the matching Challenge runtime target without rewriting the saved setup, closing the map-or-act compatibility failure after a successful board reconciliation.
- Infinite Bounty wave recognition now covers both reviewed top-bar layouts, scores only the uniquely owned dark/blue Wave pill, and requires independent gameplay-HUD ownership before a recognized number can advance safe-exit evidence.

### Tests

- Passed 1,613 application and detector tests, 5 Deep Debug Viewer tests, 14 Detector Viewer tests, all 88 main-app dark/light UI states, and all 10 Detector Viewer dark/light UI states.

## [1.3.0-beta.49] - 2026-07-31

### Added

- Detector Viewer can load every supported repository dataset image with one action and jump directly to fixtures through a searchable filename-first frame picker.
- Detector Viewer automatically selects the detector associated with each fixture or its strongest positive production evidence, while keeping the editable frame picker themed in dark and light modes.
- Detector Viewer can annotate repository fixtures with per-detector expected results, implementation notes, and one or more labeled canonical detection regions. Edits autosave atomically to `datasets/detector-annotations.json` and remain separate from production detector decisions.

### Changed

- Bounty Board ownership now requires only the independently aligned **Back + Calendar** action rail and the bounded **Bounty Board** header. A local glyph recognizer can corroborate the marked header crop when its normal gold-image evidence changes; neither header path can authorize the Board without the live action rail.

### Tests

- Passed 1,579 application and detector tests, 5 Deep Debug Viewer tests, 13 Detector Viewer tests, and all 10 Detector Viewer dark/light UI states.
- Added a privacy-safe field regression containing only the annotated Bounty Board header and Back + Calendar rail, recolored-header fallback coverage, missing-owner negatives, and the complete repository cross-state corpus.

## [1.3.0-beta.48] - 2026-07-31

### Added

- Added a source-only Detector Viewer that opens images, recursive image folders, and Deep Debug ZIPs to visualize production-owned detector geometry, actions, evidence, thresholds, and explicit inspection limitations in dark and light themes.
- Added a machine coverage audit that independently checks every production detector, recognizer, image matcher, detector-owned metrics surface, and scorer against the inspection catalog.

### Changed

- Saved-team selection is now remembered globally for one user-started Macro operation and one Roblox process. Routes sharing the same Team reuse it, while a failed switch, process replacement, stop, or new start returns the state to unknown.
- Bounty Board usable slots are derived from live yellow Reroll and green Claim controls, so a dimmed card may occupy any slot without receiving input.
- Detector ownership across Settings, Events, Challenges, Teams, Refuel, Stage selection, chat, placement, and Expeditions now favors bounded structural and semantic evidence over exact one-pixel decorations or mutable content brightness.
- Detector engineering policy now requires bounded local raster-variation coverage, independent owner evidence, and structural crop ownership before OCR or template output may corroborate a state.

### Fixed

- Mythic Bounty #2 now routes its wave-30 objective to Flower Forest instead of Fairy King Forest. The unreleased Bounty-state schema is reset rather than translating the incorrect objective key.
- Claim processing now distinguishes a dimmed card from a new live Reroll action across two fresh Board observations. Both outcomes count the claim and clear completed progress, but only a dimmed card removes that Bounty number from the reroll pool until UTC reset.
- Villain Invasion Event Home no longer rejects the live Event Gamemode button when a narrow red border rasterizes one row thinner.
- Fractionally scaled detector subregions now map both half-open edges without expanding past their matched owner.

### Tests

- Passed 1,574 application and detector tests, 5 Deep Debug Viewer tests, 6 Detector Viewer tests, all 88 main-app dark/light UI states, and all 8 Detector Viewer dark/light UI states.
- Added field-derived dimmed-card coverage for every horizontal Bounty slot, conditional claim-settlement coverage, all ten Bounty definitions, operation-scoped Team reuse, one-pixel raster variants, mutable-content variants, cross-state negatives, and Detector Viewer coverage/input/UI smoke tests.

## [1.3.0-beta.47] - 2026-07-30

### Fixed

- The first Bounty selection of every user-started Macro operation now processes the live Bounty Board before saved objectives can launch. This startup transaction claims completed cards, reconciles manually changed active numbers, applies the configured reroll/parking policy, and persists the result.
- Recoverable restarts retry an interrupted initial Bounty reconciliation but preserve active-work-first execution after a completed and saved reconciliation.

### Tests

- Passed 1,458 application and detector tests, 5 Deep Debug Viewer tests, and all 88 dark/light UI snapshot states.

## [1.3.0-beta.46] - 2026-07-30

### Fixed

- Bounty Challenge handoff now closes each verified Play layer, uses the existing in-match Return to Lobby door and confirmation flow, and separately proves Lobby before reopening Events.
- Bounty mode now finishes every currently executable active objective before reopening the Bounty Board to claim or reroll. Recovery re-entry resumes persisted active work instead of creating an unnecessary board visit.
- Saved-team preparation now recognizes a valid Load Team confirmation over a brightly populated Team 1 roster by prioritizing the live Confirm action and visible Load Team rows while retaining the modal-structure safety gates.

### Tests

- Passed 1,455 application and detector tests, 5 Deep Debug Viewer tests, and all 88 dark/light UI snapshot states.

## [1.3.0-beta.45] - 2026-07-30

### Added

- Macro Plan now includes a **Bounty** task mode that opens the Event Bounty Board, identifies Mythic Bounties by their number-only suffix, claims completed cards, and routes deterministic Raid, Story Infinite, and Challenge objectives.
- Bounty tasks expose a zero-through-four non-viable parking policy. Lower values spend more Gold to find overlapping viable work sooner; higher values retain more non-viable Mythics to reduce reroll cost.
- Bounty progress is stored locally for the one Roblox account assumed per Windows user. The UTC daily claim count resets independently while unfinished active Bounty objectives remain available for reconciliation.

### Changed

- The Add Task mode selector uses two rows and blocks Bounty creation until Spirit City Raid Act 1 and all five Story Infinite Placement Setups are configured.
- Infinite Bounty objectives merge by map and run through two waves beyond the highest covered target. Exact wave recognition is backed by a bounded increasing-wave fallback before the existing in-match Lobby return.
- Bounty rerolls use a verified 200 ms cadence. The explicit insufficient-Gold banner is authoritative; bounded unchanged-Mythic and no-Mythic attempt limits remain fallback evidence.
- Conditional Challenge Bounties wait through an ordinary Challenge cooldown but reroll after the daily Challenge limit. Insufficient Gold finishes already-active viable work and defers new rerolls until the next Macro start.

### Fixed

- Completed Bounty cards now map their left-shifted green Claim action back to the owning card for slot and number recognition while still clicking the live Claim center.
- Saved-team preparation now accepts dense, brightly populated Unit Inventories from their stable gold header and four live actions instead of rejecting them because changing roster artwork reduced whole-panel darkness.
- Bounty Board entry now detects the unhovered live row, supports the Beginner's Path vertical offset, and requires a stable owner-state action instead of depending on the mouse hover highlight.
- Infinite Bounty runs no longer fail during wave-counter initialization because the embedded 0-through-100 template payload is normalized and length-validated before recognition.
- Bounty slot scanning stops once every Mythic retainable under the current Challenge and parking policy is already active, avoiding an unnecessary fifth-slot reroll in the zero-parking daily-limit case.
- Partial Bounty objectives now continue directly from the verified Lobby into the next deterministic route. The board reopens only when a whole active Bounty is ready to claim.
- Returning to Lobby after an Infinite objective now invalidates the previous match's Fast camera-preparation cache, so the next map prepares its own camera pose.
- Bounty objective-complete Discord notifications now use the supported objective event title and success accent.
- Upgrading users receive the field-confirmed Gold Mine and Resource Drill route defaults once, while later user edits remain preserved.

### Tests

- Passed 1,450 application and detector tests, 5 Deep Debug Viewer tests, and all 88 dark/light UI snapshot states.

## [1.3.0-beta.44] - 2026-07-30

### Fixed

- Saved-team preparation now distinguishes Unit Inventory from the Unit Teams list before advancing, clicks the live detected Teams action instead of a stale coordinate, and boundedly retries when the verified Unit Inventory remains open.

### Tests

- Passed 1,384 application and detector tests plus 5 Deep Debug Viewer tests.

## [1.3.0-beta.43] - 2026-07-30

### Added

- Match settings now persist one default Targeting priority and Auto Upgrade priority for newly authored Place actions. Map clicks and the Add Match Step popup inherit those values without rewriting existing Match Steps.

### Fixed

- Expedition recovery now keeps a verified transition pending through slow, unclassified teleport/loading frames and uses the existing bounded hard observation grace. It no longer retries the stale map-preview Start action merely because loading exceeded the soft window.

### Tests

- Passed 1,375 application and detector tests, 5 Deep Debug Viewer tests, and all 86 dark/light UI snapshot states.

## [1.3.0-beta.42] - 2026-07-29

### Fixed

- Scheduled resource-refuel Utilities no longer open Play after the final station. They press the configured Areas key directly, select the verified Lobby category and Spawn card, and prove Lobby before scheduler handoff.
- A Utility that inherits an already-open Play surface now closes every verified Play layer through its detector-owned Back action before beginning the Areas-to-Lobby route.

### Tests

- Passed 1,372 application and detector tests plus 5 Deep Debug Viewer tests.

## [1.3.0-beta.41] - 2026-07-29

### Added

- Macro Plan now includes a **Utilities** task category with Gold Mine refuel, Resource Drill refuel, and combined routes. Each task runs immediately when first eligible, then repeats after its configured one-minute-through-seven-day interval without blocking lower-priority work while it waits.
- Placement Setup now authors one ordered **Match Steps** timeline with Place Unit, Reconfigure Unit, Delay, and Upgrade Unit actions around one required **Start Game** step.
- Placement Setup advanced mode now exposes bounded action timing and state-proof controls for Step Mode plus optional prestart verification and a configured post-Repeat-Stage delay for Recording Mode.

### Changed

- Scheduled refuel Utilities reuse the calibrated Resource refuel settings, require the configured Toggle Areas Menu key, and enter and leave through verified Lobby/Play states. Plans and share codes persist the selected refuel route and interval without including account-level key bindings or route calibration settings.
- The Before Start / After Start phase selector and marker badges are removed. Actions above Start Game execute before its verified click and actions below execute afterward; Start Game may be reordered but cannot be added, edited, duplicated, or removed. Legacy phase offsets migrate to an equivalent ordered timeline with explicit Delay actions.
- Lobby and match preparation now distinguish the fixed open/closed Roblox chat indicators and close a verified open panel before Settings, placement, or recording input. The detector ignores the optional microphone/headset slot and never clicks from unknown evidence.
- Match Steps now grow with the Placement Setup page, use compact color-coded action blocks and modal editors, and reference repeated placements through stable IDs such as `6a`, `6b`, and `6c`. Only placement actions appear on the map.
- Placement-map labels now move both horizontally and vertically to avoid one another, placement points, and connector text; wheel zoom is reserved for the map only while the outer page is already at its top boundary.
- Macro Plan drag-and-drop now follows the nearest card-half insertion boundary, auto-scrolls near the viewport edges, and no longer requires hovering over a narrow gap.

### Fixed

- Advanced manual-recording Repeat Stage handoffs now apply the configured prestart policy consistently across Challenge, Expedition, Story/Raid, and Event routes. Challenge retries no longer add a fixed 3.5-second transition wait on top of a delay-only advanced setup.
- Challenge placement handoff now defers the complete action suffix beginning at the first Start-dialog-obscured coordinate, so Delay, Reconfigure, and Upgrade actions cannot run ahead of their placement owner.
- Placement Setup **Test playback** now runs actions on both sides of a freshly verified Start Game click instead of testing the two action groups without their real lifecycle boundary.
- Unit targeting and Auto Upgrade state now reset once per match, while Expedition retry queues contain placement actions only.
- Selected-unit proof waits retain their strict consecutive-frame and click-attempt requirements while allowing slow capture devices to finish observations inside one bounded hard deadline.
- Resource refuel now recognizes Gold Mine and Resource Drill in both fuel-present and missing-fuel states, revalidates the live Max and Confirm actions, and performs at most two verified cleanup actions before failing closed.
- Match Step rows now refresh targeting and Auto Upgrade summaries immediately after editing.
- Mouse-wheel input over Match Steps now scrolls the Placement Setup page, and adding or selecting a map placement no longer jumps down to its row.
- Advanced Step Mode skips the selected-unit click when visual proof is disabled and a placement already requests the default Target First / Auto Off state.
- Auto Upgrade reconfiguration now remembers each placed unit's current state and cycles forward with the minimum required key taps instead of holding the key or normalizing an already-known state.
- Removing a placed-unit action now removes its dependent Reconfigure, Upgrade, and Sell actions in the same autosaved edit instead of leaving an invalid timeline.
- Match Steps support a lazily required Sell Unit binding and action, with selected-panel proof and post-sale closure verification.
- Advanced placement, reconfiguration, and Upgrade-readiness checks are independently configurable. Upgrade actions wait for a stable green control, wait through ordinary gray, and stop cleanly at the wider Maxed control.
- Resource Utilities now return through the verified Areas Lobby category and Spawn card because the configured Play key is open-only.

### Tests

- Passed 1,362 application and detector tests, 5 Deep Debug Viewer tests, and all 86 dark/light UI snapshot states.

## [1.3.0-beta.40] - 2026-07-28

### Fixed

- Placement-map marker labels now size for their content and automatically fan into collision-free horizontal lanes, keeping dense coordinates readable without changing saved placement points.

## [1.3.0-beta.39] - 2026-07-28

### Changed

- Manual recording playback now treats +/- 50 milliseconds as its timing-quality target and continues on the original absolute timeline unless signed drift reaches the +/- 2,000-millisecond hard stop.

## [1.3.0-beta.38] - 2026-07-28

### Added

- Step Mode Placement Setups now expose one through eight total placement attempts. The default is one fast attempt, and any additional attempt retries only the row whose selected-unit proof was missing.

### Changed

- Step Mode now places each Before Start phase or equal-due After Start batch through one bounded Quick Placement transaction: Cancel Placement, hold Quick Placement, select only when the consecutive unit slot changes, send three clicks per coordinate over 50 milliseconds, then release and verify/configure each row separately.
- Manual recording playback now runs its absolute event clock on a dedicated timing worker and performs target checks through direct Win32 probes, reducing scheduler and diagnostic interference while retaining the exact inclusive +/- 50-millisecond safety bound.
- The process-recovery circuit now permits ten Roblox restarts in ten minutes and blocks the eleventh.

### Fixed

- Manual-playback timing misses now report their signed drift and event boundary as local playback errors instead of being mistaken for recoverable Roblox timeouts that relaunch the private server.
- Equal-due After Start placement rows now remain in one Quick Placement batch, while later offsets retain their independent schedule.

## [1.3.0-beta.37] - 2026-07-28

### Added

- Newly authored placement steps now default Auto Upgrade to Priority 1. Existing saved steps remain unchanged, and missing legacy values still load as Off.

### Changed

- Step Mode now holds the configured Quick Placement key and repeats one direct unit-slot tap plus one saved-coordinate click until the selected-unit panel is confirmed, with an eight-attempt hard cap. The retired Cancel Placement normalization, cyan Quick Placement image gate, double-click pairs, and timed mouse approaches are removed.

### Fixed

- Private-server restart checkboxes now save immediately. Navigating away from the Dashboard no longer restores the previous **Restart Roblox at Macro start** or recovery setting.

### Tests

- Passed 1,238 application and detector tests, 5 Deep Debug Viewer tests, and all 72 dark/light UI snapshot states.

## [1.3.0-beta.36] - 2026-07-28

### Fixed

- Quick Placement selection proof now follows the complete cyan title/icon through the field-confirmed upward prestart HUD phase while retaining every original component and pixel-population threshold. A real selected unit is no longer skipped merely because that stable indicator renders above the retained baseline.
- Expedition prestart logging now reports confirmed placements against configured placements instead of describing skipped rows as sent input.

### Tests

- Revalidated `1,250` application and detector tests, `5` Deep Debug Viewer tests, repository policy, and the zero-warning Release build.

## [1.3.0-beta.35] - 2026-07-28

### Added

- Placement Setup now exposes **Recording Mode** and **Step Mode** as a route-header selector. Recording Mode uses the saved recording picker while preserving ordinary placement steps for an immediate return to Step Mode.
- Advanced manual recordings now live under the new **Experimental** Settings section.
- Expedition Placement Setup now permits one placement per unit slot across Before Start and After Start, and all setup editors reject new points inside the fixed central hotbar/HUD region. Existing invalid rows remain loadable so users can inspect, move, or remove them.

### Changed

- Manual recording playback now allows each event's total elapsed playback time to be up to 50 milliseconds early or late while retaining absolute-timeline scheduling and pre/post-send safety checks.
- Settings Diagnostics now makes clear that key bindings are read-only there and directs edits to Controls on the Dashboard.

### Fixed

- Manual recording capture now reconstructs mixed keyboard and mouse input from the wrap-safe native Windows hook timeline, persists an explicit action anchor for a one-pixel pointer difference, and rejects larger incomplete pointer paths before saving.
- Ordinary placement now tries one immediate place/select pair, parks the pointer so a hover card cannot cover selected-unit proof, and falls back to at most three bounded 50-pixel/200-millisecond place/select pairs without repeating unit-key normalization.
- Added a default-unset **Quick Placement key** under Dashboard > Controls. Step Mode now holds that physical key after normalization and requires two stable cyan Quick Placement frames before coordinate input; one failed proof receives one extra unit-slot tap and one recheck, while a second miss skips the row safely. Recording Mode remains exempt.
- An ordinary placement that still lacks selected-unit proof after all eight clicks is now logged and skipped so later configured steps continue. The skipped row receives no targeting, Auto Upgrade, or success callback and does not trigger private-server recovery.
- Villain Invasion Event Home now accepts the field-observed one-pixel Event Gamemode lower border while retaining the selected tab, label, action, carousel, and Act-selector ownership checks.
- Switching between Recording Mode and Step Mode autosaves only the recording assignment and leaves every ordinary placement step intact.
- Compact Placement Setup now hands a mouse-wheel gesture from the nested placement-step list to the outer workspace when the inner list reaches its top or bottom boundary, keeping the rest of the page reachable.

### Tests

- Revalidated `1,249` application and detector tests, `5` Deep Debug Viewer tests, repository policy, and all 72 dark/light UI snapshots.

## [1.3.0-beta.34] - 2026-07-27

### Fixed

- Placement Setup now scrolls oversized placement-step rows by pixels, keeping Auto Upgrade priority and After Start delay reachable with the mouse wheel, scrollbar, and keyboard at compact and medium window sizes.

### Tests

- Added structural reachability checks at 960 by 640 and 1400 by 1080, and expanded the dark/light UI snapshot matrix to 68 states.

## [1.3.0-beta.33] - 2026-07-27

### Changed

- Placement Setup is now Fast-only. Camera Models, learned yaw alignment, and their authoring surfaces are removed; public-beta plans and presets that still contain legacy camera-model fields remain readable but stop safely with clear replacement guidance before Roblox input.
- UI recognition waits across navigation, recovery, setup, match start, and terminal handoff are observation-aware for slower devices while preserving each workflow's hard deadline and input-attempt cap.
- Story Hard mode is available only for Act routes. Mastery and Infinite routes hide the unsupported option and clear any stale Hard-mode value.

### Fixed

- Settings preparation recognizes the field-observed high-contrast gear without accepting selected, shifted, or unrelated top-bar controls.
- Event navigation recognizes the shifted Act selector before retained home evidence, waits for delayed Event content, and accepts a selected Villain Invasion home whose decorative header has not finished rendering while still requiring its owned selected tab and live Event Gamemode action.
- Navigation and recovery actions now require fresh state-owned evidence, bounded retries and recovery transactions, and never authorize input from a static fallback coordinate.
- Manual recording playback accepts the one-pixel client-coordinate variance observed after acknowledged pointer movement while retaining its focus, timing, button-state, and drift safeguards.
- Fast-only cleanup preserves automatic Macro Plan and Placement Setup saving, editable placement phases, Auto Upgrade priorities, recording exclusivity, and the existing pinned-Roblox coverage policy.

### Tests

- Revalidated all 1,181 application and detector tests, all 5 Deep Debug Viewer tests, repository policy, and 66 dark/light UI snapshots.

## [1.3.0-beta.32] - 2026-07-27

### Added

- Added automatic persistence for every committed Macro Plan and Placement Setup edit, including a visible pending/saving/saved/error state and forced flushes before plan switches, playback, sharing, navigation, and shutdown.
- Added editable Before Start / After Start phase selectors to existing placement steps.
- Added per-step Auto Upgrade priorities from Off through Priority 6. Playback sends the matching Auto Upgrade Unit key zero through six times after verified placement and targeting.
- Added compact X actions beside optional Anime Expeditions keybindings so a binding can be explicitly unset without changing Escape's capture-cancel behavior.

### Changed

- Replaced Roblox accessibility navigation with verified visual clicks: Settings uses the opaque gear at either supported top-bar offset, and in-match Lobby return uses the opaque door action before verifying the confirmation and final Lobby.
- Pinned Roblox now remains attached when focus moves to a non-overlapping window or another monitor. It suspends only for overlapping foreign windows or app-owned dialogs, while page/scroll/minimize auto-detach still minimizes Roblox.
- Manual-recording Placement Setups remain exclusive: retained ordinary steps stay saved but do not execute while a recording is selected.

### Fixed

- Startup restart can now reach UI Scale correction from a stable noncanonical-scale Lobby before requiring strict post-preflight Lobby confirmation.
- Challenge navigation waits for the live Preview Start action instead of accepting a retained post-match Change Gamemode action.
- Event entry recognizes the selected colored tab and selects Villain Invasion when another Event is initially active, while avoiding an extra click when Villain Invasion is already selected.
- Story and Raid handoff accepts stable shared Play-selector evidence when a locked, dim mode tile prevents specialized Stage classification, avoiding false Toggle Play Menu binding errors and repeated key presses.
- Autosave drains edits queued during an active write, preserves chained rename ancestry, waits for in-flight placement writes during shutdown, retries failed writes, and prevents stale completion messages.
- Placement key validation now requires only the bindings used by active ordinary steps; manual recordings and empty phase subsets no longer fail on irrelevant unset placement keys.

### Tests

- Added deterministic autosave lifecycle, placement phase/priority, vision-guided Settings/door, startup-order, pinned-window exposure, Event selection, Challenge Start, and shared Play-selector regressions.
- Revalidated all 1,135 application and detector tests, all 5 Deep Debug Viewer tests, repository policy, and 72 dark/light UI snapshots.

## [1.3.0-beta.31] - 2026-07-27

### Added

- Added opt-in manual input recordings for advanced Fast no-align routes. Recordings capture physical keyboard, mouse movement, clicks, and wheel input, replay against absolute microsecond timestamps with a hard 10-millisecond drift limit, and can be armed for playback with the global macro hotkey.
- Extended Fast share codes with referenced Fast no-align legacy presets and their complete ordinary placement dependencies while keeping manual recordings device-local and excluded.
- Added Event Villain Invasion Act 1 Angle 2 with its separate deterministic approach, placement setup, route identity, and Lobby handoff boundary.
- Fixed Event entry for accounts whose Events page initially selects an Event other than Villain Invasion.
- Added a per-placement Auto Upgrade choice, a separate Auto Upgrade Unit binding, and a renamed Toggle Auto Upgrade Placed Units binding.
- Added vision-based Misc settings and UI Scale input navigation. Startup UI Scale and required game-settings preparation now have independent controls; scale is measured only from the open Settings panel.

### Changed

- Existing settings are migrated once to schema v3 to enable startup private-server restart, failed-recovery private-server restart, startup UI Scale correction, and required game-settings correction. Schema v2's combined preparation choice seeds both independent checks, and subsequent user changes remain authoritative; new installs begin enabled at schema v3.
- Reworked Macro Plan authoring around popup task and loop settings editors, direct insertion into loops, compact nested block surfaces, accessible challenge-type tiles, and one centered insertion indicator per sibling boundary.
- Removed the unused task Enabled state. Legacy disabled tasks load as active and the retired field is omitted when plans are saved or shared.
- Moved the bounded Run log into the unused lower portion of Current run and kept the pinned Roblox view left-aligned.
- Manual-recording routes now show only Team and Recording controls; ordinary placement steps remain preserved but hidden until recording mode is disabled.
- Same-route repeats retain the prepared camera and selected Team until a Lobby, rejoin, recovery, process, or route boundary invalidates them.
- Detector references now ship only as validated application content inside the portable ZIP and installer. The separate detector-pack download, Settings card, update checks, rollback surface, and update preferences were removed.
- Portable ZIPs now expose `ExpeditionsMacro.exe` at the extraction root while keeping runtime dependencies in the adjacent `ExpeditionsMacro` directory.

### Fixed

- Keybinding setup and validation errors now direct users to scroll down to Controls on the Dashboard while preserving the required matching Anime Expeditions binding.
- Pinned Roblox now suspends for foreign foreground applications and app-owned dialogs, preventing it from floating over other programs or hiding macro errors, then resumes only after Dashboard regains foreground.
- Automatic Dashboard unpinning now minimizes Roblox when the live view is scrolled away, another page opens, or the utility is minimized; manual unpin and application shutdown still restore Roblox without minimizing it.
- Maximized custom-chrome windows now honor the selected monitor's taskbar work area and minimum tracking size.
- Placement playback closes and verifies the selected-unit panel after every unit, preventing the panel from obstructing the next placement coordinate.
- Slow devices no longer discard valid Play, Continue, or Lobby observations merely because one detector pass consumed the old wall-clock stability window.
- Expedition map selection tolerates the observed vertical lighting displacement without lowering its confidence gates.
- When the UI Scale check is enabled, startup measures the open Settings panel directly and corrects values outside the inclusive 0.98–1.02 rendered range through vision-guided clicks. Disabling that check skips scale measurement instead of estimating it from the Lobby.
- Manual Recordings keeps its themed list surface while recording or playback is armed/running, and Dashboard pinning no longer re-covers modal errors.
- Team 4 loading now accepts its field-observed confirmation layout with exactly two visible underlying Load Team rows while retaining the existing modal and action gates.

### Tests

- Revalidated all 1,056 application and detector tests, all 5 Deep Debug Viewer tests, repository policy, and 72 dark/light UI snapshots.


## [1.3.0-beta.30] - 2026-07-26

### Added

- Split the workspace into a live Dashboard and a focused Macro Plan editor. The Dashboard owns run controls, status, logs, Discord reporting, private-server recovery, startup preparation, and keybindings.
- Added an interactive pinned Roblox window over the Dashboard. Roblox remains a real top-level window that accepts direct input, and its original style, bounds, and topmost state are restored when unpinned, navigating away, minimizing, or exiting.
- Replaced the single range-loop form with Scratch-style Loop blocks that users add, remove, and drag tasks or finite loops into. Plans support multiple finite loops, nesting up to three levels, and at most one root-terminal Forever loop.
- Added current production-path latency measurements to the Debug detection benchmark.

### Changed

- Macro-plan loop persistence, scheduling, progress, and Fast no-align share codes now preserve the visible nested block structure while remaining compatible with task-only and legacy range-loop plans.
- State-specific detection paths reuse per-frame evidence and cached action-button matches while retaining equivalence with the full detector path.
- After the last configured placement, playback dismisses the selected-unit panel through bounded verified clicks and parks the cursor at the idle corner.

### Fixed

- Pinned Roblox no longer becomes a non-interactive mirror or remains attached after leaving the Dashboard.
- Loop editing no longer creates a default loop, restricts a plan to one finite range, or loses valid nested structure while moving blocks.
- Selected-unit cleanup no longer leaves the final unit panel open after placement playback completes.

### Tests

- Added nested-loop validation, scheduling, migration, share-code, drag/drop, interactive pinning, detector-equivalence, benchmark, and final-placement cleanup coverage.
- Revalidated all 912 application and detector tests, all 5 Deep Debug Viewer tests, repository policy, and 36 dark/light UI snapshots.

## [1.3.0-beta.29] - 2026-07-26

### Added

- Added contiguous Macro-plan loops with selectable start/stop tasks, a finite run amount or Forever mode, persisted loop progress, and share-code support.
- Added 50–200% visual zoom, direct mouse-wheel zoom, middle-button drag panning, and per-placement targeting priority to Fast no-align Placement Setup.
- Added required Change Unit Targeting, Upgrade Unit, and Toggle Auto Upgrade Unit bindings, and renamed every existing binding to match its in-game action.
- Added a Debug performance benchmark that separates capture, mode detection, root recovery, and total production-path latency.
- Added a strict selected-unit panel detector using the red Close and blue Priority/First anchors.
- Added a default-on option to close a verified Roblox process and join the configured private server once at Macro startup.

### Changed

- Placement playback selects the slot, cancels it, then taps the slot three times to force a deterministic select/deselect/reselect state. Each of up to eight bounded attempts then approaches from 50 client pixels away over 200 milliseconds and clicks the same coordinate until the selected-unit panel is stable; it never repeats the key/cancel normalization for one placement.
- Live-match monitoring uses exact mode-owned state subsets, per-frame detector/action caches, and root-only recovery checks. Deep Debug avoids capture stack-trace work while disabled.

### Fixed

- Challenge-to-Event handoff now closes the shared selector and retained Challenge party through their detected Back actions before Event attempts Lobby navigation.
- Ordinary unit hover cards can no longer count as proof that a placement succeeded.
- Selected-unit confirmation now includes the final timeout-boundary sample instead of issuing an unnecessary retry.
- Placement steps preserve the active theme while playback temporarily locks editing.

### Tests

- Added Macro-loop, startup restart, current keybinding, selected-unit, placement retry, Challenge-to-Event recovery, detector-equivalence, and performance-benchmark coverage.

## [1.3.0-beta.28] - 2026-07-26

### Added

- Added Villain Invasion Event Act 4 navigation, Fast no-align Placement Setup, stable yellow-emblem selection, final Victory support, and a recoverable 17-minute watchdog for its 25-wave match.
- Added a startup guard that checks the effective Windows display scale on Roblox's current monitor and asks the user to change it to 100% before automation input begins.
- Placement Setup steps can now be reordered by dragging their grip within the Before Start or After Start phase. A compact timing panel beside the step actions configures the setup's interval between placements and default After Start delay.

### Changed

- Deep Debug now saves a frame before and after every high-level Roblox automation action, including mouse movement, clicks, drags, scrolling, key presses, camera actions, and window changes.
- Startup UI-scale and required-settings checks first pitch the camera straight down without changing zoom or yaw, preventing world interaction prompts from taking accessibility focus.
- Placement playback keeps the cursor at the active coordinate throughout one placement sequence, makes three bounded placement clicks, and parks the cursor only before moving to the next configured unit.

### Fixed

- Event act selection now follows the act's stable colored emblem after scrolling rather than relying on a fixed carousel coordinate.
- Reward-card suppression is limited to Expedition mode, so valid Challenge, Story, Raid, and Event prestart dialogs cannot be hidden by a colorful gameplay-frame false positive.
- Returning to Lobby now uses accessibility navigation only to open the confirmation, then clicks the verified red confirmation action directly and waits for a stable Lobby before recovery continues.

### Tests

- Added Event Act 4 selector, detail, Victory, runtime-policy, placement-map, and cross-state detector regressions.
- Added display-scale, pitch-only startup preparation, action-boundary Deep Debug, manual Lobby confirmation, cursor-retaining placement, triple-click, drag ordering, timing persistence, and Fast no-align share compatibility coverage.

## [1.3.0-beta.27] - 2026-07-26

### Added

- Placement Setup and the legacy placement editor now accept the keyboard `Delete` key to remove the selected placement step while preserving ordinary Delete behavior inside editable fields.

### Changed

- UI Scale and required game-settings normalization now runs once per top-level macro start. After that startup check succeeds, ordinary Lobby returns and private-server process recovery resume the incomplete task without changing settings again; recovery may retry only an initial preflight that never completed.

### Tests

- Added current Story and Raid Victory fixtures with separate **Next Stage**, **Repeat Stage**, and **View Party** actions, proving both modes still detect Victory and select the live Repeat Stage control.
- Added recovery coverage proving a successful startup preflight is not repeated after Roblox is restarted and the saved plan is reloaded.

## [1.3.0-beta.26] - 2026-07-25

### Added

- Added a configurable **Placement cancel key** under Settings > Controls, defaulting to `Z` to match Anime Expeditions' Cancel Placement binding.

### Changed

- Placement playback now normalizes an uncertain selected-unit state before every attempt, explicitly reselects the intended unit, then preserves the existing target jitter and 200 ms settle before placing.

### Fixed

- Event-to-Event and every normal-mode-to-Event scheduler transition now return through a verified Lobby because Event routes are not available in Play.
- Event-to-Story, Raid, Challenge, or Expedition transitions now open the verified post-match party panel and select **Change Gamemode** before handing off shared navigation.

### Tests

- Added scheduler routing-matrix, placement-input-order, default-key, key-conflict, and settings-persistence regressions.

## [1.3.0-beta.25] - 2026-07-25

### Added

- Added a Debug **Teleport to lobby** action that exercises the production accessibility-navigation and Lobby-readiness path with full Deep Debug coverage.
- Placement Setup now accepts the `1` through `6` number-row and numpad keys to select a unit slot while preserving ordinary numeric text entry.

### Changed

- Placement playback now primes each target with two acknowledged one-pixel cursor passes, settles for 200 milliseconds, then uses the existing jittered click to place the unit.

### Fixed

- Startup settings normalization now supports both observed Miscellaneous page row positions after the Event update while retaining strict toggle-state verification.
- Registered the Debug lobby action's Lucide icon locally so the app cannot fail during WPF startup while resolving that button.

### Tests

- Added a privacy-reviewed compact Event-update Miscellaneous settings fixture and retained legacy-layout coverage.
- Revalidated all 824 application and detector tests, all 5 Deep Debug Viewer tests, and 28 dark/light UI snapshots from both current source and the packaged application.

## [1.3.0-beta.24] - 2026-07-25

### Added

- Added Villain Invasion Event Acts 1 through 3 to Fast no-align Placement Setup and Macro plans, including lobby-only Event navigation, deterministic first-load movement, saved-team and placement support, Repeat Stage handling, and a 12-minute recoverable match watchdog.
- Added a Debug held-key utility that arms an arbitrary supported key and duration for an F6-triggered input test with full Deep Debug coverage.

### Changed

- Updated Expedition map, difficulty, and Select Stage navigation for the current Play interface.
- Startup now opens and closes Settings through paced accessibility navigation, calibrates UI scale before requiring Lobby detection, and disables the new Event Theme setting alongside the existing required settings.

### Fixed

- Event-themed Lobby frames no longer collide with the Settings detector, while a fully loaded themed Lobby remains recoverable.
- Event Victory is recognized both with and without the optional Next Stage action; repeat runs continue to select Repeat Stage rather than advancing.
- Returning to Lobby through accessibility navigation now verifies the confirmation transition before handing control back to startup.

### Tests

- Added privacy-reviewed Event navigation, prestart, Victory, Defeat, placement-map, current Expedition selector, current Settings, themed Lobby, and Lobby-exit fixtures to the cross-state detector corpus.
- Revalidated all 719 fast tests, 102 golden-image tests, 5 Deep Debug Viewer tests, and 28 packaged dark/light UI snapshots.

## [1.3.0-beta.23] - 2026-07-25

### Changed

- Renamed the Fast no-align import/export panel to **Share config**.

### Fixed

- Startup now validates installed detector-pack files before trusting their version and atomically restores a damaged cache from the bundled copy. An incomplete application bundle now reports actionable clean-reinstall guidance instead of only naming the first missing detector image.

## [1.3.0-beta.22] - 2026-07-25

### Changed

- Simplified Fast no-align Placement Setup by removing the redundant global placement interval and After Start delay fields. Newly authored steps use a 900 ms placement interval, and new After Start placements default to 30 seconds while remaining independently editable in the step list.
- Fast no-align and legacy macro plans are now shown only in their matching workflow. Upgrading settings from beta.20 enables Fast no align by default while preserving legacy presets, plans, camera models, and placement models for users who later disable it.
- Tightened the Macro task editor so mode-specific options align directly beneath the primary fields without an empty label rail or oversized blank area.

### Fixed

- UI snapshot validation now records page-level progress, exits without hidden modal dialogs on renderer errors, reports a bounded timeout, and uses the standard render size when a hosted display cannot contain the wider Placement Setup canvas.

### Tests

- Added upgrade-default, plan-workflow separation, and Fast no-align timing-default regressions.
- Revalidated the complete application, detector, Deep Debug Viewer, dark/light UI, portable, and installer release paths.

## [1.3.0-beta.21] - 2026-07-25

### Added

- Added the default Fast no-align workflow. **Placement Setup** now authors placements directly on native 808 by 611 map captures, stores Before Start and independently delayed After Start steps, owns the saved Team, supports right-click removal, and enforces seven-pixel point spacing.
- Added shared Placement Setup categories for all Expedition maps and for each Story map, with exact route overrides and exact-only Raid acts.
- Added compact `EMFAST1:` text import/export for Fast no-align plans and their resolved Placement Setups without camera models, run history, application settings, diagnostics, or secrets.
- Added optional startup UI-scale and required-game-settings normalization, enabled by default, plus standalone UI Scale and Settings tools in Debug.

### Changed

- Macro Plan now owns route policy such as Challenge types, defeat retries, Story Hard mode, Expedition difficulty, extraction, and boss targets. Legacy preset and camera-model editors remain available only when Fast no align is disabled.
- Fast no align prepares zoom and pitch once per Roblox process and reuses the preserved pose across repeat and post-match handoffs until Lobby or a Roblox restart invalidates it.
- UI-scale calibration now measures the rendered Settings panel and adjusts from feedback instead of assuming the same numeric scale renders equally on every device.
- The Macro Plan and Placement Setup workspaces use wider, grouped layouts with route categories collapsed by default and primary actions kept beside their owning controls.

### Fixed

- Saved-team scrolling now converges until the requested full Load Team action is stable, accepts a proven near-target Roblox snap, and routes an exhausted UI deadline through private-server recovery instead of a premature hard stop.
- Recoverable Roblox UI and session failures preserve diagnostics, restart through the configured private server, and retry the same incomplete task without recording progress.
- Accessibility navigation now waits for the Settings animation, uses physical paced key taps, and returns to the UI-scale input reliably across smaller and larger rendered layouts.
- Deep Debug path serialization now removes the Windows profile name and other protected local values from archived settings, events, errors, and model metadata.

### Tests

- Added Fast placement authoring, category fallback, plan sharing, startup preflight, rendered-scale feedback, required-settings detection, physical keyboard, team-scroll, and recovery regressions.
- Added privacy-reviewed canonical placement maps and Settings/UI-scale fixtures to packaged and golden-image validation.

## [1.3.0-beta.20] - 2026-07-25

### Added

- Added a Debug **Fast no align** utility that prepares a standardized Roblox camera by zooming fully out, temporarily enabling Shift Lock, and clamping the pitch straight down without changing yaw.
- Added bounded match-runtime watchdogs that preserve diagnostic evidence and route impossible-duration Challenge, Story Act/Mastery, Raid, and checkpoint-targeted Expedition runs through the existing private-server recovery path.
- Added safe rare-unit-drop dismissal for Spirit City Raid Acts 2 and 3 after all configured placement work completes.

### Fixed

- Private-server recovery now waits for three consecutive stable Lobby detections before returning control to the scheduler, so teleport, loading, selector, or other intermediate frames cannot consume Play-key attempts or navigation input.
- Expedition reward-card detection now requires the live **Select Upgrade** action rails and is no longer evaluated by Challenge, Story, or Raid, preventing a colorful Flower Forest prestart from being mistaken for a reward chooser.
- Recoverable match-runtime failures now save configured diagnostics before Roblox is restarted and retry the same incomplete task without recording Victory, Defeat, or task progress.

### Tests

- Added privacy-reviewed Flower Forest prestart and Spirit City rare-unit-drop fixtures plus cross-mode negatives.
- Added stable-Lobby recovery, match-runtime policy, scheduler recovery, Raid drop-dismissal, and gameplay-HUD regressions.

## [1.3.0-beta.19] - 2026-07-24

### Changed

- Updated the experimental Debug refuel route defaults from verified manual tuning: Gold Mine now uses `W 3000 ms`, `A 820 ms`, `W 2600 ms`; Resource Drill uses `W 3000 ms`, `A 750 ms`, `W 1000 ms`, `A 1600 ms`.
- Portable ZIPs again contain one top-level `ExpeditionsMacro` folder so extraction tools cannot scatter the self-contained application files into an unrelated directory.

## [1.3.0-beta.18] - 2026-07-24

### Added

- Added experimental Gold Mine and Resource Drill refuel routes to the Debug workspace for dataset collection and route tuning. Tests can start from the current lobby or relaunch through the configured private server, use a configurable Areas key, teleport through Expeditions Hub, replay configurable movement paths, and verify the detected fuel controls.
- Added a configurable Areas menu key under Settings. It is used only by the experimental refuel Debug tools in this release.

### Fixed

- Challenge Victory detection now uses the stable Close, View Party, and repeated roster-reward structure instead of animated cyan result artwork, preventing bright Victory frames from being mistaken for ordinary gameplay.
- Expedition card-reward detection now recognizes the current bright cyan progress header without weakening its button and gameplay-negative checks, allowing the macro to choose a card before Roblox auto-selects one.
- Obsolete schema 1 and 2 camera models now produce an actionable preset-specific error instead of a raw JSON deserialization failure. Expedition presets also warn immediately when their referenced camera model is missing.

### Tests

- Added privacy-reviewed current Victory, bright card-reward, Areas, Expeditions Hub, Gold Mine, and Resource Drill fixtures to the full cross-state detector corpus.
- Added Debug refuel route, physical held-key, settings persistence, Deep Debug, and legacy-camera compatibility regressions.

## [1.3.0-beta.17] - 2026-07-24

### Fixed

- Saved-team loading now anchors the scrollbar thumb to the detected Unit Teams panel, preventing similarly sized gray scenery from being dragged.
- Team 7 and Team 8 alignment now over-drags to the physical bottom limit so Roblox clamps the thumb instead of leaving the lower Load Team actions clipped after an under-travelled drag.
- Raid detail detection now supports the current Spirit City panel under both Roblox font settings even when the red Close circle visually connects to its border, allowing the configured act selection to proceed.
- Dense camera setup now discards a proven repeated revolution when sampling skips the first loop seam, measures fingerprint separation over a consistent angular neighborhood, constrains post-pulse lookup to the physically reachable yaw range, and accepts a verified fine-mouse return beside the atlas seam instead of oscillating between one left and one right arrow pulse.

### Tests

- Added a privacy-reviewed Team list fixture containing both the real scrollbar thumb and a valid-height gray scenery decoy.
- Added privacy-reviewed current Raid detail fixtures with Roblox's custom and default fonts.

## [1.3.0-beta.16] - 2026-07-24

### Fixed

- Challenge daily-limit evidence now persists across one-match scheduler handoffs. When every regular Challenge remains unavailable after a complete global reset, the task becomes ineligible until the next midnight UTC while the scheduler continues with the next eligible priority.
- Saved-team loading now recognizes the Team 7 and Team 8 Load Team confirmation after reaching the shared bottom scrollbar limit, even when the clicked or current row dims one underlying green action.
- Current wide Challenge Defeat panels now override their background HUD controls and complete the match normally. The obsolete small-Play-button post-match state was removed; the configured Play key continues to open navigation directly from the terminal.

### Tests

- Added privacy-reviewed Team 7, Team 8, and current wide Challenge Defeat fixtures to the saved-team, terminal, and cross-state navigation regressions.

## [1.3.0-beta.15] - 2026-07-24

### Added

- Added an optional Debug workspace with production Play-navigation and saved-team tests, current-screen inspection, client standardization, live detector/action checkpoints, frame review, single-step input authorization, and continuous resume.
- Deep Debug archives now identify Debug tools and step modes and retain their ordered detector, action, frame, and input checkpoints.

### Changed

- Portable ZIPs now place `ExpeditionsMacro.exe` directly at the archive root instead of inside an additional `ExpeditionsMacro` folder.
- Camera setup now builds a dense hybrid yaw atlas from one continuously held arrow turn sampled at up to roughly 60 FPS. Loop completion requires both the regional fingerprint and an independently matching fine-yaw reference (or an exact structural match), then verifies the stationary corrected goal. A transient moving-zero capture no longer invalidates a fine sweep that returns correctly, and pulse calibration searches only physically possible atlas ranges. Normal setup targets less than 20 seconds, while a separate 120-second hard timeout protects against a stalled operation.
- New camera models use schema version 4 so dense visual positions and discrete arrow-pulse distance remain distinct; existing schema 3 camera models remain supported.
- Verified same-route repeats reuse the already-loaded team and aligned camera; navigation, recovery, interruption, or route changes invalidate only the preparation that must be repeated.
- Discord result totals now retain whole-plan runtime, victories, and defeats across mode changes while continuing to report each individual match runtime.

### Fixed

- Runtime alignment now uses a strong, isolated dense-atlas fingerprint to begin bounded closed-loop correction even when the nearest moving-sweep frame has lower pixel registration. Ambiguous fingerprints still fall back safely, and fine-yaw plus three-frame goal verification remain mandatory.
- Dense camera setup now re-observes its captured atlas and returns to the goal in bounded arrow groups after both the continuous sweep and pulse calibration. It no longer assumes the key stops on the callback frame or that equal right/left pulse counts produce an exactly reversible visual yaw.
- Play, Challenge, Expedition, Story, Raid, and Unit Teams actions now require stable detector-owned coordinates before clicking, preventing an opening or shifting panel animation from authorizing stale input.
- Unit Teams reopening now waits for the real gray scrollbar thumb to settle and normalizes any retained non-top position before aligning the requested team.
- Switch thumbs now use fixed inner padding and a color-only focus state, preventing their circular knobs from being clipped at supported Windows scaling.

### Engineering

- Split dense camera calibration, runner preparation/reporting, Macro task execution, Debug stepping, and sanitized Deep Debug settings into focused owners while lowering existing source-debt ceilings.

## [1.3.0-beta.14] - 2026-07-23

### Fixed

- Saved-team loading now holds and drags the detected gray scrollbar thumb to a verified absolute position for Teams 1 through 8, keeping the cursor off unit cards and preventing fixed clicks from targeting a clipped lower row.
- Camera alignment now applies coarse arrow corrections in bounded groups, re-observes the saved yaw atlas after every group, and recalculates the shortest remaining direction instead of allowing rapid key timing to overshoot into a nearly complete fallback turn.

## [1.3.0-beta.13] - 2026-07-23

### Fixed

- Windows Graphics Capture now rebuilds one stalled capture session when a Roblox stage teleport pauses fresh-frame delivery beyond the normal one-second deadline, allowing the same launch to continue without repeating navigation input or returning a cached pre-teleport frame.

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

[Unreleased]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.52...HEAD
[1.3.0-beta.52]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.51...v1.3.0-beta.52
[1.3.0-beta.51]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.50...v1.3.0-beta.51
[1.3.0-beta.50]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.49...v1.3.0-beta.50
[1.3.0-beta.49]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.48...v1.3.0-beta.49
[1.3.0-beta.48]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.47...v1.3.0-beta.48
[1.3.0-beta.47]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.46...v1.3.0-beta.47
[1.3.0-beta.46]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.45...v1.3.0-beta.46
[1.3.0-beta.45]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.44...v1.3.0-beta.45
[1.3.0-beta.44]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.43...v1.3.0-beta.44
[1.3.0-beta.43]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.42...v1.3.0-beta.43
[1.3.0-beta.42]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.41...v1.3.0-beta.42
[1.3.0-beta.41]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.40...v1.3.0-beta.41
[1.3.0-beta.40]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.39...v1.3.0-beta.40
[1.3.0-beta.39]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.38...v1.3.0-beta.39
[1.3.0-beta.38]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.37...v1.3.0-beta.38
[1.3.0-beta.37]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.36...v1.3.0-beta.37
[1.3.0-beta.36]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.35...v1.3.0-beta.36
[1.3.0-beta.35]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.34...v1.3.0-beta.35
[1.3.0-beta.34]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.33...v1.3.0-beta.34
[1.3.0-beta.33]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.32...v1.3.0-beta.33
[1.3.0-beta.32]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.31...v1.3.0-beta.32
[1.3.0-beta.31]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.30...v1.3.0-beta.31
[1.3.0-beta.30]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.28...v1.3.0-beta.30
[1.3.0-beta.28]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.27...v1.3.0-beta.28
[1.3.0-beta.27]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.26...v1.3.0-beta.27
[1.3.0-beta.26]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.25...v1.3.0-beta.26
[1.3.0-beta.25]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.24...v1.3.0-beta.25
[1.3.0-beta.24]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.23...v1.3.0-beta.24
[1.3.0-beta.23]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.22...v1.3.0-beta.23
[1.3.0-beta.22]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.21...v1.3.0-beta.22
[1.3.0-beta.21]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.20...v1.3.0-beta.21
[1.3.0-beta.20]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.19...v1.3.0-beta.20
[1.3.0-beta.19]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.18...v1.3.0-beta.19
[1.3.0-beta.18]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.17...v1.3.0-beta.18
[1.3.0-beta.17]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.16...v1.3.0-beta.17
[1.3.0-beta.16]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.15...v1.3.0-beta.16
[1.3.0-beta.15]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.14...v1.3.0-beta.15
[1.3.0-beta.14]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.13...v1.3.0-beta.14
[1.3.0-beta.13]: https://github.com/LeniLilac/expeditions-macro/compare/v1.3.0-beta.12...v1.3.0-beta.13
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
