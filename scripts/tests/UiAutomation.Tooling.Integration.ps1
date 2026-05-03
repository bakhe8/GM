param(
    [string]$OutputRoot = ".\scratch\UIAcceptance\latest",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$uiExplorePath = Join-Path $repoRoot "scripts\ui_explore.ps1"
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$summaryPath = Join-Path $resolvedOutputRoot "tooling-integration-summary.md"
$failureCapturePath = Join-Path $resolvedOutputRoot "tooling-integration-failure.png"
$relativeFailureCapturePath = ".\scratch\UIAcceptance\latest\tooling-integration-failure.png"
if (Test-Path -LiteralPath $failureCapturePath) {
    Remove-Item -LiteralPath $failureCapturePath -Force
}
$results = New-Object System.Collections.Generic.List[object]
$suiteFailed = $false
$failureMessage = $null

function ConvertFrom-UiExploreOutput {
    param(
        [Parameter(Mandatory)]
        [object[]]$OutputLines
    )

    $text = (($OutputLines | ForEach-Object { "$_" }) -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

function Invoke-UiExploreJson {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Arguments
    )

    $invokeParameters = @{}
    foreach ($key in $Arguments.Keys) {
        $value = $Arguments[$key]
        if ($value -is [bool] -or $value -is [System.Management.Automation.SwitchParameter]) {
            $invokeParameters[$key] = [bool]$value
            continue
        }

        if ($null -eq $value) {
            continue
        }

        $stringValue = [string]$value
        if ([string]::IsNullOrWhiteSpace($stringValue)) {
            continue
        }

        $invokeParameters[$key] = $stringValue
    }

    $output = & $uiExplorePath @invokeParameters
    return ConvertFrom-UiExploreOutput -OutputLines @($output)
}

function Invoke-UiExploreExpectedFailure {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Arguments
    )

    $invokeParameters = @{}
    foreach ($key in $Arguments.Keys) {
        $value = $Arguments[$key]
        if ($value -is [bool] -or $value -is [System.Management.Automation.SwitchParameter]) {
            $invokeParameters[$key] = [bool]$value
            continue
        }

        if ($null -eq $value) {
            continue
        }

        $stringValue = [string]$value
        if ([string]::IsNullOrWhiteSpace($stringValue)) {
            continue
        }

        $invokeParameters[$key] = $stringValue
    }

    try {
        & $uiExplorePath @invokeParameters | Out-Null
        throw "Expected ui_explore to fail, but it succeeded."
    }
    catch {
        if ($_.Exception.Message -eq "Expected ui_explore to fail, but it succeeded.") {
            throw
        }

        return $_.Exception.Message
    }
}

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
    [void]$lines.Add("# UI Tooling Integration Summary")
    [void]$lines.Add("")
    [void]$lines.Add("- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")")
    [void]$lines.Add("- ReuseRunningSession: $ReuseRunningSession")
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
        if (Test-Path -LiteralPath $failureCapturePath) {
            [void]$lines.Add("")
            [void]$lines.Add("- Failure capture: [tooling-integration-failure.png](./tooling-integration-failure.png)")
        }
    }

    [System.IO.File]::WriteAllLines($summaryPath, $lines)
}

function Add-SyntheticRuntimeFaultEvent {
    param(
        [Parameter(Mandatory)]
        [string]$EventLogPath,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$Title,
        [string]$Message = "Synthetic integration fault"
    )

    $directory = Split-Path -Parent $EventLogPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $record = [ordered]@{
        Timestamp = (Get-Date).ToString("o")
        Category = "runtime.fault"
        Action = $ActionName
        Payload = [ordered]@{
            Severity = "Error"
            Title = $Title
            Message = $Message
            IsTerminating = $false
            ExceptionType = "Synthetic.IntegrationFault"
            ExceptionMessage = $Message
        }
    } | ConvertTo-Json -Depth 8 -Compress

    Add-Content -Path $EventLogPath -Value $record -Encoding UTF8
}

