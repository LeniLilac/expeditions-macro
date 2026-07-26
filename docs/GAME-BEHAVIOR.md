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
- Evidence: a manual Story Victory sequence reviewed on 2026-07-22 using v1.3.0-beta.6 shows terminal, Play-key transition, Story party, and shared selector. Four later passive captures begin after the physical Play keypress and independently show Story, Raid, Expedition, and Challenge party-to-selector transitions; they do not contain Victory or Defeat terminals and are not evidence for terminal recognition. The direct terminal keypress path also existed in v1.2.1's Expedition mode-switch workflow. A beta.13 deep-debug run reviewed on 2026-07-24 preserves the current wide School Grounds Defeat panel and confirms that the background hotbar Play control and **Game Results** button must not replace terminal recognition. A second beta.13 run preserves consecutive Rose Kingdom Victory frames whose animated cyan reward artwork intermittently erased the former generic panel score while the Close, **View Party**, and roster-reward structures remained unchanged.
- Protected by: `StageHandoffPolicyTests.DifferentModeVictory_UsesTheFieldObservedPlayMenuSequence`, `ExpeditionRunPolicyTests.CompletedRunHandoff_UsesOnlyStateOwnedActions`, `ChallengeScreenDetectorTests.CurrentWideDefeatPanel_WinsOverBackgroundHudControls`, `ChallengeScreenDetectorTests.AnimatedVictoryRewards_RemainStableAcrossConsecutiveFrames`, and Challenge handoff policy tests.

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

### GB-004: Return to the lobby from a match

- Status: **Field confirmed**.
- Entry: an active match, including its Victory or Defeat terminal.
- Play-interface prerequisite: the top **Back to Lobby** control is unavailable while the post-match Play interface owns the screen. If Play is open, click its detected bottom-left **Back** action and verify each resulting layer before clicking again. A Challenge handoff can require two actions: shared game-mode selector to retained Challenge party, then retained party to the underlying match.
- Control: enable Roblox accessibility navigation from its reset position, move right twice to **Back to Lobby**, and press Enter.
- Intermediate: an **Exit Confirmation** modal asks whether the player wants to exit the game. It contains a red **Return to Lobby** action and a gray **Cancel** action.
- Next action: disable accessibility navigation, detect the modal in consecutive captures, then mouse-click its detected red **Return to Lobby** action. Require consecutive captures where the modal is absent before waiting for Lobby.
- Exit: Roblox enters its teleport/loading sequence and eventually reaches a stable Lobby.
- Do not: press the Toggle Play Menu key to close nested post-match Play layers, or begin accessibility navigation while one remains open. Do not send accessibility Down/Enter while the confirmation is open; Roblox can move accessibility focus through controls behind the modal instead of confirming it. Do not reuse a terminal close-button coordinate or continue navigation before the modal closure and Lobby are each verified.
- Failure rule: retry the detector-owned red action once if the verified modal remains open. A missing modal, rejected action, changed Roblox session, or unstable Lobby is a recoverable Roblox UI/session failure.
- Unverified: the post-Cancel state was not exercised and must not be assumed.
- Evidence: user-described manual input and passive captures reviewed on 2026-07-22 and 2026-07-26; the latter preserves the shared game-mode selector, retained Challenge party, their stable bottom-left Back actions, the accessibility-opened confirmation, and the subsequent teleport sequence.
- Protected by: `LobbyExitConfirmationDetectorTests`, `MatchLobbyNavigatorTests.ReturnUsesAccessibilityToOpenThenMouseToConfirm`, and `EventPlayInterfaceCloserTests.ChallengeSelectorHandoff_ClicksBackThroughPartyBeforeLobbyNavigation`.

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
  2. Click the configured Raid map and verify its detail panel from the shared red header, top accent edge, dark body, and independently detected live **Select Stage** action. Do not require the decorative Close circle to remain an isolated component; the current panel border can connect to it under both Roblox font settings.
  3. Select the configured Raid act.
  4. Click the detected **Select Stage** action and verify a launch-ready Raid party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Raid prestart screen is verified.
- Evidence: a 17-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6. Physical clicks are user reported; the capture shows the shared selector, Raid map selector, act changes, party preview, teleport, and prestart. Two beta.13 Deep Debug traces reviewed on 2026-07-24 show the current Spirit City detail panel with Roblox's custom and default fonts. Both panels expose the same live **Select Stage** action, while their red Close circles merge with the panel border and therefore cannot be required as isolated button components.
- Protected by: current Raid screen/action detector tests and Raid navigation tests. Add a full workflow-sequence test when the navigation orchestrator is next changed.

### GB-008: Enter an Expedition route from the shared game-mode selector

- Status: **Field confirmed**.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Expedition** mode tile and verify the Expedition map-selection screen.
  2. Select the configured map from the current left-side card rail and verify its cyan active perimeter. Retain the prior compact selector as a supported legacy layout.
  3. Select the configured difficulty from the current lower-left control and wait for its color transition to settle before verifying the active green, red, or purple value.
  4. Click the detected **Select Stage** action and verify a launch-ready Expedition party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Expedition prestart screen is verified.
- Evidence: a 16-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6 covers the prior selector. A second passive capture reviewed on 2026-07-25 covers all three current map cards, all three difficulties across multiple maps, the current **Select Stage** action, and an unchanged launch preview.
- Protected by: Expedition recovery/navigation, current-and-legacy map-selection, difficulty-stability, preview, and prestart tests, including `CurrentExpeditionSelectorTests.Selector_ReportsLiveStateAndActions`.

