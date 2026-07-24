# Field-observed game behavior

This ledger records Anime Expeditions behavior that has been established from reviewed captures or a prior public build. It keeps automation changes grounded in observed state transitions rather than assumptions about how the game ought to behave.

## Evidence levels

- **Field confirmed**: a timestamped capture and a user-reported or automation-recorded input show the transition.
- **Release retained**: a public app version intentionally used the behavior, but the exact transition has not yet been re-captured.
- **Unverified**: a working hypothesis. It must not override field-confirmed behavior.

## Navigation ledger

### GB-000: Toggle Shift Lock for camera workflows

- Status: **Release retained** for Left Ctrl; configurable physical-key behavior is implemented for users whose in-game binding differs.
- Entry: camera setup, manual Auto Align, or a macro camera-preparation stage with Shift Lock initially off.
- Action: center the cursor, press the Shift Lock key saved under Settings > Controls, then perform pitch and fine-yaw movement while Roblox owns relative pointer motion.
- Exit: press the same snapshotted key during cleanup after success, cancellation, or failure so Shift Lock returns off.
- Key identity: Left/Right Shift and Left/Right Ctrl are distinct physical bindings. The app also supports ordinary letters, numbers, symbols, numpad keys, function keys, and common control keys accepted by the Settings picker.
- Do not: change the configured binding while an operation is running, reuse the macro/Play/Units key, or replace the physical scan-code path with a visible absolute cursor movement.
- Protected by: camera alignment custom-key/cleanup tests, Shift Lock settings validation, and Windows physical scan-code mapping tests.

### GB-001: Leave a completed match for a different mode

- Status: the terminal-to-party transition is **field confirmed** for Story Victory and user confirmed for Story, Raid, Expedition, and Challenge match-end screens generally. The party-to-selector transition is **field confirmed** independently for all four modes.
- Entry: a Victory or Defeat terminal when the next scheduled task is not the exact same repeatable route.
- Action: press the configured Toggle Play Menu key while the terminal remains open.
- Intermediate: the mode's post-match party appears. Story, Raid, and Expedition parties expose **Change Map** and **Change Gamemode**; the Challenge party exposes **Change Gamemode** without a Change Map action.
- Next action: click the detected **Change Gamemode** control.
- Exit: shared game-mode selector.
- Do not: close the terminal before pressing the Play key, substitute a visible Play-button click for the configured key, or assume every mode's party has the same button layout.
- Failure rule: after three key attempts without a recognized party or game-mode selector, stop with the Play-binding setup error.
- Evidence: a manual Story Victory sequence reviewed on 2026-07-22 using v1.3.0-beta.6 shows terminal, Play-key transition, Story party, and shared selector. Four later passive captures begin after the physical Play keypress and independently show Story, Raid, Expedition, and Challenge party-to-selector transitions; they do not contain Victory or Defeat terminals and are not evidence for terminal recognition. The direct terminal keypress path also existed in v1.2.1's Expedition mode-switch workflow. A beta.13 deep-debug run reviewed on 2026-07-24 preserves the current wide School Grounds Defeat panel and confirms that the background hotbar Play control and **Game Results** button must not replace terminal recognition.
- Protected by: `StageHandoffPolicyTests.DifferentModeVictory_UsesTheFieldObservedPlayMenuSequence`, `ExpeditionRunPolicyTests.CompletedRunHandoff_UsesOnlyStateOwnedActions`, `ChallengeScreenDetectorTests.CurrentWideDefeatPanel_WinsOverBackgroundHudControls`, and Challenge handoff policy tests.

### GB-002: Repeat an identical scheduled route

- Status: **Field confirmed**.
- Entry: terminal screen with the next eligible task resolved to the exact same mode and preset.
- Action: click the detected **Repeat Stage** control.
- Exit: the same route's prestart screen.
- Preparation reuse: after the Repeat Stage transition reaches a verified prestart screen, preserve the already-loaded team and camera pose. Do not reopen Units or repeat camera alignment before the next match.
- Invalidation: lobby, AFK Chamber, disconnect, rejoin, recovery navigation, a different mode/map/preset, or any route that did not arrive through the verified Repeat Stage transition invalidates camera reuse. Recovery also invalidates the loaded-team assumption.
- Do not: reopen the Play interface or repeat unchanged preparation for an identical uninterrupted route.
- Exception: Challenge victories always return through Play because the rotation can advance to a different stage. A Challenge defeat uses Repeat Stage only when a configured retry remains.
- Protected by: scheduler continuation, `RepeatedRoutePreparationStateTests`, and Challenge continuation policy tests.

