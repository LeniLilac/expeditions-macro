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
- Input, capture, coordinator, or cancellation: focused Windows/workflow tests and the full non-golden suite. Manual-input changes must cover malformed schemas, every supported event type, both wheel directions, ignored hotkey transitions, initial pointer state, the allowed one-pixel client-coordinate rounding variance, rejection beyond one pixel without corrective movement, pre/post-send timing drift, focus/geometry loss, cancellation, and held-input cleanup.
- UI/XAML: relevant view-model/page tests, a Release build, and `scripts/Render-UiSnapshots.ps1 -Configuration Release`; inspect all 66 expected snapshots in both themes, including empty/nested loop plans, the loop-settings and add-task popups, Story Act/Mastery/Infinite option visibility, ready/armed/running Recordings states, the bounded Dashboard run log in wide and compact layouts, the compact stacked Dashboard at the 960 by 640 minimum window, Placement Setup recording settings, the controls plus scrolled placement-step rail at the minimum window, and the collapsed app-navigation/Placement-catalog rails.
- Placement phase editing: cover both Before Start / After Start directions, boundary ordering, unchanged step fields, no-op and invalid requests, autosave persistence, phase-local drag/move behavior, and both themed Placement Setup views.
- Autosave lifecycle: use deterministic blocked writes to prove a flush drains edits queued during an active save, plan renames retain their original replacement ancestry, setup shutdown waits for active saves and deletes, stale completions cannot replace newer status, failures remain retryable, and switching suppresses load-generated edits.
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
- Event navigation work must preserve Act-selector precedence over Event Home, including the shifted full-width selector rail and opaque subtitle, while proving that Event Home still accepts its selected Villain Invasion card plus live Event Gamemode action before decorative header artwork finishes.
- Runtime-recovery changes must prove typed restart-candidate classification, diagnostics before restart, stable Lobby recovery, unchanged incomplete-task progress, operation-scoped startup preflight, and circuit-breaker exhaustion. Do not make generic `InvalidOperationException` recoverable.
- Fast share-code tests must round-trip the plan and loops, referenced Fast presets, exact route overrides or resolved category fallbacks, unit slots and coordinates, Teams, placement phases and delays, targeting, per-step Auto Upgrade priority, default timing, route identity, and impossibility thresholds while proving task progress, manual recordings, retired camera-model data, app settings, and secrets are absent.
- Macro-plan persistence tests must prove a legacy task with `"enabled": false` loads as active and that normalized plan/share JSON omits the retired field.
- A plan whose resolved Placement Setup references a manual input recording must fail share export clearly. Tests must reject raw recording payloads and recording references in decoded bundles and prove no local recording can be silently collected or imported.
- When manual user input is paired with passive capture, label the input as user reported rather than claiming it came from the event log.