### GB-009: Enter an eligible Challenge from the shared game-mode selector

- Status: **Field confirmed** for the captured Regular Challenge route and user confirmed for the cooldown/type-selection process.
- Entry: shared game-mode selector.
- Actions and verified states:
  1. Click the detected **Challenge** mode tile and verify the Challenge selector.
  2. Inspect the configured Challenge types and their cooldown or daily-limit state. Do not select a type that is unavailable.
  3. Select the eligible Challenge type and configured challenge row, then verify its detail panel.
  4. Click the detected **Select Stage** action and verify a launch-ready Challenge party preview as described in GB-011.
  5. Click the detected **Start** action and wait through teleport/loading until the Challenge prestart screen is verified.
- State-ownership rule: upgrade reward cards exist only in Expedition matches. Expedition monitoring owns the `reward` state; shared Story, Raid, Challenge, and Event prestart detection must never consult that matcher or let it suppress a valid **Start Game** dialog. The Expedition-only detector still requires the real dark **Select Upgrade** action rails, so colorful map scenery cannot become an actionable reward state.
- Load-failure rule: if the bounded wait expires after a Teleporting transition, report the last current state rather than claiming Roblox is still teleporting. Treat the exhausted stage load as a session-level failure so configured private-server recovery can restart Roblox and retry the same incomplete task without progress.
- Unavailable rule: when all configured Challenge types are on cooldown or exhausted, leave the selector through its verified close action before returning control to the scheduler or waiting.
- Daily-limit rule: retain unavailable-rotation evidence across scheduler handoffs for the full Macro operation. If every regular Challenge remains unavailable after a complete global half-hour reset, treat the account's daily limits as exhausted until the next `00:00 UTC`; run the next eligible task instead of probing Challenges every half hour.
- Detail-return rule: after clicking **Back** from an available or cooldown detail, wait for a stable Challenge selector before clicking again. The detail can remain visible while the first click is transitioning; a second immediate Back can land on the restored list and reopen a challenge.
- Evidence: a 16-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6 shows the successful route. A 61-frame beta.9 deep-debug run reviewed on 2026-07-23 records the delayed detail-to-selector transition and the second Back click reopening a challenge. A beta.14 Deep Debug run reviewed on 2026-07-24 records all regular Challenges unavailable across multiple reset epochs while invocation-local state incorrectly scheduled another half-hour probe. A beta.18 Deep Debug run reviewed on 2026-07-25 records one valid Teleporting frame followed by 270 stable Flower Forest prestart observations whose colorful scenery falsely satisfied an Expedition-only reward-card heuristic inherited by the Challenge path. A beta.27 Deep Debug run reviewed on 2026-07-26 records the same loaded Flower Forest prestart at 98% Start-dialog confidence while the unrelated reward matcher also scored 97%; the Start dialog remains the owned state.
- Protected by: Challenge selector, cooldown, preview, handoff, and scheduler tests, including `ChallengeMacroRunnerTests.ChallengeDetailBack_WaitsThroughStaleFramesBeforeAnotherClick`, `ChallengeMacroRunnerTests.LoadedUnknownScreen_AfterTeleportUsesRuntimeRecovery`, `ChallengeScreenDetectorTests.ColorfulFlowerForestScenery_DoesNotSuppressTheStartDialog`, `DetectorPackGoldenTests.RewardSelection_RequiresLiveActionRailsOverColorfulScenery`, and `ChallengeRunPolicyTests.SeparateScheduledInvocations_SharedStateInfersDailyLimitUntilMidnightUtc`.

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
- Action: locate the live gray scrollbar thumb three to nine pixels right of the detected red Close-control center, reject candidates outside the field-observed 60–95 pixel height range, and require consecutive matching thumb geometry before acting. If a settled reopening is not at the top, repeatedly over-drag the detected thumb above the track within a ten-second deadline so Roblox clamps it at the verified top position, then re-verify it. Hold the left mouse button on that thumb and drag it to the requested team's absolute alignment.
- Alignment: Teams 1 through 6 each align as the first fully visible row and use that row's detected green **Load Team** button. Teams 7 and 8 over-drag below the track so Roblox clamps the thumb at the shared bottom limit, then use the second and third fully visible buttons respectively.
- Exit: require consecutive aligned frames with a full-height Load Team button, click the detected button, and verify the Load Team confirmation before continuing. The clicked or current bottom row can dim one green button below the ordinary modal threshold; two other visible Load Team rows plus the independently detected confirmation action are sufficient evidence.
- Do not: wheel-scroll with the cursor over unit cards, click a clipped third row, rely on scroll position persisting after the interface closes, or assume the older and current panel widths place the scrollbar at the same X coordinate.
- Failure rule: within a fifteen-second target-alignment deadline, keep re-detecting the live thumb and requested full-height Load button. If the button is not yet actionable, drag again toward the target even when the thumb is merely near it. Never use a fixed adjustment count or click a neighboring/clipped row. An exhausted deadline is a recoverable Roblox UI failure.
- Evidence: three beta.13 team-selection runs and two passive manual-navigation captures reviewed on 2026-07-23. Team 3 failed because its fixed `(580, 447)` action landed below a clipped button. The manual drag sequence establishes one aligned position per Team 1–6 and the shared bottom limit for Teams 7–8. Physical manual drags are user reported; the passive captures preserve their resulting positions. A later macro trace reviewed on 2026-07-24 shows the opening thumb rising to center `240` while an unrelated gray background run at approximately `x=644`, `y=190–435` caused the old longest-run heuristic to report center `312`. Two beta.15 Debug runs reached and clicked the correct Team 7/8 buttons, but the confirmation detector rejected the visible dialog because one underlying green row covered 9.8% rather than the former 10% requirement. Five beta.16 Team 8 failures and two successes establish two additional boundaries: scenery at `x=644–650` can form a valid-height gray run while the real top thumb remains at `x=628`, and a drag ending at the desired bottom center can under-travel to centers `341–380`. Only the successful controls reached the clamped bottom center `396`; at center `373`, the apparent third action region still belongs to Team 7. A beta.20 Team 3 trace reviewed on 2026-07-25 records two accepted drags whose second result settles seven pixels above the nominal center while Team 3's Load button is already fully visible and stable. The live row band must include its full button, and a near-target tolerance must accept this Roblox snap.
- Protected by: `TeamSelectionServiceTests.Select_AlignsAndLoadsEveryTeamWithoutWheelScrolling`, `Select_WaitsForTheOpeningAnimationAndUsesTheRealTopThumb`, `Select_NormalizesAReopenedScrolledListBeforeLoading`, `Select_AcceptsAStableNearTargetScrollbarUndershoot`, `Select_LowerTeamsDragPastTheTrackSoRobloxClampsAtBottom`, and the aligned/opening/background-decoy cases in `TeamScreenDetectorTests`.