### GB-003: Load a saved team

- Status: **Field confirmed**.
- Entry: a recognized mode-specific prestart screen.
- Action: open Units with the configured Units key, select Teams, load the configured slot, handle the equipment confirmation, then close Units.
- Exit: the same prestart screen.
- Do not: start team loading from the shared Play interface.
- Protected by: `StageScreenDetectorTests.TeamLoadGuard_RequiresPrestartAndRejectsPlaySelector` and team-selection tests.

### GB-004: Return to the lobby from a match terminal

- Status: **Field confirmed** on a Story Victory terminal; user confirmed the same control semantics for match-end screens generally.
- Entry: a Victory or Defeat terminal screen.
- Control: the small red button with the white exit-door icon at the far right of the terminal action rail.
- Action: click the red exit-door control.
- Intermediate: the terminal dims and an **Exit Confirmation** modal asks whether the player wants to exit the game. It contains a red **Return to Lobby** action and a gray **Cancel** action.
- Next action: click the detected **Return to Lobby** confirmation.
- Exit: Roblox enters its teleport/loading sequence and eventually reaches the lobby.
- Do not: treat the initial red terminal button as an immediate lobby transition, reuse a terminal close-button coordinate, or continue navigation before the confirmation and lobby are each verified.
- Failure rule for the future 1.4.x implementation: require the confirmation modal before clicking again, then wait for verified lobby detection with a bounded timeout. Stop safely if either transition is missing.
- Unverified: the post-Cancel state was not exercised in this capture and must not be assumed.
- Evidence: user-described manual input and a 12-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6; the sequence shows terminal, confirmation, teleport/loading, and lobby.
- Protected by: documentation only. The planned 1.4.x detector/workflow must add terminal-action placement, confirmation classification/action, cancellation-negative, and lobby-transition tests before using this behavior.

### GB-005: Prevent an intentional long idle from reaching the AFK Chamber

- Status: **Field confirmed** in a beta.8 Deep Debug run: an inactive Infinite match entered the AFK Chamber roughly every 18 minutes. The exact game-side timeout remains account/server dependent.
- Existing recovery: the app already detects the AFK Chamber, chooses **Return to Lobby**, verifies the lobby, and navigates back to the configured route.
- Product rule: when a task intentionally waits longer than ten minutes, perform a keepalive before ten minutes elapse and repeat it while the wait continues. Use an eight-minute cadence so timing jitter cannot cross the ten-minute safety boundary.
- Preferred action: focus and revalidate Roblox, verify a known non-text idle state, then send one ordinary `O` key pulse through the shared keyboard-input path.
- Why `O`: Roblox uses `O` for Zoom Out, and this app already uses that binding during camera preparation. At the fully zoomed-out limit it is effectively idempotent, while an arbitrary click could activate a UI control.
- Preconditions: do not send the pulse while a text field may own keyboard focus, during a transition, or while another workflow owns input. Camera-dependent workflows must still perform their normal zoom/pitch preparation afterward rather than assuming the keepalive established camera state.
- Do not: use a blind mouse click as the default keepalive, wait until the ten-minute boundary, or send only one pulse for a multi-hour wait.
- Failure rule: if Roblox cannot be focused or the pulse cannot be sent, defer it for one minute without stopping the active workflow; retain normal AFK-Chamber recovery as the final fallback.
- Evidence: a 2h17m beta.8 Deep Debug archive contained four AFK-Chamber transfers and zero `O` events, proving the prior policy had not been wired into runtime. The final 200 frames also show that Play-key attempts during Return-to-Lobby loading can consume all retries before lobby appears.
- Protected by: `InactivityKeepAliveTests`, active-match integration in every mode runner, Challenge cooldown waiting, and the Stage handoff policy that suppresses Play-key input until an AFK return reaches a verified navigation destination.

### GB-006: Enter a Story route from the shared game-mode selector

