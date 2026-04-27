param(
    [string]$OutputRoot = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$modulePath = Join-Path $repoRoot "scripts\UIAutomation.Acceptance.psm1"
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$summaryPath = Join-Path $resolvedOutputRoot "tooling-unit-summary.md"
$results = New-Object System.Collections.Generic.List[object]
$suiteFailed = $false
$failureMessage = $null

function Assert-RegressionCondition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,
        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Add-RegressionResult {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [bool]$Success,
        [double]$DurationMs = 0,
        [string]$Notes = ""
    )

    [void]$results.Add([pscustomobject]@{
        Name = $Name
        Success = $Success
        DurationMs = [math]::Round($DurationMs, 2)
        Notes = $Notes
    })
}

function Invoke-RegressionStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $ScriptBlock
        $stopwatch.Stop()
        Add-RegressionResult -Name $Name -Success $true -DurationMs $stopwatch.Elapsed.TotalMilliseconds
        return $result
    }
    catch {
        $stopwatch.Stop()
        Add-RegressionResult -Name $Name -Success $false -DurationMs $stopwatch.Elapsed.TotalMilliseconds -Notes $_.Exception.Message
        throw
    }
}

function Write-RegressionSummary {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# UI Tooling Unit Summary")
    [void]$lines.Add("")
    [void]$lines.Add("- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")")
    [void]$lines.Add("- Passed: $(@($results | Where-Object Success).Count)")
    [void]$lines.Add("- Failed: $(@($results | Where-Object { -not $_.Success }).Count)")
    [void]$lines.Add("")
    [void]$lines.Add("| Step | Result | Duration (ms) | Notes |")
    [void]$lines.Add("| --- | --- | ---: | --- |")

    foreach ($item in $results) {
        $resultLabel = if ($item.Success) { "PASS" } else { "FAIL" }
        $notes = if ([string]::IsNullOrWhiteSpace($item.Notes)) { "" } else { $item.Notes.Replace("|", "/") }
        [void]$lines.Add("| $($item.Name) | $resultLabel | $($item.DurationMs) | $notes |")
    }

    if ($suiteFailed -and -not [string]::IsNullOrWhiteSpace($failureMessage)) {
        [void]$lines.Add("")
        [void]$lines.Add("## Failure")
        [void]$lines.Add("")
        [void]$lines.Add($failureMessage)
    }

    [System.IO.File]::WriteAllLines($summaryPath, $lines)
}

try {
    Import-Module $modulePath -Force

    $supportedApi = Invoke-RegressionStep -Name "supported-api-categories" -ScriptBlock {
        $payload = @(Get-UiSupportedApi)
        Assert-RegressionCondition ($payload.Count -ge 6) "Supported API catalog returned fewer categories than expected."
        $categories = @($payload | ForEach-Object { $_.Category })
        foreach ($required in @("Session", "Diagnostics", "Windows", "Elements", "Dialogs", "Capture")) {
            Assert-RegressionCondition ($categories -contains $required) "Supported API catalog is missing category '$required'."
        }

        return $payload
    }

    Invoke-RegressionStep -Name "supported-api-no-duplicates" -ScriptBlock {
        $commands = @($supportedApi | ForEach-Object { @($_.Commands) })
        $duplicates = @($commands | Group-Object | Where-Object { $_.Count -gt 1 })
        Assert-RegressionCondition ($duplicates.Count -eq 0) "Supported API catalog contains duplicate command names."
        return $commands
    } | Out-Null

    Invoke-RegressionStep -Name "exported-commands-match-supported-api" -ScriptBlock {
        $supportedCommands = @($supportedApi | ForEach-Object { @($_.Commands) } | Sort-Object -Unique)
        $exportedCommands = @((Get-Command -Module UIAutomation.Acceptance | Select-Object -ExpandProperty Name) | Sort-Object -Unique)
        $expected = @("Get-UiSupportedApi") + $supportedCommands
        $missing = @($expected | Where-Object { $_ -notin $exportedCommands })
        $unexpected = @($exportedCommands | Where-Object { $_ -notin $expected })
        Assert-RegressionCondition ($missing.Count -eq 0) "Module exports are missing supported commands: $($missing -join ', ')."
        Assert-RegressionCondition ($unexpected.Count -eq 0) "Module exports contain unexpected commands: $($unexpected -join ', ')."
        return $exportedCommands
    } | Out-Null

    Invoke-RegressionStep -Name "repo-root-points-to-workspace" -ScriptBlock {
        $resolved = Get-UiAcceptanceRepoRoot
        Assert-RegressionCondition ([System.IO.Path]::GetFullPath($resolved) -eq $repoRoot) "Get-UiAcceptanceRepoRoot did not resolve the current repository root."
        return $resolved
    } | Out-Null

    Invoke-RegressionStep -Name "diagnostics-paths-shape" -ScriptBlock {
        $paths = Get-UiDiagnosticsPaths
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace($paths.StorageRoot)) "Diagnostics paths did not include StorageRoot."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace($paths.LogsRoot)) "Diagnostics paths did not include LogsRoot."
        Assert-RegressionCondition ($paths.ShellStatePath -like "*.json") "Diagnostics paths returned an unexpected ShellStatePath."
        Assert-RegressionCondition ($paths.EventLogPath -like "*.jsonl") "Diagnostics paths returned an unexpected EventLogPath."
        return $paths
    } | Out-Null

    Invoke-RegressionStep -Name "calibration-profile-shape" -ScriptBlock {
        $profile = Get-UiCalibrationProfile
        Assert-RegressionCondition ($null -ne $profile.latencyThresholdsMs) "Calibration profile is missing latency thresholds."
        Assert-RegressionCondition ([int]$profile.latencyThresholdsMs.acceptable -gt 0) "Calibration acceptable threshold is invalid."
        Assert-RegressionCondition ([int]$profile.latencyThresholdsMs.slow -gt [int]$profile.latencyThresholdsMs.acceptable) "Calibration slow threshold should be greater than acceptable."
        return $profile
    } | Out-Null

    Invoke-RegressionStep -Name "timeline-read-maxcount" -ScriptBlock {
        $entries = @(Get-UiTimelineEntries -MaxCount 3)
        Assert-RegressionCondition ($entries.Count -le 3) "Get-UiTimelineEntries exceeded the requested MaxCount."
        return $entries
    } | Out-Null

    Invoke-RegressionStep -Name "performance-summary-shape" -ScriptBlock {
        $summary = Get-UiPerformanceSummary -MaxCount 5
        Assert-RegressionCondition ($null -ne $summary.Thresholds) "Performance summary is missing top-level thresholds."
        Assert-RegressionCondition ($null -ne $summary.Actions) "Performance summary is missing grouped actions."
        Assert-RegressionCondition ($summary.TotalActions -ge 0) "Performance summary returned an invalid total action count."
        return $summary
    } | Out-Null
}
catch {
    $suiteFailed = $true
    $failureMessage = $_.Exception.Message
}
finally {
    Write-RegressionSummary
}

if ($suiteFailed) {
    throw "UI tooling unit suite failed. See $summaryPath"
}

[pscustomobject]@{
    Summary = $summaryPath
    Passed = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
} | Format-List