### GB-015: Re-observe grouped coarse camera correction

- Status: **Field confirmed** for the saved 72-step Expedition camera model.
- Entry: the stabilized runtime view has either a registered match or a strong, isolated dense fingerprint match in the saved full-turn yaw atlas but does not already match the goal.
- Action: choose the shortest direction, send no more than six arrow pulses, capture the resulting pose, and recalculate the shortest correction from the newly observed atlas position.
- Dense evidence rule: a below-threshold registered match may earn only this bounded feedback path when it retains at least 20% structural evidence, its fingerprint is at least 94%, and that fingerprint is at least six percentage points above every atlas position outside a density-scaled angular neighborhood. During setup restoration only, a verified prior position plus bounded input may replace global isolation for the next physically constrained lookup; it still cannot complete setup without direct goal verification.
- Exit: stop grouped correction once the goal atlas is reached, then retain direct scoring, fine-yaw refinement, and three-frame final verification.
- Do not: send the entire predicted correction as one rapid open-loop arrow sequence, assume its accumulated yaw equals the separately observed calibration samples, or treat a fingerprint match as successful final alignment.
- Failure rule: stop the feedback loop when atlas evidence becomes ambiguous, the same non-goal position repeats, a position cycle appears, ten observations are consumed, or one full-turn pulse budget is reached. Continue through the existing bounded refinement/full-turn fallback rather than oscillating.
- Evidence: beta.13 setup and manual Auto Align Deep Debug archives reviewed on 2026-07-23. Setup produced a clean 100% zero pose and 72-step return. The random runtime pose matched atlas position 37 at 88%; a rapid 35-pulse correction landed near position 8 instead of zero, after which the fixed rightward fallback needed 60 of 72 steps to recover the 96% goal. A later beta.14 dense setup/alignment pair located the runtime yaw at 99% fingerprint confidence with 16% remote separation but only 39% registered structure; its predicted 27 right pulses were within four pulses of the verified 31-pulse goal, while every six-pulse re-observation retained at least 97% fingerprint confidence and 7% remote separation.
- Protected by: `CameraClosedLoopCorrectionTests.Align_WhenRapidArrowBatchOvershoots_ReobservesAndUsesShortestCorrection`, `Align_WhenCoarseObservationDoesNotMove_StopsFeedbackBeforeFallback`, and `CameraCoarseAtlasEvidencePolicyTests`.

### GB-016: Learn dense yaw without assuming arrow timing

