# Bundled detector references

The application compiles stable visual references into a versioned directory containing `manifest.json` plus compact PNG payloads. The manifest declares the required 808 by 611 Roblox client size, state regions, click actions, selection references, difficulty and node hue prototypes, the empty-hotbar reference, and a SHA-256/size entry for every payload file.

Detector data is application content, not a separately installed product feature. Every portable ZIP and installer contains the exact version selected by `AnimeExpeditionsDetectorSpec.BundledPackVersion`. The app does not check for, download, select, or roll back detector data independently of an application release.

At startup, the bundled manifest and every declared payload are validated before automation can run. A validated copy is synchronized to the application-managed local cache used by runtime and diagnostic tooling. Any older, newer, mismatched, or damaged cached copy is replaced by the data bundled with that application version. A damaged application bundle stops startup with clean-reinstall guidance rather than attempting to run with partial detector coverage.

Release packaging independently validates the published detector manifest, sizes, and hashes before creating the portable archive or installer. `Verify-Release.ps1` repeats those checks against the actual portable ZIP and fails when a reference is absent, misplaced, the wrong size, or hash-mismatched.

## Build from the dataset tool

Run `ExpeditionsMacro.DatasetBuilder`, choose the local dataset root, choose an output folder and semantic version, then select **Build detector pack**.

The command-line equivalent is:

```powershell
dotnet run --project tools/ExpeditionsMacro.DatasetBuilder -- --build datasets/anime-expeditions/expeditions detector-packs 1.0.2
```

After reviewing the generated manifest and payload, update the bundled content entry in `ExpeditionsMacro.App.csproj` and `AnimeExpeditionsDetectorSpec.BundledPackVersion` together. Do not publish the generated directory as an independent release asset.

The current app may layer strict specialized detectors over the compiled references for field-confirmed UI revisions. The 2026-07-25 Expedition selector, for example, uses the cyan perimeter of the active left-side map card, the lower-left difficulty color, and the independently detected live **Select Stage** button while retaining the compiled compact selector as a legacy fallback. Villain Invasion Event pages are likewise specialized because that route is Lobby-only and does not belong to the shared Play-interface state graph. When another Event is selected, its catalog detector requires the wide cyan selected tab and chevron plus the thin red Villain Invasion rail without a chevron; it never scores the changing card artwork. Villain Invasion home requires the wide red selected tab and chevron plus the independent red Event Gamemode action. Every specialized detector still consumes canonical 808 by 611 client frames and must carry reviewed fixtures plus cross-state negative coverage.

Placement confirmation is another strict specialized detector. A placed unit is accepted only when the fixed lower-left panel exposes both its red Close control and its initial blue **Priority / First** control. The dark panel body contributes confidence but is not sufficient or required on its own, and an ordinary hover/info card must remain a negative.
