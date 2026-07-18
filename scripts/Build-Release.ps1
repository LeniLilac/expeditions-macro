[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$DotNetPath = 'dotnet',

    [switch]$SkipTests,

    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $repository 'artifacts'))
if (-not $artifacts.StartsWith($repository, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifact output resolved outside the repository.'
}

$publish = Join-Path $artifacts 'publish\ExpeditionsMacro'
$release = Join-Path $artifacts 'release'
if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish, $release | Out-Null

& $DotNetPath restore (Join-Path $repository 'ExpeditionsMacro.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

if (-not $SkipTests) {
    & $DotNetPath test (Join-Path $repository 'tests\ExpeditionsMacro.Tests\ExpeditionsMacro.Tests.csproj') -c Release --no-restore --filter 'Category!=Golden' --logger 'trx;LogFileName=release-tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }
}

& $DotNetPath publish (Join-Path $repository 'src\ExpeditionsMacro.App\ExpeditionsMacro.App.csproj') `
    -c Release -r win-x64 --self-contained true --no-restore `
    -p:Version=$Version -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }

Copy-Item -LiteralPath (Join-Path $repository 'README.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'NOTICE.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'PRIVACY.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $repository 'THIRD-PARTY-NOTICES.md') -Destination $publish

$portable = Join-Path $release "ExpeditionsMacro-$Version-win-x64.zip"
Compress-Archive -Path $publish -DestinationPath $portable -CompressionLevel Optimal

$packVersion = '1.0.1'
$packId = 'anime-expeditions-expeditions'
$packRoot = Join-Path $repository "detector-packs\$packId\$packVersion"
$packArchive = Join-Path $release "$packId-$packVersion.zip"
Compress-Archive -Path (Join-Path $packRoot '*') -DestinationPath $packArchive -CompressionLevel Optimal

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
        & $compilerPath "/DAppVersion=$Version" "/DRepositoryRoot=$repository" "/DOutputDir=$release" (Join-Path $repository 'installer\ExpeditionsMacro.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
    }
    else {
        Write-Warning 'Inno Setup 6 was not found. Portable and detector archives were still created.'
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
