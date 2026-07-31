# Expeditions Macro

Expeditions Macro is a Windows desktop utility for repeatable Anime Expeditions, Challenge, Story, Raid, supported Event, and Bounty runs in Roblox. It combines prioritized task plans, deterministic Fast pose preparation, editable unit placement, saved-team selection, UI-state detection, recovery, checkpoint extraction, and optional Discord reporting in one native app.

![Expeditions Macro in dark mode](docs/images/app-dark.png)

It uses screen capture and ordinary Windows input. It does not inject into Roblox, read process memory, or bypass anti-cheat systems.

> This is an independent, noncommercial community project. It is not affiliated with Roblox Corporation, Anime Expeditions, or the game's developers. Automation may be restricted by a game or platform's rules; you are responsible for how you use it.

## What it does

- Starts and stops with one configurable global hotkey; **F6** is the default, with letters, digits, punctuation, numpad keys, and supported function keys available.
- Starts only from the fully loaded Roblox lobby, then navigates to the configured mode, map, and difficulty.
- By default, checks Anime Expeditions' required Gameplay, Graphics, Units, and Misc settings before the first task and corrects mismatches after normalizing UI Scale to 1.00.
- Runs any enabled Trait, Stat, and Sprite Challenges on the global half-hour reset, recognizes five rotating maps, and loads the matching Placement Setup.
- Supports one ordered Match Steps timeline around a required Start Game boundary, plus configurable Challenge defeat retries; while Challenges are unavailable, the Macro scheduler selects the next eligible task by priority.
- Navigates Story Acts 1-5, Infinite, and Mastery across five maps, plus Spirit City Raid Acts 1-3.
- Navigates Villain Invasion Event Acts 1-4 from Lobby, including act-specific initial-load preparation and recoverable runtime guards: 12 minutes for Acts 1-3 and 17 minutes for the 25-wave Act 4.
- Runs deterministic Mythic Bounty objectives from the Event Bounty Board, merges overlapping Infinite-map work, claims up to the ten-per-UTC-day limit, and preserves unfinished active Bounties between launches.
- Saves prioritized Macro plans that can rotate Challenge, Expedition, Story, Raid, supported Event, Bounty, and interval-based resource-refuel Utility routes while preserving progress between launches.
- Optionally opens Units and loads Team 1-8 before a configured run.
- Uses the sole supported **Fast no align** preparation flow, which fully zooms out, temporarily enables Shift Lock, sets a top-down pitch, and preserves the map's deterministic starting yaw.
- Creates an ordered Match Steps timeline directly on bundled 808 by 611 map screenshots. Place Unit actions own coordinates; Delay, Reconfigure Unit, Upgrade Unit, and the required Start Game boundary are ordered in the same timeline.
- Offers opt-in **advanced manual recordings** for complete prestart-to-gameplay keyboard, mouse, and wheel sequences. Playback targets signed drift within +/- 50 ms on one absolute timeline and stops only at the +/- 2,000 ms hard limit.
- Detects start, checkpoint, continue, confirmation, reward, victory, defeat, lobby, disconnect, and AFK Chamber screens.
- Captures the Roblox window directly instead of reading overlapping desktop windows, and normalizes HDR/Auto HDR pixels to SDR before detection.
- Detects reward cards from the stable reward overlay and available Select Upgrade controls, including layouts where a card is still collapsed or moving and regardless of rarity color.
- Extracts at the first real checkpoint or after a configured number of boss nodes. The spawn is not counted because it has no Extract action.
- Handles an early defeat even when extraction was planned later.
- Rejoins after a Roblox disconnect, an unexpected lobby teleport, or an inactivity teleport to the AFK Chamber. From the AFK Chamber it chooses **Return to Lobby**, then navigates back to the configured map and difficulty.
- Sends a safe `O` pulse every eight minutes during long-running gameplay and Challenge waits so normal inactivity does not reach the AFK Chamber.
- Detects the rare blue-void stage load before camera movement or placement, returns through Play, and retries the same task.
- Can optionally close only the verified Roblox player process and reopen a saved private server when bounded in-client recovery cannot restore the run.
- Confirms recovery screens across consecutive captures before rejoining, so one animation frame cannot reset an active run or its checkpoint-extraction progress.
- Optionally sends Discord Components V2 reports with a Roblox screenshot when each mode starts, per-match and whole-plan runtime on results, whole-plan victory/defeat totals that persist across mode changes, recovery notices, semantic state accents, and localized Discord timestamps. A configured Discord user ID receives five restricted mentions when a macro stops unexpectedly.
- Records an unlimited timed Roblox screenshot sequence from Settings and packages the frames plus a manifest into one diagnostic ZIP. Automatic failure diagnostics keep the latest 10 action-state frames and add 10 frames at half-second intervals after an unexpected macro error.
- Stores webhook secrets with Windows DPAPI and emits no telemetry.

