#Requires -Version 5.1
<#
.SYNOPSIS
    Builds, zips and generates a N.I.N.A. plugin manifest for Naztronomy Tools.

.DESCRIPTION
    Produces an ARCHIVE-type release under artifacts/release:
      - <AssemblyName>-<version>.zip          (the plugin DLL + pdb)
      - <AssemblyName>-<version>.manifest.json (NINA repository manifest with SHA256)

    Plugin metadata (name, author, GUID, version, URLs, descriptions, minimum NINA
    version) is read from Properties/AssemblyInfo.cs so there is a single source of truth.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER InstallerUrl
    The public URL the release .zip will be downloadable from. Written into the
    manifest's Installer.URL. Can be left empty and filled in later.

.EXAMPLE
    ./Package-Plugin.ps1 -InstallerUrl "https://github.com/Naztronaut/NINA-Plugins/releases/download/v1.0.0.0/Naztronomy.NINA.Tools-1.0.0.0.zip"
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$InstallerUrl = ''
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$assemblyName = 'Naztronomy.NINA.Tools'
$targetFramework = 'net8.0-windows7.0'

$projectDir = Join-Path $root $assemblyName
$projectFile = Join-Path $projectDir "$assemblyName.csproj"
$assemblyInfoPath = Join-Path $projectDir 'Properties\AssemblyInfo.cs'

$info = Get-Content -LiteralPath $assemblyInfoPath -Raw

function Get-Match([string]$pattern) {
    if ($info -match $pattern) { return $Matches[1] }
    return ''
}

function Get-Meta([string]$key) {
    $escaped = [regex]::Escape($key)
    # Verbatim (@"...") first, then a normal string.
    $verbatim = '(?s)AssemblyMetadata\("' + $escaped + '",\s*@"((?:[^"]|"")*)"\)'
    if ($info -match $verbatim) { return ($Matches[1] -replace '""', '"') }
    $normal = 'AssemblyMetadata\("' + $escaped + '",\s*"((?:\\.|[^"\\])*)"\)'
    if ($info -match $normal) { return ($Matches[1] -replace '\\"', '"' -replace '\\\\', '\') }
    return ''
}

function ConvertTo-Version([string]$text) {
    $p = $text.Split('.')
    return [ordered]@{
        Major = [int]$p[0]
        Minor = [int]$p[1]
        Patch = [int]$p[2]
        Build = [int]$p[3]
    }
}

$guid = Get-Match 'Guid\("([^"]+)"\)'
$version = Get-Match 'AssemblyFileVersion\("([^"]+)"\)'
$name = Get-Match 'AssemblyTitle\("([^"]+)"\)'
$author = Get-Match 'AssemblyCompany\("([^"]+)"\)'
$shortDescription = Get-Match 'AssemblyDescription\("((?:\\.|[^"\\])*)"\)'
$shortDescription = $shortDescription -replace '\\"', '"' -replace '\\\\', '\'

if ([string]::IsNullOrWhiteSpace($guid) -or [string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read GUID/version from $assemblyInfoPath"
}

Write-Host "Building $assemblyName $version ($Configuration)..."
& dotnet build $projectFile -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$dll = Join-Path $projectDir "bin\$Configuration\$targetFramework\$assemblyName.dll"
$pdb = Join-Path $projectDir "bin\$Configuration\$targetFramework\$assemblyName.pdb"
if (!(Test-Path -LiteralPath $dll)) { throw "Built assembly not found: $dll" }

$artifacts = Join-Path $root 'artifacts'
$release = Join-Path $artifacts 'release'
$staging = Join-Path $artifacts "staging\$assemblyName"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null
New-Item -ItemType Directory -Path $release -Force | Out-Null

Copy-Item -LiteralPath $dll -Destination $staging
if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination $staging }

$zipPath = Join-Path $release "$assemblyName-$version.zip"
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force

$checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()

$tags = @()
$tagString = Get-Meta 'Tags'
if (-not [string]::IsNullOrWhiteSpace($tagString)) {
    $tags = @($tagString.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

$manifest = [ordered]@{
    Name                      = $name
    Identifier                = ([guid]$guid).ToString('D')
    Version                   = ConvertTo-Version $version
    Author                    = $author
    Homepage                  = Get-Meta 'Homepage'
    Repository                = Get-Meta 'Repository'
    License                   = Get-Meta 'License'
    LicenseURL                = Get-Meta 'LicenseURL'
    ChangelogURL              = Get-Meta 'ChangelogURL'
    Tags                      = $tags
    MinimumApplicationVersion = ConvertTo-Version (Get-Meta 'MinimumApplicationVersion')
    Descriptions              = [ordered]@{
        ShortDescription = $shortDescription
        LongDescription  = Get-Meta 'LongDescription'
        FeaturedImageURL = Get-Meta 'FeaturedImageURL'
        ScreenshotURL    = Get-Meta 'ScreenshotURL'
        AltScreenshotURL = Get-Meta 'AltScreenshotURL'
    }
    Installer                 = [ordered]@{
        URL          = $InstallerUrl
        Type         = 'ARCHIVE'
        Checksum     = $checksum
        ChecksumType = 'SHA256'
    }
}

$manifestPath = Join-Path $release "$assemblyName-$version.manifest.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host ''
Write-Host "Created:"
Write-Host "  $zipPath"
Write-Host "  $manifestPath"
Write-Host "  SHA256: $checksum"
if ([string]::IsNullOrWhiteSpace($InstallerUrl)) {
    Write-Warning "Installer.URL is empty. Re-run with -InstallerUrl once the release asset URL is known, or edit the manifest."
}
