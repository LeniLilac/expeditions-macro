# Detector Viewer

Detector Viewer is a standalone Windows inspection tool for the production vision code used by Expeditions Macro. It overlays detector-owned regions and live actions on canonical captures, then shows the production result, confidence, explicit gates, emitted checks, measurements, and provenance.

## Open a source

- Choose **Open source** for a PNG, JPEG, BMP, TIFF, or Deep Debug ZIP.
- Choose **Open folder** to index supported images recursively without loading the complete folder into memory.
- Drop any supported source onto the window.
- Use Left/Right to move between frames, `F` to fit, Ctrl+wheel to zoom, and middle-drag to pan.
- Hover the image to inspect its canonical client coordinate and RGB/HSL values.

Production detectors require an 808 by 611 Roblox client frame. Other image sizes remain viewable but are not evaluated.

## Reading the evidence

- **Pass/Fail** appears only for an explicit production result, a production-emitted Boolean condition, or a metric wired to its exact production gate.
- Named constants discovered from production code are advisory. The viewer does not infer which metric they grade.
- Static regions that a detector exposes are drawn exactly. Dynamically translated or locally searched geometry is shown only when the production path returns or traces it.
- **Unavailable** entries keep every detector visible in the catalog and state why a safe standalone detail path is not exposed.
- A live action is the coordinate returned by the selected production detector for the selected frame. Static geometry never becomes an action.

Detector Viewer is read-only. It neither controls Roblox nor changes detector decisions.

## Validation commands

```powershell
dotnet test tools/ExpeditionsMacro.DetectorViewer.Tests/ExpeditionsMacro.DetectorViewer.Tests.csproj -c Release
ExpeditionsMacro.DetectorViewer.exe --smoke <output-directory>
ExpeditionsMacro.DetectorViewer.exe --snapshot-ui <output-directory>
```

The smoke command writes machine-readable catalog coverage. The snapshot command renders dark/light matched, negative, error, and minimum-size UI states.