- Status: **Field confirmed** for the captured Mastery route; the user confirmed Act, Infinite, and Mastery as the selectable run types.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Story** mode tile and verify the Story map selector.
  2. Click the configured Story map and verify its detail panel.
  3. Select the configured Act, **Infinite**, or **Mastery** option. An Act also requires its configured act number and difficulty.
  4. Click the detected **Select Stage** action and verify a launch-ready Story party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Story prestart screen is verified.
- Do not: reuse one run-type accent as the detector for all Story variants; Act, Infinite, and Mastery use different accent colors and layouts.
- Evidence: a 21-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6. Physical clicks are user reported; the capture shows every resulting screen from the shared selector through Story prestart.
- Protected by: current Story screen/action detector tests and Story navigation tests. Add a full workflow-sequence test when the navigation orchestrator is next changed.

### GB-007: Enter a Raid route from the shared game-mode selector

- Status: **Field confirmed** for Spirit City and its captured act selection.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Raid** mode tile and verify the Raid map selector.
  2. Click the configured Raid map and verify its detail panel.
  3. Select the configured Raid act.
  4. Click the detected **Select Stage** action and verify a launch-ready Raid party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Raid prestart screen is verified.
- Evidence: a 17-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6. Physical clicks are user reported; the capture shows the shared selector, Raid map selector, act changes, party preview, teleport, and prestart.
- Protected by: current Raid screen/action detector tests and Raid navigation tests. Add a full workflow-sequence test when the navigation orchestrator is next changed.

### GB-008: Enter an Expedition route from the shared game-mode selector

- Status: **Field confirmed**.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Expedition** mode tile and verify the Expedition map-selection screen.
  2. Select the configured map and verify it is active.
  3. Select the configured difficulty and wait for the animated selection to settle before verifying the active value.
  4. Click the detected **Select Stage** action and verify a launch-ready Expedition party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Expedition prestart screen is verified.
- Evidence: a 16-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6. Physical clicks are user reported; the capture shows map and difficulty selection, party preview, teleport, and prestart.
- Protected by: Expedition recovery/navigation, map-selection, difficulty-stability, preview, and prestart tests.

### GB-009: Enter an eligible Challenge from the shared game-mode selector

- Status: **Field confirmed** for the captured Regular Challenge route and user confirmed for the cooldown/type-selection process.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Challenge** mode tile and verify the Challenge selector.
  2. Inspect the configured Challenge types and their cooldown or daily-limit state. Do not select a type that is unavailable.
  3. Select the eligible Challenge type and configured challenge row, then verify its detail panel.
  4. Click the detected **Select Stage** action and verify a launch-ready Challenge party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Challenge prestart screen is verified.
- Unavailable rule: when all configured Challenge types are on cooldown or exhausted, leave the selector through its verified close action before returning control to the scheduler or waiting.
- Daily-limit rule: retain unavailable-rotation evidence across scheduler handoffs for the full Macro operation. If every regular Challenge remains unavailable after a complete global half-hour reset, treat the account's daily limits as exhausted until the next `00:00 UTC`; run the next eligible task instead of probing Challenges every half hour.
- Detail-return rule: after clicking **Back** from an available or cooldown detail, wait for a stable Challenge selector before clicking again. The detail can remain visible while the first click is transitioning; a second immediate Back can land on the restored list and reopen a challenge.
- Evidence: a 16-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6 shows the successful route. A 61-frame beta.9 deep-debug run reviewed on 2026-07-23 records the delayed detail-to-selector transition and the second Back click reopening the detail. A beta.14 Deep Debug run reviewed on 2026-07-24 records all regular Challenges unavailable across multiple reset epochs while invocation-local state incorrectly scheduled another half-hour probe.
- Protected by: Challenge selector, cooldown, preview, handoff, and scheduler tests, including `ChallengeMacroRunnerTests.ChallengeDetailBack_WaitsThroughStaleFramesBeforeAnotherClick` and `ChallengeRunPolicyTests.SeparateScheduledInvocations_SharedStateInfersDailyLimitUntilMidnightUtc`.

### GB-010: Mode details differ between lobby and post-match party contexts

