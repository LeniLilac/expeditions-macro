# Detector Viewer

Detector Viewer is a standalone Windows inspection tool for the production vision code used by Expeditions Macro. It overlays detector-owned regions and live actions on canonical captures, then shows the production result, confidence, explicit gates, emitted checks, measurements, and provenance.

## Open a source

- Choose **Repo datasets** or press Ctrl+D to index every supported image under the current checkout's `datasets` folder.
- Choose **Open source** for a PNG, JPEG, BMP, TIFF, or Deep Debug ZIP. Rolling-retention archives expose their retained PNG tail; use Deep Debug Viewer for the full text timeline and intentionally pruned frame records.
- Choose **Open folder** to index supported images recursively without loading the complete folder into memory.
- Drop any supported source onto the window.
- Type a filename into the frame picker to jump directly to a dataset image. The filename appears before its relative folder so prefix search remains useful across the complete corpus.
- Changing frames automatically selects the strongest production detector associated with the fixture name or live positive evidence. A strong filename association remains selected when that detector fails, so broken fixtures are inspectable instead of hidden by the failure.
- Use Left/Right to move between frames, `F` to fit, Ctrl+wheel to zoom, and middle-drag to pan.
- Hover the image to inspect its canonical client coordinate and RGB/HSL values.

Production detectors require an 808 by 611 Roblox client frame. Other image sizes remain viewable but are not evaluated.

## Reading the evidence

- **Pass/Fail** appears only for an explicit production result, a production-emitted Boolean condition, or a metric wired to its exact production gate.
- Named constants discovered from production code are advisory. The viewer does not infer which metric they grade.
- Static regions that a detector exposes are drawn exactly. Dynamically translated or locally searched geometry is shown only when the production path returns or traces it.
- **Unavailable** entries keep every detector visible in the catalog and state why a safe standalone detail path is not exposed.
- A live action is the coordinate returned by the selected production detector for the selected frame. Static geometry never becomes an action.

Detector Viewer never controls Roblox or changes detector decisions. Its only write operation is the explicit repository annotation manifest described below.

The repository button is available when the viewer runs anywhere beneath an Expeditions Macro checkout. Standalone copies outside a checkout can use **Open folder** and select a copied `datasets` directory instead.

## Annotate detector fixtures

1. Open **Repo datasets** and choose an image. The Viewer automatically selects its likely detector.
2. Choose **Annotate** in the right pane.
3. Set **Should match**, **Should not match**, or **Needs review**.
4. Drag directly on the image to add one or more canonical detection regions.
5. Select a region to rename or delete it, and add implementation notes when needed.

Every edit is atomically autosaved to `datasets/detector-annotations.json`. Entries are keyed by repository-relative image path and detector ID, so one fixture can describe different expectations for multiple detectors. Coordinates always use the canonical 808 by 611 client frame. Annotation overlays are implementation guidance only; they never authorize input or alter production detector results.

## Validation commands

```powershell
dotnet test tools/ExpeditionsMacro.DetectorViewer.Tests/ExpeditionsMacro.DetectorViewer.Tests.csproj -c Release
ExpeditionsMacro.DetectorViewer.exe --smoke <output-directory>
ExpeditionsMacro.DetectorViewer.exe --snapshot-ui <output-directory>
```

The smoke command writes machine-readable catalog coverage. The snapshot command renders dark/light matched, negative, error, and minimum-size UI states.
