# AGENTS.md

This file applies to the entire repository. It is the working agreement for coding agents and contributors making changes to Expeditions Macro.

## Project purpose and boundaries

Expeditions Macro is a Windows-only .NET/WPF utility that automates repeatable Anime Expeditions runs in Roblox through screen capture and ordinary Windows input.

- Do not inject into Roblox, read or modify Roblox process memory, hook the game, or add anti-cheat bypasses.
- Keep the project noncommercial and preserve the notices and license terms.
- Treat Roblox, Anime Expeditions, Discord, and GitHub as external systems that can change independently.
- Prefer deterministic, inspectable automation over hidden heuristics or broad retries.

## Repository map

- `src/ExpeditionsMacro.Core`: immutable geometry, imaging, models, persistence, and workflow contracts. It must not depend on WPF, Win32, or OpenCV.
- `src/ExpeditionsMacro.Windows`: Win32 window discovery/sizing, GDI capture, F6, DPAPI, keyboard input, Roblox-compatible clicks, cursor parking, and relative right-drag input.
- `src/ExpeditionsMacro.Vision`: image normalization, adaptive matching, specialized detectors, compiled detector packs, node hues, difficulty selection, and hotbar checks.
- `src/ExpeditionsMacro.Automation`: camera calibration/alignment, placement workflows, the Expeditions state machine, recovery, Discord notifications, and detector-pack updates.
- `src/ExpeditionsMacro.App`: the WPF shell, pages, themes, dialogs, and the exclusive input-owner coordinator.
- `tools/ExpeditionsMacro.DatasetBuilder`: builds a compact, hashed detector pack from the checked-in capture corpus.
- `tests/ExpeditionsMacro.Tests`: unit, integration, Windows-input, UI snapshot, detector, and golden-image regression tests.
- `datasets/anime-expeditions/expeditions`: reviewed 808 by 611 Roblox client captures used as detector fixtures.
- `detector-packs`: versioned compiled detector packs shipped with the app.
- `scripts`: build, verification, release, icon, snapshot, and Discord announcement tooling.
- `docs`: architecture, detector-pack, release, and product documentation.

Read `README.md`, `CONTRIBUTING.md`, `docs/ARCHITECTURE.md`, `docs/DETECTOR-PACKS.md`, and `datasets/README.md` before changing the corresponding subsystem.

## Non-negotiable runtime invariants

- The canonical Roblox client area is 808 by 611 pixels. Store all capture regions, click targets, camera regions, and placement coordinates relative to the client, never the desktop or outer window.
- Workflows standardize Roblox to the canonical client size and leave it there; do not restore old outer bounds after app startup, setup, capture, placement, or macro runs.
- Always release held mouse buttons, keyboard state, and temporary shift lock on success, cancellation, and failure.
- Keep automation cooperative and cancellation-aware. Pass cancellation tokens through delays, polling loops, input, downloads, and long image operations where supported.
- Only one workflow may own Roblox input. Route UI actions through the coordinator rather than starting competing workers.
- Focus and revalidate the Roblox window immediately before capture or input when a delay or external transition could make the handle or bounds stale.
- Roblox may ignore a direct cursor teleport. Use the shared click path, including the small relative motion that makes Roblox acknowledge the position, and park the cursor through the same registered-motion path after clicks.
- Camera yaw uses extended scan-code Left/Right-arrow pulses for coarse movement and relative mouse motion while right mouse is held for fine refinement. Never use visible absolute cursor movement for camera rotation, and always release either input path on cancellation.

## Vision and detector rules

- Build detectors from stable UI structure. Do not depend on avatars, player counts, map names, rotating artwork, reward icons, lighting, moving units, hover state, or other account/session-specific content.
- Prefer several independent signals (geometry, color, edge structure, repeated layout) over one large screenshot template.
- Keep action coordinates tied to the detected structure. If a detector tolerates scale or translation, map its click through the same match rather than using a stale fixed point.
- Do not lower a threshold merely to make a reported screenshot pass. Identify the changing pixels, remove them from the signal, and add a negative-regression check.
- State priority matters. A new detector must not steal frames from earlier states such as disconnect, lobby, reward, victory, or defeat.
- Every detector fix needs a representative 808 by 611 fixture and a golden test proving both classification and action placement.
- Run the full cross-state corpus after changing matching, state ordering, thresholds, adaptive scales, regions, or preprocessing. A focused passing test is not sufficient.

### Dataset hygiene