try {
    if (-not $ReuseRunningSession) {
        Get-Process GuaranteeManager -ErrorAction SilentlyContinue | Stop-Process -Force
    }

    Invoke-RegressionStep -Name "probe-clean-launch" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $ReuseRunningSession
        }

        Assert-RegressionCondition ($payload.MainWindow.AutomationId -eq "Shell.MainWindow") "Probe did not return Shell.MainWindow."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Clean launch did not start from a single main window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-adaptive-snapshot" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        Assert-RegressionCondition ($null -ne $payload.CapabilityDefinitions) "HostState did not return capability definitions."
        Assert-RegressionCondition (@($payload.CapabilityDefinitions | Where-Object Name -eq "BurstCapture").Count -eq 1) "HostState did not include BurstCapture definition."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "heuristicsstate-direct-shape" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HeuristicsState"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($null -ne $payload.HeuristicDefinitions) "HeuristicsState did not return heuristic definitions."
        Assert-RegressionCondition (@($payload.HeuristicDefinitions | Where-Object Name -eq "runtime-fault").Count -eq 1) "HeuristicsState did not include runtime-fault."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-exploration-assist-early" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "ExplorationAssist"
            LeaseMilliseconds = 12000
            Reason = "integration-exploration-assist-early"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "ExplorationAssist") "CapabilityOn did not activate ExplorationAssist."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-visual-anomaly-hover" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 900
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilityCaptures).Count -ge 3) "ExplorationAssist did not create burst evidence for the visual-anomaly path."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-heuristic-visual-anomaly" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decision = @($payload.RecentHeuristicDecisions | Where-Object {
            $_.Payload.StrategyName -eq "visual-anomaly"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($decision.Count -eq 1) "ExplorationAssist did not record a visual-anomaly heuristic decision."
        Assert-RegressionCondition ([string]$payload.HeuristicOperatorView.Status -in @("intervened", "guided", "cooling-down", "monitoring")) "HeuristicOperatorView returned an unexpected status."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-stubborn-control-failure" -ScriptBlock {
        $message = Invoke-UiExploreExpectedFailure -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Definitely.Missing.Target"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace($message)) "The stubborn-control path did not return a failure message."
        return $message
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-heuristic-stubborn-control" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decision = @($payload.RecentHeuristicDecisions | Where-Object {
            $_.Payload.StrategyName -eq "stubborn-control"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($decision.Count -eq 1) "ExplorationAssist did not record a stubborn-control heuristic decision."
        Assert-RegressionCondition ([string]$decision[0].Decision -eq "guided") "Stubborn-control should guide the next modality rather than pretending it already solved the control."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "MouseTrace").Count -ge 1) "ExplorationAssist did not arm MouseTrace after a stubborn-control failure."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-exploration-assist-early" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "ExplorationAssist"
            Reason = "integration-exploration-assist-early-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "ExplorationAssist").Count -eq 0) "ExplorationAssist remained active after the early block."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "faultstate-direct-shape" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "FaultState"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($null -ne $payload.FaultSummary) "FaultState did not return FaultSummary."
        Assert-RegressionCondition ($payload.FaultSummary.SignalCount -ge 0) "FaultState returned an invalid signal count."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-fault-watch" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "FaultWatch"
            LeaseMilliseconds = 8000
            Reason = "integration-fault-watch"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "FaultWatch") "CapabilityOn did not activate FaultWatch."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "FaultWatch").Count -eq 1) "FaultWatch was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "fault-watch-detects-runtime-fault-event" -ScriptBlock {
        $faultState = Invoke-UiExploreJson -Arguments @{
            Action = "FaultState"
            ReuseRunningSession = $true
        }

        Add-SyntheticRuntimeFaultEvent -EventLogPath ([string]$faultState.DiagnosticsPaths.EventLogPath) -ActionName "integration.synthetic-fault" -Title "Synthetic FaultWatch Error" -Message "Synthetic runtime fault for integration coverage."
        Start-Sleep -Milliseconds 150

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $captures = @($payload.CapabilityCaptures)
        Assert-RegressionCondition ($captures.Count -ge 3) "FaultWatch did not collect burst evidence after a synthetic runtime fault."

        $hostState = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }
        $observation = @($hostState.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "FaultWatch" -and $_.Kind -eq "fault-signal"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($observation.Count -eq 1) "FaultWatch did not record a fault-signal observation."
        Assert-RegressionCondition ([int]$observation[0].Payload.SignalCount -ge 1) "FaultWatch fault-signal observation did not include any signals."
        Assert-RegressionCondition ([string]$hostState.CapabilityOperatorView.Status -eq "intervened") "CapabilityOperatorView should report an intervened state after a fault signal."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "fault-watch-repeat-suppressed" -ScriptBlock {
        $faultState = Invoke-UiExploreJson -Arguments @{
            Action = "FaultState"
            ReuseRunningSession = $true
        }

        Add-SyntheticRuntimeFaultEvent -EventLogPath ([string]$faultState.DiagnosticsPaths.EventLogPath) -ActionName "integration.synthetic-fault" -Title "Synthetic FaultWatch Error" -Message "Synthetic runtime fault for integration coverage."
        Start-Sleep -Milliseconds 150

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilityCaptures).Count -eq 0) "FaultWatch should have suppressed repeated fault evidence during cooldown."

        $hostState = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }
        $decision = @($hostState.RecentCapabilityDecisions | Where-Object {
            $_.CapabilityName -eq "FaultWatch" -and $_.Action -eq "Windows" -and $_.Decision -eq "suppressed"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($decision.Count -eq 1) "FaultWatch did not record a suppressed decision after the repeated fault signal."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-fault-watch" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "FaultWatch"
            Reason = "integration-fault-watch-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "FaultWatch").Count -eq 0) "FaultWatch remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-exploration-assist-fault" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "ExplorationAssist"
            LeaseMilliseconds = 8000
            Reason = "integration-exploration-assist-fault"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "ExplorationAssist") "CapabilityOn did not activate ExplorationAssist for the runtime-fault path."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "heuristic-runtime-fault-direct" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilityCaptures).Count -ge 3) "ExplorationAssist did not collect evidence for the runtime-fault path."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-heuristic-runtime-fault" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decision = @($payload.RecentHeuristicDecisions | Where-Object {
            $_.Payload.StrategyName -eq "runtime-fault"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($decision.Count -eq 1) "ExplorationAssist did not record a runtime-fault heuristic decision."
        Assert-RegressionCondition ([string]$decision[0].Decision -eq "triggered") "runtime-fault heuristic should intervene directly."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-exploration-assist-fault" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "ExplorationAssist"
            Reason = "integration-exploration-assist-fault-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "ExplorationAssist").Count -eq 0) "ExplorationAssist remained active after the runtime-fault block."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-burst-capture" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "BurstCapture"
            LeaseMilliseconds = 8000
            Reason = "integration-burst-check"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "BurstCapture") "CapabilityOn did not activate BurstCapture."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "BurstCapture").Count -eq 1) "BurstCapture was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "windows-burst-capture-evidence" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $captures = @($payload.CapabilityCaptures)
        Assert-RegressionCondition ($captures.Count -ge 4) "BurstCapture did not produce the expected multi-frame evidence."
        foreach ($capture in $captures) {
            Assert-RegressionCondition (Test-Path -LiteralPath $capture.Path) "A burst capture frame path did not exist on disk."
        }

        $hostState = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }
        $burstSummary = @($hostState.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "BurstCapture" -and $_.Kind -eq "burst-sequence"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($burstSummary.Count -eq 1) "HostState did not record a burst-sequence observation."
        Assert-RegressionCondition ([int]$burstSummary[0].Payload.FrameCount -ge 4) "burst-sequence observation reported too few frames."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$burstSummary[0].Payload.ContactSheetPath)) "burst-sequence observation did not include a contact sheet path."
        Assert-RegressionCondition (Test-Path -LiteralPath ([string]$burstSummary[0].Payload.ContactSheetPath)) "Burst contact sheet was not created on disk."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-burst-capture" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "BurstCapture"
            Reason = "integration-burst-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "BurstCapture").Count -eq 0) "BurstCapture remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "media-state-provider-catalog" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MediaState"
            ReuseRunningSession = $true
        }

        $psrProvider = @($payload.MediaProviders | Where-Object Name -eq "Psr.ScreenTrace" | Select-Object -First 1)
        Assert-RegressionCondition ($psrProvider.Count -eq 1) "MediaState did not expose Psr.ScreenTrace."
        Assert-RegressionCondition ([string]$psrProvider[0].Availability -eq "available") "Psr.ScreenTrace should be available on this machine."
        Assert-RegressionCondition ($null -ne $payload.MediaScopeView) "MediaState did not expose MediaScopeView."
        Assert-RegressionCondition ($payload.MediaScopeView.VideoCapture.PSObject.Properties.Name -contains "ScopeStatus") "MediaState MediaScopeView is missing VideoCapture.ScopeStatus."
        Assert-RegressionCondition ($null -ne $payload.AudioScopePolicy) "MediaState did not expose AudioScopePolicy."
        Assert-RegressionCondition (-not [bool]$payload.AudioScopePolicy.AcceptsSystemMixFallback) "AudioScopePolicy should reject system-mix fallback."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "audio-sidecar-start-blocked" -ScriptBlock {
        $message = Invoke-UiExploreExpectedFailure -Arguments @{
            Action = "AudioOn"
            LeaseMilliseconds = 5000
            Reason = "integration-audio-blocked"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($message -like "*No available audio provider is wired right now*") "AudioOn failure did not explain the missing provider."
        Assert-RegressionCondition ($message -like "*per-app attested audio*") "AudioOn failure did not mention the scoped-audio policy."

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MediaState"
            ReuseRunningSession = $true
        }

        $blockedEvent = @($payload.MediaSession.RecentEvents | Where-Object Kind -eq "audio-start-blocked" | Select-Object -First 1)
        Assert-RegressionCondition ($blockedEvent.Count -eq 1) "AudioOn blocked start did not record an audio-start-blocked event."
        Assert-RegressionCondition ([string]$blockedEvent[0].Payload.DesiredScopeModel -eq "per-app-attested") "Blocked audio start did not preserve the desired scope model."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "video-sidecar-start" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "VideoOn"
            LeaseMilliseconds = 8000
            Reason = "integration-direct-video"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.MediaSession.VideoCapture.IsActive) "VideoOn did not activate the video sidecar."
        Assert-RegressionCondition ([string]$payload.MediaSession.VideoCapture.ProviderName -eq "Psr.ScreenTrace") "VideoOn did not use Psr.ScreenTrace."
        Assert-RegressionCondition ([int]$payload.MediaSession.VideoCapture.LiveProcessCount -ge 1) "VideoOn did not leave a live provider process running."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "video-sidecar-restart-cleanly" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "VideoOn"
            LeaseMilliseconds = 8000
            Reason = "integration-direct-video-restart"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.MediaSession.VideoCapture.IsActive) "Restarting VideoOn left the sidecar inactive."
        Assert-RegressionCondition ([int]$payload.MediaSession.VideoCapture.LiveProcessCount -ge 1) "Restarting VideoOn did not recover a live provider process."
        $restartEvidence = @($payload.MediaSession.RecentEvents | Where-Object {
            ($_.Kind -eq "provider-preflight-cleanup") -or
            ($_.Kind -eq "video-stopped" -and [string]$_.Payload.Reason -eq "restart-before-start")
        } | Select-Object -First 1)
        Assert-RegressionCondition ($restartEvidence.Count -eq 1) "VideoOn restart did not record a clean single-instance handoff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "video-sidecar-stop" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "VideoOff"
            Reason = "integration-direct-video-stop"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (-not $payload.MediaSession.VideoCapture.IsActive) "VideoOff did not stop the video sidecar."
        Assert-RegressionCondition ([string]$payload.MediaSession.VideoCapture.ArtifactStatus -in @("saved", "missing")) "VideoOff did not report a clear artifact status."
        if ([string]$payload.MediaSession.VideoCapture.ArtifactStatus -eq "saved") {
            Assert-RegressionCondition (Test-Path -LiteralPath ([string]$payload.MediaSession.VideoCapture.ArtifactPath)) "VideoOff artifact path did not exist on disk."
        }
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "capability-video-start" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "VideoCapture"
            LeaseMilliseconds = 8000
            Reason = "integration-capability-video"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "VideoCapture") "CapabilityOn did not activate VideoCapture."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "VideoCapture").Count -eq 1) "VideoCapture was not present in the active capability session."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-media-after-capability-video" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        Assert-RegressionCondition ($payload.MediaSession.VideoCapture.IsActive) "HostState did not report an active media video sidecar after CapabilityOn."
        Assert-RegressionCondition ([string]$payload.MediaSession.VideoCapture.ProviderName -eq "Psr.ScreenTrace") "HostState media session did not report Psr.ScreenTrace."
        Assert-RegressionCondition ($null -ne $payload.MediaScopeView) "HostState did not expose MediaScopeView after CapabilityOn."
        Assert-RegressionCondition ([int]$payload.MediaScopeView.VideoCapture.TargetProcessId -gt 0) "MediaScopeView did not bind VideoCapture to a target process."
        Assert-RegressionCondition ([string]$payload.MediaScopeView.VideoCapture.ScopeStatus -in @("monitoring", "monitoring-external", "monitoring-unknown")) "MediaScopeView returned an invalid active scope status."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "capability-video-stop" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "VideoCapture"
            Reason = "integration-capability-video-stop"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "VideoCapture").Count -eq 0) "VideoCapture remained active after CapabilityOff."
        $mediaState = Invoke-UiExploreJson -Arguments @{
            Action = "MediaState"
            ReuseRunningSession = $true
        }
        Assert-RegressionCondition (-not $mediaState.MediaSession.VideoCapture.IsActive) "MediaState still showed an active video sidecar after CapabilityOff."
        Assert-RegressionCondition ([string]$mediaState.MediaSession.VideoCapture.ArtifactStatus -in @("saved", "missing")) "Capability-driven video stop did not report a clear artifact status."
        Assert-RegressionCondition ($null -ne $mediaState.MediaScopeView) "MediaState did not expose MediaScopeView after CapabilityOff."
        Assert-RegressionCondition ([string]$mediaState.MediaScopeView.VideoCapture.ScopeStatus -in @("clean", "mixed", "external", "unknown")) "MediaScopeView returned an invalid final scope status."
        Assert-RegressionCondition ([string]$mediaState.MediaScopeView.VideoCapture.EvidenceIsolation -in @("program-window", "program-plus-related-window", "contaminated", "unknown")) "MediaScopeView returned an invalid evidence isolation label."
        if ([string]$mediaState.MediaSession.VideoCapture.ArtifactStatus -eq "saved") {
            Assert-RegressionCondition (Test-Path -LiteralPath ([string]$mediaState.MediaSession.VideoCapture.ArtifactPath)) "Capability-driven video stop did not preserve a saved artifact."
        }
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-mouse-trace" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "MouseTrace"
            LeaseMilliseconds = 8000
            Reason = "integration-mouse-trace"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "MouseTrace") "CapabilityOn did not activate MouseTrace."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "MouseTrace").Count -eq 1) "MouseTrace was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-move-settings-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseMove"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Settings"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Shell.Sidebar.Settings") "MouseMove did not resolve the Settings sidebar target."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-mouse-trace-after-move" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $trace = @($payload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "MouseTrace" -and $_.Kind -eq "mouse-trace" -and $_.Payload.Action -eq "MouseMove"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($trace.Count -eq 1) "MouseTrace did not record a mouse-trace observation after MouseMove."
        Assert-RegressionCondition ([int]$trace[0].Payload.SampleCount -ge 2) "MouseTrace observation reported too few samples."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-click-settings-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseClick"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Settings"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Button -eq "Left") "MouseClick did not report a left-button action."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-settings-after-mouse-click" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Settings") "MouseClick on Settings sidebar did not switch to Settings workspace."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-hover-guarantees-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 150
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Shell.Sidebar.Guarantees") "MouseHover did not resolve the Guarantees sidebar target."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-doubleclick-guarantees-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseDoubleClick"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.ClickCount -eq 2) "MouseDoubleClick did not report a double-click."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-guarantees-after-mouse-doubleclick" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Guarantees") "MouseDoubleClick on Guarantees sidebar did not return to Guarantees workspace."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-scroll-main-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseScroll"
            WindowAutomationId = "Shell.MainWindow"
            ScrollDelta = -120
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.ScrollDelta -eq -120) "MouseScroll did not echo the expected delta."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-drag-global-search" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseDrag"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.GlobalSearchBox"
            DeltaX = 24
            DeltaY = 0
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.DeltaX -eq 24) "MouseDrag did not report the expected horizontal delta."
        Assert-RegressionCondition ([int]$payload.EndPosition.X -gt [int]$payload.StartPosition.X) "MouseDrag did not move the cursor to the right."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-rightclick-main-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseRightClick"
            WindowAutomationId = "Shell.MainWindow"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Button -eq "Right") "MouseRightClick did not report a right-button action."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-clean-after-mouse-actions" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([string]::IsNullOrWhiteSpace([string]$payload.Health.ActiveDialogTitle)) "A dialog remained open after mouse actions."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Mouse actions left the session with more than one visible window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-mouse-trace" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "MouseTrace"
            Reason = "integration-mouse-trace-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "MouseTrace").Count -eq 0) "MouseTrace remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-reactive-assist" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "ReactiveAssist"
            LeaseMilliseconds = 8000
            Reason = "integration-reactive-assist"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "ReactiveAssist") "CapabilityOn did not activate ReactiveAssist."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "ReactiveAssist").Count -eq 1) "ReactiveAssist was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "reactive-hover-guarantees-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 900
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Shell.Sidebar.Guarantees") "Reactive hover did not resolve the Guarantees sidebar target."
        Assert-RegressionCondition (@($payload.CapabilityCaptures).Count -ge 3) "ReactiveAssist did not escalate to a burst evidence set on a slow hover."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-reactive-trigger-after-hover" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $trace = @($payload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "ReactiveAssist" -and $_.Kind -eq "reactive-trigger" -and $_.Payload.Action -eq "MouseHover"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($trace.Count -eq 1) "ReactiveAssist did not record a reactive-trigger observation after the slow hover."
        Assert-RegressionCondition (@($trace[0].Payload.Reasons | Where-Object Kind -eq "slow-action").Count -ge 1) "ReactiveAssist did not explain the hover anomaly as a slow action."
        Assert-RegressionCondition ([int]$trace[0].Payload.CaptureCount -ge 3) "ReactiveAssist observation did not report burst evidence."
        Assert-RegressionCondition ($null -ne $payload.CapabilityOperatorView) "HostState did not expose CapabilityOperatorView."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$payload.CapabilityOperatorView.Summary)) "CapabilityOperatorView summary was empty."
        Assert-RegressionCondition ([string]$payload.CapabilityOperatorView.Status -eq "intervened") "CapabilityOperatorView should report an intervened state after a reactive trigger."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$payload.CapabilityOperatorView.SecondarySummary)) "CapabilityOperatorView did not expose a secondary summary after a reactive trigger."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$payload.CapabilityOperatorView.Guidance)) "CapabilityOperatorView did not expose operator guidance after a reactive trigger."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "reactive-hover-guarantees-sidebar-suppressed" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 900
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilityCaptures).Count -eq 0) "ReactiveAssist should have suppressed repeated anomaly evidence during cooldown."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-reactive-suppressed-after-repeat" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decision = @($payload.RecentCapabilityDecisions | Where-Object {
            $_.CapabilityName -eq "ReactiveAssist" -and $_.Action -eq "MouseHover" -and $_.Decision -eq "suppressed"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($decision.Count -eq 1) "ReactiveAssist did not record a suppressed decision after the repeated hover."
        Assert-RegressionCondition ([string]$decision[0].Summary -like "*تجنب الإزعاج المتكرر*") "ReactiveAssist suppressed decision did not explain the cooldown behavior."
        $reactiveCoolingDownVisible = ([string]$payload.CapabilityOperatorView.Status -eq "cooling-down") -or
            (@($payload.CapabilityOperatorView.CoolingDownCapabilities | Where-Object Name -eq "ReactiveAssist").Count -eq 1) -or
            (@($payload.CapabilityOperatorView.DecisionDigest | Where-Object {
                $_.CapabilityName -eq "ReactiveAssist" -and $_.Decision -eq "suppressed"
            }).Count -ge 1)
        Assert-RegressionCondition $reactiveCoolingDownVisible "CapabilityOperatorView did not preserve the ReactiveAssist cooldown context after repeated anomaly suppression."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$payload.CapabilityOperatorView.Guidance)) "CapabilityOperatorView guidance should remain readable after repeated anomaly suppression."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-reactive-assist" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "ReactiveAssist"
            Reason = "integration-reactive-assist-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "ReactiveAssist").Count -eq 0) "ReactiveAssist remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-autocapture-on-failure" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "AutoCaptureOnFailure"
            LeaseMilliseconds = 8000
            Reason = "integration-failure-capture"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "AutoCaptureOnFailure") "CapabilityOn did not activate AutoCaptureOnFailure."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "AutoCaptureOnFailure").Count -eq 1) "AutoCaptureOnFailure was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "expected-failure-invalid-key" -ScriptBlock {
        $message = Invoke-UiExploreExpectedFailure -Arguments @{
            Action = "Key"
            KeyName = "Bogus"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($message.Contains("Unsupported key 'Bogus'")) "The expected failure did not return the invalid-key message."
        return $message
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-failure-bundle-after-invalid-key" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $bundle = @($payload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "AutoCaptureOnFailure" -and $_.Kind -eq "failure-bundle" -and $_.Payload.Action -eq "Key"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($bundle.Count -eq 1) "AutoCaptureOnFailure did not record a failure-bundle observation after the invalid key action."
        Assert-RegressionCondition ([int]$bundle[0].Payload.CaptureCount -ge 3) "failure-bundle did not report a multi-frame failure evidence set."
        Assert-RegressionCondition ([string]$payload.CapabilityOperatorView.Status -eq "intervened") "CapabilityOperatorView should report an intervened state after a captured failure."
        Assert-RegressionCondition ([string]$payload.CapabilityOperatorView.LastDecision.CapabilityName -eq "AutoCaptureOnFailure") "CapabilityOperatorView did not surface AutoCaptureOnFailure as the last decision after the captured failure."
        foreach ($capturePath in @($bundle[0].Payload.CapturePaths)) {
            Assert-RegressionCondition (Test-Path -LiteralPath ([string]$capturePath)) "A failure-bundle capture path did not exist on disk."
        }
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "expected-failure-invalid-key-suppressed" -ScriptBlock {
        $message = Invoke-UiExploreExpectedFailure -Arguments @{
            Action = "Key"
            KeyName = "Bogus"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($message.Contains("Unsupported key 'Bogus'")) "The repeated expected failure did not return the invalid-key message."
        return $message
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-failure-suppressed-after-repeat" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decision = @($payload.RecentCapabilityDecisions | Where-Object {
            $_.CapabilityName -eq "AutoCaptureOnFailure" -and $_.Action -eq "Key" -and $_.Decision -eq "suppressed"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($decision.Count -eq 1) "AutoCaptureOnFailure did not record a suppressed decision after the repeated failure."
        Assert-RegressionCondition ([string]$decision[0].Summary -like "*تكرر الفشل نفسه سريعًا*") "AutoCaptureOnFailure suppressed decision did not explain the calmer failure behavior."
        $failureCoolingDownVisible = ([string]$payload.CapabilityOperatorView.Status -eq "cooling-down") -or
            (@($payload.CapabilityOperatorView.CoolingDownCapabilities | Where-Object Name -eq "AutoCaptureOnFailure").Count -eq 1) -or
            (@($payload.CapabilityOperatorView.DecisionDigest | Where-Object {
                $_.CapabilityName -eq "AutoCaptureOnFailure" -and $_.Decision -eq "suppressed"
            }).Count -ge 1)
        Assert-RegressionCondition $failureCoolingDownVisible "CapabilityOperatorView did not preserve the AutoCaptureOnFailure cooldown context after repeated failure suppression."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-autocapture-on-failure" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "AutoCaptureOnFailure"
            Reason = "integration-failure-capture-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "AutoCaptureOnFailure").Count -eq 0) "AutoCaptureOnFailure remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "sidebar-settings" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "الإعدادات"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "Sidebar") "Sidebar action payload was not returned for Settings."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "refresh-settings-paths" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Settings.Toolbar.Refresh"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Settings.Toolbar.Refresh") "Failed to target Settings refresh button."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "observe-settings-quiet-status" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Elements"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Status.Primary"
            MaxResults = 1
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Elements.Count -ge 1) "Could not read Shell.Status.Primary after popup action."
        $status = $payload.Elements[0]
        $message = @($status.HelpText, $status.ItemStatus, $status.Name) -join " "
        Assert-RegressionCondition ($message.Contains("تم نسخ ملخص مسارات التشغيل.")) "Settings popup action did not produce the expected quiet status message."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "sidebar-reports-for-history-print" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "التقارير"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "Sidebar") "Sidebar action payload was not returned for Reports."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "filter-history-print-report" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "SetField"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Reports.SearchBox"
            Value = "طباعة سجل"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "SetField") "Report catalog search field was not updated."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "select-history-print-report" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Reports.Row.Show.operational.guarantee-history-print"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Reports.Row.Show.operational.guarantee-history-print") "Could not select the guarantee history print report."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-history-print-input" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Reports.Detail.RunButton"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowTitle = "طباعة سجل ضمان محدد"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.Name -eq "طباعة سجل ضمان محدد") "WaitWindow did not resolve the guarantee history print input dialog."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "fill-history-print-input" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "SetField"
            WindowTitle = "طباعة سجل ضمان محدد"
            Label = "رقم الضمان"
            Value = "BG-2026-0007"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "SetField") "Guarantee history print input was not filled."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-exploration-assist-external" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "ExplorationAssist"
            LeaseMilliseconds = 12000
            Reason = "integration-exploration-assist-external"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "ExplorationAssist") "CapabilityOn did not activate ExplorationAssist for the external-window path."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-history-print-window" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowTitle = "طباعة سجل ضمان محدد"
            Name = "إنشاء التقرير"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowTitle = "GuaranteeManager - Print"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.Name -eq "GuaranteeManager - Print") "WaitWindow did not resolve the external print window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "windows-include-external-print" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $printWindow = @($payload.Windows | Where-Object { $_.Name -eq "GuaranteeManager - Print" } | Select-Object -First 1)
        Assert-RegressionCondition ($printWindow.Count -eq 1) "Windows catalog did not include the external print window."
        Assert-RegressionCondition ($printWindow[0].ProcessId -ne $payload.ProcessId) "External print window was not detected as a cross-process window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-heuristic-external-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $decisions = @($payload.RecentHeuristicDecisions | Where-Object {
            $_.Payload.StrategyName -eq "external-window"
        })
        Assert-RegressionCondition ($decisions.Count -ge 1) "ExplorationAssist did not record an external-window heuristic decision."

        $triggeredDecision = @($decisions | Where-Object {
            [string]$_.Decision -eq "triggered"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($triggeredDecision.Count -eq 1) "external-window heuristic did not intervene directly when the related external window first appeared."

        $latestDecision = $decisions[0]
        Assert-RegressionCondition ([string]$latestDecision.Decision -in @("triggered", "suppressed")) "external-window heuristic returned an unexpected decision state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "close-print-and-wait" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowTitle = "GuaranteeManager - Print"
            Name = "Cancel"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindowClosed"
            WindowTitle = "GuaranteeManager - Print"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "External print window did not close within the expected wait window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-exploration-assist-external" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "ExplorationAssist"
            Reason = "integration-exploration-assist-external-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "ExplorationAssist").Count -eq 0) "ExplorationAssist remained active after the external-window block."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-clean-after-integration" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([string]::IsNullOrWhiteSpace([string]$payload.Health.ActiveDialogTitle)) "A dialog remained open after integration flow."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Session did not return to a single main window after integration flow."
        return $payload
    } | Out-Null
}
catch {
    $suiteFailed = $true
    $failureMessage = $_.Exception.Message
    try {
        Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            IncludeCapture = $true
            OutputPath = $relativeFailureCapturePath
            ReuseRunningSession = $true
        } | Out-Null
    }
    catch {
    }
}
finally {
    Write-RegressionSummary
}

if ($suiteFailed) {
    throw "UI tooling integration suite failed. See $summaryPath"
}

[pscustomobject]@{
    Summary = $summaryPath
    FailureCapture = if (Test-Path -LiteralPath $failureCapturePath) { $failureCapturePath } else { $null }
    Passed = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
} | Format-List
