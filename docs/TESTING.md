# Testing and validation

Use .NET SDK 10.0.302 or a compatible later .NET 10 patch.

## Standard checks

```powershell
./scripts/Test-RepositoryPolicy.ps1
dotnet restore ExpeditionsMacro.slnx --locked-mode
dotnet build ExpeditionsMacro.slnx -c Release --no-restore
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Release --no-build
dotnet test tools/ExpeditionsMacro.DeepDebugViewer.Tests/ExpeditionsMacro.DeepDebugViewer.Tests.csproj -c Release --no-build
git diff --check
```

Start with focused tests while iterating, then expand according to risk. Do not report success from compilation alone.

## Risk-based scope

- Pure documentation or policy text: repository policy check and `git diff --check`.
- Core models/persistence: focused unit tests plus the full non-golden suite.
- Input, capture, coordinator, or cancellation: focused Windows/workflow tests and the full non-golden suite. Manual-input changes must cover malformed schemas, every supported event type, both wheel directions, ignored hotkey transitions, initial pointer state, delayed native hook callbacks, stable same-millisecond ordering, wrap-safe native timestamps, mixed keyboard/mouse ordering, explicit one-pixel action anchors, incomplete-path rejection before save, the allowed one-pixel client-coordinate rounding variance, rejection beyond one pixel without corrective movement, the signed +/- 50-millisecond quality target, continuation on unchanged absolute deadlines below the signed +/- 2,000-millisecond hard stop, rejection at that hard stop before and after send, a dedicated non-thread-pool timing worker, direct uninstrumented live client-bound probes during the timed loop, non-restart classification for a timing miss, focus/geometry loss, cancellation, and held-input cleanup.
- UI/XAML: relevant view-model/page tests, a Release build, and `scripts/Render-UiSnapshots.ps1 -Configuration Release`; inspect all 74 expected snapshots in both themes, including configured/unset Quick Placement controls and their compact X, empty/nested loop plans, the loop-settings and add-task popups, Story Act/Mastery/Infinite option visibility, ready/armed/running Recordings states, the bounded Dashboard run log in wide and compact layouts, the compact stacked Dashboard at the 960 by 640 minimum window, the Placement Setup Recording Mode / Step Mode selector and its stacked narrow header, collision-free dense placement-marker labels, the placement-attempt and between-unit-check fields, Settings > Experimental, the dedicated read-only Settings Diagnostics guidance, the reachable placement-step fields at the minimum window and a 1400 by 1080 medium window, the nested placement-list-to-workspace wheel handoff at both compact scroll boundaries, and the collapsed app-navigation/Placement-catalog rails.
- Placement playback attempts: prove the default-unset physical Quick Placement and Cancel Placement bindings, left/right modifier identity, compact X clearing, conflict rejection, persistence, and plan-start resolution across direct and legacy setup dependencies. Recording Mode with preserved steps and an empty Step Mode setup must remain exempt. For each phase batch, prove `Cancel down/up -> Quick Placement down -> placement rows -> Quick Placement up -> Cancel down/up`, with exactly three complete clicks over 50 milliseconds per row and no timed mouse approach or cyan-title detector. Consecutive rows for the same unit must reuse its Quick Placement selection; a changed unit must tap its slot once. Selected-unit proof, targeting, Auto Upgrade, dismissal, callbacks, and between-check delays must begin only after the complete placement batch and trailing Cancel input. Equal-due After Start rows must batch together while later custom offsets remain separate. The default total-attempt count is one. Higher counts may re-place and recheck only an unproved row; confirmed rows and the complete phase must never replay. Exhausted proof must log and skip only that row, continue the next check, send no targeting or Auto Upgrade input, omit the successful-placement callback, and avoid runtime/session recovery. Quick Placement and atomic mouse-button release must survive cancellation and exceptions. Authoring, persistence, share, and legacy-load tests must cover the inclusive 1-8 attempt range. Newly authored points still default Auto Upgrade to Priority 1 while missing legacy `auto_upgrade` values remain Off.
- Placement authoring and phase editing: cover both Before Start / After Start directions, boundary ordering, unchanged step fields, no-op and invalid requests, autosave persistence, phase-local drag/move behavior, content-sized marker labels, dense collision-free marker lanes, map-edge bounds, and both themed Placement Setup views. Expedition authoring must reject a unit slot already used in either phase; every mode must reject the fixed central hotbar/HUD rectangle. Legacy duplicate and HUD-conflicting rows must remain loadable/editable, persist until the user changes them, and be skipped individually at runtime rather than invalidating the complete setup. Runtime tests must prove that an empty or flickering Expedition hotbar slot cannot replace selected-unit proof or invoke the successful-placement callback.
- Autosave lifecycle: use deterministic blocked writes to prove a flush drains edits queued during an active save, plan renames retain their original replacement ancestry, setup shutdown waits for active saves and deletes, stale completions cannot replace newer status, failures remain retryable, and switching suppresses load-generated edits. Recording Mode / Step Mode changes must persist only the recording assignment while preserving every ordinary placement step and route setting.
- Observation-aware UI waits: cover every owned startup, recovery, Lobby, Match Lobby, Settings, Play, Stage, Challenge, Event, Expedition, Team, Refuel, and placement path on a deliberately slow detector clock. Prove an already-observed positive sample is retained, the required fresh confirmations can complete, hard deadlines remain bounded, input-attempt/click/drag caps are unchanged, and no manifest/static action fallback can authorize input without live owner-state proof.
- Fast-only preparation: prove new settings and authoring expose only Fast preparation, zoom/pitch preparation sends no horizontal yaw input, and public-beta camera-model presets remain deserialize-visible but stop with migration guidance before Roblox discovery or input.
- Detector regions, matching, thresholds, ordering, preprocessing, or action placement: focused detector tests and the complete cross-state golden corpus.
- Match-to-Lobby navigation: both fixed top-bar door offsets, microphone/headset independence, stable same-geometry proof, ignored-click redetection, cancellation before physical input, verified exit confirmation, and final stable Lobby proof.
- Story/Raid option navigation: retained selected-row positives, every wrong option row, Normal/Hard label ownership, delayed target selection, unchanged remembered selection, stable action coordinates, one click per configured option, and safe stop before Select Stage.
- Dataset changes: privacy review every image, update counts/docs, then run the complete golden corpus.
- Release packaging: `scripts/Build-Release.ps1` and `scripts/Verify-Release.ps1` in addition to the above. Verification must enforce the root-apphost plus nested-dependency layout, require every hash-verified detector payload declared by the bundled manifest, extract under a temporary path containing spaces, launch the root executable from an unrelated working directory, and require a successful packaged UI snapshot run.

