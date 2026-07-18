# Detector packs

A detector pack is a versioned directory or ZIP containing `manifest.json` plus compact PNG references. The manifest declares the required 808 by 611 Roblox client size, state regions, click actions, selection references, node-hue prototypes, the empty-hotbar reference, and a SHA-256/size entry for every payload file.

The app validates all paths, sizes, and hashes before installation. A new version is staged, the current pack becomes `previous`, and rollback swaps those two directories.

## Build from the UI

Run `ExpeditionsMacro.DatasetBuilder`, choose the local dataset root, choose an output folder and semantic version, then select **Build detector pack**.

## Build from the command line

```powershell
dotnet run --project tools/ExpeditionsMacro.DatasetBuilder -- --build datasets/anime-expeditions/expeditions detector-packs/anime-expeditions-expeditions 1.0.0
```

For release updates, ZIP the contents of the version directory so `manifest.json` is at the archive root. Name the GitHub Release asset:

`anime-expeditions-expeditions-<version>.zip`

The app checks stable releases at `LeniLilac/expeditions-macro`, prompts before installation, and never updates a pack while automation owns Roblox input.
