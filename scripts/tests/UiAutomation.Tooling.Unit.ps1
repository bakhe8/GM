param(
    [string]$OutputRoot = ".\scratch\UIAcceptance\latest",
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
        Assert-RegressionCondition ($payload.Count -ge 7) "Supported API catalog returned fewer categories than expected."
        $categories = @($payload | ForEach-Object { $_.Category })
        foreach ($required in @("Session", "Diagnostics", "Heuristics", "Windows", "Elements", "Dialogs", "Capture", "Media")) {
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

    Invoke-RegressionStep -Name "media-provider-catalog-shape" -ScriptBlock {
        $providers = @(Get-UiMediaProviderCatalog)
        Assert-RegressionCondition ($providers.Count -ge 2) "Media provider catalog returned fewer providers than expected."
        $psrProvider = @($providers | Where-Object Name -eq "Psr.ScreenTrace" | Select-Object -First 1)
        Assert-RegressionCondition ($psrProvider.Count -eq 1) "Media provider catalog is missing Psr.ScreenTrace."
        Assert-RegressionCondition ($psrProvider[0].Kind -eq "Video") "Psr.ScreenTrace should be exposed as a video provider."
        Assert-RegressionCondition ($psrProvider[0].Availability -in @("available", "unavailable")) "Psr.ScreenTrace returned an invalid availability label."
        Assert-RegressionCondition ([string]$psrProvider[0].ScopeModel -eq "global-session-attested") "Psr.ScreenTrace should expose global-session-attested scope."
        Assert-RegressionCondition (-not [bool]$psrProvider[0].SupportsProcessIsolation) "Psr.ScreenTrace should not pretend to be process-isolated."
        Assert-RegressionCondition ([bool]$psrProvider[0].SupportsForegroundAttestation) "Psr.ScreenTrace should expose foreground attestation support."
        $audioProvider = @($providers | Where-Object Name -eq "Audio.None" | Select-Object -First 1)
        Assert-RegressionCondition ($audioProvider.Count -eq 1) "Media provider catalog is missing Audio.None."
        Assert-RegressionCondition ([string]$audioProvider[0].ScopeModel -eq "planned-per-app-attested") "Audio.None should describe the planned scoped-audio model."
        Assert-RegressionCondition (-not [bool]$audioProvider[0].SupportsSystemMixCapture) "Audio.None should not imply system-mix capture support."
        return $providers
    } | Out-Null

    Invoke-RegressionStep -Name "media-session-state-shape" -ScriptBlock {
        $state = Get-UiMediaSessionState
        Assert-RegressionCondition ($null -ne $state.SessionId) "Media session state is missing SessionId."
        Assert-RegressionCondition ($null -ne $state.VideoCapture) "Media session state is missing VideoCapture."
        Assert-RegressionCondition ($null -ne $state.AudioCapture) "Media session state is missing AudioCapture."
        Assert-RegressionCondition ($state.PSObject.Properties.Name -contains "RecentArtifacts") "Media session state is missing RecentArtifacts."
        Assert-RegressionCondition ($state.VideoCapture.PSObject.Properties.Name -contains "ScopeContract") "Media session state is missing VideoCapture.ScopeContract."
        Assert-RegressionCondition ($state.AudioCapture.PSObject.Properties.Name -contains "ScopeContract") "Media session state is missing AudioCapture.ScopeContract."
        Assert-RegressionCondition ($null -ne $state.VideoCapture.ScopeContract.ScopeStatus) "VideoCapture scope contract is missing ScopeStatus."
        return $state
    } | Out-Null

    Invoke-RegressionStep -Name "media-scope-view-shape" -ScriptBlock {
        $scopeView = Get-UiMediaScopeView
        Assert-RegressionCondition ($null -ne $scopeView) "Get-UiMediaScopeView did not return a payload."
        Assert-RegressionCondition ($null -ne $scopeView.VideoCapture) "Media scope view is missing VideoCapture."
        Assert-RegressionCondition ($scopeView.VideoCapture.PSObject.Properties.Name -contains "ScopeStatus") "Media scope view is missing ScopeStatus."
        Assert-RegressionCondition ($scopeView.VideoCapture.PSObject.Properties.Name -contains "EvidenceIsolation") "Media scope view is missing EvidenceIsolation."
        Assert-RegressionCondition ($scopeView.VideoCapture.PSObject.Properties.Name -contains "TrustedForReasoning") "Media scope view is missing TrustedForReasoning."
        Assert-RegressionCondition ($null -ne $scopeView.AudioCapture) "Media scope view is missing AudioCapture."
        Assert-RegressionCondition ([string]$scopeView.AudioCapture.SourcePolicy -eq "per-app-attested-required") "Audio scope view did not expose the expected source policy."
        Assert-RegressionCondition ([string]$scopeView.AudioCapture.ScopeStatus -in @("unavailable", "blocked")) "Audio scope view should remain unavailable or blocked without a provider."
        return $scopeView
    } | Out-Null

    Invoke-RegressionStep -Name "audio-scope-policy-shape" -ScriptBlock {
        $policy = Get-UiAudioScopePolicy
        Assert-RegressionCondition ($null -ne $policy) "Get-UiAudioScopePolicy did not return a payload."
        Assert-RegressionCondition ([string]$policy.PolicyName -eq "AIFirstScopedAudio") "Audio scope policy returned an unexpected policy name."
        Assert-RegressionCondition ([string]$policy.ProviderReadiness -eq "unavailable") "Audio scope policy should remain unavailable until a provider is wired."
        Assert-RegressionCondition ([string]$policy.DesiredScopeModel -eq "per-app-attested") "Audio scope policy returned an unexpected desired scope model."
        Assert-RegressionCondition (-not [bool]$policy.AcceptsSystemMixFallback) "Audio scope policy should reject system-mix fallback."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$policy.StartBlockedReason)) "Audio scope policy should explain why start is blocked."
        return $policy
    } | Out-Null

    Invoke-RegressionStep -Name "app-log-path-shape" -ScriptBlock {
        $path = Get-UiAppLogPath
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace($path)) "Get-UiAppLogPath returned an empty path."
        Assert-RegressionCondition ($path -like "*log_*.txt") "Get-UiAppLogPath returned an unexpected filename."
        return $path
    } | Out-Null

    Invoke-RegressionStep -Name "simple-logger-line-parse" -ScriptBlock {
        $module = Get-Module UIAutomation.Acceptance
        Assert-RegressionCondition ($null -ne $module) "UIAutomation.Acceptance module was not loaded for log-line parsing."

        $parsed = & $module {
            param($line)
            ConvertFrom-UiSimpleLoggerLine -Line $line
        } "2026-04-28 09:33:22 [ERROR] [App_DispatcherUnhandledException] [App Startup] Sample failure message"

        Assert-RegressionCondition ($null -ne $parsed) "ConvertFrom-UiSimpleLoggerLine returned null."
        Assert-RegressionCondition ([string]$parsed.Level -eq "ERROR") "ConvertFrom-UiSimpleLoggerLine did not parse the level."
        Assert-RegressionCondition ([string]$parsed.Caller -eq "App_DispatcherUnhandledException") "ConvertFrom-UiSimpleLoggerLine did not parse the caller."
        Assert-RegressionCondition ([string]$parsed.Message -like "*Sample failure message") "ConvertFrom-UiSimpleLoggerLine did not parse the message tail."
        return $parsed
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
        Assert-RegressionCondition ($definitions.Count -ge 8) "Capability definitions returned fewer items than expected."
        foreach ($required in @("BurstCapture", "AutoCaptureOnFailure", "ReactiveAssist", "ExplorationAssist", "FaultWatch", "VideoCapture", "AudioCapture", "MouseTrace")) {
            Assert-RegressionCondition (@($definitions | Where-Object Name -eq $required).Count -eq 1) "Capability definitions are missing '$required'."
        }

        $burstDefinition = @($definitions | Where-Object Name -eq "BurstCapture" | Select-Object -First 1)
        Assert-RegressionCondition ($burstDefinition.Count -eq 1) "BurstCapture definition could not be resolved."
        Assert-RegressionCondition ([int]$burstDefinition[0].DefaultFrameCount -ge 2) "BurstCapture definition should expose a multi-frame default."
        Assert-RegressionCondition ([int]$burstDefinition[0].DefaultIntervalMs -ge 0) "BurstCapture definition returned an invalid interval."

        $failureDefinition = @($definitions | Where-Object Name -eq "AutoCaptureOnFailure" | Select-Object -First 1)
        Assert-RegressionCondition ($failureDefinition.Count -eq 1) "AutoCaptureOnFailure definition could not be resolved."
        Assert-RegressionCondition ([int]$failureDefinition[0].DefaultFrameCount -ge 2) "AutoCaptureOnFailure should expose a multi-frame default."

        $reactiveDefinition = @($definitions | Where-Object Name -eq "ReactiveAssist" | Select-Object -First 1)
        Assert-RegressionCondition ($reactiveDefinition.Count -eq 1) "ReactiveAssist definition could not be resolved."
        Assert-RegressionCondition ([string]$reactiveDefinition[0].ProviderState -eq "available") "ReactiveAssist should already be exposed as available."
        Assert-RegressionCondition ([int]$reactiveDefinition[0].DefaultSlowActionMs -gt 0) "ReactiveAssist should expose a positive slow-action threshold."

        $explorationAssistDefinition = @($definitions | Where-Object Name -eq "ExplorationAssist" | Select-Object -First 1)
        Assert-RegressionCondition ($explorationAssistDefinition.Count -eq 1) "ExplorationAssist definition could not be resolved."
        Assert-RegressionCondition ([string]$explorationAssistDefinition[0].ProviderState -eq "available") "ExplorationAssist should already be exposed as available."
        Assert-RegressionCondition ([int]$explorationAssistDefinition[0].DefaultLeaseMs -gt 0) "ExplorationAssist should expose a positive lease."

        $faultWatchDefinition = @($definitions | Where-Object Name -eq "FaultWatch" | Select-Object -First 1)
        Assert-RegressionCondition ($faultWatchDefinition.Count -eq 1) "FaultWatch definition could not be resolved."
        Assert-RegressionCondition ([string]$faultWatchDefinition[0].ProviderState -eq "available") "FaultWatch should already be exposed as available."
        Assert-RegressionCondition ([int]$faultWatchDefinition[0].DefaultLookbackSeconds -ge 10) "FaultWatch should expose a meaningful lookback window."
        Assert-RegressionCondition ([int]$faultWatchDefinition[0].DefaultCooldownMs -gt 0) "FaultWatch should expose a positive cooldown."

        $mouseTraceDefinition = @($definitions | Where-Object Name -eq "MouseTrace" | Select-Object -First 1)
        Assert-RegressionCondition ($mouseTraceDefinition.Count -eq 1) "MouseTrace definition could not be resolved."
        Assert-RegressionCondition ([string]$mouseTraceDefinition[0].ProviderState -eq "available") "MouseTrace should already be exposed as available."
        Assert-RegressionCondition ([int]$mouseTraceDefinition[0].DefaultSampleCount -ge 2) "MouseTrace definition should expose a multi-sample default."
        Assert-RegressionCondition ([int]$mouseTraceDefinition[0].DefaultIntervalMs -ge 0) "MouseTrace definition returned an invalid interval."

        $videoDefinition = @($definitions | Where-Object Name -eq "VideoCapture" | Select-Object -First 1)
        Assert-RegressionCondition ($videoDefinition.Count -eq 1) "VideoCapture definition could not be resolved."
        Assert-RegressionCondition ([string]$videoDefinition[0].ProviderState -in @("available", "unavailable")) "VideoCapture returned an invalid provider state."
        Assert-RegressionCondition ([string]$videoDefinition[0].EscalationLevel -eq "last-resort") "VideoCapture should be marked as a last-resort capability."
        Assert-RegressionCondition (-not [bool]$videoDefinition[0].AutoTriggerAllowed) "VideoCapture should not be auto-triggered."
        Assert-RegressionCondition ([bool]$videoDefinition[0].RequiresExplicitOperatorIntent) "VideoCapture should require explicit operator intent."
        Assert-RegressionCondition (@($videoDefinition[0].PreferredPredecessors).Count -ge 2) "VideoCapture should advertise image-first predecessors."
        Assert-RegressionCondition (@($videoDefinition[0].PreferredPredecessors) -contains "BurstCapture") "VideoCapture should prefer BurstCapture before escalation."
        Assert-RegressionCondition (@($videoDefinition[0].PreferredPredecessors) -contains "AutoCaptureOnFailure") "VideoCapture should prefer AutoCaptureOnFailure before escalation."

        $audioDefinition = @($definitions | Where-Object Name -eq "AudioCapture" | Select-Object -First 1)
        Assert-RegressionCondition ($audioDefinition.Count -eq 1) "AudioCapture definition could not be resolved."
        Assert-RegressionCondition ([string]$audioDefinition[0].ProviderState -in @("available", "unavailable")) "AudioCapture returned an invalid provider state."

        return $definitions
    } | Out-Null

    Invoke-RegressionStep -Name "fault-state-payload-shape" -ScriptBlock {
        $payload = Get-UiFaultStatePayload -ProcessId 0 -MaxResults 5
        Assert-RegressionCondition ($null -ne $payload.DiagnosticsPaths) "FaultState payload is missing DiagnosticsPaths."
        Assert-RegressionCondition ($payload.PSObject.Properties.Name -contains "RecentFaultSignals") "FaultState payload is missing RecentFaultSignals."
        Assert-RegressionCondition ($payload.PSObject.Properties.Name -contains "FaultSummary") "FaultState payload is missing FaultSummary."
        Assert-RegressionCondition ($payload.FaultSummary.SignalCount -ge 0) "FaultSummary returned an invalid signal count."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-definitions-shape" -ScriptBlock {
        $definitions = @(Get-UiExplorationHeuristicDefinitions)
        Assert-RegressionCondition ($definitions.Count -ge 4) "Heuristic definitions returned fewer items than expected."
        foreach ($required in @("runtime-fault", "external-window", "stubborn-control", "visual-anomaly")) {
            Assert-RegressionCondition (@($definitions | Where-Object Name -eq $required).Count -eq 1) "Heuristic definitions are missing '$required'."
        }

        $runtimeFault = @($definitions | Where-Object Name -eq "runtime-fault" | Select-Object -First 1)
        Assert-RegressionCondition ([string]$runtimeFault[0].RecommendedCapability -eq "FaultWatch") "runtime-fault should recommend FaultWatch."
        return $definitions
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-state-payload-shape" -ScriptBlock {
        $payload = Get-UiHeuristicStatePayload -ProcessId 0 -MaxResults 5
        Assert-RegressionCondition ($payload.PSObject.Properties.Name -contains "HeuristicDefinitions") "HeuristicState payload is missing HeuristicDefinitions."
        Assert-RegressionCondition ($payload.PSObject.Properties.Name -contains "Recommendations") "HeuristicState payload is missing Recommendations."
        Assert-RegressionCondition ($payload.PSObject.Properties.Name -contains "HeuristicOperatorView") "HeuristicState payload is missing HeuristicOperatorView."
        return $payload
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

    Invoke-RegressionStep -Name "capability-operator-view-shape" -ScriptBlock {
        $module = Get-Module UIAutomation.Acceptance
        Assert-RegressionCondition ($null -ne $module) "UIAutomation.Acceptance module was not loaded for operator view inspection."

        $session = [pscustomobject]@{
            SessionId = "unit-operator-view"
            IsActive = $true
            Mode = "free-explore"
            Reason = "unit-operator"
            ProcessId = 0
            CreatedAt = (Get-Date).ToString("o")
            UpdatedAt = (Get-Date).ToString("o")
            LastTouchedAt = (Get-Date).ToString("o")
            LastAction = "MouseHover"
            StopReason = $null
            StoppedAt = $null
            ActiveCapabilities = @(
                [pscustomobject]@{
                    Name = "ReactiveAssist"
                    Category = "Adaptive"
                    ProviderState = "available"
                    Reason = "unit-reactive"
                    LeaseMilliseconds = 2500
                    ActivatedAt = (Get-Date).AddMilliseconds(-400).ToString("o")
                    ExpiresAt = (Get-Date).AddMilliseconds(1800).ToString("o")
                    Metadata = $null
                }
            )
            CapabilityHistory = @()
            RecentArtifacts = @()
            RecentObservations = @()
            RecentDecisions = @(
                [pscustomobject]@{
                    Timestamp = (Get-Date).ToString("o")
                    CapabilityName = "ReactiveAssist"
                    Action = "MouseHover"
                    Decision = "suppressed"
                    Summary = "رُصدت anomaly لكن ReactiveAssist تجاهلت التفعيل مؤقتًا لتجنب الإزعاج المتكرر."
                    Payload = $null
                },
                [pscustomobject]@{
                    Timestamp = (Get-Date).AddMilliseconds(-300).ToString("o")
                    CapabilityName = "ReactiveAssist"
                    Action = "MouseHover"
                    Decision = "triggered"
                    Summary = "فعّلت ReactiveAssist evidence بصرية خفيفة لأن anomaly الحالية تستحق متابعة."
                    Payload = $null
                }
            )
        }

        $view = & $module {
            param($inputSession)
            Get-UiCapabilityOperatorView -SessionState $inputSession
        } $session

        Assert-RegressionCondition ([string]$view.Status -eq "cooling-down") "Operator view did not classify the synthetic session as cooling-down."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$view.Summary)) "Operator view summary was empty."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$view.SecondarySummary)) "Operator view secondary summary was empty."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$view.Guidance)) "Operator view guidance was empty."
        Assert-RegressionCondition ([int]$view.Signals.ActiveCapabilityCount -eq 1) "Operator view did not report the active capability count."
        Assert-RegressionCondition (@($view.CoolingDownCapabilities | Where-Object Name -eq "ReactiveAssist").Count -eq 1) "Operator view did not surface ReactiveAssist as cooling-down."
        Assert-RegressionCondition ([string]$view.LastDecision.Decision -eq "suppressed") "Operator view did not surface the latest decision."
        Assert-RegressionCondition (@($view.DecisionDigest).Count -ge 1) "Operator view did not expose a decision digest."
        return $view
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-operator-view-shape" -ScriptBlock {
        $module = Get-Module UIAutomation.Acceptance
        Assert-RegressionCondition ($null -ne $module) "UIAutomation.Acceptance module was not loaded for heuristic operator view inspection."

        $session = [pscustomobject]@{
            SessionId = "unit-heuristic-view"
            IsActive = $true
            Mode = "free-explore"
            Reason = "unit-heuristic"
            ProcessId = 0
            CreatedAt = (Get-Date).ToString("o")
            UpdatedAt = (Get-Date).ToString("o")
            LastTouchedAt = (Get-Date).ToString("o")
            LastAction = "Click"
            StopReason = $null
            StoppedAt = $null
            ActiveCapabilities = @(
                [pscustomobject]@{
                    Name = "ExplorationAssist"
                    Category = "Adaptive"
                    ProviderState = "available"
                    Reason = "unit-heuristic"
                    LeaseMilliseconds = 2500
                    ActivatedAt = (Get-Date).AddMilliseconds(-250).ToString("o")
                    ExpiresAt = (Get-Date).AddMilliseconds(1800).ToString("o")
                    Metadata = $null
                }
            )
            CapabilityHistory = @()
            RecentArtifacts = @()
            RecentObservations = @()
            RecentDecisions = @(
                [pscustomobject]@{
                    Timestamp = (Get-Date).ToString("o")
                    CapabilityName = "ExplorationAssist"
                    Action = "Click"
                    Decision = "guided"
                    Summary = "اختارت heuristics مسار stubborn-control."
                    Payload = [pscustomobject]@{
                        StrategyName = "stubborn-control"
                        SuggestedActions = @("MouseClick", "MouseHover")
                    }
                }
            )
        }

        $view = & $module {
            param($inputSession)
            Get-UiHeuristicOperatorView -SessionState $inputSession
        } $session

        Assert-RegressionCondition ([string]$view.Status -eq "guided") "Heuristic operator view did not classify the synthetic session as guided."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$view.Summary)) "Heuristic operator view summary was empty."
        Assert-RegressionCondition ($view.Active) "Heuristic operator view did not mark ExplorationAssist as active."
        Assert-RegressionCondition (@($view.DecisionDigest).Count -ge 1) "Heuristic operator view did not expose a decision digest."
        return $view
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