- Status: **Release retained**; the exact continuous-versus-pulsed rate remains device-local.
- Entry: camera setup has standardized the Roblox client, zoom, pitch, and Shift Lock state and captured a stable goal.
- Action: capture every signed fine-yaw position and verify its stationary return, then hold Right Arrow once while sampling regional visual fingerprints at up to roughly 60 FPS. Stop on the first turn only when the returning fingerprint also matches a signed fine-yaw reference or the registered structural score independently verifies the exact goal. If that seam falls between samples and a later repeated seam is recognized, compare distributed fingerprint and registered-structure pairs across both candidate revolutions and retain the first period only when the repetition is proven. After releasing the key, locate the stationary pose in the one-turn atlas, return in bounded arrow groups with a fresh observation after every group, and fine-verify the goal. If fine correction verifies the full-resolution goal while the circular atlas remains at the adjacent `1` or last bin, accept the visual goal instead of sending a coarse pulse across the seam. Use bounded observations after three and six discrete pulses—extending to twelve when needed—to calibrate dense-bin-to-pulse conversion, then perform the same closed-loop atlas return.
- Exit: save a schema 4 model only after both the sweep release and pulse probe have independently returned to the fine-verified goal. Normal setup targets less than 20 seconds; a stalled operation may continue until the independent 120-second hard timeout.
- Do not: infer yaw solely from elapsed hold time, save two revolutions as one atlas because the first exact seam sample was skipped, fold a slow single revolution without distributed structural proof, assume the camera stops on the callback frame, assume equal opposing pulse counts are visually reversible, undo a verified fine-mouse goal merely because the coarse atlas is one seam bin away, require two consecutive goal frames while the camera is still moving, fail a complete fine sweep because one moving zero capture was transient, treat descriptor similarity as final verification, save a partial sweep, or replace bounded runtime goal verification with a fingerprint match.
- Failure rule: release every held key or mouse button, restore the captured Shift Lock key, leave any existing model untouched, and report the setup failure.
- Evidence: eleven same-map beta.14 Deep Debug setups reviewed on 2026-07-23. All eight completed turns entered the saved fine neighborhood on their first turn, but only one produced a moving frame above the old near-exact structural gate; three additional attempts returned to a strong stationary zero after one transient moving-zero capture. A later eleven-run, multi-map beta.14 set contained seven successes and four retries: two recognized the loop but landed beyond the ±16-pixel fine window after key release, while two sent 12 right and 12 left calibration pulses yet remained at only 32–33% goal confidence. Successful siblings on the same scenes required 4–8 fine steps after release and as many as 14 after the pulse probe, confirming that both boundaries require atlas feedback rather than input-count reversal. Near-identical beta.15 success/failure controls reviewed on 2026-07-24 used the same 30 ms arrow setting and map: the success recognized 67 positions at the first 3.02-second seam, while the failure skipped that seam and saved 100 positions only after a second 6.03-second seam. A beta.16 failure repeated the same pattern with 138 positions over 6.05 seconds. Distributed retrospective comparisons independently identify the duplicated cycle in all supplied six-second traces. A later local-build setup reviewed on 2026-07-24 reached the goal by fine mouse correction from atlas bin `1/78`, but the former controller ignored that verified return and alternated between `1/78` and `77/78` for 30 arrow pulses before its round limit accepted the same goal.
- Protected by: `DenseCameraAtlasTests.Calibrate_DenseHybridAtlasCompletesWithinBudget`, `Calibrate_DenseGoalReturnUsesClosedLoopAtlasFeedback`, `Calibrate_MissedFirstClosureDiscardsTheRepeatedTurn`, `DensePeriodPolicyDoesNotFoldOneSlowRevolution`, `DenseLoopPolicy_RequiresExactOrIndependentFineEvidence`, `Align_DenseAtlasConvertsVisualBinsToPulseDistance`, schema compatibility tests, and the existing final-verification camera suite.

### GB-017: Wait for navigation actions to stop moving

- Status: **Release retained** across Play, Challenge, Expedition, Story, Raid, and saved-team navigation.
- Entry: a detector recognizes an actionable interface while Roblox may still be animating its panel into place.
- Action: require the expected state across its configured stable-frame count. When the detector owns a live action center, also require at least two consecutive action observations within three client pixels before clicking.
- Exit: click the action from the latest stable observation, then verify the destination state normally.
- Do not: accept a single late Play frame, reuse an action coordinate from an earlier animation frame, or let identical state labels alone authorize a moving detector-owned button.
- Failure rule: if the state or action moves, reset the stability candidate and continue within the existing bounded navigation timeout.
- Evidence: the Unit Teams opening sequence reviewed on 2026-07-24 retains the `Teams` state while its real scrollbar is still moving. Earlier field captures also establish vertically shifted Story/Raid party and Challenge dialog families whose live action locations differ.
- Protected by: `StableNavigationActionTrackerTests`, `ChallengeMacroRunnerTests.PlayMenuKey_LateTransitionBeforeRetry_IsAcceptedWithoutAnotherPress`, `LobbyPlayKey_LateKeyTransition_IsAcceptedWithoutAnotherPress`, and the Stage/Challenge/Expedition navigation suites.

### GB-018: Isolated Debug operations

- Status: **Product contract** built on the field-confirmed navigation states above.
- Navigation start: the user explicitly chooses either a verified lobby with Play closed or a verified post-match result/party state. An already-open unrelated selector is rejected instead of being treated as the requested start.
- Navigation end: enter the chosen Expedition, Challenge, Story, or Raid route and stop at stable prestart. Do not align the camera, load a team, place units, or click Start Game.
- Team start/end: begin with Units closed, open Units through the configured key, load Team 1–8 through the production scrollbar/action verifier, and close Units before completing.
- Fast no align: begin with Shift Lock off and a visible supported Roblox window. Standardize the client to 808 by 611, clamp maximum zoom, temporarily enable the configured Shift Lock key, use only vertical relative camera drags to clamp top-down pitch, then restore Shift Lock. Arrow pulses and horizontal relative movement are forbidden so yaw remains unchanged.
- Step semantics: a detection checkpoint may pause after a detector observation; an action checkpoint pauses before input. Previous/Next only review captured history. Step authorizes one pending live boundary, while Run resumes without additional gates.
- Ownership: every Debug tool uses the exclusive operation coordinator. When Deep Debug is enabled, the archive includes the selected tool/preset, step mode, ordered checkpoints, frames, detector traces, and resulting input events.
- Do not: interpret rewind as an attempt to reverse already-sent Roblox input, or maintain separate Debug-only click coordinates.
- Protected by: `CameraPosePreparationServiceTests`, `DebugCheckpointControllerTests`, the existing mode navigation suites, saved-team tests, Deep Debug archive tests, and both-theme Debug page snapshots.

### GB-019: Experimental resource-refuel route tests

