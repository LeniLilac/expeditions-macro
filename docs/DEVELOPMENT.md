# Development guide

This guide holds the detailed engineering conventions intentionally kept out of the root `AGENTS.md`.

## Repository map

- `src/ExpeditionsMacro.Core`: immutable geometry, imaging, models, persistence, and workflow contracts. It must not depend on WPF, Win32, or OpenCV.
- `src/ExpeditionsMacro.Windows`: Win32 discovery/sizing, Windows Graphics Capture, HDR-to-SDR conversion, global hotkeys, DPAPI, and Roblox-compatible input.
- `src/ExpeditionsMacro.Vision`: normalization, matching, specialized detectors, detector packs, node hues, and hotbar checks.
- `src/ExpeditionsMacro.Automation`: camera and placement workflows, mode runners, scheduling, recovery, diagnostics, Discord, and updates.
- `src/ExpeditionsMacro.App`: WPF shell, pages, themes, dialogs, and exclusive operation coordination.
- `tools/ExpeditionsMacro.DatasetBuilder`: compiles reviewed captures into detector packs.
- `tools/ExpeditionsMacro.DeepDebugViewer`: local viewer for deep-debug archives.
- `tests/ExpeditionsMacro.Tests`: application, Windows-input, detector, workflow, and golden-image tests.
- `datasets`: reviewed detector fixtures. See `datasets/README.md` before editing.
- `detector-packs`: versioned compiled packs shipped with the app.
- `eng`: machine-readable repository-policy configuration.
- `scripts`: build, policy, verification, release, snapshot, and diagnostic tooling.

## Dependency direction

The allowed product graph is:

```text
Core <- Vision ----\
  ^                 \
  +---- Windows -----> Automation -> App
  +-------------------------------> App
```

- Core is portable and owns contracts, not platform behavior.
- Vision may consume Core only.
- Windows may consume Core only.
- Automation may consume Core, Vision, and Windows.
- App composes all product layers; lower layers never reference it.
- Tools and tests have explicit project-reference allowlists in `eng/repository-policy.json`.

`scripts/Test-RepositoryPolicy.ps1` checks every project reference against this graph and requires policy review when a project is added.

## Source organization and size budgets

The repository uses enforceable line budgets as a structural warning, not as a substitute for design judgment:

- Production C# and XAML under `src` and non-test `tools`: 500 lines per file.
- Test C# and XAML under `tests` or a `*.Tests` tool: 800 lines per file.
- PowerShell under `scripts`: 500 lines per file.
- Root `AGENTS.md`: 120 lines so it remains a useful entry point.

Existing files above a budget are recorded with an exact ceiling and rationale in `eng/repository-policy.json`. The policy check fails if one grows, if new unlisted debt appears, or if a reduced file leaves a stale higher ceiling. Lower the recorded ceiling whenever a debt file shrinks; remove its entry once it meets the normal limit.

When a file approaches its budget:

- Split by ownership, state, lifecycle, or external boundary—not arbitrary line ranges.
- Keep orchestration readable by extracting state-specific services and pure policies.
- Prefer small domain-named types over generic `Helper`, `Manager`, or `Utils` dumping grounds.
- Avoid broad barrel/facade modules that hide dependencies.
- Do not use partial classes, generated files, compressed formatting, or one-line methods to evade the budget.
- If a rare declarative file truly must exceed a limit, add one exact-path exception with a concrete rationale. Never add wildcard exceptions.

## Runtime and input conventions

- Store every region, click target, camera region, and placement coordinate relative to the Roblox client.
- Standardize the client before detector-dependent work and keep it standardized afterward.
- Pass cancellation tokens through delays, polling, input, capture, downloads, and long processing where supported.
- Release every held input in `finally` or an equivalent guaranteed cleanup path.
- Focus and revalidate the window after transitions; rediscover stale handles rather than continuing with stale geometry.
- Use shared input primitives. Roblox requires acknowledged relative movement before clicks, hardware scan codes for some keys, and relative right-drag motion for fine camera control.
- Intentional waits over ten minutes follow the keepalive behavior documented in `docs/GAME-BEHAVIOR.md`.

## Vision and datasets

- Build detectors from stable structure, using independent geometry, color, edge, and repeated-layout evidence.
- Do not depend on avatars, player counts, rotating artwork, map names, reward art, hover state, moving units, or session lighting.
- Tie actions to the same live structure that was detected; translated/scaled detections must translate/scale their action.
- Do not lower a threshold just to pass one screenshot. Identify unstable pixels, replace the signal, and add a negative regression.
- Preserve state priority so new detectors do not steal disconnect, lobby, reward, terminal, or other earlier states.
- Store only reviewed 808 by 611 Roblox client PNGs. Exclude desktop chrome, other applications, notifications, chats, account names, tokens, and webhooks.
- Update corpus counts and documentation when fixtures change. Rebuild a detector pack only when its manifest/reference payload changes.

## UI conventions

- Reuse shared WPF resources and controls. Keep dark and light themes equally usable.
- Put explanatory copy below headings and above controls; do not squeeze prose beside inputs.
- Prevent clipping at supported Windows scaling and test long labels, disabled states, and narrow layouts.
- Show friendly model/preset names rather than record `ToString()` output or schema details.
- Keep capture, image processing, network, polling, and release work off the UI thread.
- Render and inspect both themes after UI changes.

## Persistence, networking, and secrets

- User data belongs under `%LocalAppData%\ExpeditionsMacro`, never the installation directory.
- Validate downloaded paths, hashes, sizes, versions, and compatibility before installation; retain rollback behavior.
- Document network behavior in `PRIVACY.md`; do not add telemetry implicitly.
- Discord messages use Components V2 and explicit `allowed_mentions`.
- Test secrets must be visibly fake. Never place production or personal credentials in fixtures.

## Coding conventions

- Follow `.editorconfig`: UTF-8, CRLF, final newline, four spaces for C#/XAML and two for JSON/YAML/project files.
- Use nullable annotations, file-scoped namespaces, immutable records/models, and explicit validation at persistence/external boundaries.
- Keep comments focused on why a non-obvious constraint exists.
- Avoid compatibility shims for unreleased formats unless a current public release requires migration.
- Treat a dirty worktree as user-owned. Never discard unrelated modifications.
