param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [string]$UnrealPakSource = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$projectFile = Join-Path $projectRoot "ScumPakWizard.csproj"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = "D:\SCUM_MOD_FACTORY\deliveries\ScumPakWizard-win64"
}

function Resolve-UnrealPakSource([string]$inputPath) {
    if (-not [string]::IsNullOrWhiteSpace($inputPath)) {
        return $inputPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNREALPAK_SOURCE)) {
        return $env:UNREALPAK_SOURCE
    }

    $bundled = Join-Path $projectRoot "tools\UnrealPak"
    if (Test-Path (Join-Path $bundled "UnrealPak.exe")) {
        return $bundled
    }

    $epicRoot = "C:\Program Files\Epic Games"
    if (Test-Path $epicRoot) {
        $ueDirs = Get-ChildItem -Path $epicRoot -Directory -Filter "UE_*" | Sort-Object -Property Name -Descending
        foreach ($dir in $ueDirs) {
            $candidate = Join-Path $dir.FullName "Engine\Binaries\Win64"
            if (Test-Path (Join-Path $candidate "UnrealPak.exe")) {
                return $candidate
            }
        }
    }

    $fallback = "C:\Program Files\Epic Games\UE_4.27\Engine\Binaries\Win64"
    if (Test-Path (Join-Path $fallback "UnrealPak.exe")) {
        return $fallback
    }

    return ""
}

$resolvedUnrealPakSource = Resolve-UnrealPakSource -inputPath $UnrealPakSource
if ([string]::IsNullOrWhiteSpace($resolvedUnrealPakSource)) {
    throw "UnrealPak source not found. Use -UnrealPakSource '...\Engine\Binaries\Win64' or set `$env:UNREALPAK_SOURCE."
}

if (-not (Test-Path (Join-Path $resolvedUnrealPakSource "UnrealPak.exe"))) {
    throw "UnrealPak.exe missing in: $resolvedUnrealPakSource"
}

$publishTemp = Join-Path $projectRoot "bin\publish-temp"
if (Test-Path $publishTemp) {
    Remove-Item -Recurse -Force $publishTemp
}
New-Item -ItemType Directory -Path $publishTemp | Out-Null

dotnet publish $projectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishTemp

if (Test-Path $OutputRoot) {
    Remove-Item -Recurse -Force $OutputRoot
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

Copy-Item -Path (Join-Path $publishTemp "*") -Destination $OutputRoot -Recurse -Force

$targetWin64Dir = Join-Path $OutputRoot "Engine\Binaries\Win64"
New-Item -ItemType Directory -Path $targetWin64Dir -Force | Out-Null

$requiredCoreFiles = @(
    "UnrealPak.exe",
    "UnrealPak.modules",
    "UnrealPak.target",
    "UnrealPak.version"
)

foreach ($name in $requiredCoreFiles) {
    $src = Join-Path $resolvedUnrealPakSource $name
    if (-not (Test-Path $src)) {
        throw "Missing required UnrealPak runtime file: $src"
    }
    Copy-Item -LiteralPath $src -Destination (Join-Path $targetWin64Dir $name) -Force
}

Get-ChildItem -Path $resolvedUnrealPakSource -File | Where-Object { $_.Name -match '^UnrealPak-.*\.dll$' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetWin64Dir $_.Name) -Force
}

$engineRoot = (Resolve-Path (Join-Path $resolvedUnrealPakSource "..\..")).Path
$engineConfigSource = Join-Path $engineRoot "Config"
if (-not (Test-Path $engineConfigSource)) {
    throw "Engine\\Config not found in UnrealPak source root: $engineConfigSource"
}

$engineConfigTarget = Join-Path $OutputRoot "Engine\Config"
Copy-Item -Path $engineConfigSource -Destination $engineConfigTarget -Recurse -Force

if (-not (Test-Path (Join-Path $targetWin64Dir "UnrealPak.exe"))) {
    throw "Failed to copy UnrealPak.exe into release package."
}

$zipPath = "$OutputRoot.zip"
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path (Join-Path $OutputRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release ready:"
Write-Host "Folder: $OutputRoot"
Write-Host "Zip:    $zipPath"