- Status: **Field confirmed** for Story, Raid, Challenge, and Expedition.
- Entry: a mode detail or map-selection screen reached from the shared game-mode selector.
- Lobby context: a fresh party exposes **Select Stage** beside the purple **Enter Matchmaking** action.
- Post-match context: pressing the configured Play key on a terminal and choosing **Change Gamemode** preserves the existing party. The resulting mode detail omits **Enter Matchmaking** and retains or expands **Select Stage**.
- Detector rule: identify the mode from its stable detail structure and require the live **Select Stage** action. Treat **Enter Matchmaking** as optional corroboration, never as a state-defining requirement.
- Action rule: click the detected **Select Stage** component because its center and width differ between the two contexts.
- Do not: require the purple action, use its absence as a cooldown/error signal, or reuse a fixed Select Stage coordinate across the narrow and wide rails.
- Evidence: two passive diagnostic captures reviewed on 2026-07-22 provide lobby and post-match detail frames for all four modes. A reported Raid Act 2 failure proves the strict two-button gate blocked act selection before any Act 2 click was sent.
- Protected by: `ModeDetailVariantTests` and the retained fixtures under `datasets/anime-expeditions/navigation-variants/`.

### GB-011: Launch previews differ between lobby and retained post-match parties

- Status: **Field confirmed** for Story Act, Story Infinite, Story Mastery, Raid, Challenge, and Expedition in both party contexts.
- Entry: a mode's **Select Stage** action has been clicked and its party preview is visible.
- Lobby family: the action rail contains **Start**, an optional **Change Map**, and **Disband**.
- Post-match family: the retained party action rail contains **Start**, an optional **Change Map**, and **Change Gamemode**.
- Launch rule: treat either family as launch-ready only when the live green **Start** component is independently detected across the configured stable-frame count, then click that detected component.
- Handoff rule: keep the families distinct outside launch. **Change Gamemode** is the verified path back to the shared selector; **Disband** is not interchangeable with it.
- Do not: require **Disband** before starting, click the `PostMatchPreview` state's Change Gamemode action when the current workflow is waiting to launch, or use a fixed Start coordinate without detecting the live button.
- Evidence: one failing beta.7 deep-debug Story run and two passive beta.7 diagnostic captures reviewed on 2026-07-22. The deep log records Select Stage followed by repeated 94% `PostMatchPreview` recognition with no Start click; the passive captures cover all six listed route variants in both contexts.
- Protected by: `StageHandoffPolicyTests.PreviewWait_AcceptsEitherPartyFamilyOnlyWithADetectedStartAction` and `StageScreenDetectorTests.BothPartyPreviewFamilies_MapTheLiveStartButton`. Challenge and Expedition retain their existing preview/action regression suites.

### GB-012: A prestart UI can load without stage geometry

- Status: **Field confirmed**.
- Entry: an Expedition prestart screen after the route has loaded.
- Visible failure: the Start Game dialog, hotbar, and HUD are present, but the world behind them remains a nearly uniform blue field rather than rendering map geometry.
- Action: do not rotate the camera, place units, or click Start. Open Play with the configured binding, return through the existing party flow, and select the same configured route again.
- Exit: retry camera preparation only after the saved world regions contain stable rendered geometry.
- Escalation: if bounded in-client recovery fails and private-server restart is configured, close only the verified Roblox player process, relaunch the saved server through the registered Roblox protocol, reload the saved plan, and retry the same incomplete task.
- Do not: report this as ordinary low camera confidence, consume camera-alignment attempts, increment task progress, or continue placement over the missing world.
- Evidence: a six-hour beta.8 Deep Debug run reviewed on 2026-07-23. The blue void began around frame 27,092; the same run eventually returned through Play, re-entered the route, and loaded normally.
- Protected by: `CameraWorldReadinessTests.BlueVoid_IsRejectedWhileRenderedMapRemainsReady`, `RobloxRuntimeRecoveryPolicyTests`, and `RecoveringMacroSchedulerTests.RuntimeFailure_RestartsRobloxAndRetriesTheIncompleteTask`.

### GB-013: A stage teleport can briefly pause window capture