## Install

1. Open the [latest GitHub Release](https://github.com/LeniLilac/expeditions-macro/releases/latest).
2. Download `ExpeditionsMacro-<version>-win-x64-setup.exe`, or the portable ZIP.
3. Verify the file against `SHA256SUMS.txt` if desired.

Extract the complete portable ZIP into its own folder, then run the root `ExpeditionsMacro.exe`. Keep the adjacent `ExpeditionsMacro` dependency folder beside the executable. Windows 10 or Windows 11 x64 is required. Release builds are self-contained; a separate .NET installation is not required.

Follow the [Expeditions Macro setup guide](https://docs.google.com/document/d/10NeDNa3BNEwPEpZj0oVQiR98_7GN67dmKS-OZwaxALM/edit?usp=sharing) for a visual walkthrough. Join the public [Expeditions Macro Discord](https://discord.gg/wE6XSVyXsN) for setup help, bug reports, plan sharing, and release announcements. Both links are available from the app sidebar.

## First-time setup

The instructions below refer to the **macro hotkey**. It defaults to **F6** and can be changed by opening the **Dashboard**, scrolling down to **Controls**, clicking the key button, and pressing a letter, number, punctuation key, numpad key, or supported function key.

Before starting a macro, assign Anime Expeditions' **Toggle Play Menu** action to one letter from A through Z. Open the Expeditions Macro **Dashboard**, scroll down to **Controls**, click **Toggle Play Menu key**, and press that same letter. This setting intentionally starts empty, so the app shows an immediate setup popup and refuses to start until it is configured. Keep it different from the macro start/stop hotkey. The same key opens Play from the lobby as well as from Victory, Defeat, or an unstarted match, avoiding the small on-screen Play button. If three verified presses do not open Play, the macro stops with a setup popup instead of continuing through an unreliable click path.

Start every Macro plan from the fully loaded lobby with Play, Areas, Units, and the game Settings panel closed. Roblox chat may be open: the macro distinguishes the filled open-chat indicator from the outlined closed-chat indicator and closes a verified open panel before Lobby preparation or match input. **Check and fix UI Scale at macro start** and **Check and fix required game settings at macro start** are independent and enabled by default. The UI Scale check directly measures the open Settings panel, accepts the inclusive 0.98–1.02 rendered range, and corrects the numeric input through the detected Misc tab when needed. The required-settings check separately verifies the Gameplay, Graphics, Units, and Misc profile toggles without changing UI Scale. When its UI Scale check is disabled, the macro performs no scale measurement or scale input; the old scene-sensitive Lobby estimate is not used. When both checks are disabled, startup sends no Settings input and only requires the stable Lobby gate. The macro visually locates and clicks the small in-game gear to open and close Settings; it does not use accessibility navigation or the red close button. The Misc tab and UI Scale field are also detected before they are clicked. The in-match lobby return similarly uses the detected door button. These fixed-scale top-bar controls are detected directly in both voice-chat layouts and do not depend on the optional microphone or headset icon. The required Graphics profile turns **Event Theme Enabled** off; this keeps the Lobby and route detectors on their retained visual theme. The standalone Debug UI Scale and game-settings actions use the same Fast pose preparation; UI Scale can run from any fully loaded game state and does not require Lobby. Controls without a required state are left untouched. The macro stops before sending route input if an enabled check cannot verify its panel, page, input, scrollbar, or required toggle.

Keep the Windows display scale at **100% on the monitor containing Roblox**. Every operation checks that monitor before sending input and checks it again if Roblox is replaced or moved during a run. At any other scale, the app stops with the detected percentage and asks you to change that monitor to 100%, restart Roblox, and retry. This is separate from Anime Expeditions' in-game UI Scale; the macro validates but never changes the Windows setting.

If a Placement Setup should load a saved Team, also assign the game's **Toggle Unit Inventory** action to a letter, then scroll down to **Controls** on the Dashboard and record the same letter as **Toggle Unit Inventory key**. Leave the setup's Team at **Don't change** when the active team should remain untouched.

Set **Toggle Cancel Unit Placement key** and **Quick Placement key** to the same physical keys used by Anime Expeditions. Both start unset and are required before a plan or test can run any Step Mode placement actions; Recording Mode routes and setups with no placement actions do not require them. For each contiguous placement batch in the Match Steps timeline, playback first taps Cancel Placement, holds Quick Placement across the batch, then releases Quick Placement and taps Cancel Placement again before selected-unit checks. Left and right Shift/Ctrl remain distinct. The compact **X** beside either binding unsets it. Keep every configured control different from the others.

Set **Change Unit Targeting**, **Upgrade Unit**, **Auto Upgrade Unit**, and **Toggle Auto Upgrade Placed Units** to matching A-Z letters in both Anime Expeditions and **Controls** on the Expeditions Macro Dashboard when a workflow uses them. These controls intentionally start empty and can be unset with their adjacent **X**. Change Unit Targeting is required only for placement steps after First; Auto Upgrade Unit is required only for Priority 1 through Priority 6. Unrelated or manual-recording-only routes do not require unused action keys. Existing **Toggle Auto Upgrade Unit** values migrate to the renamed **Toggle Auto Upgrade Placed Units** control. Every configured letter-based control, Quick Placement, Toggle Shift Lock, and the macro hotkey must use a distinct key.

The **Toggle Shift Lock key** defaults to **Left Ctrl**. If Anime Expeditions uses a different Shift Lock binding, scroll down to **Controls** on the Dashboard, click that key, and press the matching physical key. Left and right Shift/Ctrl are stored separately; letters, numbers, symbols, numpad keys, function keys, and common control keys are also supported. It can be unset with its adjacent **X**; a workflow reports the missing binding only when its camera preparation needs Shift Lock.

**Restart Roblox at Macro start** is enabled by default under **Macro > Roblox reconnect** and requires a valid private-server link. The two reconnect checkboxes save immediately when changed, so navigating away cannot restore an older value. Each time you deliberately start a Macro plan, the app closes only a verified Roblox player process, opens that private server through the installed Roblox client, and waits for a stable Lobby before doing startup checks. It runs once for that Macro operation—not between tasks or loop runs. Disable it only when you want to keep the already-open Roblox session and will start from a clean Lobby yourself.

### 1. Fast pose preparation

Fast no align is the only active preparation workflow. It standardizes Roblox to the 808 by 611 client, fully zooms out, temporarily enables Shift Lock, sets the supported top-down pitch, and preserves the route's repeatable starting yaw. **Placement Setup** organizes routes into collapsed map categories. Select an Expeditions or Story-map category to create one shared fallback, then add a child route only when that map, act, or mode needs an override. Macro Plan owns route policy such as Challenge types, difficulty, extraction, boss count, Story Act Hard mode, and defeat retries.

Legacy JSON remains readable so upgrades do not lose user data. Older Fast preset-backed plan tasks remain visible and readable; compatible Fast tasks continue through the current workflow. A task that still references the retired Camera Model workflow stops before any Roblox input and tells the user to replace it with a current route and Placement Setup.

### 2. Create a Placement Setup

1. Open **Placement Setup**. Select a category header to configure its shared fallback, or expand it and choose a child route to create an exact override. Raid acts remain direct routes because their maps differ.
2. Choose the saved Team for that category or route, or leave it at **Don't change**.
3. Choose a unit slot, then click the native 808 by 611 map screenshot to add a placement action. Auto Upgrade supports Off and Priority 1 through Priority 6 and defaults to Priority 1 for newly authored placements. Use the 50–200% visual zoom when points are close together; scroll over the map to zoom only while the outer page is at its top, and hold the middle mouse button while dragging to pan. Saved coordinates remain canonical. Points must be at least 7 client pixels apart and cannot be inside the fixed central hotbar/HUD region. Expedition setups allow each unit slot only once in the complete timeline.
4. Use **Add action** to create a placement, delay, unit reconfiguration, or single-unit upgrade action. The timeline always contains exactly one **Start Game** step. Actions above it run before Start; actions below it run after Start. Start Game can be reordered to change that boundary, but it cannot be edited, removed, or duplicated. Use explicit Delay actions wherever the runner should wait.
5. Drag any Match Step by its grip, or use its arrow controls, to reorder the complete timeline. Only Place Unit actions appear on the map; repeated unit slots receive stable labels such as `6a`, `6b`, and `6c`, and Reconfigure/Upgrade actions select those labels instead of owning duplicate coordinates. Right-clicking a map marker removes its placement action; Start Game has no map marker or remove action. The Match Steps list grows with the page and the outer workspace owns vertical scrolling.
6. Use **Match settings** to set the delay between selected-unit checks and total placement attempts from 1 through 8. One placement attempt is the default. Advanced mode can override action delays and selected-unit proof, plus the manual-recording prestart check and playback-start delay, for this setup only. Every committed change saves automatically. Wait for **All changes saved**, then use **Test playback** to prepare the Fast pose and replay the complete timeline through the ordinary production input path.

Legacy setups with separate Before Start and After Start phases migrate automatically. Their before-Start actions are placed above the required Start Game step; after-Start offsets become explicit Delay actions below it while equal-time actions retain their original order. The phase selector and `B`/`A` marker definitions are no longer part of Placement Setup authoring. Older setups containing a now-disallowed duplicate Expedition unit or central-HUD coordinate still load for editing; those invalid rows are logged and skipped at runtime.

For each contiguous placement batch, playback taps Cancel Placement, holds Quick Placement, and attempts every valid row before checking any result. It taps a unit slot only when that slot differs from the immediately preceding row, because Quick Placement preserves the current selection, then clicks the saved coordinate three times over 50 milliseconds without a drag approach. After all rows in that batch have been attempted, playback releases Quick Placement, taps Cancel Placement, and begins a separate verification pass. It clicks each saved coordinate once, parks the pointer so a hover card cannot cover the selected-unit panel, and requires that panel across two stable frames. The selected-unit panel is the only placement proof; playback does not image-detect the cyan Quick Placement title or infer success from an empty Expedition hotbar slot.

A normally confirmed placement receives the configured targeting priority and then zero through six Auto Upgrade Unit presses for Off through Priority 6 before its panel is dismissed. If proof is absent and the setup allows another total attempt, playback re-places and rechecks only that failed row; successful rows and the rest of the phase are not replayed. After the configured total attempts are exhausted, the macro logs and skips only that row without targeting, Auto Upgrade, or successful-placement progress, then continues with the next configured step. The default of one therefore performs one fast placement batch and never retries a missing row. This local placement miss does not request private-server recovery. The required game-settings check keeps Anime Expeditions' global Auto-Upgrade Placed Units option off so each successfully confirmed step remains independently controlled.

For an advanced fully manual route, turn on **Enable advanced manual recordings** under **Settings > Experimental**. On the **Recordings** page, arm a recording from the confirmed prestart screen, focus Roblox, press the macro hotkey, manually click Start and perform the run, then press the hotkey again before Victory or Defeat. Physical key presses, mouse buttons, client-relative mouse movement, and vertical or horizontal scrolling are ordered on one native Windows timeline and saved against absolute microsecond offsets. A one-pixel button or wheel difference becomes an explicit saved pointer anchor; a larger incomplete path stops recording before save. Playback pauses screen detection and pose preparation and runs its absolute clock on a dedicated timing worker rather than depending on ordinary thread-pool wakeups. Signed drift inside ±50 milliseconds meets the quality target; larger drift continues on the original absolute event schedule until the ±2,000-millisecond hard stop. A hard timing miss reports its signed event boundary and stops locally instead of restarting Roblox. Before each click or wheel event, the real pointer must be within one client pixel of the recorded position; playback never silently corrects or jumps it at that boundary. The runner resumes Victory/Defeat detection only after playback finishes.

Choose **Recording Mode** in the route header, then select the recording from the main Placement Setup controls. Both changes save automatically. Recording Mode replaces Start and ordinary placement playback while preserving every configured step; choose **Step Mode** to restore those steps immediately. **Prepare recording start** runs the same Fast pose and deterministic spawn movement used by that route before you record or test it. By default, playback requires the verified prestart screen. Advanced Match settings may skip that proof for a known Repeat Stage route and wait a configured delay before playback; Victory/Defeat detection still resumes only after the recording ends. A per-setup impossibility threshold can override the normal post-playback terminal deadline; zero retains the route default. Recordings are stored locally and may contain sensitive typed input. Their raw events never enter Deep Debug or share codes. A plan whose resolved setup uses Recording Mode cannot be exported until that setup returns to Step Mode. See [Manual input recordings](docs/MANUAL-INPUT-RECORDINGS.md).

### 3. Configure a Macro plan

1. Open **Macro**, create a plan, and choose a route directly.
2. Configure its target and route policy. Challenge tasks own enabled Challenge types and defeat retries; Expeditions own difficulty, extraction, and boss nodes before extraction; Story owns defeat retries, while Hard mode is available only for Story Act routes. Story Infinite and Mastery always run their single supported mode. Raid and Event own defeat retries. Utilities choose Gold Mine refuel, Resource Drill refuel, or both plus an interval in minutes. Bounty chooses how many always-non-viable Mythics to park from zero through four: zero favors faster overlapping work, while four favors lower reroll-Gold use.
3. Add and order tasks. The first eligible row always runs next. Remove a task when it should no longer participate in the plan.
4. Optionally choose **Add loop block** one or more times. Finite blocks may be separate or nested, and an inner block completes all of its runs for each containing-loop run. One optional **Forever** block may begin at any row but must end at the final task; it can contain finite blocks or follow earlier finite blocks. Crossing ranges are rejected. Loop runs reset only their target baselines, never lifetime victories, defeats, or runtime.
5. Use **Export code** to copy the plan, its loop/task structure, any referenced compatible Fast presets, and the exact ordinary Placement Setup overrides or category fallbacks it resolves to as a compact `EMFAST1:` Base64 string. The complete setup includes the ordered Match Steps timeline and required Start Game boundary, coordinates, delays, targeting, per-step Auto Upgrade priority, Team, route, default timing, total placement attempts, and impossibility threshold. A Bounty task includes its six required route dependencies: Spirit City Raid Act 1 and Story Infinite for all five maps. **Import code** validates and restores that dependency-complete bundle on another device. Manual recordings, local Bounty progress, retired Camera Models, app settings, webhooks, private-server links, diagnostics, and task progress are never included.
6. Plan name, task, order, and loop changes save automatically. Wait for **Saved**, then press the macro hotkey.

The scheduler never interrupts a live match. It updates saved task progress only after the current runner returns control, then reevaluates priority. A refuel Utility runs once immediately, records its next due time only after success, and lets lower-priority work continue until that interval expires. It uses the locally calibrated route timings from the Debug resource-refuel surface and requires the matching **Toggle Areas Menu key** under Dashboard Controls. Bounty is recurring: it becomes eligible again when an ordinary Challenge cooldown ends or at the next UTC day after ten claims. If reroll Gold runs out, it finishes already-active viable work and suppresses only Bounty for the rest of that Macro start; starting the Macro again permits another board reconciliation and reroll attempt. **Reset progress** clears victories, defeats, runtime, completion, and recurring-task eligibility for the plan.

### Optional Roblox restart recovery

Under **Macro > Roblox reconnect**, paste either a modern Roblox private-server share link or a legacy `privateServerLinkCode` link. The same global protected link supports the default-on one-time startup reset and default-on runtime restart recovery; it is not part of an individual plan or share code. On the first beta.31 load of settings written by an older version, startup restart, runtime restart recovery, UI Scale correction, and required game-settings correction are enabled once. Current settings schema v4 records the migration in `settings.json`, so later user changes to all four independent switches remain unchanged. A schema v2 combined startup-preparation choice migrates to both new preparation switches without overriding that saved choice.

Normal in-client lobby, disconnect, AFK, party, and blue-void recovery remains the first path. Verified runtime/session failures—including a missing or resized Roblox window, startup Lobby/settings stalls, ignored UI transitions, team-scroll alignment failure, and navigation timeout—then use private-server recovery:

1. verifies the visible Roblox window belongs to a supported Roblox player process;
2. closes only that process;
3. launches the private server through Windows' registered `roblox://` protocol;
4. waits for a new verified Roblox PID; and
5. reruns the stable-Lobby startup check, reloads the saved plan, and retries the same incomplete task without adding progress.

An automatic diagnostic is saved before each restart. Recoverable errors do not emit the terminal Discord “stopped unexpectedly” ping. Invalid plan or Placement Setup data, unsupported detector layouts, a bad Play key binding, malformed private-server links, manual-playback timing misses, and the ten-restarts-per-ten-minutes circuit remain hard stops. Without restart recovery enabled and a valid private-server link, a recoverable runtime exception must still stop because the app has no authorized rejoin destination.

This launch does not use a browser, stored account credentials, cookies, or process injection. The current Windows user must already be signed into the installed Roblox client. The private-server link grants access to that server, so it is protected with Windows DPAPI and excluded from logs and diagnostics.

## Runtime behavior

The Expeditions loop prepares the Fast pose, places units, starts the node, and watches for:

- the next Start button;
- checkpoint, Continue, or confirmation actions;
- reward-card selection;
- unplaced hotbar units that need retrying;
- extraction when the boss target is met;
- victory or defeat, followed by retry;
- lobby, disconnect, or AFK Chamber recovery.

The Challenges loop navigates the fixed three-entry selector, recognizes the rotating map, prepares the Fast pose, runs its map-specific Match Steps around the required Start Game boundary, handles Victory or Defeat, opens Play with the configured in-game key, and returns through **Change Gamemode**. When no selected Challenge is eligible, it returns through the verified game-mode selector and lets the Macro scheduler choose the next eligible task without depending on the small Play icon or hotbar layout.

Story and Raid runners navigate from Play to their configured route, optionally load a saved Team, prepare the Fast pose, run the ordered Match Steps around Start Game, monitor their terminal states, and return to Play after Victory or the final Defeat. Expedition alone owns upgrade reward cards. The Macro scheduler consumes one result at a time and then selects the highest-priority eligible task.

Villain Invasion Event routes begin from a verified Lobby because Event is not present in Play. Act 1 offers two separate Fast no align placement setups: Angle 1 uses `W 750 ms`, `D 750 ms`, `W 750 ms`, while Angle 2 extends the final `W` hold to 2100 ms for a closer view of the front path. Switching between those setups returns to Lobby before entering the next match. Act 2 keeps its deterministic initial movement, while Acts 3–4 need none. Verified Repeat Stage handoffs preserve the camera, player position, and unchanged team only when the complete route—including the Act 1 angle—matches. Event may hand off to a normal mode through Play, but a normal mode entering Event—or one Event route entering another—returns to Lobby first.

Bounty begins from verified Lobby, detects the live unhovered or highlighted Bounty Board Event row even when Beginner's Path shifts it down, and scans the horizontally arranged cards. The detected row coordinate must remain stable across fresh frames before input. Clicking a yellow reroll waits at least 200 milliseconds before evaluating the result. A reroll confirmation proves that the current card is Mythic; the macro cancels it, recognizes only the anchored `#1` through `#10` suffix, and either retains or rerolls the card according to the task's parking policy. Viable cards never consume that parking limit. Bounties 7 and 9 remain parked through an ordinary Challenge cooldown, but are rerolled after the daily Challenge limit makes their objective unavailable. Once every Mythic retainable under the current parking and Challenge policy is already active, later slots are left untouched. The explicit **You need 1000 Gold** banner stops new rerolls; 100 ordinary rerolls without a Mythic and four confirmed unchanged Mythic rerolls are bounded fallback evidence. Active objectives persist locally for the one Roblox account assumed per Windows user. At UTC midnight only the local daily claim count resets; unfinished active cards remain until the board reconciliation proves otherwise.

The ten Mythic definitions are deterministic, so Bounty combines compatible work before starting a route. Multiple objectives on the same Infinite map use one run through two waves beyond the highest required target; for example, wave-15 and wave-45 Rose Kingdom objectives complete together after wave 47 begins. Exact image recognition of the safe exit wave is preferred. If it misses, three strictly increasing later wave observations may prove that the target has already passed without letting one false reading end a run. Bounty then uses the existing verified in-match Lobby-door flow, reopens the board, claims completed cards, and continues until all ten daily claims are complete, only blocked Challenge work remains, or reroll Gold is unavailable.

Leave shift lock off before starting Fast pose preparation. Preparation centers the pointer, uses the configured Shift Lock key before the pitch drag, and uses that same key during cleanup after success, cancellation, or failure.

UI-state waits are observation-aware: a slower capture or detector pass is allowed enough samples to satisfy the same consecutive-frame proof instead of losing its confirmation window to processing time. Every wait still has a bounded hard deadline, and actions are revalidated from their owning live screen before input. A prestart UI over a textureless blue world is treated as an incomplete stage load: no pose or placement input is sent, and automatic recovery returns through Play to retry the same route.

Stopping is cooperative. The app releases right mouse and shift-lock state where applicable, cancels pending work, and leaves Roblox at the standardized client size used for detection.

Roblox discovery verifies the owning player process instead of trusting a window title alone, so unrelated windows such as a Notepad document containing “Roblox” are ignored. If Roblox recreates its window during a teleport, the app refreshes the verified handle and retries focus. Standard sizing first keeps the normal window frame; when Windows or Roblox clamps that frame above 808 by 611, the app temporarily uses a verified borderless window so the exact client geometry can still be applied. The original frame style is restored when the app exits or an explicit bounds restore is requested.

### Diagnostic screenshot capture

Open **Settings**, enter a capture name and interval under **Debug capture**, then choose **Arm capture**. Focus Roblox and press the macro hotkey to start; press it again to stop. The app uses the standard 808 by 611 client size and writes a same-name ZIP under `diagnostics/`. A completed same-name capture replaces the previous ZIP. Enable the log option when a bug report needs both screenshots and the current run log.

Automatic failure capture is enabled by default. It retains the latest 10 action-state frames from the active macro, then captures 10 more Roblox-client frames at 0.5-second intervals after an unexpected Expeditions or Challenge error. These captures use timestamped ZIP names and do not run after a normal completion or manual Stop. The app keeps the 10 newest automatic error ZIPs and removes older ones; manual diagnostic ZIPs are not affected.

**Deep debug logging** is a separate, disabled-by-default option in Settings. Enabling it requires confirming a red storage warning because every operation can produce a multi-gigabyte ZIP. While enabled, detector captures are retained, high-level automation actions capture the Roblox client immediately before and after the action, and detector score/state, generated input, and workflow events are written in the same ordered sequence. Recording and test-playback operations also produce archives, but raw manual-recording events remain excluded and playback contributes only a metadata summary. A failed or canceled action receives a final best-effort frame. The archive contains sanitized app settings, the selected plan and presets, the active detector pack, referenced Placement Setups, and a sanitized run log. Retired Camera Model and camera-shortcut payloads are not copied into new archives. A ZIP is finalized after success, cancellation, or failure and is never removed automatically. Discord webhook values, protected webhook material, Discord user IDs, and the active Windows username/profile path are excluded.

**Debug workspace** is another disabled-by-default Settings option. Enabling it reveals a Debug tab for isolated Play navigation, saved-team swapping, a configurable held-key test, resource-refuel route calibration, current-screen inspection, Roblox client standardization, and a performance benchmark. The benchmark separates capture, mode detection, root-recovery detection, and total work for the selected production path, reporting average/p95 latency and the expected rate with the normal poll interval. The held-key test captures one supported physical key and a 1–120,000 ms duration, arms the operation, and begins only when the macro hotkey is pressed. It rejects the active macro hotkey and always releases the held key when stopped. Navigation tests start from an explicitly selected lobby or post-match state and stop at verified prestart before pose preparation, placement, or Start Game. Resource-refuel calibration/tests require the matching **Toggle Areas Menu key** under **Controls** on the Dashboard; scheduled Utilities reuse those saved local timings and retry count. Live checkpoint modes can run continuously, pause before each input action, or pause after every detector observation; Previous and Next review captured frames only, while Step authorizes one live boundary and Run resumes continuous execution. Debug tools use the same exclusive operation coordinator and produce ordinary Deep Debug archives when Deep Debug logging is enabled.

Developers can replay these archives with the source-only [Deep Debug Viewer](tools/ExpeditionsMacro.DeepDebugViewer/README.md), which synchronizes captured frames with nearby detector, workflow, and input events. The viewer is not included in release artifacts.

Developers can inspect the same captures with the standalone [Detector Viewer](tools/ExpeditionsMacro.DetectorViewer/README.md). It visualizes every production detector's owned regions, live action, explicit checks, thresholds, and result, including clear limitations where an internal helper does not expose a safe standalone detail path.

## Local files and privacy

Application data is stored under `%LocalAppData%\ExpeditionsMacro`:

- `placement-models/`
- `presets/`
- `challenge-presets/`
- `story-presets/`
- `raid-presets/`
- `macro-plans/`
- `diagnostics/`
- `logs/`
- `settings.json`

See [PRIVACY.md](PRIVACY.md) for the exact network and screenshot behavior. Do not publish logs, setup files, or screenshots without reviewing them for account names, chat, notifications, or other private information.

## Build from source

Requirements:

- Windows 10/11 x64
- .NET SDK 10.0.302 or a compatible later 10.0 patch
- Git
- Inno Setup 6 only when creating the installer

```powershell
dotnet restore ExpeditionsMacro.slnx
dotnet build ExpeditionsMacro.slnx -c Debug
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Debug
```

The repository includes the detector image dataset, so the standard test command runs both unit tests and the complete golden-image regression suite. See [datasets/README.md](datasets/README.md) for its structure and capture requirements.

Build release artifacts:

```powershell
.\scripts\Generate-Icon.ps1
.\scripts\Build-Release.ps1 -Version 1.2.0
```

The release script publishes the self-contained app, creates the portable ZIP, optionally invokes Inno Setup, and writes SHA-256 checksums plus a dependency inventory. The exact compiled detector references are bundled inside both application packages and verified during packaging.

Pushing a stable `vX.Y.Z` tag runs the normal release workflow. After GitHub publishes the verified assets, the workflow normally sends a Components V2 announcement to the public Discord `#releases` channel using the encrypted `DISCORD_RELEASE_WEBHOOK_URL` repository secret. Maintainers can include `[skip discord]` in the tagged commit message to suppress that announcement. Prerelease tags such as `vX.Y.Z-beta.N`, `vX.Y.Z-alpha.N`, and `vX.Y.Z-rc.N` instead use the silent prerelease workflow, are marked as GitHub prereleases, do not become the latest stable release, and never send a Discord announcement.

CI runs fast tests, six golden-image shards, and dark/light UI snapshots as independent parallel jobs. Silent prerelease packaging also runs independently, so a beta can become downloadable before validation finishes. Any failing validation remains visible on the tagged commit and must be fixed before promoting the build to stable.

## Project layout

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for layer boundaries, [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) and [docs/TESTING.md](docs/TESTING.md) for contributor policy, [docs/GAME-BEHAVIOR.md](docs/GAME-BEHAVIOR.md) for field-confirmed navigation, [docs/CHALLENGE-MODE.md](docs/CHALLENGE-MODE.md) for Challenge behavior, and [docs/DETECTOR-PACKS.md](docs/DETECTOR-PACKS.md) for the bundled reference format.

## License

Source code is available under the [PolyForm Noncommercial License 1.0.0](LICENSE.md). Commercial use is not granted. Third-party game content and marks remain owned by their respective owners; see [NOTICE.md](NOTICE.md).