## Useful targeted commands

```powershell
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Release --filter "Category!=Golden"
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Release --filter "FullyQualifiedName~DetectorPackGoldenTests"
./scripts/Render-UiSnapshots.ps1 -Configuration Release
dotnet run --project tools/ExpeditionsMacro.DatasetBuilder -- --build datasets/anime-expeditions/expeditions detector-packs <pack-version>
```

The golden corpus is intentionally slower and is sharded in GitHub Actions. Local focused checks do not replace the required full corpus after detector changes.

## Regression evidence

- Reproduce a reported failure before changing code.
- For deep-debug archives, generate timestamped contact sheets with `scripts/New-DiagnosticContactSheet.ps1` before examining singleton frames.
- Encode the confirmed state/action sequence as a policy, workflow, or detector test.
- Every detector fix requires a representative privacy-reviewed 808 by 611 fixture plus positive and relevant negative coverage.
- Startup settings work must cover the Lobby gate, the opening-animation negative, UI Scale 0.80/1.00/1.20, device-dependent rendered-scale feedback, all required pages, both Units scrollbar boundaries, both fixed Settings-gear offsets in normal and high-contrast outline forms plus their selected forms, ignored gear actions, no voice-control dependency, and unknown-control safe stops.
- Event navigation work must preserve Act-selector precedence over Event Home, including the shifted full-width selector rail and opaque subtitle, while proving that Event Home still accepts its selected Villain Invasion card plus live Event Gamemode action before decorative header artwork finishes and when the lower button border renders one pixel thinner.
- Runtime-recovery changes must prove typed restart-candidate classification, diagnostics before restart, stable Lobby recovery, unchanged incomplete-task progress, operation-scoped startup preflight, and circuit-breaker exhaustion. Do not make generic `InvalidOperationException` recoverable.
- Fast share-code tests must round-trip the plan and loops, referenced Fast presets, exact route overrides or resolved category fallbacks, unit slots and coordinates, Teams, placement phases and delays, targeting, per-step Auto Upgrade priority, default timing, route identity, and impossibility thresholds while proving task progress, manual recordings, retired camera-model data, app settings, and secrets are absent.
- Macro-plan persistence tests must prove a legacy task with `"enabled": false` loads as active and that normalized plan/share JSON omits the retired field.
- A plan whose resolved Placement Setup references a manual input recording must fail share export clearly. Tests must reject raw recording payloads and recording references in decoded bundles and prove no local recording can be silently collected or imported.
- When manual user input is paired with passive capture, label the input as user reported rather than claiming it came from the event log.