- Status: **Field confirmed**.
- Entry: a launch-ready party has accepted **Start** and Roblox is switching through its experience splash and stage teleport screens.
- Observed failure: the Roblox window, PID, and canonical 808 by 611 client remain valid, but Windows Graphics Capture can deliver no post-barrier compositor frame for longer than the normal one-second fresh-frame deadline.
- Action: dispose the stalled capture session, create one replacement against the same verified Roblox window and live client geometry, and wait through its bounded initial-frame allowance.
- Exit: resume ordinary detector polling from the first fresh replacement-session frame without repeating **Start**, another navigation click, or any camera input.
- Failure rule: if the replacement session also times out, report the existing session-level capture failure so configured outer recovery can take over.
- Do not: return a cached pre-teleport frame, weaken the post-input freshness barrier, resend the launch input, or retry capture without a bound.
- Evidence: a beta.12 automatic error archive reviewed on 2026-07-23 shows successful School Grounds Infinite navigation followed by a capture failure during the experience splash; its post-failure frames reach the correct prestart five seconds later. A Deep Debug retry on the same PID records the complete splash-to-teleport-to-prestart sequence without a navigation change. A separately replicated Spirit City Raid 2 Deep Debug run records one Start click, the same valid Roblox PID and 808 by 611 client, then the exact post-barrier `TimeoutException` while the teleport continued normally in failure-diagnostic frames.
- Protected by: `WindowsRobloxAutomationTests.WindowCapture_TransientFreshFrameTimeoutRecreatesSessionOnce` and `WindowCapture_RepeatedFreshFrameTimeoutRemainsBounded`.

### GB-014: Align saved Unit Teams through the scrollbar thumb

- Status: **Field confirmed** for Teams 1 through 8.
- Entry: the Unit Teams list is visible at the canonical client size. Reopening this interface resets its scroll position to the top.
- Opening transition: the panel and its real scrollbar thumb slide upward from the bottom. The surrounding Roblox scene can expose a much taller neutral-gray strip beside the panel, so gray color alone does not identify the thumb.
- Action: locate the live gray scrollbar thumb at the right edge, reject candidates outside the field-observed 60–95 pixel height range, and require consecutive matching thumb geometry before acting. If a settled reopening is not at the top, drag the detected thumb to the verified top position and re-verify it. Then hold the left mouse button on that thumb and drag it to the requested team's absolute alignment.
- Alignment: Teams 1 through 6 each align as the first fully visible row and use that row's detected green **Load Team** button. Teams 7 and 8 share the scrollbar's bottom limit and use the second and third fully visible buttons respectively.
- Exit: require consecutive aligned frames with a full-height Load Team button, click the detected button, and verify the Load Team confirmation before continuing. The clicked or current bottom row can dim one green button below the ordinary modal threshold; two other visible Load Team rows plus the independently detected confirmation action are sufficient evidence.
- Do not: wheel-scroll with the cursor over unit cards, click a clipped third row, rely on scroll position persisting after the interface closes, or assume the older and current panel widths place the scrollbar at the same X coordinate.
- Failure rule: re-detect and realign the thumb once after an ignored Load click, then stop with a team-alignment error rather than clicking another row.
- Evidence: three beta.13 team-selection runs and two passive manual-navigation captures reviewed on 2026-07-23. Team 3 failed because its fixed `(580, 447)` action landed below a clipped button. The manual drag sequence establishes one aligned position per Team 1–6 and the shared bottom limit for Teams 7–8. Physical manual drags are user reported; the passive captures preserve their resulting positions. A later macro trace reviewed on 2026-07-24 shows the opening thumb rising to center `240` while an unrelated gray background run at approximately `x=644`, `y=190–435` caused the old longest-run heuristic to report center `312`. Two beta.15 Debug runs reviewed on 2026-07-24 reached and clicked the correct Team 7/8 buttons, but the confirmation detector rejected the visible dialog because one underlying green row covered 9.8% rather than the former 10% requirement.
- Protected by: `TeamSelectionServiceTests.Select_AlignsAndLoadsEveryTeamWithoutWheelScrolling`, `Select_WaitsForTheOpeningAnimationAndUsesTheRealTopThumb`, `Select_NormalizesAReopenedScrolledListBeforeLoading`, and the aligned/opening cases in `TeamScreenDetectorTests`.

### GB-015: Re-observe grouped coarse camera correction

