# AGENTS.md

This file applies to the entire repository. It contains only the rules that must remain visible for every change; detailed guidance lives in the linked documents.

## Mission and hard boundaries

Expeditions Macro is a Windows-only .NET/WPF utility that automates repeatable Anime Expeditions runs in Roblox through screen capture and ordinary Windows input.

- Do not inject into Roblox, read or modify Roblox process memory, hook the game, or add anti-cheat bypasses.
- Keep the project noncommercial and preserve its license and notices.
- Treat Roblox, Anime Expeditions, Discord, and GitHub as independently changing external systems.
- Prefer deterministic, inspectable state transitions over hidden heuristics, blind clicks, or unbounded retries.
- Never commit credentials, webhook URLs, user logs, local models, private captures, or generated build output.

## Read before changing

- Every change: [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
- Architecture, Windows input, capture, or workflow ownership: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
- Navigation or game-state behavior: [docs/GAME-BEHAVIOR.md](docs/GAME-BEHAVIOR.md). Field-observed behavior outranks assumptions.
- Vision or detector work: [docs/DETECTOR-PACKS.md](docs/DETECTOR-PACKS.md) and [datasets/README.md](datasets/README.md).
- Test scope and commands: [docs/TESTING.md](docs/TESTING.md).
- Releases: [docs/RELEASING.md](docs/RELEASING.md).

## Non-negotiable runtime invariants

- The canonical Roblox client is 808 by 611 pixels. Captures, regions, and actions use client-relative coordinates, never desktop or outer-window coordinates.
- Workflows standardize Roblox to the canonical client size and leave it there.
- Only one workflow may own Roblox input. Route operations through the coordinator, remain cancellation-aware, and release held keys, mouse buttons, and temporary shift lock on every exit path.
- Revalidate and focus the Roblox window before input after delays or external transitions. Use the shared acknowledged-motion click path; do not replace it with direct cursor teleportation.
- Coarse camera yaw uses extended scan-code arrow pulses; fine yaw uses relative motion while right mouse is held. Never rotate with visible absolute cursor motion.
- Detect and verify each owned state before acting or handing control to another runner. Preserve the transitions recorded in the game-behavior ledger.
- Webhook URLs are secrets: protect them with DPAPI and redact them from errors, logs, captures, tests, and release output.

## Architecture and source health

- Preserve project direction: `Core <- Vision/Windows <- Automation <- App`. Core remains platform-independent; Win32 stays in Windows; vision contains no workflow orchestration; WPF stays in App.
- Run `./scripts/Test-RepositoryPolicy.ps1` after structural changes. CI and release builds run it too.
- New production source files must stay at or below 500 lines, tests at or below 800, and repository scripts at or below 500. Existing oversized files are exact debt ratchets in `eng/repository-policy.json`: they may shrink but must not grow.
- Split code by cohesive owner or lifecycle before adding another responsibility. Do not create god classes, catch-all helpers, broad compatibility facades, or partial-class fragments whose only purpose is evading the size check.
- Do not hand-edit or track generated `bin`, `obj`, `artifacts`, `TestResults`, installer output, dependency inventories, or checksums.

## Required change loop

1. Inspect `git status`; preserve unrelated user work.
2. Reproduce bugs from supplied captures/logs before editing. Review deep-debug transitions as timestamped contact sheets first.
3. Make the smallest root-cause fix that preserves layer and runtime invariants.
4. Add focused regression coverage, then run the broader suites required by [docs/TESTING.md](docs/TESTING.md).
5. Run the repository policy check and `git diff --check`; review the complete intended diff.
6. Update user-facing and engineering documentation in the same change when behavior or policy changes.

## Definition of done

A change is complete only when the reported scenario passes, negative cases still fail safely, policy checks and risk-proportionate tests pass, documentation is current, the working tree has no accidental files, and any requested artifact, commit, push, or release is verifiably present.