- Store only the Roblox client area as PNG; exclude title bars, desktop chrome, other applications, notifications, chat, account names, tokens, and webhook URLs.
- Review every contributed screenshot before committing it. Crop or redact private material before it enters Git history.
- Preserve original pixels when possible. Document any crop, resize, or synthetic transformation used to create a fixture.
- Add variation across users, maps, lighting, animation, hover states, and UI revisions rather than many near-identical consecutive frames.
- Update hard-coded corpus counts and `datasets/README.md` when fixtures change.
- Rebuild and version the compiled detector pack only when its manifest or reference payload changes. Specialized app-detector changes alone do not require a pack-version bump.

## UI conventions

- Keep the WPF UI clean in both dark and light themes. Use shared resources and existing controls before adding page-local styles.
- Place explanatory copy beneath its section heading and above controls. Avoid side-by-side prose that compresses inputs or causes overlap.
- Inputs and buttons must not clip text at supported Windows scaling. Check long labels, disabled states, and narrow layouts.
- Keep user-facing model and preset selectors limited to friendly names; do not expose record `ToString()` output or schema internals.
- Do not block the UI thread with capture, network, image processing, polling, or release work.
- When UI changes, render and inspect both snapshot themes with `scripts/Render-UiSnapshots.ps1`.

## Persistence, networking, and secrets

- Application data belongs under `%LocalAppData%\ExpeditionsMacro`; do not write user models or settings into the installation directory.
- Webhook URLs are secrets. Keep them protected by DPAPI and redact them from exceptions, logs, tests, screenshots, and release output.
- Never commit real bot tokens, webhook URLs, GitHub tokens, logs, local models, or private screenshots. Test credentials must be obviously fake.
- Network behavior must remain documented in `PRIVACY.md`. Avoid adding telemetry.
- Validate downloaded detector-pack paths, hashes, sizes, versions, and compatibility before installation, and retain rollback behavior.
- Discord payloads use Components V2. Restrict `allowed_mentions` explicitly and do not add an accent color unless the product requirement changes.

## Coding style

- Follow `.editorconfig`: UTF-8, CRLF, final newline, four spaces for C#/XAML and two spaces for JSON/YAML/project files.
- Use nullable annotations, file-scoped namespaces, immutable records/models, and explicit validation at persistence or external-system boundaries.
- Preserve layer direction. Core stays platform-independent; UI logic stays out of automation and vision; Win32 calls stay in the Windows project.
- Keep comments focused on the reason for a non-obvious constraint, especially Roblox input quirks and detector gates.
- Avoid compatibility shims for unreleased prototype formats unless a current public release requires migration.
- Do not hand-edit generated `bin`, `obj`, `artifacts`, installer output, dependency inventory, or checksum files.

## Build and test

Use .NET SDK 10.0.302 or a compatible later .NET 10 patch.

```powershell
dotnet restore ExpeditionsMacro.slnx --locked-mode
dotnet build ExpeditionsMacro.slnx -c Release --no-restore
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Release --no-build
```

Useful targeted commands:

```powershell
dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Release --filter "FullyQualifiedName~DetectorPackGoldenTests"
./scripts/Render-UiSnapshots.ps1 -Configuration Release
dotnet run --project tools/ExpeditionsMacro.DatasetBuilder -- --build datasets/anime-expeditions/expeditions detector-packs <pack-version>
```

The complete golden-image suite is intentionally slower than ordinary unit tests and can take several minutes on hosted Windows runners.

## Change workflow

1. Inspect `git status` and preserve unrelated user changes.
2. Reproduce a bug with the supplied log, screenshot, video, or fixture before changing code.
3. Make the smallest cross-layer change that fixes the root cause while preserving the invariants above.
4. Add focused regression coverage and then run the relevant broader suite.
5. Review `git diff --check`, the full staged diff, and generated artifacts before committing.
6. Stage explicit paths when the worktree contains anything outside the requested scope.
7. Document user-visible behavior in the changelog, release notes, README, privacy policy, or detector documentation as applicable.

## Release checklist

- Choose the next semantic app version and update `VersionPrefix` in `Directory.Build.props`.
- Move relevant `CHANGELOG.md` entries from Unreleased into a dated version section and update comparison links.
- Add `docs/release-notes/<version>.md` using the established release-note format.
- Run the full Release test suite, UI snapshot verification when applicable, and `scripts/Build-Release.ps1` plus `scripts/Verify-Release.ps1` when preparing artifacts locally.
- Commit and push the release-ready source before creating the signed/annotated version tag used by the workflow.
- Confirm the GitHub Release has the installer, portable ZIP, detector pack, checksums, and dependency inventory.
- Confirm CI, the tag-triggered Release workflow, and the Discord release announcement succeed.
- Never retag or overwrite an existing public release. Fix forward with a new version.

## Definition of done

A change is complete only when the reported scenario passes, relevant negative cases still fail safely, the working tree contains no accidental files, tests are green in proportion to risk, user-facing behavior is documented, and any requested commit/release is verifiably present on GitHub.