- Status: **Field confirmed for manual navigation; experimental Debug-only automation**.
- Start A: the player is in the standard lobby pose with Areas closed. Start B: close the verified Roblox process, launch the saved private-server link, then wait for the same stable lobby.
- Areas route: press the configured **Toggle Areas** letter, select **Expeditions**, and click **Expeditions Hub**.
- Blind movement: after the Hub click, wait for teleport completion, then perform the user-configured Gold Mine route (`W`, `A`, `W`) or Resource Drill route (`W`, `A`, `W`, `A`). Do not use image/color detection during this movement.
- Default routes: Gold Mine holds `W` for 3000 ms, `A` for 820 ms, then `W` for 2600 ms. Resource Drill holds `W` for 3000 ms, `A` for 750 ms, `W` for 1000 ms, then `A` for 1600 ms. Saved Debug settings remain user-controlled.
- Interaction: press `E`, then begin visual verification. Require the expected Gold Mine or Resource Drill panel, click its detected **Add Fuel** action, require the quantity dialog, click detected **Max**, then click the confirmed green action.
- Retry: if `E` does not expose the expected station panel, reopen Areas, teleport to Expeditions Hub, and replay the complete configured route. Bound retries to the saved Debug value.
- Between stations/end: use Areas again between Gold Mine and Resource Drill. After the final station, open Play with the configured Play key.
- Ownership: use the same coordinator, focus/canonical-client checks, acknowledged clicks, held-key cleanup, checkpoint stepping, and Deep Debug tracing as other automation.
- Product boundary: this tool exists only on the Debug page for route calibration and dataset gathering. It is not a Macro task, has no automatic schedule, and must not infer an eight-hour due time.
- Evidence: a 419-frame passive capture reviewed on 2026-07-24 covers Lobby → Areas → Expeditions Hub → Gold Mine → Add Fuel → Areas → Expeditions Hub → Resource Drill → Add Fuel → Play. Physical movement/input timing is user described because the passive archive contains frames but no input events.
- Protected by: `ResourceRefuelDetectorTests`, `ResourceRefuelServiceTests`, Areas-binding persistence/conflict tests, Deep Debug sanitized-settings tests, and both-theme Debug/Settings snapshots.

### GB-020: Select a high-saturation Expedition reward

- Status: **Field confirmed**.
- Entry: a live three-card Expedition reward chooser with a global blue/cyan wash, a thin progress bar, and three **Select Upgrade** actions.
- Action: identify the thin bright progress bar independently from the broader wash, require the repeated live action row, then click the first detected **Select Upgrade** action.
- Exit: the reward chooser closes and ordinary node monitoring resumes.
- Do not: reject the chooser merely because a color/HDR profile makes the full header band cyan, click a card from the overlay color alone, or wait for Roblox's automatic selection timer.
- Evidence: three consecutive beta.16 Deep Debug frames reviewed on 2026-07-24. The complete chooser remained visible for about 24 seconds with a reward score of zero and no macro input; Roblox eventually auto-selected the right card. The affected profile made all 55 rows of the old broad cyan search band pass, while the actual bright progress bar remained a distinct seven-row structure.
- Protected by: `DetectorPackGoldenTests.RewardDetector_IsRarityIndependentAcrossAllCapturedCardDatasets`, the complete cross-state corpus, and `Expedition_Reward_Select5`.

### GB-021: Recover a match that exceeds its possible runtime

- Status: **User-confirmed product contract** based on observed match ceilings.
- Entry: the macro has clicked Start Game and no Victory, Defeat, or root recovery state has completed the active match.
- Fifteen-wave limit: Story Acts 1-5, Story Mastery, every Regular Challenge, and Raid Acts 1-3 must reach Victory or Defeat within 12 minutes. This deliberately exceeds the observed two-to-eight-minute range so slow and debuff-heavy teams remain valid.
- Infinite rule: Story Infinite has no match-runtime watchdog because its valid duration is indeterminate.
- Expedition limit: when checkpoint extraction is enabled, a zero-boss target has a 10-minute limit; positive targets have 15 minutes per requested boss (`1 = 15`, `2 = 30`, `3 = 45`, and so on). An Expedition with extraction disabled has no target-derived watchdog.
- Failure rule: emit a recoverable timeout before any task progress is recorded. Save configured failure diagnostics, close only the verified Roblox process, relaunch the configured private server, reload the saved plan, and retry the same incomplete task. The existing three-restarts-per-ten-minutes circuit breaker remains authoritative.
- Do not: count a watchdog timeout as Victory, Defeat, a defeat retry, or completed task progress; impose the 12-minute limit on Story Infinite; or restart without a configured private-server target.
- Protected by: `MatchRuntimePolicyTests`, `RecoveringMacroSchedulerTests.RuntimeFailure_RestartsRobloxAndRetriesTheIncompleteTask`, and the mode-monitor suites.

### GB-022: Dismiss rare Raid unit drops after placement

- Status: **Field confirmed** for Spirit City Acts 2 and 3.
- Entry: Start Game has been clicked and every configured after-start placement step has returned successfully. The monitor has then observed the ordinary unit hotbar, Unit Manager action, and Stage Info action in consecutive settled frames.
- Trigger: while no Victory or Defeat candidate is visible, the established gameplay HUD disappears in consecutive frames. The rare unit-drop presentation hides all three signals and requires a click anywhere to close; its unit, rarity, and artwork can vary.
- Action: click the bottom-right client resting area at `(783, 586)` through the acknowledged client-relative click path. If the HUD remains hidden, retry no more than once per second until gameplay or a terminal candidate returns.
- Scope: enable this behavior only for Raid Acts 2 and 3. Story, Challenge, Expedition, and Raid Act 1 do not own this unit-drop rule.
- Do not: inspect for the overlay before after-start placement completes, click continuously during placement playback, recognize one specific unit or shiny variant, or click after a Victory or Defeat candidate appears.
- Evidence: a five-frame beta.17 passive capture reviewed on 2026-07-25 shows the stable 8th Sword drop presentation for five seconds with the hotbar and both right-side gameplay actions absent.
- Protected by: `RaidDropDismissalTrackerTests` and `StageGameplayHudDetectorTests`.

