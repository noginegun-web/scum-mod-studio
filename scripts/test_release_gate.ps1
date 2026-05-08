param(
    [string]$Configuration = "Release",
    [string]$AppExe = "",
    [int]$Port = 49321,
    [int]$SchemaSampleSize = 12,
    [switch]$FullAssetAudit,
    [int]$AuditThrottle = 8,
    [int]$MinFullAuditAssets = 500,
    [string]$AuditReportPath = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$projectFile = Join-Path $projectRoot "ScumPakWizard.csproj"

function Invoke-Step([string]$Name, [scriptblock]$Block) {
    Write-Host "==> $Name"
    & $Block
}

function Test-HttpJson([string]$Uri, [int]$TimeoutSec = 30) {
    Invoke-RestMethod -Uri $Uri -TimeoutSec $TimeoutSec
}

function Wait-AppReady([string]$BaseUrl) {
    for ($i = 0; $i -lt 45; $i++) {
        try {
            $health = Test-HttpJson "$BaseUrl/health" 3
            if ($health.ok) {
                return
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "App did not become healthy at $BaseUrl."
}

function Get-ModAssets([string]$BaseUrl) {
    $pageSize = 300
    $first = Test-HttpJson "$BaseUrl/api/modding/assets?page=1&pageSize=$pageSize" 120
    $total = [int]$first.total
    $pages = [math]::Ceiling($total / $pageSize)
    $assets = @($first.items)
    for ($page = 2; $page -le $pages; $page++) {
        $assets += (Test-HttpJson "$BaseUrl/api/modding/assets?page=$page&pageSize=$pageSize" 120).items
    }

    return $assets
}

function Invoke-SchemaAudit([string]$BaseUrl, [object[]]$Assets, [int]$Throttle) {
    $Assets | ForEach-Object -Parallel {
        $asset = $_
        $baseUrl = $using:BaseUrl
        $uri = "$baseUrl/api/modding/schema?assetId=$([uri]::EscapeDataString([string]$asset.assetId))"
        try {
            $schema = Invoke-RestMethod -Uri $uri -TimeoutSec 180
            $fields = @($schema.fields).Count
            $lists = @($schema.listTargets).Count
            [pscustomobject]@{
                ok = $true
                assetId = [string]$asset.assetId
                categoryId = [string]$asset.categoryId
                displayName = [string]$asset.displayName
                relativePath = [string]$asset.relativePath
                fieldCount = $fields
                listTargetCount = $lists
                usable = ($fields -gt 0 -or $lists -gt 0)
                warnings = (@($schema.warnings) -join " | ")
                error = $null
            }
        } catch {
            [pscustomobject]@{
                ok = $false
                assetId = [string]$asset.assetId
                categoryId = [string]$asset.categoryId
                displayName = [string]$asset.displayName
                relativePath = [string]$asset.relativePath
                fieldCount = 0
                listTargetCount = 0
                usable = $false
                warnings = ""
                error = $_.Exception.Message
            }
        }
    } -ThrottleLimit $Throttle
}

Invoke-Step "dotnet build" {
    dotnet build $projectFile -c $Configuration --no-restore
}

Invoke-Step "JavaScript syntax" {
    node --check (Join-Path $projectRoot "ui\app.js")
}

if ([string]::IsNullOrWhiteSpace($AppExe)) {
    $AppExe = Join-Path $projectRoot "bin\$Configuration\net8.0-windows\ScumPakWizard.exe"
}

if (-not (Test-Path $AppExe)) {
    throw "App executable not found: $AppExe"
}

$baseUrl = "http://127.0.0.1:$Port"
$script:startedProcess = $null
$script:assets = @()
$alreadyRunning = $false
try {
    try {
        $health = Test-HttpJson "$baseUrl/health" 3
        $alreadyRunning = [bool]$health.ok
    } catch {
        $alreadyRunning = $false
    }

    if (-not $alreadyRunning) {
        Invoke-Step "start app" {
            $script:startedProcess = Start-Process -FilePath $AppExe -ArgumentList "--no-browser" -WorkingDirectory (Split-Path $AppExe) -WindowStyle Hidden -PassThru
            Wait-AppReady $baseUrl
        }
    }

    Invoke-Step "status smoke" {
        $status = Test-HttpJson "$baseUrl/api/status" 120
        if (-not $status.scumFound) {
            throw "SCUM installation was not found."
        }
        if (-not $status.unrealPakFound) {
            throw "UnrealPak was not found."
        }
        Write-Host "SCUM: $($status.scumRoot)"
        Write-Host "Build: $($status.buildId)"
        Write-Host "UnrealPak: $($status.unrealPakPath)"
    }

    Invoke-Step "asset catalog smoke" {
        $script:assets = @(Get-ModAssets $baseUrl)
        if ($FullAssetAudit -and $script:assets.Count -lt $MinFullAuditAssets) {
            Write-Host "Visible assets are still in fast-start mode ($($script:assets.Count)); waiting for full catalog..."
            for ($i = 0; $i -lt 120; $i++) {
                Start-Sleep -Seconds 5
                $script:assets = @(Get-ModAssets $baseUrl)
                if ($script:assets.Count -ge $MinFullAuditAssets) {
                    break
                }
            }
        }

        if ($script:assets.Count -eq 0) {
            throw "No visible mod assets returned by API."
        }
        if ($FullAssetAudit -and $script:assets.Count -lt $MinFullAuditAssets) {
            throw "Full catalog did not become ready. Visible assets: $($script:assets.Count), expected at least $MinFullAuditAssets."
        }
        Write-Host "Visible assets: $($script:assets.Count)"
    }

    $auditTargets = if ($FullAssetAudit) {
        $script:assets
    } else {
        $script:assets | Group-Object categoryId | ForEach-Object {
            $_.Group | Select-Object -First ([math]::Max(1, $SchemaSampleSize))
        }
    }

    Invoke-Step "schema audit" {
        $audit = @(Invoke-SchemaAudit $baseUrl $auditTargets $AuditThrottle)
        $empty = @($audit | Where-Object { $_.ok -and -not $_.usable })
        $errors = @($audit | Where-Object { -not $_.ok })

        if ([string]::IsNullOrWhiteSpace($AuditReportPath)) {
            $auditRoot = Join-Path $projectRoot ".codex-audit"
            New-Item -ItemType Directory -Force -Path $auditRoot | Out-Null
            $scope = if ($FullAssetAudit) { "full" } else { "sample" }
            $AuditReportPath = Join-Path $auditRoot "release-gate-schema-$scope.json"
        }

        $summary = [pscustomobject]@{
            generatedUtc = [DateTimeOffset]::UtcNow.ToString("o")
            fullAssetAudit = [bool]$FullAssetAudit
            total = $audit.Count
            usable = @($audit | Where-Object usable).Count
            empty = $empty.Count
            errors = $errors.Count
            byCategory = @($audit | Group-Object categoryId | ForEach-Object {
                [pscustomobject]@{
                    categoryId = $_.Name
                    count = $_.Count
                    usable = @($_.Group | Where-Object usable).Count
                    empty = @($_.Group | Where-Object { $_.ok -and -not $_.usable }).Count
                    errors = @($_.Group | Where-Object { -not $_.ok }).Count
                }
            })
            emptyExamples = @($empty | Select-Object -First 30 categoryId,relativePath,displayName,warnings)
            errorExamples = @($errors | Select-Object -First 30 categoryId,relativePath,displayName,error)
        }

        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $AuditReportPath -Encoding UTF8
        Write-Host "Schema report: $AuditReportPath"
        Write-Host "Usable: $($summary.usable) / $($summary.total); empty: $($summary.empty); errors: $($summary.errors)"

        if ($errors.Count -gt 0) {
            throw "Schema audit found $($errors.Count) schema errors."
        }

        if ($empty.Count -gt 0) {
            throw "Schema audit found $($empty.Count) visible assets without fields or editable lists."
        }
    }
}
finally {
    if ($script:startedProcess -and -not $script:startedProcess.HasExited) {
        Stop-Process -Id $script:startedProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Release gate passed."
