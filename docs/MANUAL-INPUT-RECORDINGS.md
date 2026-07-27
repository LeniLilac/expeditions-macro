# Manual input recordings

Manual recordings are an opt-in advanced Fast no-align workflow for
routes where replaying one complete manual input sequence is faster than
detector-driven placement.

Enable **Manual input recordings** under Settings to expose the
**Recordings** page and the recording selector under Placement Setup.

## User workflow

1. Load the intended route and stop on its confirmed prestart screen.
2. If the route normally applies deterministic spawn movement, choose
   **Prepare recording start** in Placement Setup first. This runs the
   production Fast no-align preparation and that route's movement.
3. Open **Recordings**, enter a name, and choose **Arm recording**.
4. Focus Roblox and press the global macro hotkey. Record the complete
   sequence, including the click on **Start Game**.
5. Press the macro hotkey again before Victory or Defeat appears.
6. Select the saved recording in that route's Placement Setup and save.
7. Select the recording and choose **Arm playback**. Focus Roblox, then
   press the global macro hotkey to start the test from the same prestart
   state. Press the hotkey again to stop playback. The Placement Setup
   playback action uses the same armed-hotkey boundary.

At runtime, the mode runner still verifies prestart and performs its
normal route, team, camera, and deterministic positioning preparation.
It then gives exclusive Roblox input ownership to the selected recording
instead of clicking Start or running placement steps. Screen detection
and camera work remain paused throughout playback. When playback ends,
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

Playback schedules every event against its absolute microsecond offset
from one monotonic `Stopwatch`; it never accumulates a chain of relative
delays. Long waits end before the target and use a bounded final spin.
The player checks timing immediately before and after each injected event
and stops if the actual event time differs from its recording by more
than 10 milliseconds. This makes timing drift a visible safe failure
instead of silently slowing or compressing the route.

## Input and cleanup boundary

`IManualInputRecorder` and `IManualInputPlayback` remain behind the same
exclusive operation coordinator as every other Roblox workflow. The
Windows implementations:

- verify and focus the owning Roblox player process;
- standardize and continuously require the 808 by 611 client;
- require Roblox to remain foreground with stationary screen bounds;
- observe only physical low-level keyboard and mouse input;
- ignore injected and lower-integrity-injected events;
- exclude the macro hotkey on both key-down and key-up;
- reject pointer input outside the Roblox client;
- establish the saved initial pointer through acknowledged motion before
  starting the playback clock;
- refuse a mouse button or wheel event when the real pointer is not at
  the recorded client coordinate;
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
has **Use manual recording** enabled. Turn off that option before
exporting; the ordinary placement steps remain saved and become the
shareable route again. Import rejects both raw recording payloads and
recording references instead of silently collecting replayable input.
Schema 1 recording-free bundles remain readable, while schema 2 adds
referenced Fast no-align presets and their complete ordinary placement
dependencies.

A recording contains replayable raw keyboard, mouse movement, click,
wheel, and timing data. Do not type chat messages, credentials,
private-server codes, or other sensitive text while recording.

Raw recorded inputs remain excluded from Deep Debug archives. Deep Debug
records only one metadata summary after playback, including the recording
ID, event count, maximum timing drift, and success state. Share bundles
never collect recordings, screenshots, app settings, webhooks,
private-server links, diagnostics, or Windows profile paths.

Turning off Advanced manual recordings hides its authoring surfaces but
does not erase saved route assignments. A plan cannot start a route that
references a manual recording until the feature is re-enabled or the
recording assignment is removed.

A recording cannot be deleted while any saved Placement Setup route
references it. Turn off **Use manual recording** on each referencing
route before deleting the recording.
