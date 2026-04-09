param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Channel = "win",
    [string]$PackId = "ScumModStudio",
    [string]$PackTitle = "SCUM Mod Studio",
    [string]$PackAuthors = "SCUM Mod Studio",
    [string]$OutputRoot = "",
    [string]$RepoUrl = "",
    [string]$GithubToken = "",
    [string]$ReleaseNotesPath = "",
    [string]$UnrealPakSource = "",
    [switch]$PublishToGithub
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$projectFile = Join-Path $projectRoot "ScumPakWizard.csproj"
$basePublishScript = Join-Path $scriptDir "publish_release.ps1"

if (-not (Test-Path $basePublishScript)) {
    throw "Base publish script not found: $basePublishScript"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "builds\velopack-release"
}

$version = ([xml](Get-Content $projectFile)).Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version is missing in $projectFile"
}

$appDir = Join-Path $OutputRoot "app"
$velopackDir = Join-Path $OutputRoot "releases"

if (Test-Path $OutputRoot) {
    Remove-Item -Recurse -Force $OutputRoot
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

& $basePublishScript `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -OutputRoot $appDir `
    -UnrealPakSource $UnrealPakSource

$appSettingsPath = Join-Path $appDir "appsettings.json"
if (Test-Path $appSettingsPath) {
    $json = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
    if (-not $json.AppUpdate) {
        $json | Add-Member -MemberType NoteProperty -Name AppUpdate -Value ([pscustomobject]@{})
    }

    $json.AppUpdate.Enabled = $true
    if (-not [string]::IsNullOrWhiteSpace($RepoUrl)) {
        $json.AppUpdate.RepoUrl = $RepoUrl
    }

    $json | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $appSettingsPath -Encoding UTF8
}

Push-Location $projectRoot
try {
    dotnet tool restore

    $packArgs = @(
        "tool", "run", "vpk", "pack",
        "--packId", $PackId,
        "--packVersion", $version,
        "--packDir", $appDir,
        "--mainExe", "ScumPakWizard.exe",
        "--packTitle", $PackTitle,
        "--packAuthors", $PackAuthors,
        "--runtime", $Runtime,
        "--channel", $Channel,
        "--outputDir", $velopackDir
    )

    if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
        $packArgs += @("--releaseNotes", $ReleaseNotesPath)
    }

    if ($PublishToGithub) {
        if ([string]::IsNullOrWhiteSpace($RepoUrl)) {
            throw "RepoUrl is required when -PublishToGithub is used."
        }

        if ([string]::IsNullOrWhiteSpace($GithubToken)) {
            $GithubToken = $env:GITHUB_TOKEN
        }

        if ([string]::IsNullOrWhiteSpace($GithubToken)) {
            $GithubToken = $env:GH_TOKEN
        }

        if ([string]::IsNullOrWhiteSpace($GithubToken)) {
            throw "GitHub token is required for upload. Use -GithubToken or set GITHUB_TOKEN / GH_TOKEN."
        }

        $downloadArgs = @(
            "tool", "run", "vpk", "download", "github",
            "--repoUrl", $RepoUrl,
            "--outputDir", $velopackDir,
            "--channel", $Channel,
            "--token", $GithubToken
        )

        dotnet @downloadArgs
    }

    dotnet @packArgs

    if ($PublishToGithub) {
        $uploadArgs = @(
            "tool", "run", "vpk", "upload", "github",
            "--repoUrl", $RepoUrl,
            "--outputDir", $velopackDir,
            "--channel", $Channel,
            "--token", $GithubToken,
            "--publish",
            "--releaseName", "$PackTitle $version",
            "--tag", "v$version"
        )

        dotnet @uploadArgs
    }
}
finally {
    Pop-Location
}

Write-Host "Velopack release ready:"
Write-Host "App folder: $appDir"
Write-Host "Release files: $velopackDir"
if ($PublishToGithub) {
    Write-Host "Published to: $RepoUrl"
}
