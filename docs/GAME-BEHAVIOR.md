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
- Evidence: a manual Story Victory sequence reviewed on 2026-07-22 using v1.3.0-beta.6 shows terminal, Play-key transition, Story party, and shared selector. Four later passive captures begin after the physical Play keypress and independently show Story, Raid, Expedition, and Challenge party-to-selector transitions; they do not contain Victory or Defeat terminals and are not evidence for terminal recognition. The direct terminal keypress path also existed in v1.2.1's Expedition mode-switch workflow.
- Protected by: `StageHandoffPolicyTests.DifferentModeVictory_UsesTheFieldObservedPlayMenuSequence`, `ExpeditionRunPolicyTests.CompletedRunHandoff_UsesOnlyStateOwnedActions`, and Challenge handoff policy tests.

### GB-002: Repeat an identical scheduled route

- Status: **Field confirmed**.
- Entry: terminal screen with the next eligible task resolved to the exact same mode and preset.
- Action: click the detected **Repeat Stage** control.
- Exit: the same route's prestart screen.
- Do not: reopen the Play interface for an identical route.
- Exception: Challenge victories always return through Play because the rotation can advance to a different stage. A Challenge defeat uses Repeat Stage only when a configured retry remains.
- Protected by: scheduler continuation and Challenge continuation policy tests.

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

- Status: **User confirmed** that Anime Expeditions teleports an inactive player to the AFK Chamber after an extended idle period to avoid Roblox's ordinary inactivity disconnect. The exact game-side timeout was not measured.
- Existing recovery: the app already detects the AFK Chamber, chooses **Return to Lobby**, verifies the lobby, and navigates back to the configured route.
- Product rule: when a task intentionally waits longer than ten minutes, perform a keepalive before ten minutes elapse and repeat it while the wait continues. Use an eight-minute cadence so timing jitter cannot cross the ten-minute safety boundary.
- Preferred action: focus and revalidate Roblox, verify a known non-text idle state, then send one ordinary `O` key pulse through the shared keyboard-input path.
- Why `O`: Roblox uses `O` for Zoom Out, and this app already uses that binding during camera preparation. At the fully zoomed-out limit it is effectively idempotent, while an arbitrary click could activate a UI control.
- Preconditions: do not send the pulse while a text field may own keyboard focus, during a transition, or while another workflow owns input. Camera-dependent workflows must still perform their normal zoom/pitch preparation afterward rather than assuming the keepalive established camera state.
- Do not: use a blind mouse click as the default keepalive, wait until the ten-minute boundary, or send only one pulse for a multi-hour wait.
- Failure rule for the future implementation: if Roblox cannot be focused or the idle-safe state cannot be verified, skip the input and retain normal AFK-Chamber recovery rather than interacting blindly.
- Evidence: current application input behavior and documentation establish `O` as the primary zoom-out key; AFK-Chamber fixtures and recovery tests establish the recovery destination. The exact inactivity-trigger duration remains unverified.
- Protected by: documentation only. Before scheduled keepalive is enabled, add tests for waits below ten minutes, repeated eight-minute pulses, cancellation, focus/state rejection, input ownership, and fallback to AFK recovery.

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
- Detail-return rule: after clicking **Back** from an available or cooldown detail, wait for a stable Challenge selector before clicking again. The detail can remain visible while the first click is transitioning; a second immediate Back can land on the restored list and reopen a challenge.
- Evidence: a 16-frame passive diagnostic capture reviewed on 2026-07-22 using v1.3.0-beta.6 shows the successful route. A 61-frame beta.9 deep-debug run reviewed on 2026-07-23 records the delayed detail-to-selector transition and the second Back click reopening the detail.
- Protected by: Challenge selector, cooldown, preview, handoff, and scheduler tests, including `ChallengeMacroRunnerTests.ChallengeDetailBack_WaitsThroughStaleFramesBeforeAnotherClick`.

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
