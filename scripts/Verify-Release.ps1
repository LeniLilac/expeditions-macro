[CmdletBinding()]
param(
    [string]$ReleaseDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\release'),
    [int]$PortableSmokeTimeoutMilliseconds = 180000
)

$ErrorActionPreference = 'Stop'
if ($PortableSmokeTimeoutMilliseconds -lt 1000) {
    throw 'Portable smoke timeout must be at least one second.'
}
$checksums = Join-Path $ReleaseDirectory 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $checksums)) { throw 'SHA256SUMS.txt is missing.' }

foreach ($line in [System.IO.File]::ReadAllLines($checksums)) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Malformed checksum line: $line" }
    $path = Join-Path $ReleaseDirectory $Matches[2]
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing release asset: $($Matches[2])" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Matches[1]) { throw "Checksum mismatch: $($Matches[2])" }
}

$standaloneDetectorArchives = @(
    Get-ChildItem `
        -LiteralPath $ReleaseDirectory `
        -Filter 'anime-expeditions-expeditions-*.zip' `
        -File)
if ($standaloneDetectorArchives.Count -ne 0) {
    throw 'Detector data must be bundled with the application, not published as a separate archive.'
}

$portable = Get-ChildItem -LiteralPath $ReleaseDirectory -Filter 'ExpeditionsMacro-*-win-x64.zip' -File | Select-Object -First 1
if (-not $portable) { throw 'The portable application archive is missing.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($portable.FullName)
try {
    $entryPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $entriesByPath =
        [System.Collections.Generic.Dictionary[
            string,
            System.IO.Compression.ZipArchiveEntry]]::new(
                [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        $normalized = $entry.FullName.Replace('\', '/').TrimEnd('/')
        if (-not $normalized) { continue }
        $segments = @($normalized.Split('/'))
        if ($normalized.StartsWith('/') -or
            $normalized.Contains(':') -or
            $segments -contains '' -or
            $segments -contains '.' -or
            $segments -contains '..' -or
            -not $entryPaths.Add($normalized)) {
            throw "Portable archive contains an invalid or duplicate path: $($entry.FullName)"
        }
        $entriesByPath.Add($normalized, $entry)
    }

    $rootEntries = @($archive.Entries | Where-Object {
        $normalized = $_.FullName.Replace('\', '/').TrimEnd('/')
        $_.Name -and $normalized -and -not $normalized.Contains('/')
    })
    if ($rootEntries.Count -ne 1 -or
        -not $rootEntries[0].FullName.Replace('\', '/').Equals(
            'ExpeditionsMacro.exe',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        $names = ($rootEntries | ForEach-Object FullName) -join ', '
        throw "Portable archive root must contain only ExpeditionsMacro.exe. Found: $names"
    }

    $unexpectedRootDirectories = @($archive.Entries | Where-Object {
        $entryPath = $_.FullName.Replace('\', '/')
        $normalized = $entryPath.TrimEnd('/')
        $entryPath.EndsWith('/') -and
            -not $normalized.Contains('/') -and
            -not $normalized.Equals(
                'ExpeditionsMacro',
                [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($unexpectedRootDirectories.Count -ne 0) {
        $names = ($unexpectedRootDirectories | ForEach-Object FullName) -join ', '
        throw "Portable archive contains unexpected root directories: $names"
    }

    $topLevelFolders = @($entryPaths |
        ForEach-Object {
            if ($_.Contains('/')) { $_.Split('/', 2)[0] }
        } |
        Where-Object { $_ } |
        Select-Object -Unique)
    if ($topLevelFolders.Count -ne 1 -or
        -not $topLevelFolders[0].Equals(
            'ExpeditionsMacro',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Portable archive must contain exactly one dependency folder named ExpeditionsMacro. Found: $($topLevelFolders -join ', ')"
    }

    if ($entryPaths.Contains('ExpeditionsMacro/ExpeditionsMacro.exe')) {
        throw 'Portable dependency folder must not contain a second ExpeditionsMacro.exe.'
    }
    $requiredEntries = @(
        'ExpeditionsMacro/ExpeditionsMacro.dll',
        'ExpeditionsMacro/ExpeditionsMacro.deps.json',
        'ExpeditionsMacro/ExpeditionsMacro.runtimeconfig.json',
        'ExpeditionsMacro/hostfxr.dll',
        'ExpeditionsMacro/coreclr.dll',
        'ExpeditionsMacro/OpenCvSharpExtern.dll',
        'ExpeditionsMacro/msvcp140.dll',
        'ExpeditionsMacro/vcruntime140.dll',
        'ExpeditionsMacro/vcruntime140_1.dll'
    )
    foreach ($required in $requiredEntries) {
        if (-not $entryPaths.Contains($required)) {
            throw "Portable archive is missing required dependency: $required"
        }
    }

    $detectorPackId = 'anime-expeditions-expeditions'
    $detectorPackVersion = '1.0.2'
    $detectorRoot = "ExpeditionsMacro/Resources/DetectorPacks/$detectorPackId/$detectorPackVersion"
    $detectorManifestPath = "$detectorRoot/manifest.json"
    if (-not $entriesByPath.ContainsKey(
            $detectorManifestPath)) {
        throw 'Portable archive is missing its bundled detector manifest.'
    }
    $detectorManifestEntry =
        $entriesByPath[$detectorManifestPath]
    $manifestReader = [System.IO.StreamReader]::new(
        $detectorManifestEntry.Open())
    try {
        $detectorManifest =
            $manifestReader.ReadToEnd() |
            ConvertFrom-Json
    }
    finally {
        $manifestReader.Dispose()
    }
    if ($detectorManifest.pack_id -ne $detectorPackId -or
        $detectorManifest.version -ne $detectorPackVersion) {
        throw 'Portable detector manifest identity does not match the required bundled release data.'
    }
    foreach ($file in $detectorManifest.files) {
        $relative = $file.path.Replace('\', '/')
        $segments = @($relative.Split('/'))
        if ($relative.StartsWith('/') -or
            $relative.Contains(':') -or
            $segments -contains '' -or
            $segments -contains '.' -or
            $segments -contains '..') {
            throw "Portable detector manifest contains an unsafe path: $($file.path)"
        }
        $payloadPath = "$detectorRoot/$relative"
        if (-not $entriesByPath.ContainsKey(
                $payloadPath)) {
            throw "Portable detector payload is missing or has the wrong size: $($file.path)"
        }
        $entry = $entriesByPath[$payloadPath]
        if ($entry.Length -ne [long]$file.bytes) {
            throw "Portable detector payload is missing or has the wrong size: $($file.path)"
        }
        $stream = $entry.Open()
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $actual = -join @(
                $sha256.ComputeHash($stream) |
                    ForEach-Object {
                        $_.ToString('x2')
                    })
        }
        finally {
            $sha256.Dispose()
            $stream.Dispose()
        }
        if ($actual -ne $file.sha256.ToLowerInvariant()) {
            throw "Portable detector payload failed its SHA-256 check: $($file.path)"
        }
    }
}
finally {
    $archive.Dispose()
}

if ($portable.BaseName -notmatch '^ExpeditionsMacro-(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?)-win-x64$') {
    throw "Portable archive has an invalid release filename: $($portable.Name)"
}
$portableVersion = $Matches.version
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$smokeRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryBase "Expeditions Macro portable smoke $([Guid]::NewGuid().ToString('N'))"))
if (-not $smokeRoot.StartsWith(
        $temporaryBase,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Portable smoke directory resolved outside the temporary directory.'
}

try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory(
        $portable.FullName,
        $smokeRoot)
    $rootExecutable = Join-Path $smokeRoot 'ExpeditionsMacro.exe'
    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        $rootExecutable).ProductVersion
    if (-not $productVersion -or
        -not $productVersion.StartsWith(
            $portableVersion,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Portable root apphost reports '$productVersion' instead of '$portableVersion'."
    }

    $snapshotOutput = Join-Path $smokeRoot 'snapshot output'
    $quotedSnapshotOutput = '"' + $snapshotOutput + '"'
    $process = Start-Process `
        -FilePath $rootExecutable `
        -ArgumentList @('--snapshot-ui', $quotedSnapshotOutput) `
        -WorkingDirectory $temporaryBase `
        -WindowStyle Hidden `
        -PassThru
    if (-not $process.WaitForExit($PortableSmokeTimeoutMilliseconds)) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
        throw 'Portable root apphost smoke test timed out.'
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $errorFile = Join-Path $snapshotOutput 'snapshot-error.txt'
        $details = if (Test-Path -LiteralPath $errorFile) {
            Get-Content -Raw -LiteralPath $errorFile
        }
        else {
            'No snapshot error record was written.'
        }
        throw "Portable root apphost exited with code $($process.ExitCode). $details"
    }
    if (@(Get-ChildItem -LiteralPath $snapshotOutput -Filter '*.png' -File).Count -eq 0) {
        throw 'Portable root apphost did not render any UI snapshots.'
    }
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
}

Write-Host 'Release checksums, portable layout, and root apphost smoke test are valid.'
