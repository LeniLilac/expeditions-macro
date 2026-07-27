[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-(?:[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*))?$')]
    [string]$Version,

    [string]$DotNetPath = 'dotnet',

    [switch]$SkipTests,

    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$numericVersion = $Version.Split('-', 2)[0]
$repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $repository 'artifacts'))
if (-not $artifacts.StartsWith($repository, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifact output resolved outside the repository.'
}

& (Join-Path $repository 'scripts\Test-RepositoryPolicy.ps1') -RepositoryRoot $repository

$publish = Join-Path $artifacts 'publish\ExpeditionsMacro'
$portableStage = Join-Path $artifacts 'portable'
$portableRootExecutable = Join-Path $portableStage 'ExpeditionsMacro.exe'
$portableDependencies = Join-Path $portableStage 'ExpeditionsMacro'
$release = Join-Path $artifacts 'release'
$detectorPackId = 'anime-expeditions-expeditions'
$detectorPackVersion = '1.0.2'
if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish, $portableStage, $release | Out-Null

& $DotNetPath restore (Join-Path $repository 'ExpeditionsMacro.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

if (-not $SkipTests) {
    & $DotNetPath test (Join-Path $repository 'tests\ExpeditionsMacro.Tests\ExpeditionsMacro.Tests.csproj') -c Release --no-restore --filter 'Category!=Golden' --logger 'trx;LogFileName=release-tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }

    & $DotNetPath test (Join-Path $repository 'tools\ExpeditionsMacro.DeepDebugViewer.Tests\ExpeditionsMacro.DeepDebugViewer.Tests.csproj') -c Release --no-restore --logger 'trx;LogFileName=deep-debug-viewer-tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Deep Debug Viewer tests failed.' }
}

& $DotNetPath publish (Join-Path $repository 'src\ExpeditionsMacro.App\ExpeditionsMacro.App.csproj') `
    -c Release -r win-x64 --self-contained true --no-restore `
    -p:Version=$Version -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false `
    "-p:PortableRootAppHostPath=$portableRootExecutable" `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }

# OpenCvSharp's Windows native binding requires the Visual C++ 2015-2022 x64
# runtime. Deploy it app-local so both the installer and portable archive work
# on clean Windows profiles without a separate prerequisite installer.
$vcRuntimeFiles = @(
    'concrt140.dll',
    'msvcp140.dll',
    'msvcp140_1.dll',
    'msvcp140_2.dll',
    'msvcp140_atomic_wait.dll',
    'msvcp140_codecvt_ids.dll',
    'vcruntime140.dll',
    'vcruntime140_1.dll',
    'vcruntime140_threads.dll'
)
$vcRuntimeCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($env:VCToolsRedistDir)) {
    $vcRuntimeCandidates += Join-Path $env:VCToolsRedistDir 'x64\Microsoft.VC143.CRT'
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $visualStudio = & $vswhere -latest -products * -property installationPath
    if (-not [string]::IsNullOrWhiteSpace($visualStudio)) {
        $vcRuntimeCandidates += Get-ChildItem (Join-Path $visualStudio 'VC\Redist\MSVC') -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\Microsoft.VC143.CRT' }
    }
}
$vcRuntimeCandidates += Join-Path $env:WINDIR 'System32'
$vcRuntimeSource = $vcRuntimeCandidates |
    Where-Object {
        $candidate = $_
        $missing = @($vcRuntimeFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $candidate $_)) })
        $missing.Count -eq 0
    } |
    Select-Object -First 1
if (-not $vcRuntimeSource) { throw 'The Microsoft Visual C++ 2015-2022 x64 runtime files were not found.' }
foreach ($file in $vcRuntimeFiles) {
    Copy-Item -LiteralPath (Join-Path $vcRuntimeSource $file) -Destination $publish
}