- Status: **Field confirmed** for the saved 72-step Expedition camera model.
- Entry: the stabilized runtime view has either a registered match or a strong, isolated dense fingerprint match in the saved full-turn yaw atlas but does not already match the goal.
- Action: choose the shortest direction, send no more than six arrow pulses, capture the resulting pose, and recalculate the shortest correction from the newly observed atlas position.
- Dense evidence rule: a below-threshold registered match may earn only this bounded feedback path when it retains at least 20% structural evidence, its fingerprint is at least 96%, and that fingerprint is at least six percentage points above every atlas position outside the selected two-neighbor yaw cluster.
- Exit: stop grouped correction once the goal atlas is reached, then retain direct scoring, fine-yaw refinement, and three-frame final verification.
- Do not: send the entire predicted correction as one rapid open-loop arrow sequence, assume its accumulated yaw equals the separately observed calibration samples, or treat a fingerprint match as successful final alignment.
- Failure rule: stop the feedback loop when atlas evidence becomes ambiguous, the same non-goal position repeats, a position cycle appears, ten observations are consumed, or one full-turn pulse budget is reached. Continue through the existing bounded refinement/full-turn fallback rather than oscillating.
- Evidence: beta.13 setup and manual Auto Align Deep Debug archives reviewed on 2026-07-23. Setup produced a clean 100% zero pose and 72-step return. The random runtime pose matched atlas position 37 at 88%; a rapid 35-pulse correction landed near position 8 instead of zero, after which the fixed rightward fallback needed 60 of 72 steps to recover the 96% goal. A later beta.14 dense setup/alignment pair located the runtime yaw at 99% fingerprint confidence with 16% remote separation but only 39% registered structure; its predicted 27 right pulses were within four pulses of the verified 31-pulse goal, while every six-pulse re-observation retained at least 97% fingerprint confidence and 7% remote separation.
- Protected by: `CameraClosedLoopCorrectionTests.Align_WhenRapidArrowBatchOvershoots_ReobservesAndUsesShortestCorrection`, `Align_WhenCoarseObservationDoesNotMove_StopsFeedbackBeforeFallback`, and `CameraCoarseAtlasEvidencePolicyTests`.

### GB-016: Learn dense yaw without assuming arrow timing

- Status: **Release retained**; the exact continuous-versus-pulsed rate remains device-local.
- Entry: camera setup has standardized the Roblox client, zoom, pitch, and Shift Lock state and captured a stable goal.
- Action: capture every signed fine-yaw position and verify its stationary return, then hold Right Arrow once while sampling regional visual fingerprints at up to roughly 60 FPS. Stop on the first turn only when the returning fingerprint also matches a signed fine-yaw reference or the registered structural score independently verifies the exact goal. After releasing the key, locate the stationary pose in the completed atlas, return in bounded arrow groups with a fresh observation after every group, and fine-verify the goal. Use bounded observations after three and six discrete pulses—extending to twelve when needed—to calibrate dense-bin-to-pulse conversion, then perform the same closed-loop atlas return.
- Exit: save a schema 4 model only after both the sweep release and pulse probe have independently returned to the fine-verified goal. Normal setup targets less than 20 seconds; a stalled operation may continue until the independent 120-second hard timeout.
- Do not: infer yaw solely from elapsed hold time, assume the camera stops on the callback frame, assume equal opposing pulse counts are visually reversible, require two consecutive goal frames while the camera is still moving, fail a complete fine sweep because one moving zero capture was transient, treat descriptor similarity as final verification, save a partial sweep, or replace bounded runtime goal verification with a fingerprint match.
- Failure rule: release every held key or mouse button, restore the captured Shift Lock key, leave any existing model untouched, and report the setup failure.
- Evidence: eleven same-map beta.14 Deep Debug setups reviewed on 2026-07-23. All eight completed turns entered the saved fine neighborhood on their first turn, but only one produced a moving frame above the old near-exact structural gate; three additional attempts returned to a strong stationary zero after one transient moving-zero capture. A later eleven-run, multi-map beta.14 set contained seven successes and four retries: two recognized the loop but landed beyond the ±16-pixel fine window after key release, while two sent 12 right and 12 left calibration pulses yet remained at only 32–33% goal confidence. Successful siblings on the same scenes required 4–8 fine steps after release and as many as 14 after the pulse probe, confirming that both boundaries require atlas feedback rather than input-count reversal.
- Protected by: `DenseCameraAtlasTests.Calibrate_DenseHybridAtlasCompletesWithinBudget`, `Calibrate_DenseGoalReturnUsesClosedLoopAtlasFeedback`, `DenseLoopPolicy_RequiresExactOrIndependentFineEvidence`, `Align_DenseAtlasConvertsVisualBinsToPulseDistance`, schema compatibility tests, and the existing final-verification camera suite.