### GB-023: Resume a private-server restart only from stable Lobby

- Status: **Product contract**.
- Entry: process-level recovery has closed the verified Roblox PID, launched the configured private-server URI, and discovered a different supported Roblox player PID.
- Readiness: standardize the new client to 808 by 611, then require three consecutive detector observations whose recovery state is exactly `lobby`.
- Intermediate rule: unknown, splash, teleport, loading, Play, selector, prestart, result, AFK, Disconnect, capture-error, and wrong-size observations reset Lobby stability and send no Roblox input.
- Exit: return control to the scheduler only after stable Lobby, reload the saved plan, and retry the same incomplete task.
- Failure rule: if the new process never reaches stable Lobby before the bounded recovery deadline, surface a session-level recovery failure. Do not consume Play-key attempts or route clicks during that wait.
- Protected by: `RobloxLobbyReadinessGateTests`.

### GB-024: Preserve deterministic yaw for Fast no align placements

- Status: **User-confirmed product contract** with canonical map captures.
- Entry: a supported match has reached verified prestart. Roblox loads the map at its deterministic starting yaw, and the preset uses Fast no align.
- Preparation: standardize the client to 808 by 611, clamp maximum zoom, temporarily enable the configured Shift Lock key, clamp straight-down pitch with vertical relative movement, then restore Shift Lock. Send no arrow pulse or horizontal relative movement.
- Reuse: prepare once per top-level macro operation and Roblox process. Reuse the pose across Repeat Stage and post-match Play handoffs, including a different mode or map, because those paths preserve zoom, pitch, and yaw.
- Invalidation: prepare again after any verified Lobby observation, after Roblox restarts into a new PID, or when a new top-level macro operation begins. A failed or canceled preparation is never cached.
- Placement authoring: use the exact embedded native 808 by 611 screenshot for the selected category, mode, map, act, or Story run. Store every point with its unit slot and Before Start or After Start phase. Keep the stable Before Start group above the stable After Start group on load, insertion, drag/arrow reorder, and save; a reorder may move a step only inside its own phase. Each setup stores its delay between placement sequences and the default offset assigned to newly authored After Start points. Changing that default preserves points whose offsets were independently customized. After Start points carry independent absolute offsets from the Start click; equal offsets preserve author order for ties. Reject points less than 7 client pixels apart and allow direct marker removal without editing coordinates.
- Placement playback: tap the saved unit slot, send the configured Cancel Placement key, then tap that same slot three times to force a deterministic select/deselect/reselect state. This normalization runs once per configured placement. For each click attempt, position the cursor exactly 50 client pixels horizontally from the saved target, move into the coordinate over 200 milliseconds through acknowledged motion, send one cursor-retaining placement click, and require two consecutive selected-unit-panel confirmations. If confirmation is absent, repeat only that timed approach and click, up to eight bounded attempts; never repeat the key/cancel normalization or park inside the same placement sequence. After confirmation, tap the configured Change Unit Targeting key zero through eight times for First, Last, Closest, Strongest, Boss, Weakest, Shielded, Fastest, or None. Park only between complete unit-placement sequences.
- Placement proof: after a candidate placement click, accept the unit as selected only when the fixed lower-left panel exposes both its red Close control and initial blue **Priority / First** control. The dark panel body is supporting evidence only. An ordinary unit hover/info card is not placement proof, and the final configured targeting priority is applied by key taps without image-reading its label.
- Compatibility: resolve an exact Fast setup before its category fallback. The Expeditions category covers Expedition maps 1-3. Each Story-map category covers that map's Acts 1-5, Infinite, Mastery, and Challenge. Raid acts have no category fallback. A category from another map or mode remains incompatible. Camera-model placements and Fast placements cannot be mixed. Legacy files with no strategy field remain Camera model placements.
- Evidence: eleven privacy-reviewed canonical frames extracted from a passive Deep Debug capture on 2026-07-25 cover five shared Story/Challenge maps, all three Spirit City Raid acts, and all three Expedition maps.
- Protected by: `PlacementAuthoringTests`, `PlacementServiceTests`, `FastNoAlignPreparationSessionTests`, preset validation tests, runner compatibility checks, and both-theme Placement Models snapshots.

### GB-025: Normalize UI Scale before stable-Lobby startup

