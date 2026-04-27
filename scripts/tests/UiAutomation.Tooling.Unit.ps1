param(
    [string]$OutputRoot = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not ("System.Drawing.Color" -as [type])) {
    Add-Type -AssemblyName System.Drawing
}

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

function New-TestBitmap {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [System.Drawing.Color]$Color,
        [int]$Width = 12,
        [int]$Height = 12
    )

    if (-not ("System.Drawing.Bitmap" -as [type])) {
        Add-Type -AssemblyName System.Drawing
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    try {
        for ($y = 0; $y -lt $Height; $y++) {
            for ($x = 0; $x -lt $Width; $x++) {
                $bitmap.SetPixel($x, $y, $Color)
            }
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }

    return $Path
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

    Invoke-RegressionStep -Name "cursor-position-shape" -ScriptBlock {
        $cursor = Get-UiCursorPosition
        Assert-RegressionCondition ($null -ne $cursor) "Get-UiCursorPosition did not return a cursor payload."
        Assert-RegressionCondition ($cursor.PSObject.Properties.Name -contains "X") "Cursor payload did not include X."
        Assert-RegressionCondition ($cursor.PSObject.Properties.Name -contains "Y") "Cursor payload did not include Y."
        return $cursor
    } | Out-Null

    Invoke-RegressionStep -Name "capability-definitions-shape" -ScriptBlock {
        $definitions = @(Get-UiCapabilityDefinitions)
        Assert-RegressionCondition ($definitions.Count -ge 5) "Capability definitions returned fewer items than expected."
        foreach ($required in @("BurstCapture", "AutoCaptureOnFailure", "VideoCapture", "AudioCapture", "MouseTrace")) {
            Assert-RegressionCondition (@($definitions | Where-Object Name -eq $required).Count -eq 1) "Capability definitions are missing '$required'."
        }

        $burstDefinition = @($definitions | Where-Object Name -eq "BurstCapture" | Select-Object -First 1)
        Assert-RegressionCondition ($burstDefinition.Count -eq 1) "BurstCapture definition could not be resolved."
        Assert-RegressionCondition ([int]$burstDefinition[0].DefaultFrameCount -ge 2) "BurstCapture definition should expose a multi-frame default."
        Assert-RegressionCondition ([int]$burstDefinition[0].DefaultIntervalMs -ge 0) "BurstCapture definition returned an invalid interval."

        $mouseTraceDefinition = @($definitions | Where-Object Name -eq "MouseTrace" | Select-Object -First 1)
        Assert-RegressionCondition ($mouseTraceDefinition.Count -eq 1) "MouseTrace definition could not be resolved."
        Assert-RegressionCondition ([string]$mouseTraceDefinition[0].ProviderState -eq "available") "MouseTrace should already be exposed as available."
        Assert-RegressionCondition ([int]$mouseTraceDefinition[0].DefaultSampleCount -ge 2) "MouseTrace definition should expose a multi-sample default."
        Assert-RegressionCondition ([int]$mouseTraceDefinition[0].DefaultIntervalMs -ge 0) "MouseTrace definition returned an invalid interval."

        return $definitions
    } | Out-Null

    Invoke-RegressionStep -Name "capability-session-lifecycle" -ScriptBlock {
        $started = Start-UiCapabilitySession -ProcessId 0 -Mode "tooling-unit" -Reason "unit-lifecycle" -ForceNew
        Assert-RegressionCondition ($started.IsActive) "Capability session did not start as active."
        $sessionPath = Get-UiCapabilitySessionPath
        Assert-RegressionCondition (Test-Path -LiteralPath $sessionPath) "Capability session file was not created."
        $current = Get-UiCapabilitySessionState
        Assert-RegressionCondition ($null -ne $current) "Capability session state could not be read after start."
        Assert-RegressionCondition ($current.SessionId -eq $started.SessionId) "Capability session state returned a different session id."
        $stopped = Stop-UiCapabilitySession -Reason "unit-lifecycle-stop"
        Assert-RegressionCondition (-not $stopped.IsActive) "Capability session did not stop cleanly."
        return $sessionPath
    } | Out-Null

    Invoke-RegressionStep -Name "capability-enable-disable-cycle" -ScriptBlock {
        try {
            $enabled = Enable-UiCapability -CapabilityName "BurstCapture" -Reason "unit-enable" -LeaseMilliseconds 250
            $activeNames = @($enabled.Session.ActiveCapabilities | ForEach-Object { $_.Name })
            Assert-RegressionCondition ($activeNames -contains "BurstCapture") "BurstCapture was not added to the active capability list."
            $disabled = Disable-UiCapability -CapabilityName "BurstCapture" -Reason "unit-disable"
            $remainingNames = @($disabled.ActiveCapabilities | ForEach-Object { $_.Name })
            Assert-RegressionCondition ($remainingNames -notcontains "BurstCapture") "BurstCapture was not removed from the active capability list."
            return $disabled
        }
        finally {
            Stop-UiCapabilitySession -Reason "unit-enable-disable-cleanup" | Out-Null
        }
    } | Out-Null

    Invoke-RegressionStep -Name "capability-lease-expiry" -ScriptBlock {
        try {
            Enable-UiCapability -CapabilityName "AutoCaptureOnFailure" -Reason "unit-expiry" -LeaseMilliseconds 60 | Out-Null
            Start-Sleep -Milliseconds 120
            $session = Invoke-UiCapabilityBrokerSweep -Persist
            $activeNames = if ($null -ne $session -and $null -ne $session.ActiveCapabilities) { @($session.ActiveCapabilities | ForEach-Object { $_.Name }) } else { @() }
            Assert-RegressionCondition ($activeNames -notcontains "AutoCaptureOnFailure") "Expired capability lease remained active after broker sweep."
            return $session
        }
        finally {
            Stop-UiCapabilitySession -Reason "unit-expiry-cleanup" | Out-Null
        }
    } | Out-Null

    Invoke-RegressionStep -Name "capability-observations-read-maxcount" -ScriptBlock {
        $entries = @(Get-UiCapabilityObservationEntries -MaxCount 3)
        Assert-RegressionCondition ($entries.Count -le 3) "Get-UiCapabilityObservationEntries exceeded the requested MaxCount."
        return $entries
    } | Out-Null

    Invoke-RegressionStep -Name "contact-sheet-generation" -ScriptBlock {
        $artifactsRoot = Join-Path $resolvedOutputRoot "tooling-unit-artifacts"
        $firstImage = New-TestBitmap -Path (Join-Path $artifactsRoot "sheet-a.png") -Color ([System.Drawing.Color]::FromArgb(255, 52, 152, 219))
        $secondImage = New-TestBitmap -Path (Join-Path $artifactsRoot "sheet-b.png") -Color ([System.Drawing.Color]::FromArgb(255, 46, 204, 113))
        $sheetPath = Join-Path $artifactsRoot "sheet-output.png"
        $created = New-UiContactSheet -ImagePaths @($firstImage, $secondImage) -DestinationPath $sheetPath -Columns 2
        Assert-RegressionCondition (Test-Path -LiteralPath $created) "New-UiContactSheet did not create the destination image."
        return $created
    } | Out-Null

    Invoke-RegressionStep -Name "compare-images-detect-difference" -ScriptBlock {
        $artifactsRoot = Join-Path $resolvedOutputRoot "tooling-unit-artifacts"
        $referencePath = New-TestBitmap -Path (Join-Path $artifactsRoot "compare-reference.png") -Color ([System.Drawing.Color]::FromArgb(255, 52, 152, 219))
        $actualPath = New-TestBitmap -Path (Join-Path $artifactsRoot "compare-actual.png") -Color ([System.Drawing.Color]::FromArgb(255, 231, 76, 60))
        $diffPath = Join-Path $artifactsRoot "compare-diff.png"
        $comparison = Compare-UiImages -ReferencePath $referencePath -ActualPath $actualPath -DiffPath $diffPath -Tolerance 5 -SampleStep 1
        Assert-RegressionCondition ($comparison.ChangedPixels -gt 0) "Compare-UiImages did not detect the intentional color difference."
        Assert-RegressionCondition ($comparison.DifferenceRatio -gt 0) "Compare-UiImages returned a zero difference ratio for different images."
        Assert-RegressionCondition (Test-Path -LiteralPath $diffPath) "Compare-UiImages did not create the diff image."
        return $comparison
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