Copy-Item -LiteralPath (Join-Path $repository 'README.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'NOTICE.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'PRIVACY.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'THIRD-PARTY-NOTICES.md') -Destination $publish

$bundledDetectorRoot = Join-Path $publish "Resources\DetectorPacks\$detectorPackId\$detectorPackVersion"
$bundledDetectorManifestPath = Join-Path $bundledDetectorRoot 'manifest.json'
if (-not (Test-Path -LiteralPath $bundledDetectorManifestPath -PathType Leaf)) {
    throw 'The published application is missing its bundled detector manifest.'
}
$bundledDetectorManifest = Get-Content -Raw -LiteralPath $bundledDetectorManifestPath | ConvertFrom-Json
if ($bundledDetectorManifest.pack_id -ne $detectorPackId -or
    $bundledDetectorManifest.version -ne $detectorPackVersion) {
    throw 'The published detector manifest identity does not match the required bundled release data.'
}
$bundledDetectorPrefix = [System.IO.Path]::GetFullPath($bundledDetectorRoot) + [System.IO.Path]::DirectorySeparatorChar
foreach ($file in $bundledDetectorManifest.files) {
    $payload = [System.IO.Path]::GetFullPath(
        (Join-Path $bundledDetectorRoot $file.path.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
    if (-not $payload.StartsWith(
            $bundledDetectorPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The bundled detector manifest contains an unsafe path: $($file.path)"
    }
    $info = Get-Item -LiteralPath $payload -ErrorAction SilentlyContinue
    if (-not $info -or $info.Length -ne [long]$file.bytes) {
        throw "The published detector payload is missing or has the wrong size: $($file.path)"
    }
    $hash = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $file.sha256.ToLowerInvariant()) {
        throw "The published detector payload failed its SHA-256 check: $($file.path)"
    }
}

New-Item -ItemType Directory -Force -Path $portableDependencies | Out-Null
foreach ($item in @(Get-ChildItem -LiteralPath $publish -Force)) {
    if ($item.Name -eq 'ExpeditionsMacro.exe') { continue }
    Copy-Item -LiteralPath $item.FullName -Destination $portableDependencies -Recurse -Force
}
if (-not (Test-Path -LiteralPath $portableRootExecutable -PathType Leaf)) {
    throw 'The portable root apphost was not created.'
}
if (-not (Test-Path -LiteralPath (Join-Path $portableDependencies 'ExpeditionsMacro.dll') -PathType Leaf)) {
    throw 'The portable dependency directory is missing ExpeditionsMacro.dll.'
}

$portable = Join-Path $release "ExpeditionsMacro-$Version-win-x64.zip"
# Keep the discoverable native apphost at the ZIP root while containing every
# managed, native, and content dependency in one adjacent application folder.
Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portable -CompressionLevel Optimal

$dependencyInventory = Join-Path $release 'dependencies.json'
& $DotNetPath list (Join-Path $repository 'ExpeditionsMacro.slnx') package --include-transitive --format json | Set-Content -LiteralPath $dependencyInventory -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Dependency inventory generation failed.' }

if (-not $SkipInstaller) {
    $compiler = Get-Command 'iscc.exe' -ErrorAction SilentlyContinue
    if (-not $compiler) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
        )
        $candidate = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($candidate) { $compiler = Get-Item -LiteralPath $candidate }
    }
    if ($compiler) {
        $compilerPath = if ($compiler.PSObject.Properties.Name -contains 'Source') { $compiler.Source } else { $compiler.FullName }
        & $compilerPath "/DAppVersion=$Version" "/DAppNumericVersion=$numericVersion" "/DRepositoryRoot=$repository" "/DOutputDir=$release" (Join-Path $repository 'installer\ExpeditionsMacro.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
    }
    else {
        Write-Warning 'Inno Setup 6 was not found. The portable archive was still created.'
    }
}

$checksumFile = Join-Path $release 'SHA256SUMS.txt'
$checksumLines = Get-ChildItem -LiteralPath $release -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
[System.IO.File]::WriteAllLines($checksumFile, $checksumLines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Release artifacts written to $release"
Get-ChildItem -LiteralPath $release -File | Select-Object Name, Length
