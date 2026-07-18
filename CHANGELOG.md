# Changelog

All notable changes to Expeditions Macro are documented here.

## [1.0.1] - 2026-07-18

### Fixed

- Prevented camera-region details from overlapping the region and overlay controls.
- Centered single-line input text so focused values are no longer vertically clipped at Windows display scaling.
- Made camera-model setup and standalone alignment temporarily enable shift lock and restore it after success, cancellation, or failure.
- Allowed the UI snapshot renderer to run while the normal single app instance is open, without changing normal single-instance behavior.

## [1.0.0] - 2026-07-18

### Added

- Unified native Windows app for Expeditions runs, camera-model calibration, and placement-model recording, editing, and testing.
- Roblox-relative capture regions and placement coordinates with temporary client-size restoration.
- Full-turn yaw learning with coarse shortest-path alignment and fine mouse-drag correction.
- Detector-pack-driven lobby, map, difficulty, node, reward, checkpoint, victory, defeat, and disconnect handling.
- Configurable checkpoint extraction, including the first checkpoint or the first checkpoint after a chosen number of boss nodes.
- Lobby and disconnect recovery, including direct starts from the lobby.
- Optional Discord Components V2 reports with protected webhook storage.
- Dark, light, and system themes; F6 start/stop; local logs; detector-pack updates; portable and installer releases.
- Reproducible detector fixtures with full golden-image regression coverage in public CI.

[1.0.1]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.1
[1.0.0]: https://github.com/LeniLilac/expeditions-macro/releases/tag/v1.0.0