- Status: **Field confirmed**.
- Entry: the Roblox client is standardized to 808 by 611, Settings is closed, and any fully loaded game view exposes the stable top-left Settings gear. Before enabling accessibility navigation for either UI Scale or required-settings correction, temporarily enable the configured Shift Lock and clamp pitch to the repeatable straight-down limit. Do not zoom or change yaw; restore Shift Lock before accessibility input. This prevents world/background interactable UI from joining the accessibility focus path while the player faces forward. The standalone Debug UI Scale action may begin from Lobby, gameplay, prestart, or another loaded state. A Macro plan still requires a clean Lobby before task navigation, but that verification occurs only after UI Scale is canonical.
- UI-scale action: after the pitch-only preparation completes, enable accessibility navigation from the loaded game view, move Right to Settings, and press Enter to open it. Wait for the panel and its red Close control to finish their opening animation. When scale is not canonical, hold that settled state for one second, move Left to restore the in-panel navigation origin, then use physical accessibility keys spaced by 500 ms. Move Down seven times to Misc and press Enter; require two more consecutive settled-panel detections plus a one-second hold before moving Right, Down, Down, then Left. This route returns accessibility focus to the UI Scale input whether the smaller-scale layout initially selects the slider or the input. Press Enter to select the input, clear it, enter an initial `1.00`, and apply. Measure two consecutive settled rendered-panel scales; if the result is not canonical, enter `applied value / observed rendered scale`, rounded to two decimals and clamped to 0.80–1.20. Repeat boundedly while the field remains selected until the rendered panel independently verifies canonical scale. Numeric `1.00` is not assumed to render identically on every device.
- Intermediate: disable accessibility navigation, re-enable it to reset the focus origin, move Right to Settings, press Enter to close the panel, then disable accessibility navigation again. Verify the panel itself is gone without relying on Lobby detection. Macro startup then requires three consecutive clean Lobby detections at canonical scale and reopens Settings through the same accessibility-owned Settings action. This clears the Settings search/filter before coordinate-based page navigation. The standalone UI Scale Debug action ends after panel closure and does not require Lobby.
- Required profile: Gameplay enables Auto Skip Waves and disables Auto Vote Start, Show Match End Rewards, Display Pinned Quests, Select Unit on Placement, Display Path Visualizers, Auto Retry, and Auto Next. Graphics disables Camera Shake, Depth of Field, Night Time, and Event Theme while enabling Low Detail. Units disables other/own Unit VFX, Ability Effects, Unit Aura, Trait Aura, and Damage Indicators while enabling Strict Phantom Placement, Prioritize Phantom Placement, Auto-Upgrade Placed Units, and Auto Abilities on Placement. Misc disables the update log on login and enables Auto Sprint. Unlisted controls have no required state and remain untouched.
- Units scroll: detect and drag the live blue scrollbar thumb to verified top and bottom boundaries before using the corresponding toggle coordinates.
- Exit: close Settings and require three consecutive Lobby detections again before the scheduler may choose its first task.
- Do not: begin accessibility navigation while a forward-facing camera exposes world interactables, zoom or rotate yaw during the protective pitch clamp, require scale-dependent Lobby recognition before canonical rendered UI Scale is verified, assume numeric `1.00` is universal, click a page or toggle before scale verification, infer a toggle from label text or a custom font, act on the opening animation, click an unknown control, or begin task navigation from a non-Lobby screen. Disabling automatic correction skips both camera preparation and Settings work; it does not remove the stable-Lobby start rule.
- Failure rule: use bounded page, toggle, scale, and scrollbar verification. Never click an uncertain control or begin match navigation. A recognized control that ignores input, an unstable panel/Lobby, or a lost/resized Roblox window is a recoverable UI/session failure. If the initial preflight never completed, recovery may retry it; after it succeeds, do not rerun it during the same macro operation. An unrecognized control remains a hard compatibility failure.
- Evidence: two pixel-identical 287-frame diagnostic captures reviewed as timestamped contact sheets on 2026-07-25 cover accessibility navigation, UI Scale 0.80/1.00/1.20, all required pages, and both Units scrollbar boundaries. A beta.20 Debug UI Scale trace contains 19 fully loaded Lobby frames whose scale-dependent recovery detector returned no Lobby state; the old pre-scale gate therefore sent zero input and failed before opening Settings. A current-update capture adds **Event Theme Enabled** to Graphics and moves the Misc controls. A red Event-theme Lobby trace also proves that a bright railing can resemble the Settings Close component unless the detector independently requires the Settings panel's dark body.
- Protected by: `CameraPosePreparationServiceTests.PreparePitchOnly_PreservesZoomAndYaw`, `MacroStartupPreflightServiceTests.UiScaleDebug_NonLobbyStartDoesNotRequireLobby`, `MacroStartupPreflightServiceTests.GameSettingsDebug_PreparesPitchBeforeAccessibility`, `MacroStartupPreflightServiceTests.NonLobbyStart_NormalizesUiScaleBeforeLobbyGate`, `MacroStartupPreflightServiceTests.DeviceDependentScale_UsesRenderedFeedback`, `MacroStartupPreflightServiceTests.EventThemeLobby_IsAcceptedAsCleanBeforeSettingsNormalization`, `UiScaleFeedbackPolicyTests`, `GameSettingsScreenDetectorTests`, `AppSettingsStoreTests`, physical keyboard scan-code tests, the complete cross-state corpus, and both-theme Settings snapshots.

### GB-026: Recover owned Roblox UI failures without recording progress

- Status: **Product safety contract**.
- Entry: a Macro plan owns Roblox and a verified runtime/session operation cannot finish safely: the window is missing, changed, unfocusable, or incorrectly sized; a known panel, button, or scrollbar will not settle or acknowledge input; or a verified navigation transition times out.
- Action: stop task input, save the configured automatic diagnostic, close only the verified Roblox player process, and reopen the configured private server through the registered `roblox://` protocol.
- Resume: require a new Roblox PID and three stable Lobby frames, reload the saved plan, and retry the same task. UI Scale/game-settings preflight is operation-scoped and is not repeated after a successful startup check. The failed attempt must not increment victories, defeats, runtime target, or completion.
- Hard boundaries: cancellation, invalid plan/setup/model data, unsupported detector layout, an invalid Play key binding, malformed private-server link, internal state-machine invariant, or no configured restart target never authorizes process restart. Stop after three restarts in ten minutes.
- Notification: recoverable attempts may create diagnostics and recovery log entries, but must not claim the Macro stopped unexpectedly while it is continuing.
- Protected by: `RobloxRuntimeRecoveryPolicyTests`, `RecoveringMacroSchedulerTests`, Team/UI action regressions, and lobby-readiness tests.