### GB-017: Wait for navigation actions to stop moving

- Status: **Release retained** across Play, Challenge, Expedition, Story, Raid, and saved-team navigation.
- Entry: a detector recognizes an actionable interface while Roblox may still be animating its panel into place.
- Action: require the expected state across its configured stable-frame count. When the detector owns a live action center, also require at least two consecutive action observations within three client pixels before clicking.
- Exit: click the action from the latest stable observation, then verify the destination state normally.
- Do not: accept a single late Play frame, reuse an action coordinate from an earlier animation frame, or let identical state labels alone authorize a moving detector-owned button.
- Failure rule: if the state or action moves, reset the stability candidate and continue within the existing bounded navigation timeout.
- Evidence: the Unit Teams opening sequence reviewed on 2026-07-24 retains the `Teams` state while its real scrollbar is still moving. Earlier field captures also establish vertically shifted Story/Raid party and Challenge dialog families whose live action locations differ.
- Protected by: `StableNavigationActionTrackerTests`, `ChallengeMacroRunnerTests.PlayMenuKey_LateTransitionBeforeRetry_IsAcceptedWithoutAnotherPress`, `LobbyPlayKey_LateKeyTransition_IsAcceptedWithoutAnotherPress`, and the Stage/Challenge/Expedition navigation suites.

### GB-018: Isolated Debug navigation and team tests

- Status: **Product contract** built on the field-confirmed navigation states above.
- Navigation start: the user explicitly chooses either a verified lobby with Play closed or a verified post-match result/party state. An already-open unrelated selector is rejected instead of being treated as the requested start.
- Navigation end: enter the chosen Expedition, Challenge, Story, or Raid route and stop at stable prestart. Do not align the camera, load a team, place units, or click Start Game.
- Team start/end: begin with Units closed, open Units through the configured key, load Team 1–8 through the production scrollbar/action verifier, and close Units before completing.
- Step semantics: a detection checkpoint may pause after a detector observation; an action checkpoint pauses before input. Previous/Next only review captured history. Step authorizes one pending live boundary, while Run resumes without additional gates.
- Ownership: every Debug tool uses the exclusive operation coordinator. When Deep Debug is enabled, the archive includes the selected tool/preset, step mode, ordered checkpoints, frames, detector traces, and resulting input events.
- Do not: interpret rewind as an attempt to reverse already-sent Roblox input, or maintain separate Debug-only click coordinates.
- Protected by: `DebugCheckpointControllerTests`, the existing mode navigation suites, saved-team tests, Deep Debug archive tests, and both-theme Debug page snapshots.

## Reusable evidence workflow

1. Capture the complete attempt with deep debug, or pair a passive diagnostic capture with an exact user-described manual input sequence.
2. Generate timestamped contact sheets with `scripts/New-DiagnosticContactSheet.ps1`. Use enough adjacent frames to include the entry state, input response, intermediate animation or screen, and exit state.
3. Correlate `events.jsonl` input/state records when present. For physical inputs absent from passive logs, explicitly label the action as user reported.
4. Compare the sequence with this ledger. Preserve confirmed behavior unless stronger new evidence shows the game changed.
5. Add or update an entry using the template below.
6. Encode the transition as a policy or workflow regression test. Detector changes additionally require reviewed 808 by 611 fixtures and the full cross-state corpus.
7. Cite the ledger entry in non-obvious code comments or test names so later refactors retain the reason for the ordering.

## Entry template

```text
### GB-NNN: Short behavior name

- Status: Field confirmed | Release retained | Unverified.
- Entry: visible/detected starting state.
- Action: exact key, click, or wait.
- Intermediate: observable transition states.
- Next action: follow-up input, if any.
- Exit: verified destination state.
- Do not: known unsafe shortcut or ordering.
- Failure rule: bounded safe behavior when the transition does not occur.
- Evidence: date, app build, and capture type without private local paths.
- Protected by: regression test names and fixture locations.
```
