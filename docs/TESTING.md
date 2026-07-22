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
- Input, capture, coordinator, or cancellation: focused Windows/workflow tests and the full non-golden suite.
- UI/XAML: relevant view-model/page tests, a Release build, and `scripts/Render-UiSnapshots.ps1 -Configuration Release`; inspect both themes.
- Detector regions, matching, thresholds, ordering, preprocessing, or action placement: focused detector tests and the complete cross-state golden corpus.
- Dataset changes: privacy review every image, update counts/docs, then run the complete golden corpus.
- Release packaging: `scripts/Build-Release.ps1` and `scripts/Verify-Release.ps1` in addition to the above.

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
- When manual user input is paired with passive capture, label the input as user reported rather than claiming it came from the event log.
