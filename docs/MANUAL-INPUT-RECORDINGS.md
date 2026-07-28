# Manual input recordings

Manual recordings are an opt-in advanced Fast pose workflow for
routes where replaying one complete manual input sequence is faster than
detector-driven placement.

Turn on **Enable advanced manual recordings** under
**Settings > Experimental** to expose the **Recordings** page and
**Recording Mode** under Placement Setup.

## User workflow

1. Load the intended route and stop on its confirmed prestart screen.
2. If the route normally applies deterministic spawn movement, choose
   **Prepare recording start** in Placement Setup first. This runs the
   production Fast pose preparation and that route's movement.
3. Open **Recordings**, enter a name, and choose **Arm recording**.
4. Focus Roblox and press the global macro hotkey. Record the complete
   sequence, including the click on **Start Game**.
5. Press the macro hotkey again before Victory or Defeat appears.
6. In that route's Placement Setup, choose **Recording Mode**, then select
   the saved recording from the main controls. Both changes save
   automatically. Every ordinary placement step remains stored.
7. Select the recording and choose **Arm playback**. Focus Roblox, then
   press the global macro hotkey to start the test from the same prestart
   state. Press the hotkey again to stop playback. The Placement Setup
   playback action uses the same armed-hotkey boundary. Choose
   **Step Mode** to restore the preserved ordinary steps.

At runtime, the mode runner still verifies prestart and performs its
normal route, team, Fast pose, and deterministic positioning preparation.
It then gives exclusive Roblox input ownership to the selected recording
instead of clicking Start or running placement steps. Screen detection
and pose preparation remain paused throughout playback. When playback ends,
the runner resumes terminal-state detection, handles Victory or Defeat,
and uses the normal Repeat Stage or scheduler handoff.

Each setup may also override its terminal impossibility threshold from 1
through 180 minutes. Zero keeps the route's normal default. This setting
changes only the bounded wait after the recording; it does not extend or
alter the recorded input timeline.

## Supported input and timing

Schema 1 stores:

- the canonical 808 by 611 Roblox client size;
- the initial client-relative pointer position;
- a UTC creation time and total duration;
- ordered event offsets in integer microseconds;
- physical keyboard virtual keys, scan codes, and extended-key identity;
- client-relative mouse movement and all mouse-button transitions;
- vertical and horizontal wheel deltas.

Windows can deliver a low-level button callback after newer high-rate
movement callbacks even though the button's native timestamp is earlier.
The recorder therefore buffers keyboard and mouse observations together,
orders them by the wrap-safe native Windows timestamp, and uses callback
sequence only to keep events from the same native millisecond stable. It
then makes any one-pixel button or wheel gap an explicit saved mouse move
at the action coordinate. A larger unreconciled path stops capture with a
rerecord message instead of saving a sequence that would fail during
playback.

Playback schedules every event against its absolute microsecond offset
from one monotonic `Stopwatch`; it never accumulates a chain of relative
delays. A dedicated timing worker owns the complete timed loop, so ordinary
thread-pool continuation delays cannot repeatedly interrupt the recording.
Long waits end before the target and use a bounded final spin. Live
foreground and client-bound checks use direct Win32 probes inside that
worker rather than synchronous diagnostic wrappers. The player checks
timing immediately before and after each injected event.
The difference between the total elapsed playback clock and that event's
recorded absolute offset must remain within +/- 50 milliseconds. The
inclusive -50 and +50 millisecond boundaries are accepted; an earlier or
later offset stops playback with the event kind, boundary, and measured
drift. A timing miss is local to the recording and never authorizes a
Roblox restart. This per-event absolute-timeline check makes timing drift a
visible safe failure instead of silently accumulating, slowing, or
compressing the route.

The recorder excludes only the global macro start/stop hotkey. Any
physical game-action key pressed while recording, including Auto Upgrade
Unit, is part of the saved sequence and is replayed at the same moments.
Changing or unsetting an Expeditions Macro control binding later does not
rewrite an existing recording.

## Input and cleanup boundary

`IManualInputRecorder` and `IManualInputPlayback` remain behind the same
exclusive operation coordinator as every other Roblox workflow. The
Windows implementations:

- verify and focus the owning Roblox player process;
- standardize and continuously require the 808 by 611 client;
- require Roblox to remain foreground with stationary screen bounds;
- observe only physical low-level keyboard and mouse input;
- ignore injected and lower-integrity-injected events;
- order keyboard and mouse callbacks on one native Windows timeline;
- persist one-pixel action anchors and reject larger incomplete pointer
  paths before the recording can be saved;
- exclude the macro hotkey on both key-down and key-up;
- reject pointer input outside the Roblox client;
- establish the saved initial pointer through acknowledged motion before
  starting the playback clock;
- require the real pointer to be within one client pixel of the recorded
  client coordinate before a mouse button or wheel event, without
  correcting or jumping the pointer at that boundary;
- track every key and mouse button pressed during playback.

Success, cancellation, focus loss, invalid geometry, timing failure, and
`SendInput` failure all execute the same guaranteed release path. Hook
removal also runs in `finally`. An empty recording is not saved, and a
new recording name never silently overwrites an existing recording.

## Storage, sharing, and diagnostics

Recordings are stored atomically under:

```text
%LocalAppData%\ExpeditionsMacro\manual-recordings\<id>\recording.json
```

IDs are validated before path construction. Corrupt entries are skipped
while listing so one damaged file cannot hide the others.

Manual recordings are device-local and excluded from every Fast share
schema. Export stops if any Placement Setup resolved by the selected plan
uses **Recording Mode**. Return that route to **Step Mode** before
exporting; the preserved ordinary placement steps become the shareable
route again. Import rejects both raw recording payloads and recording
references instead of silently collecting replayable input. Schema 1
recording-free bundles remain readable, while schema 2 adds referenced
Fast no-align presets and their complete ordinary placement dependencies.

A recording contains replayable raw keyboard, mouse movement, click,
wheel, and timing data. Do not type chat messages, credentials,
private-server codes, or other sensitive text while recording.

Raw recorded inputs remain excluded from Deep Debug archives. Deep Debug
records only one metadata summary after playback, including the recording
ID, event count, maximum absolute timing drift, and success state. The
timing-critical loop does not emit per-event or repeated client-bound
diagnostics. Share bundles never collect recordings, screenshots, app
settings, webhooks, private-server links, diagnostics, or Windows profile
paths.

Turning off **Enable advanced manual recordings** hides the Recordings
workspace but does not erase saved route assignments. A route already in
Recording Mode remains visibly identified in Placement Setup, with its
recording picker disabled and guidance to re-enable the experimental
feature. A plan cannot start that route until the feature is re-enabled
or the route returns to Step Mode.

A recording cannot be deleted while any saved Placement Setup route
references it. Return every referencing route to **Step Mode** before
deleting the recording.
