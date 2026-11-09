# Builds a checked Thunderstore upload ZIP from the actual compiled plugin.
# This does not upload to Thunderstore or publish a GitHub release.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$ProfileDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$packageDir = Join-Path $root 'thunderstore'
$manifestPath = Join-Path $packageDir 'manifest.json'
$readmePath = Join-Path $packageDir 'README.md'
$iconPath = Join-Path $packageDir 'icon.png'
$changelogPath = Join-Path $root 'CHANGELOG.md'
$project = Join-Path $root 'src\StonewardsSpectatorSwitch\StonewardsSpectatorSwitch.csproj'
$source = Join-Path $root 'src\StonewardsSpectatorSwitch\SpectatorSwitchPlugin.cs'
$dll = Join-Path $root 'src\StonewardsSpectatorSwitch\bin\Release\netstandard2.1\StonewardsSpectatorSwitch.dll'

foreach ($path in @($manifestPath, $readmePath, $iconPath, $changelogPath, $project, $source)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing release input: $path"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.name -ne 'SpectatorSwitch') { throw "Unexpected package name: $($manifest.name)" }
$version = [string]$manifest.version_number
if ($version -cne '0.1.0') { throw "Unexpected release version: $version" }
if (@($manifest.dependencies) -notcontains 'BepInEx-BepInExPack-5.4.2305') {
    throw 'BepInExPack dependency is missing.'
}
if ([string]$manifest.description -eq '' -or ([string]$manifest.description).Length -gt 250) {
    throw 'Thunderstore manifest.description must be 1-250 characters.'
}

$sourceText = Get-Content -LiteralPath $source -Raw -Encoding UTF8
$sourceMatch = [regex]::Match($sourceText, 'PluginVersion\s*=\s*"([^"]+)"')
if (-not $sourceMatch.Success -or $sourceMatch.Groups[1].Value -cne $version) {
    throw "C# plugin version must match manifest version $version."
}
$projectText = Get-Content -LiteralPath $project -Raw -Encoding UTF8
if (-not $projectText.Contains("<Version>$version</Version>")) {
    throw "Project assembly version must match manifest version $version."
}
if ((Get-Content -LiteralPath $changelogPath -Raw -Encoding UTF8) -notmatch
    ('(?m)^##\s+' + [regex]::Escape($version) + '\b')) {
    throw "CHANGELOG.md must include version $version."
}

# Verify the exact chosen PNG rather than silently generating a replacement.
$iconBytes = [System.IO.File]::ReadAllBytes($iconPath)
$signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
if ($iconBytes.Length -lt 24) { throw 'Invalid icon: file is too small.' }
for ($i = 0; $i -lt 8; $i++) {
    if ($iconBytes[$i] -ne $signature[$i]) {
        throw 'The Thunderstore icon must be a PNG.'
    }
}
$width = ([int]$iconBytes[16] -shl 24) -bor ([int]$iconBytes[17] -shl 16) -bor
         ([int]$iconBytes[18] -shl 8) -bor [int]$iconBytes[19]
$height = ([int]$iconBytes[20] -shl 24) -bor ([int]$iconBytes[21] -shl 16) -bor
          ([int]$iconBytes[22] -shl 8) -bor [int]$iconBytes[23]
if ($width -ne 256 -or $height -ne 256) {
    throw "Thunderstore requires 256x256 icon.png, got $width x $height."
}

# A fresh build is mandatory; never package a stale DLL from an older test.
$GameDir = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
$ProfileDir = (Resolve-Path -LiteralPath $ProfileDir -ErrorAction Stop).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK is missing.' }
$managed = Join-Path $GameDir 'Stonewards_Data\Managed'
$bepCore = Join-Path $ProfileDir 'BepInEx\core'
foreach ($file in @(
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'Mirror.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $managed 'UnityEngine.UIElementsModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'Unity.InputSystem.dll'),
    (Join-Path $bepCore 'BepInEx.dll')
)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing build dependency: $file"
    }
}

Write-Host "Building SpectatorSwitch v$version..." -ForegroundColor Cyan
& dotnet build $project --configuration Release "-p:GameDir=$GameDir" "-p:BepInExDir=$(Join-Path $ProfileDir 'BepInEx')"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
    throw "Compiled DLL not found: $dll"
}
if ((Get-Item -LiteralPath $dll).Length -lt 4096) {
    throw 'Compiled DLL is unexpectedly small.'
}

# Check that the compiled DLL is not older than the source.
$dllTime = (Get-Item -LiteralPath $dll).LastWriteTimeUtc
$sourceItems = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\StonewardsSpectatorSwitch') -Filter '*.cs' -File)
$sourceItems += Get-Item -LiteralPath $project
$newest = $sourceItems | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($dllTime -lt $newest.LastWriteTimeUtc) {
    throw "Compiled DLL is older than the current source: $($newest.Name)"
}

$dist = Join-Path $root 'dist'
New-Item -Path $dist -ItemType Directory -Force | Out-Null
$zipPath = Join-Path $dist ("thereisnouser-SpectatorSwitch-$version.zip")
if (Test-Path -LiteralPath $zipPath) {
    throw "ZIP already exists: $zipPath (remove it explicitly before repackaging)."
}

$items = @(
    @{ Source = $manifestPath; Entry = 'manifest.json' },
    @{ Source = $iconPath; Entry = 'icon.png' },
    @{ Source = $readmePath; Entry = 'README.md' },
    @{ Source = $changelogPath; Entry = 'CHANGELOG.md' },
    @{ Source = $dll; Entry = 'plugins/StonewardsSpectatorSwitch/StonewardsSpectatorSwitch.dll' }
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tmpPath = "$zipPath.tmp"
$archive = $null
$check = $null
try {
    $archive = [System.IO.Compression.ZipFile]::Open(
        $tmpPath, [System.IO.Compression.ZipArchiveMode]::Create)
    foreach ($item in $items) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $item.Source, $item.Entry,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $archive.Dispose()
    $archive = $null

    $check = [System.IO.Compression.ZipFile]::OpenRead($tmpPath)
    $actual = @($check.Entries | ForEach-Object { $_.FullName })
    $expected = @($items | ForEach-Object { $_.Entry })
    if ($actual.Count -ne $expected.Count -or
        @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual).Count -gt 0) {
        throw 'Unexpected Thunderstore ZIP file layout.'
    }
    $check.Dispose()
    $check = $null
    Move-Item -LiteralPath $tmpPath -Destination $zipPath
}
finally {
    if ($null -ne $check) { $check.Dispose() }
    if ($null -ne $archive) { $archive.Dispose() }
    if (Test-Path -LiteralPath $tmpPath) {
        Remove-Item -LiteralPath $tmpPath -Force
    }
}

Write-Host ''
Write-Host "Thunderstore ZIP: $zipPath" -ForegroundColor Green
Write-Host "Version: $version | Icon: $($width)x$($height) | Files: $($items.Count)" -ForegroundColor Cyan
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
Write-Host 'Package created locally. Not uploaded to Thunderstore or GitHub Releases.' -ForegroundColor Yellow