### GB-027: Enter and repeat the Villain Invasion Event

- Status: **Field confirmed** for Acts 1–4.
- Entry: a verified Lobby with Play, Areas, Units, Settings, and the Event panel closed. Event routes have no Play-interface tile.
- Navigation: click the detected Lobby **Events** action, verify the Villain Invasion Event home, click its game-mode action, and verify the generic act-selector structure before choosing an act. Locate the configured act from its live colored emblem—purple for Act 1, green for Act 2, cyan for Act 3, and yellow for Act 4—and require that target to settle across consecutive frames before clicking its containing card. Scroll the carousel first for Acts 3 and 4; never rely on one fixed card coordinate. Then click the detected **Select Stage**, verify the preview, click its live Start action, and wait for verified prestart.
- Transition ownership: Event → Story, Challenge, Expedition, or Raid may use the post-match Play interface. Normal mode → Event and Event → Event must return to verified Lobby first because the Event route is absent from Play. If an unavailable Challenge hands scheduling control back from the shared selector, Event first backs through the shared selector and retained Challenge party with stable visual verification; only after Play is closed may it use the match-owned Back-to-Lobby control.
- Preparation: use Fast no align at the first prestart. Act 1 then holds `W` for 750 ms, `D` for 750 ms, and `W` for 750 ms. Act 2 holds `A` for 75 ms and `W` for 2000 ms. Acts 3 and 4 need no spawn movement. Trust these bounded movements and begin state detection again only after they finish.
- Repeat rule: Repeat Stage preserves player location and camera pose, so do not repeat the act-specific spawn movement, Fast no align preparation, or an unchanged saved-team load on a verified Event Repeat Stage handoff.
- Victory actions: Acts with an unlocked following act can add **Next Stage** before **Repeat Stage**; the final unlocked act omits it. Locate the yellow **Repeat Stage** action by its owned visual component instead of using a fixed action index.
- Runtime rule: Acts 1–3 are 15-wave matches with a 12-minute terminal deadline. Act 4 is a 25-wave match with a 17-minute terminal deadline. If no Victory or Defeat appears by the route-specific deadline after Start, record diagnostics and raise a recoverable session failure so configured private-server recovery can relaunch Roblox and retry the same incomplete task without progress.
- Do not: search Play for Event, send act movement more than once per initial stage load, or classify Event route pages from labels alone.
- Evidence: reviewed 808 by 611 captures from 2026-07-25 cover the Lobby Event button with and without its notification marker, Villain Invasion home, act selector, Acts 1–3 detail selection, preview, prestart, Victory both with and without **Next Stage**, Defeat, and three Fast no align placement views. A 77-frame manually dragged carousel sequence preserves the moving act-card positions and stable colored emblems. Additional 2026-07-26 captures cover the horizontally scrolled Act 4 selector, Act 4 detail, its no-movement Fast no align placement view, and its final Victory action rail. Physical carousel movement, act movement timing, Act 4's 25-wave/17-minute limit, and Repeat Stage preservation are user-confirmed behavior.
- Protected by: `EventScreenDetectorTests.ActEmblems_MapTheirLiveCards`, `EventScreenDetectorTests.ActEmblems_RejectCardsOutsideTheVisibleCarousel`, `EventRunPolicyTests`, Event placement compatibility checks, current cross-state detector tests, and the Event runner's stable verified navigation/terminal boundaries.

### GB-028: Begin a Macro plan from a fresh private-server Lobby

- Status: **Product safety contract**.
- Default entry: the user starts a top-level Macro plan with **Restart Roblox at Macro start** enabled and a valid private-server link configured.
- Action: discover the supported Roblox player window. If one is open, close only its verified Roblox PID. Pass the normalized private-server `roblox://` URI to Windows whether or not a process was open.
- Readiness: discover a new supported Roblox PID, standardize its client to 808 by 611, and require three consecutive Lobby detections before startup UI Scale/game-settings preparation or task navigation.
- Frequency: perform this reset exactly once for each user-started Macro plan. Do not repeat it between tasks, loops, ordinary Lobby returns, or after the scheduler's separately bounded runtime-recovery restart.
- Disabled rule: when the toggle is off, retain the existing stable-Lobby startup requirement and never infer permission to close Roblox.
- Failure rule: enabling the option requires a valid private-server link before the operation starts. A failed launch or Lobby-readiness timeout stops safely without sending route input to an unknown screen.
- Privacy: persist the private-server link only through the existing DPAPI-protected field. Deep Debug records only whether startup restart was enabled and whether a link was configured; it never records the link.
- Protected by: `AppSettingsStoreTests.LegacySettings_DefaultStartupChecksToEnabled`, `RecoveringMacroSchedulerTests.StartupRestart_RunsOnceBeforePreflightAndTask`, and `DeepDebugSessionTests.SuccessfulSessionArchivesFramesEventsSanitizedSettingsAndReferencedModels`.

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
