# Releasing

Releases are built from committed source on an annotated semantic-version tag. Never overwrite or retag a public release; fix forward with a new version.

## Prepare

1. Choose the next version and update `VersionPrefix`/`VersionSuffix` in `Directory.Build.props`.
2. Move relevant `CHANGELOG.md` entries out of Unreleased into a dated version section and update comparison links.
3. Copy `docs/release-notes/TEMPLATE.md` to `docs/release-notes/<version>.md`, replace every placeholder, and describe user-visible behavior, validation, and all shipped assets.
4. For a silent prerelease, state in the notes that no Discord announcement is sent.
5. Run the shared metadata and policy gate:

   ```powershell
   ./scripts/Invoke-ReleasePreflight.ps1 -Version <version>
   # Add -Silent for alpha, beta, or release-candidate tags.
   ```

6. Run the full Release test suite and UI snapshots when applicable.
7. Run `scripts/Build-Release.ps1 -Version <version>` and `scripts/Verify-Release.ps1`.
8. Audit the publish set for datasets, secrets, local paths, logs, personal models, and generated files.

## Publish and verify

1. Commit and push the release-ready source before tagging.
2. Create the signed or annotated version tag used by the release workflow.
3. Confirm CI and the tag-triggered Release workflow pass for the exact commit. Both stable and silent workflows rerun the shared release preflight before building.
4. Confirm the GitHub Release contains the installer, portable ZIP, detector pack, checksums, and dependency inventory.
5. Confirm the Discord release announcement only when the requested workflow is not a silent release.

Local test ZIPs may be built with a prerelease version without publishing, but they must still pass repository policy, package checksum verification, and a packaged-app smoke test before delivery.
