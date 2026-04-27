function Get-UiCapabilityDefinitions {
    $videoProvider = Get-UiPreferredMediaProvider -Kind "Video"
    $audioProvider = Get-UiPreferredMediaProvider -Kind "Audio"

    return @(
        [pscustomobject]@{
            Name = "BurstCapture"
            Category = "Visual"
            ProviderState = "available"
            DefaultLeaseMs = 1800
            DefaultFrameCount = 4
            DefaultIntervalMs = 90
            CreateContactSheet = $true
            Description = "يلتقط لقطات سريعة خفيفة أثناء نفس الاستكشاف الحر عند الحاجة."
        },
        [pscustomobject]@{
            Name = "AutoCaptureOnFailure"
            Category = "Visual"
            ProviderState = "available"
            DefaultLeaseMs = 3000
            DefaultCooldownMs = 1800
            DefaultFrameCount = 3
            DefaultIntervalMs = 80
            CreateContactSheet = $true
            Description = "يلتقط لقطة تلقائية عند فشل الفعل أثناء الاستكشاف."
        },
        [pscustomobject]@{
            Name = "ReactiveAssist"
            Category = "Adaptive"
            ProviderState = "available"
            DefaultLeaseMs = 3200
            DefaultSlowActionMs = 850
            DefaultCooldownMs = 3200
            DefaultFrameCount = 3
            DefaultIntervalMs = 70
            CreateContactSheet = $true
            TriggerOnSlowAction = $true
            TriggerOnExternalWindow = $true
            TriggerOnDialog = $true
            Description = "يراقب anomaly خفيفة أثناء الاستكشاف ويشغل evidence بصرية فقط عند الحاجة."
        },
        [pscustomobject]@{
            Name = "ExplorationAssist"
            Category = "Adaptive"
            ProviderState = "available"
            DefaultLeaseMs = 5000
            Description = "ينسق بين القدرات الحالية ويختار modality أنسب أثناء الاستكشاف الحر بدل إلزام النموذج بأسلوب واحد."
        },
        [pscustomobject]@{
            Name = "FaultWatch"
            Category = "Signals"
            ProviderState = "available"
            DefaultLeaseMs = 4200
            DefaultLookbackSeconds = 45
            DefaultCooldownMs = 2600
            DefaultFrameCount = 3
            DefaultIntervalMs = 85
            CreateContactSheet = $true
            TriggerOnRuntimeFault = $true
            TriggerOnProcessExit = $true
            Description = "يراقب إشارات fault الواضحة من سجل التطبيق وأحداث التشغيل بدل الاعتماد على صوت النظام."
        },
        [pscustomobject]@{
            Name = "VideoCapture"
            Category = "Media"
            ProviderState = if ($null -ne $videoProvider) { "available" } else { "unavailable" }
            DefaultLeaseMs = 4000
            Description = if ($null -ne $videoProvider) {
                "تسجيل فيديو/تتبع بصري عند الطلب عبر المزود $([string]$videoProvider.Name)."
            }
            else {
                "تسجيل فيديو قصير عند الطلب أو عند trigger."
            }
        },
        [pscustomobject]@{
            Name = "AudioCapture"
            Category = "Media"
            ProviderState = if ($null -ne $audioProvider) { "available" } else { "unavailable" }
            DefaultLeaseMs = 4000
            Description = if ($null -ne $audioProvider) {
                "التقاط صوت قصير عند الحاجة عبر المزود $([string]$audioProvider.Name)."
            }
            else {
                "التقاط صوت قصير عند الحاجة فقط."
            }
        },
        [pscustomobject]@{
            Name = "MouseTrace"
            Category = "Input"
            ProviderState = "available"
            DefaultLeaseMs = 2500
            DefaultSampleCount = 4
            DefaultIntervalMs = 40
            Description = "تتبع خفيف لحركة وتموضع الماوس داخل نفس الاستكشاف الحر عند الحاجة."
        }
    )
}

function Resolve-UiCapabilityDefinition {
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName
    )

    $definitions = @(Get-UiCapabilityDefinitions)
    $definition = $definitions | Where-Object {
        [string]::Equals($_.Name, $CapabilityName, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1

    if ($null -eq $definition) {
        throw "Unknown capability '$CapabilityName'."
    }

    return $definition
}

function Get-UiCapabilityObservationPath {
    return Get-UiCapabilityObservationsPath
}

function Get-UiCapabilityObservationEntries {
    param(
        [int]$MaxCount = 20
    )

    $path = Get-UiCapabilityObservationPath
    if (-not (Test-Path -LiteralPath $path)) {
        return @()
    }

    $lines = @(Get-Content -Path $path -Tail ([Math]::Max($MaxCount * 3, 24)) -Encoding UTF8 -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    return @($lines |
        ForEach-Object {
            try {
                $_ | ConvertFrom-Json
            }
            catch {
                $null
            }
        } |
        Where-Object { $null -ne $_ } |
        Select-Object -Last $MaxCount)
}

function Resolve-UiBurstCaptureProfile {
    param(
        [Parameter(Mandatory)]
        $CapabilityRecord
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$CapabilityRecord.Name)
    $metadata = $CapabilityRecord.Metadata
    $frameCount = if ($null -ne $metadata -and $null -ne $metadata.FrameCount) { [int]$metadata.FrameCount } else { [int]$definition.DefaultFrameCount }
    $intervalMs = if ($null -ne $metadata -and $null -ne $metadata.IntervalMs) { [int]$metadata.IntervalMs } else { [int]$definition.DefaultIntervalMs }
    $createContactSheet = if ($null -ne $metadata -and $null -ne $metadata.CreateContactSheet) { [bool]$metadata.CreateContactSheet } else { [bool]$definition.CreateContactSheet }

    return [pscustomobject]@{
        FrameCount = [Math]::Max(1, $frameCount)
        IntervalMs = [Math]::Max(0, $intervalMs)
        CreateContactSheet = $createContactSheet
    }
}

function Resolve-UiMouseTraceProfile {
    param(
        [Parameter(Mandatory)]
        $CapabilityRecord
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$CapabilityRecord.Name)
    $metadata = $CapabilityRecord.Metadata
    $sampleCount = if ($null -ne $metadata -and $null -ne $metadata.SampleCount) { [int]$metadata.SampleCount } else { [int]$definition.DefaultSampleCount }
    $intervalMs = if ($null -ne $metadata -and $null -ne $metadata.IntervalMs) { [int]$metadata.IntervalMs } else { [int]$definition.DefaultIntervalMs }

    return [pscustomobject]@{
        SampleCount = [Math]::Max(1, $sampleCount)
        IntervalMs = [Math]::Max(0, $intervalMs)
    }
}

function Resolve-UiReactiveAssistProfile {
    param(
        [Parameter(Mandatory)]
        $CapabilityRecord
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$CapabilityRecord.Name)
    $metadata = $CapabilityRecord.Metadata

    $slowActionMs = if ($null -ne $metadata -and $null -ne $metadata.SlowActionMs) { [int]$metadata.SlowActionMs } else { [int]$definition.DefaultSlowActionMs }
    $cooldownMs = if ($null -ne $metadata -and $null -ne $metadata.CooldownMs) { [int]$metadata.CooldownMs } else { [int]$definition.DefaultCooldownMs }
    $frameCount = if ($null -ne $metadata -and $null -ne $metadata.FrameCount) { [int]$metadata.FrameCount } else { [int]$definition.DefaultFrameCount }
    $intervalMs = if ($null -ne $metadata -and $null -ne $metadata.IntervalMs) { [int]$metadata.IntervalMs } else { [int]$definition.DefaultIntervalMs }
    $createContactSheet = if ($null -ne $metadata -and $null -ne $metadata.CreateContactSheet) { [bool]$metadata.CreateContactSheet } else { [bool]$definition.CreateContactSheet }
    $triggerOnSlowAction = if ($null -ne $metadata -and $null -ne $metadata.TriggerOnSlowAction) { [bool]$metadata.TriggerOnSlowAction } else { [bool]$definition.TriggerOnSlowAction }
    $triggerOnExternalWindow = if ($null -ne $metadata -and $null -ne $metadata.TriggerOnExternalWindow) { [bool]$metadata.TriggerOnExternalWindow } else { [bool]$definition.TriggerOnExternalWindow }
    $triggerOnDialog = if ($null -ne $metadata -and $null -ne $metadata.TriggerOnDialog) { [bool]$metadata.TriggerOnDialog } else { [bool]$definition.TriggerOnDialog }

    return [pscustomobject]@{
        SlowActionMs = [Math]::Max(0, $slowActionMs)
        CooldownMs = [Math]::Max(0, $cooldownMs)
        FrameCount = [Math]::Max(1, $frameCount)
        IntervalMs = [Math]::Max(0, $intervalMs)
        CreateContactSheet = $createContactSheet
        TriggerOnSlowAction = $triggerOnSlowAction
        TriggerOnExternalWindow = $triggerOnExternalWindow
        TriggerOnDialog = $triggerOnDialog
    }
}

function Resolve-UiFaultWatchProfile {
    param(
        [Parameter(Mandatory)]
        $CapabilityRecord
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$CapabilityRecord.Name)
    $metadata = $CapabilityRecord.Metadata

    $lookbackSeconds = if ($null -ne $metadata -and $null -ne $metadata.LookbackSeconds) { [int]$metadata.LookbackSeconds } else { [int]$definition.DefaultLookbackSeconds }
    $cooldownMs = if ($null -ne $metadata -and $null -ne $metadata.CooldownMs) { [int]$metadata.CooldownMs } else { [int]$definition.DefaultCooldownMs }
    $frameCount = if ($null -ne $metadata -and $null -ne $metadata.FrameCount) { [int]$metadata.FrameCount } else { [int]$definition.DefaultFrameCount }
    $intervalMs = if ($null -ne $metadata -and $null -ne $metadata.IntervalMs) { [int]$metadata.IntervalMs } else { [int]$definition.DefaultIntervalMs }
    $createContactSheet = if ($null -ne $metadata -and $null -ne $metadata.CreateContactSheet) { [bool]$metadata.CreateContactSheet } else { [bool]$definition.CreateContactSheet }
    $triggerOnRuntimeFault = if ($null -ne $metadata -and $null -ne $metadata.TriggerOnRuntimeFault) { [bool]$metadata.TriggerOnRuntimeFault } else { [bool]$definition.TriggerOnRuntimeFault }
    $triggerOnProcessExit = if ($null -ne $metadata -and $null -ne $metadata.TriggerOnProcessExit) { [bool]$metadata.TriggerOnProcessExit } else { [bool]$definition.TriggerOnProcessExit }

    return [pscustomobject]@{
        LookbackSeconds = [Math]::Max(5, $lookbackSeconds)
        CooldownMs = [Math]::Max(0, $cooldownMs)
        FrameCount = [Math]::Max(1, $frameCount)
        IntervalMs = [Math]::Max(0, $intervalMs)
        CreateContactSheet = $createContactSheet
        TriggerOnRuntimeFault = $triggerOnRuntimeFault
        TriggerOnProcessExit = $triggerOnProcessExit
    }
}

function Resolve-UiFailureCaptureProfile {
    param(
        [Parameter(Mandatory)]
        $CapabilityRecord
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$CapabilityRecord.Name)
    $metadata = $CapabilityRecord.Metadata
    $cooldownMs = if ($null -ne $metadata -and $null -ne $metadata.CooldownMs) { [int]$metadata.CooldownMs } else { [int]$definition.DefaultCooldownMs }
    $frameCount = if ($null -ne $metadata -and $null -ne $metadata.FrameCount) { [int]$metadata.FrameCount } else { [int]$definition.DefaultFrameCount }
    $intervalMs = if ($null -ne $metadata -and $null -ne $metadata.IntervalMs) { [int]$metadata.IntervalMs } else { [int]$definition.DefaultIntervalMs }
    $createContactSheet = if ($null -ne $metadata -and $null -ne $metadata.CreateContactSheet) { [bool]$metadata.CreateContactSheet } else { [bool]$definition.CreateContactSheet }

    return [pscustomobject]@{
        CooldownMs = [Math]::Max(0, $cooldownMs)
        FrameCount = [Math]::Max(1, $frameCount)
        IntervalMs = [Math]::Max(0, $intervalMs)
        CreateContactSheet = $createContactSheet
    }
}

function Add-UiCapabilityHistoryEntry {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$Event,
        [string]$Reason = "",
        $Metadata = $null
    )

    $history = @($SessionState.CapabilityHistory)
    $entry = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        CapabilityName = $CapabilityName
        Event = $Event
        Reason = $Reason
        Metadata = $Metadata
    }

    $SessionState.CapabilityHistory = @($entry) + @($history | Select-Object -First 24)
}

function Invoke-UiCapabilityBrokerSweep {
    param(
        $SessionState = $null,
        [switch]$Persist
    )

    if ($null -eq $SessionState) {
        $SessionState = Read-UiCapabilitySessionStateRaw
    }

    if ($null -eq $SessionState) {
        return $null
    }

    $activeCapabilities = @($SessionState.ActiveCapabilities)
    if ($activeCapabilities.Count -eq 0) {
        $null = Invoke-UiMediaBrokerSweep -Persist
        return $SessionState
    }

    $now = [DateTimeOffset]::Now
    $kept = New-Object System.Collections.Generic.List[object]
    $changed = $false
    foreach ($capability in $activeCapabilities) {
        $expiresAtText = [string]$capability.ExpiresAt
        if (-not [string]::IsNullOrWhiteSpace($expiresAtText) -and [DateTimeOffset]::Parse($expiresAtText) -le $now) {
            $changed = $true
            switch -Regex ([string]$capability.Name) {
                "^VideoCapture$" {
                    $null = Stop-UiVideoCaptureSidecar -Reason "capability-expired"
                    break
                }
                "^AudioCapture$" {
                    $null = Stop-UiAudioCaptureSidecar -Reason "capability-expired"
                    break
                }
            }
            Add-UiCapabilityHistoryEntry -SessionState $SessionState -CapabilityName $capability.Name -Event "expired" -Reason ([string]$capability.Reason) -Metadata $capability.Metadata
            Write-UiTimelineEvent -Action "Capability.$($capability.Name)" -Stage "expired" -Success $true -Payload @{
                CapabilityName = $capability.Name
                SessionId = $SessionState.SessionId
                Reason = $capability.Reason
            }
            continue
        }

        [void]$kept.Add($capability)
    }

    if ($changed) {
        $SessionState.ActiveCapabilities = @($kept.ToArray())
        $SessionState.UpdatedAt = (Get-Date).ToString("o")
        Save-UiCapabilitySessionState -SessionState $SessionState | Out-Null
    }

    $null = Invoke-UiMediaBrokerSweep -Persist

    return $SessionState
}

function Test-UiCapabilityEnabled {
    param(
        [string]$CapabilityName,
        $SessionState = $null
    )

    $session = Invoke-UiCapabilityBrokerSweep -SessionState $SessionState -Persist
    if ($null -eq $session -or -not $session.IsActive) {
        return $false
    }

    return @($session.ActiveCapabilities | Where-Object {
        [string]::Equals($_.Name, $CapabilityName, [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -gt 0
}

function Refresh-UiActiveCapabilityLeases {
    param(
        $SessionState = $null,
        [string]$LastAction = "",
        [switch]$Persist
    )

    if ($null -eq $SessionState) {
        $SessionState = Read-UiCapabilitySessionStateRaw
    }

    if ($null -eq $SessionState -or -not $SessionState.IsActive) {
        return $SessionState
    }

    $now = [DateTimeOffset]::Now
    $changed = $false
    $activeCapabilities = @($SessionState.ActiveCapabilities)
    foreach ($capability in $activeCapabilities) {
        $expiresAtText = [string]$capability.ExpiresAt
        if ([string]::IsNullOrWhiteSpace($expiresAtText)) {
            continue
        }

        try {
            $expiresAt = [DateTimeOffset]::Parse($expiresAtText)
        }
        catch {
            continue
        }

        if ($expiresAt -le $now) {
            continue
        }

        $leaseMs = if ([int]$capability.LeaseMilliseconds -gt 0) {
            [int]$capability.LeaseMilliseconds
        }
        else {
            [int](Resolve-UiCapabilityDefinition -CapabilityName ([string]$capability.Name)).DefaultLeaseMs
        }

        if ($leaseMs -le 0) {
            continue
        }

        $newExpiry = $now.AddMilliseconds($leaseMs).ToString("o")
        if ($newExpiry -ne $expiresAtText) {
            $capability.ExpiresAt = $newExpiry
            $changed = $true
        }
    }

    if ($changed -or -not [string]::IsNullOrWhiteSpace($LastAction)) {
        $null = Touch-UiCapabilitySession -SessionState $SessionState -LastAction $LastAction -Reason "keepalive"
    }

    if ($Persist -and $changed) {
        Save-UiCapabilitySessionState -SessionState $SessionState | Out-Null
    }

    return $SessionState
}

function Enable-UiCapability {
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [int]$ProcessId = 0,
        [string]$Reason = "manual-enable",
        [int]$LeaseMilliseconds = 0,
        $Metadata = $null
    )

    $definition = Resolve-UiCapabilityDefinition -CapabilityName $CapabilityName
    if ($definition.ProviderState -ne "available") {
        throw "Capability '$CapabilityName' is not available right now. Current state: $($definition.ProviderState)."
    }

    $session = Start-UiCapabilitySession -ProcessId $ProcessId -Mode "free-explore" -Reason "capability-enable"
    $session = Invoke-UiCapabilityBrokerSweep -SessionState $session -Persist

    $effectiveLeaseMs = if ($LeaseMilliseconds -gt 0) { $LeaseMilliseconds } else { [int]$definition.DefaultLeaseMs }
    $expiresAt = [DateTimeOffset]::Now.AddMilliseconds($effectiveLeaseMs).ToString("o")
    $existing = @($session.ActiveCapabilities | Where-Object {
        -not [string]::Equals($_.Name, $definition.Name, [System.StringComparison]::OrdinalIgnoreCase)
    })

    $activeCapability = [pscustomobject]@{
        Name = $definition.Name
        Category = $definition.Category
        ProviderState = $definition.ProviderState
        Reason = $Reason
        LeaseMilliseconds = $effectiveLeaseMs
        ActivatedAt = (Get-Date).ToString("o")
        ExpiresAt = $expiresAt
        Metadata = $Metadata
    }

    switch -Regex ($definition.Name) {
        "^VideoCapture$" {
            $null = Start-UiVideoCaptureSidecar -ProcessId $ProcessId -Reason $Reason -LeaseMilliseconds $effectiveLeaseMs
            break
        }
        "^AudioCapture$" {
            $null = Start-UiAudioCaptureSidecar -Reason $Reason -LeaseMilliseconds $effectiveLeaseMs
            break
        }
    }

    $session.ActiveCapabilities = @($existing + $activeCapability)
    $session.UpdatedAt = (Get-Date).ToString("o")
    Add-UiCapabilityHistoryEntry -SessionState $session -CapabilityName $definition.Name -Event "enabled" -Reason $Reason -Metadata $Metadata
    Save-UiCapabilitySessionState -SessionState $session | Out-Null

    Write-UiTimelineEvent -Action "Capability.$($definition.Name)" -Stage "enabled" -Success $true -Payload @{
        CapabilityName = $definition.Name
        SessionId = $session.SessionId
        LeaseMilliseconds = $effectiveLeaseMs
        Reason = $Reason
    }

    return [pscustomobject]@{
        Session = $session
        Capability = $activeCapability
    }
}

function Disable-UiCapability {
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [string]$Reason = "manual-disable"
    )

    $session = Read-UiCapabilitySessionStateRaw
    if ($null -eq $session) {
        return $null
    }

    $before = @($session.ActiveCapabilities)
    $after = @($before | Where-Object {
        -not [string]::Equals($_.Name, $CapabilityName, [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($after.Count -eq $before.Count) {
        return $session
    }

    switch -Regex ($CapabilityName) {
        "^VideoCapture$" {
            $null = Stop-UiVideoCaptureSidecar -Reason $Reason
            break
        }
        "^AudioCapture$" {
            $null = Stop-UiAudioCaptureSidecar -Reason $Reason
            break
        }
    }

    $session.ActiveCapabilities = $after
    $session.UpdatedAt = (Get-Date).ToString("o")
    Add-UiCapabilityHistoryEntry -SessionState $session -CapabilityName $CapabilityName -Event "disabled" -Reason $Reason
    Save-UiCapabilitySessionState -SessionState $session | Out-Null

    Write-UiTimelineEvent -Action "Capability.$CapabilityName" -Stage "disabled" -Success $true -Payload @{
        CapabilityName = $CapabilityName
        SessionId = $session.SessionId
        Reason = $Reason
    }

    return $session
}

function Add-UiCapabilityObservationRecord {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$Kind,
        [Parameter(Mandatory)]
        $Payload
    )

    $observation = [ordered]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $SessionState.SessionId
        CapabilityName = $CapabilityName
        Kind = $Kind
        Payload = $Payload
    } | ConvertTo-Json -Depth 8 -Compress

    Add-UiFileLineWithRetry -Path (Get-UiCapabilityObservationPath) -Line $observation

    $recentObservations = @($SessionState.RecentObservations)
    $recentEntry = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        CapabilityName = $CapabilityName
        Kind = $Kind
        Payload = $Payload
    }
    $SessionState.RecentObservations = @($recentEntry) + @($recentObservations | Select-Object -First 11)
}

function Add-UiCapabilityDecisionRecord {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$Decision,
        [Parameter(Mandatory)]
        [string]$Summary,
        $Payload = $null
    )

    $decisionRecord = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        CapabilityName = $CapabilityName
        Action = $ActionName
        Decision = $Decision
        Summary = $Summary
        Payload = $Payload
    }

    $recentDecisions = @($SessionState.RecentDecisions)
    $SessionState.RecentDecisions = @($decisionRecord) + @($recentDecisions | Select-Object -First 11)
    return $decisionRecord
}

function Get-UiRecentCapabilityDecision {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [string]$ActionName = "",
        [string[]]$DecisionKinds = @("triggered")
    )

    return @($SessionState.RecentDecisions | Where-Object {
        [string]::Equals([string]$_.CapabilityName, $CapabilityName, [System.StringComparison]::OrdinalIgnoreCase) -and
        ($DecisionKinds -contains [string]$_.Decision) -and
        ([string]::IsNullOrWhiteSpace($ActionName) -or [string]::Equals([string]$_.Action, $ActionName, [System.StringComparison]::OrdinalIgnoreCase))
    } | Select-Object -First 1)
}

function Get-UiCapabilityCooldownState {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [int]$CooldownMs = 0,
        [string]$ActionName = ""
    )

    if ($CooldownMs -le 0) {
        return [pscustomobject]@{
            IsActive = $false
            RemainingMs = 0
            LastDecision = $null
        }
    }

    $lastDecision = @(Get-UiRecentCapabilityDecision -SessionState $SessionState -CapabilityName $CapabilityName -ActionName $ActionName -DecisionKinds @("triggered") | Select-Object -First 1)
    if ($lastDecision.Count -eq 0) {
        return [pscustomobject]@{
            IsActive = $false
            RemainingMs = 0
            LastDecision = $null
        }
    }

    $elapsedMs = ([DateTimeOffset]::Now - [DateTimeOffset]::Parse([string]$lastDecision[0].Timestamp)).TotalMilliseconds
    $remainingMs = [Math]::Max(0, [int][Math]::Ceiling($CooldownMs - $elapsedMs))
    return [pscustomobject]@{
        IsActive = $remainingMs -gt 0
        RemainingMs = $remainingMs
        LastDecision = $lastDecision[0]
    }
}

function ConvertTo-UiCapabilityDurationLabel {
    param(
        [Nullable[int]]$DurationMs
    )

    if ($null -eq $DurationMs) {
        return $null
    }

    $durationValue = [int]$DurationMs
    if ($durationValue -lt 0) {
        return $null
    }

    if ($durationValue -lt 1000) {
        return "$durationValue ms"
    }

    $seconds = [math]::Round($durationValue / 1000, 1)
    if ($seconds -lt 60) {
        return "$seconds s"
    }

    $minutes = [math]::Floor($seconds / 60)
    $remainingSeconds = [math]::Round($seconds - ($minutes * 60), 1)
    return "$minutes m $remainingSeconds s"
}

function Get-UiCapabilityOperatorStatus {
    param(
        [bool]$SessionIsActive,
        [object[]]$ActiveCapabilities,
        [object[]]$CoolingDownCapabilities,
        $TopDecision = $null
    )

    if (-not $SessionIsActive) {
        return "stopped"
    }

    if (@($CoolingDownCapabilities).Count -gt 0) {
        return "cooling-down"
    }

    if ($null -ne $TopDecision -and [string]::Equals([string]$TopDecision.Decision, "triggered", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "intervened"
    }

    if (@($ActiveCapabilities).Count -gt 0) {
        return "monitoring"
    }

    return "calm"
}

function Get-UiCapabilityOperatorView {
    param(
        $SessionState = $null
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiCapabilitySessionState
    }

    if ($null -eq $SessionState) {
        return [pscustomobject]@{
            Status = "idle"
            Summary = "لا توجد جلسة قدرات حية الآن."
            SecondarySummary = "ابدأ الجلسة فقط عندما تحتاج دعمًا لحظيًا إضافيًا."
            Guidance = "استخدم CapabilityOn فقط عندما تحتاج evidence أو تتبعًا إضافيًا."
            ActiveCapabilities = @()
            CoolingDownCapabilities = @()
            Signals = [pscustomobject]@{
                ActiveCapabilityCount = 0
                CoolingDownCount = 0
                RecentDecisionCount = 0
            }
            LastDecision = $null
            DecisionDigest = @()
            RecentDecisions = @()
        }
    }

    $now = [DateTimeOffset]::Now
    $activeCapabilities = New-Object System.Collections.Generic.List[object]
    foreach ($capability in @($SessionState.ActiveCapabilities)) {
        $definition = Resolve-UiCapabilityDefinition -CapabilityName ([string]$capability.Name)
        $remainingMs = 0
        if (-not [string]::IsNullOrWhiteSpace([string]$capability.ExpiresAt)) {
            $remainingMs = [Math]::Max(0, [int][Math]::Ceiling(([DateTimeOffset]::Parse([string]$capability.ExpiresAt) - $now).TotalMilliseconds))
        }

        $recentDecision = @($SessionState.RecentDecisions | Where-Object {
            [string]::Equals([string]$_.CapabilityName, [string]$capability.Name, [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        $cooldownMs = 0
        switch -Regex ([string]$capability.Name) {
            "^ReactiveAssist$" { $cooldownMs = [int](Resolve-UiReactiveAssistProfile -CapabilityRecord $capability).CooldownMs; break }
            "^FaultWatch$" { $cooldownMs = [int](Resolve-UiFaultWatchProfile -CapabilityRecord $capability).CooldownMs; break }
            "^AutoCaptureOnFailure$" { $cooldownMs = [int](Resolve-UiFailureCaptureProfile -CapabilityRecord $capability).CooldownMs; break }
            default { $cooldownMs = 0 }
        }

        $cooldownState = if ($cooldownMs -gt 0) {
            Get-UiCapabilityCooldownState -SessionState $SessionState -CapabilityName ([string]$capability.Name) -CooldownMs $cooldownMs
        }
        else {
            [pscustomobject]@{
                IsActive = $false
                RemainingMs = 0
                LastDecision = $null
            }
        }

        $state = if ($cooldownState.IsActive -and $recentDecision.Count -gt 0 -and [string]$recentDecision[0].Decision -eq "suppressed") {
            "cooling-down"
        }
        elseif ($recentDecision.Count -gt 0 -and [string]$recentDecision[0].Decision -eq "triggered") {
            "recently-triggered"
        }
        else {
            "active"
        }

        $stateSummary = switch ($state) {
            "cooling-down" { "تركت الأداة هذه القدرة تهدأ مؤقتًا كي لا تكرر نفس evidence." ; break }
            "recently-triggered" { "تدخلت هذه القدرة قبل لحظات وقدمت evidence أوضح." ; break }
            default { "هذه القدرة جاهزة الآن وتعمل فقط عند الحاجة." }
        }

        [void]$activeCapabilities.Add([pscustomobject]@{
            Name = [string]$capability.Name
            Category = [string]$definition.Category
            State = $state
            StateSummary = $stateSummary
            Reason = [string]$capability.Reason
            LeaseRemainingMs = $remainingMs
            LeaseRemainingLabel = ConvertTo-UiCapabilityDurationLabel -DurationMs $remainingMs
            CooldownRemainingMs = [int]$cooldownState.RemainingMs
            CooldownRemainingLabel = ConvertTo-UiCapabilityDurationLabel -DurationMs ([int]$cooldownState.RemainingMs)
            LastDecision = if ($recentDecision.Count -gt 0) { $recentDecision[0] } else { $null }
            Description = [string]$definition.Description
        })
    }

    $activeCapabilityArray = @($activeCapabilities.ToArray())
    $coolingDownCapabilities = @($activeCapabilityArray | Where-Object { $_.State -eq "cooling-down" })
    $recentDecisions = @($SessionState.RecentDecisions | Select-Object -First 5)
    $topDecision = if ($recentDecisions.Count -gt 0) { $recentDecisions[0] } else { $null }
    $status = Get-UiCapabilityOperatorStatus `
        -SessionIsActive ([bool]$SessionState.IsActive) `
        -ActiveCapabilities $activeCapabilityArray `
        -CoolingDownCapabilities $coolingDownCapabilities `
        -TopDecision $topDecision

    $summary = ""
    $secondarySummary = ""
    $guidance = ""
    switch ($status) {
        "stopped" {
            $summary = "جلسة القدرات متوقفة الآن؛ لن تتدخل الأداة لحظيًا حتى تعيد تفعيل ما تحتاجه."
            $secondarySummary = "لا توجد أي قدرة تعمل في الخلفية حاليًا."
            $guidance = "أعد تشغيل CapabilityOn فقط عندما تحتاج evidence أو تتبعًا إضافيًا داخل نفس الاستكشاف."
            break
        }
        "cooling-down" {
            $names = @($coolingDownCapabilities | ForEach-Object { $_.Name })
            $summary = "الأداة رأت anomaly مهمة قبل لحظات ثم دخلت تهدئة مؤقتة كي لا تكرر evidence بشكل مزعج."
            $secondarySummary = "التهدئة الجارية الآن: $($names -join '، ')"
            $guidance = "واصل نفس المسار طبيعيًا؛ إذا استمرت الغرابة بعد انتهاء cooldown سنرى evidence جديدة بوضوح أكبر."
            break
        }
        "intervened" {
            $decisionSummary = if ($null -ne $topDecision) { [string]$topDecision.Summary } else { "التقطت الأداة evidence مفيدة قبل لحظات." }
            $summary = "الأداة تدخلت في اللحظة المناسبة وقدمت evidence أوضح دون أن توقف الاستكشاف."
            $secondarySummary = $decisionSummary
            $guidance = "راجع آخر artifact فقط إذا بقي السلوك غير واضح، ثم واصل نفس المسار ولا تغيّر أسلوبك بلا حاجة."
            break
        }
        "monitoring" {
            $names = @($activeCapabilityArray | ForEach-Object { $_.Name })
            $summary = "الأداة الآن في وضع مراقبة خفيف: $($names -join '، ')"
            $secondarySummary = "لا يوجد تدخل مزعج الآن؛ القدرات النشطة ستعمل فقط إذا احتاج المسار الحالي ذلك."
            $guidance = "تابع الاستكشاف بحرية؛ شغّل قدرة إضافية فقط إذا شعرت أن evidence الحالية لا تكفي."
            break
        }
        default {
            $summary = "الوضع الآن خفيف وهادئ؛ لا توجد قدرات إضافية تعمل في الخلفية."
            $secondarySummary = "الاستكشاف يمكن أن يستمر كما هو من غير أي عبء تشخيصي إضافي."
            $guidance = "ابق على هذا الإيقاع، وفعّل capability فقط عند لحظة احتياج حقيقية."
        }
    }

    $decisionDigest = @($recentDecisions | ForEach-Object {
        [pscustomobject]@{
            CapabilityName = [string]$_.CapabilityName
            Decision = [string]$_.Decision
            Action = [string]$_.Action
            Summary = [string]$_.Summary
            Headline = ("$([string]$_.CapabilityName) -> $([string]$_.Decision): $([string]$_.Summary)")
        }
    })

    return [pscustomobject]@{
        Status = $status
        Summary = $summary
        SecondarySummary = $secondarySummary
        Guidance = $guidance
        ActiveCapabilities = [object[]]$activeCapabilityArray
        CoolingDownCapabilities = [object[]]$coolingDownCapabilities
        Signals = [pscustomobject]@{
            ActiveCapabilityCount = $activeCapabilityArray.Count
            CoolingDownCount = $coolingDownCapabilities.Count
            RecentDecisionCount = $recentDecisions.Count
            SessionMode = [string]$SessionState.Mode
            LastAction = [string]$SessionState.LastAction
        }
        LastDecision = $topDecision
        DecisionDigest = [object[]]$decisionDigest
        RecentDecisions = [object[]]$recentDecisions
    }
}

function Save-UiCapabilityActionCapture {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$CaptureReason,
        [string]$CaptureSuffix = ""
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $capabilityRoot = Split-Path -Parent (Get-UiCapabilitySessionPath)
    $artifactRoot = Join-Path $capabilityRoot ("capabilities\\" + $session.SessionId + "\\" + $CapabilityName)
    if (-not (Test-Path -LiteralPath $artifactRoot)) {
        New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    $safeAction = if ([string]::IsNullOrWhiteSpace($ActionName)) { "action" } else { $ActionName -replace '[^A-Za-z0-9_-]', "-" }
    $safeSuffix = if ([string]::IsNullOrWhiteSpace($CaptureSuffix)) { "" } else { "-" + ($CaptureSuffix -replace '[^A-Za-z0-9_-]', "-") }
    $capturePath = Join-Path $artifactRoot ($timestamp + "-" + $safeAction + $safeSuffix + ".png")

    $windows = @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground)
    $meaningfulWindows = @($windows | Where-Object {
        $_.AutomationId -eq "Shell.MainWindow" -or -not [string]::IsNullOrWhiteSpace($_.Name)
    })
    $externalForegroundWindow = $meaningfulWindows |
        Where-Object { $_.AutomationId -ne "Shell.MainWindow" -and $_.ProcessId -ne $Process.Id } |
        Select-Object -First 1

    if ($null -ne $externalForegroundWindow) {
        Save-UiDesktopScreenshot -Path $capturePath | Out-Null
        $captureMode = "Desktop"
    }
    else {
        $window = Resolve-UiWindow -ProcessId $Process.Id
        Save-UiWindowScreenshot -Window $window -Path $capturePath | Out-Null
        $captureMode = "Window"
    }

    $artifact = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $session.SessionId
        CapabilityName = $CapabilityName
        Action = $ActionName
        Reason = $CaptureReason
        CaptureMode = $captureMode
        Path = [System.IO.Path]::GetFullPath($capturePath)
    }

    Add-UiCapabilityObservationRecord -SessionState $session -CapabilityName $CapabilityName -Kind "capture" -Payload $artifact
    $recentArtifacts = @($session.RecentArtifacts)
    $session.RecentArtifacts = @($artifact) + @($recentArtifacts | Select-Object -First 11)
    $session.UpdatedAt = (Get-Date).ToString("o")
    Save-UiCapabilitySessionState -SessionState $session | Out-Null
    return $artifact
}

function Save-UiCapabilityBurstSequence {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$CaptureReason
    )

    $profile = Resolve-UiBurstCaptureProfile -CapabilityRecord $CapabilityRecord
    $captures = New-Object System.Collections.Generic.List[object]
    $startedAt = Get-Date

    for ($index = 1; $index -le $profile.FrameCount; $index++) {
        $capture = Save-UiCapabilityActionCapture `
            -Process $Process `
            -CapabilityName ([string]$CapabilityRecord.Name) `
            -ActionName $ActionName `
            -CaptureReason $CaptureReason `
            -CaptureSuffix ("f" + $index.ToString("00"))

        if ($null -ne $capture) {
            [void]$captures.Add($capture)
        }

        if ($index -lt $profile.FrameCount -and $profile.IntervalMs -gt 0) {
            Start-Sleep -Milliseconds $profile.IntervalMs
        }
    }

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return [pscustomobject]@{
            Session = $null
            Captures = [object[]]$captures.ToArray()
            Burst = $null
        }
    }

    $burstSummary = $null
    if ($captures.Count -gt 0) {
        $contactSheetPath = $null
        if ($profile.CreateContactSheet -and $captures.Count -gt 1) {
            $firstCapturePath = [string]$captures[0].Path
            $contactSheetPath = [System.IO.Path]::ChangeExtension($firstCapturePath, $null) + "-sheet.png"
            New-UiContactSheet -ImagePaths @($captures | ForEach-Object { $_.Path }) -DestinationPath $contactSheetPath -Columns ([Math]::Min(4, $captures.Count)) | Out-Null
        }

        $burstSummary = [pscustomobject]@{
            Timestamp = (Get-Date).ToString("o")
            SessionId = $session.SessionId
            CapabilityName = [string]$CapabilityRecord.Name
            Action = $ActionName
            Reason = $CaptureReason
            FrameCount = $captures.Count
            IntervalMs = $profile.IntervalMs
            StartedAt = $startedAt.ToString("o")
            EndedAt = (Get-Date).ToString("o")
            ContactSheetPath = if (-not [string]::IsNullOrWhiteSpace($contactSheetPath)) { [System.IO.Path]::GetFullPath($contactSheetPath) } else { $null }
            FramePaths = @($captures | ForEach-Object { $_.Path })
        }

        Add-UiCapabilityObservationRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -Kind "burst-sequence" -Payload $burstSummary
        $session.UpdatedAt = (Get-Date).ToString("o")
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
    }

    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = [object[]]$captures.ToArray()
        Burst = $burstSummary
    }
}

function Save-UiMouseTraceObservation {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        $Result = $null,
        [string]$CaptureReason = "after-action",
        [string]$ErrorMessage = ""
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $profile = Resolve-UiMouseTraceProfile -CapabilityRecord $CapabilityRecord
    $samples = New-Object System.Collections.Generic.List[object]
    $startedAt = Get-Date

    for ($index = 1; $index -le $profile.SampleCount; $index++) {
        $cursor = Get-UiCursorPosition
        [void]$samples.Add([pscustomobject]@{
            Index = $index
            Timestamp = (Get-Date).ToString("o")
            X = [int]$cursor.X
            Y = [int]$cursor.Y
        })

        if ($index -lt $profile.SampleCount -and $profile.IntervalMs -gt 0) {
            Start-Sleep -Milliseconds $profile.IntervalMs
        }
    }

    $sampleArray = @($samples.ToArray())
    $firstSample = $sampleArray | Select-Object -First 1
    $lastSample = $sampleArray | Select-Object -Last 1
    $distinctPoints = @($sampleArray | Group-Object X, Y)

    $actionSnapshot = $null
    if ($null -ne $Result) {
        $actionSnapshot = [ordered]@{}
        foreach ($propertyName in @("Action", "Target", "Window", "Position", "StartPosition", "EndPosition", "Button", "ClickCount", "HoverMilliseconds", "ScrollDelta", "DeltaX", "DeltaY")) {
            if ($Result.PSObject.Properties.Name -contains $propertyName) {
                $actionSnapshot[$propertyName] = $Result.$propertyName
            }
        }
        $actionSnapshot = [pscustomobject]$actionSnapshot
    }

    $payload = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $session.SessionId
        CapabilityName = [string]$CapabilityRecord.Name
        Action = $ActionName
        Reason = $CaptureReason
        StartedAt = $startedAt.ToString("o")
        EndedAt = (Get-Date).ToString("o")
        SampleCount = $sampleArray.Count
        IntervalMs = $profile.IntervalMs
        DistinctPointCount = $distinctPoints.Count
        FirstPosition = if ($null -ne $firstSample) { [pscustomobject]@{ X = [int]$firstSample.X; Y = [int]$firstSample.Y } } else { $null }
        LastPosition = if ($null -ne $lastSample) { [pscustomobject]@{ X = [int]$lastSample.X; Y = [int]$lastSample.Y } } else { $null }
        Samples = [object[]]$sampleArray
        ActionSnapshot = $actionSnapshot
        Error = if ([string]::IsNullOrWhiteSpace($ErrorMessage)) { $null } else { $ErrorMessage }
    }

    Add-UiCapabilityObservationRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -Kind "mouse-trace" -Payload $payload
    $session.UpdatedAt = (Get-Date).ToString("o")
    Save-UiCapabilitySessionState -SessionState $session | Out-Null

    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Observation = $payload
    }
}

function Get-UiCapabilityWindowHealth {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    $windows = @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground)
    $meaningfulWindows = @($windows | Where-Object {
        $_.AutomationId -eq "Shell.MainWindow" -or -not [string]::IsNullOrWhiteSpace($_.Name)
    })

    $dialogSummary = $null
    try {
        $dialog = Get-UiActiveDialog -ProcessId $Process.Id
        if ($null -ne $dialog) {
            $dialogSummary = Get-UiElementSummary -Element $dialog
        }
    }
    catch {
    }

    $externalForegroundWindow = $meaningfulWindows |
        Where-Object { $_.AutomationId -ne "Shell.MainWindow" -and $_.ProcessId -ne $Process.Id } |
        Select-Object -First 1

    return [pscustomobject]@{
        OpenWindowCount = $meaningfulWindows.Count
        ActiveDialogTitle = if ($null -ne $externalForegroundWindow) { [string]$externalForegroundWindow.Name } elseif ($null -ne $dialogSummary) { [string]$dialogSummary.Name } else { $null }
        ExternalForegroundWindow = $externalForegroundWindow
        Dialog = $dialogSummary
        Windows = [object[]]$meaningfulWindows
    }
}

function Add-UiFailureBundleObservation {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$ErrorMessage,
        [object[]]$Captures = @(),
        $Health = $null
    )

    $shellState = Get-UiShellStateSnapshot
    $payload = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $SessionState.SessionId
        CapabilityName = $CapabilityName
        Action = $ActionName
        Error = $ErrorMessage
        CaptureCount = @($Captures).Count
        CapturePaths = @($Captures | ForEach-Object { $_.Path })
        Health = if ($null -ne $Health) {
            [pscustomobject]@{
                OpenWindowCount = $Health.OpenWindowCount
                ActiveDialogTitle = $Health.ActiveDialogTitle
                ExternalWindowTitle = if ($null -ne $Health.ExternalForegroundWindow) { [string]$Health.ExternalForegroundWindow.Name } else { $null }
            }
        }
        else {
            $null
        }
        ShellState = $shellState
    }

    Add-UiCapabilityObservationRecord -SessionState $SessionState -CapabilityName $CapabilityName -Kind "failure-bundle" -Payload $payload
    $SessionState.UpdatedAt = (Get-Date).ToString("o")
    Save-UiCapabilitySessionState -SessionState $SessionState | Out-Null
    return $payload
}

function Invoke-UiFaultWatchAfterAction {
    param(
        [int]$ProcessId = 0,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        [string]$CaptureReason = "after-action",
        [string]$ErrorMessage = ""
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    if ($ProcessId -le 0) {
        $ProcessId = [int]$session.ProcessId
    }

    $profile = Resolve-UiFaultWatchProfile -CapabilityRecord $CapabilityRecord
    $signals = @(Get-UiRecentFaultSignals -ProcessId $ProcessId -MaxCount 8 -LookbackSeconds $profile.LookbackSeconds -IncludeProcessExit:$profile.TriggerOnProcessExit)
    if (-not $profile.TriggerOnRuntimeFault) {
        $signals = @($signals | Where-Object { [string]$_.Kind -eq "process-exited" })
    }

    if ($signals.Count -eq 0) {
        Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "quiet" -Summary "لم تظهر إشارات fault جديدة تستحق التدخل بعد هذا الفعل." -Payload ([pscustomobject]@{
            DurationMs = [math]::Round($DurationMs, 2)
            ProcessId = $ProcessId
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Observation = $null
        }
    }

    $signalKey = (@($signals | ForEach-Object { [string]$_.Signature } | Select-Object -First 4) -join "|")
    $latestSignalTimestamp = [string](($signals | Select-Object -First 1).Timestamp)
    $cooldown = Get-UiCapabilityCooldownState -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -CooldownMs $profile.CooldownMs -ActionName $ActionName
    $lastTriggered = @(Get-UiRecentCapabilityDecision -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -DecisionKinds @("triggered") | Select-Object -First 1)
    $sameSignalKey = $lastTriggered.Count -gt 0 -and [string]$lastTriggered[0].Payload.SignalKey -eq $signalKey
    $sameSignalTimestamp = $sameSignalKey -and [string]$lastTriggered[0].Payload.LatestSignalTimestamp -eq $latestSignalTimestamp
    if ($sameSignalTimestamp -and -not $cooldown.IsActive) {
        Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "quiet" -Summary "لا توجد إشارة fault جديدة؛ الإشارة الحالية سبق التقاطها وتفسيرها بالفعل." -Payload ([pscustomobject]@{
            SignalKey = $signalKey
            LatestSignalTimestamp = $latestSignalTimestamp
            SignalCount = $signals.Count
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Observation = $null
        }
    }
    if ($cooldown.IsActive -and $sameSignalKey) {
        Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "suppressed" -Summary "ظهرت نفس إشارات fault مجددًا، لذلك تجاهلت الأداة إعادة evidence مؤقتًا حتى لا تصبح مزعجة." -Payload ([pscustomobject]@{
            RemainingMs = $cooldown.RemainingMs
            SignalKey = $signalKey
            LatestSignalTimestamp = $latestSignalTimestamp
            SignalCount = $signals.Count
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Observation = $null
        }
    }

    $captures = New-Object System.Collections.Generic.List[object]
    $burstSummary = $null
    $usedExistingBurstFlow = $false
    if (Test-UiCapabilityEnabled -CapabilityName "BurstCapture" -SessionState $session) {
        $usedExistingBurstFlow = $true
    }
    else {
        $targetProcess = Get-UiMediaTargetProcess -ProcessId $ProcessId
        $faultCaptureRecord = [pscustomobject]@{
            Name = [string]$CapabilityRecord.Name
            Metadata = [pscustomobject]@{
                FrameCount = $profile.FrameCount
                IntervalMs = $profile.IntervalMs
                CreateContactSheet = $profile.CreateContactSheet
            }
        }

        if ($null -ne $targetProcess) {
            $burstPayload = Save-UiCapabilityBurstSequence -Process $targetProcess -CapabilityRecord $faultCaptureRecord -ActionName $ActionName -CaptureReason "fault-signal"
            foreach ($capture in @($burstPayload.Captures)) {
                [void]$captures.Add($capture)
            }
            $burstSummary = $burstPayload.Burst
        }
    }

    $topSignal = $signals | Select-Object -First 1
    $sessionForDecision = Get-UiCapabilitySessionState
    Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "triggered" -Summary "رُصدت إشارة fault واضحة من داخل البرنامج، لذلك جُمعت evidence مباشرة من المصدر بدل انتظار صوت النظام." -Payload ([pscustomobject]@{
        SignalKey = $signalKey
        LatestSignalTimestamp = $latestSignalTimestamp
        SignalCount = $signals.Count
        TopSignalKind = if ($null -ne $topSignal) { [string]$topSignal.Kind } else { "" }
        TopSignalTitle = if ($null -ne $topSignal) { [string]$topSignal.Title } else { "" }
        CaptureCount = $captures.Count
        UsedExistingBurstFlow = $usedExistingBurstFlow
    }) | Out-Null
    Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $payload = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $session.SessionId
        CapabilityName = [string]$CapabilityRecord.Name
        Action = $ActionName
        Reason = $CaptureReason
        DurationMs = [math]::Round($DurationMs, 2)
        SignalCount = $signals.Count
        Signals = [object[]]$signals
        TrustedSignalCount = @($signals | Where-Object TrustedForReasoning).Count
        SignalKey = $signalKey
        Error = if ([string]::IsNullOrWhiteSpace($ErrorMessage)) { $null } else { $ErrorMessage }
        CaptureCount = $captures.Count
        ContactSheetPath = if ($null -ne $burstSummary) { $burstSummary.ContactSheetPath } else { $null }
        UsedExistingBurstFlow = $usedExistingBurstFlow
        ProcessId = $ProcessId
        ProcessRunning = $null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
    }

    Add-UiCapabilityObservationRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -Kind "fault-signal" -Payload $payload
    $session.UpdatedAt = (Get-Date).ToString("o")
    Save-UiCapabilitySessionState -SessionState $session | Out-Null

    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = [object[]]$captures.ToArray()
        Observation = $payload
    }
}

function Invoke-UiReactiveAssistAfterAction {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        $Result = $null
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $profile = Resolve-UiReactiveAssistProfile -CapabilityRecord $CapabilityRecord
    $health = Get-UiCapabilityWindowHealth -Process $Process
    $thresholds = Get-UiLatencyThresholds -ActionName $ActionName
    $reasons = New-Object System.Collections.Generic.List[object]

    if ($profile.TriggerOnSlowAction -and $DurationMs -ge [double]$profile.SlowActionMs) {
        [void]$reasons.Add([pscustomobject]@{
            Kind = "slow-action"
            ActualMs = [math]::Round($DurationMs, 2)
            ThresholdMs = [int]$profile.SlowActionMs
            CalibratedSlowMs = [int]$thresholds.SlowMs
        })
    }

    if ($profile.TriggerOnExternalWindow -and $null -ne $health.ExternalForegroundWindow) {
        [void]$reasons.Add([pscustomobject]@{
            Kind = "external-window"
            WindowTitle = [string]$health.ExternalForegroundWindow.Name
            WindowAutomationId = [string]$health.ExternalForegroundWindow.AutomationId
        })
    }

    if ($profile.TriggerOnDialog -and -not [string]::IsNullOrWhiteSpace([string]$health.ActiveDialogTitle)) {
        [void]$reasons.Add([pscustomobject]@{
            Kind = "dialog-visible"
            Title = [string]$health.ActiveDialogTitle
        })
    }

    if ($reasons.Count -eq 0) {
        Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "quiet" -Summary "لم تُرصد anomaly تستدعي التدخل بعد هذا الفعل." -Payload ([pscustomobject]@{
            DurationMs = [math]::Round($DurationMs, 2)
            OpenWindowCount = $health.OpenWindowCount
            ActiveDialogTitle = $health.ActiveDialogTitle
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Observation = $null
        }
    }

    $cooldown = Get-UiCapabilityCooldownState -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -CooldownMs $profile.CooldownMs -ActionName $ActionName
    if ($cooldown.IsActive) {
        Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "suppressed" -Summary "رُصدت anomaly لكن ReactiveAssist تجاهلت التفعيل مؤقتًا لتجنب الإزعاج المتكرر." -Payload ([pscustomobject]@{
            RemainingMs = $cooldown.RemainingMs
            Reasons = [object[]]@($reasons.ToArray())
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $session | Out-Null
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Observation = $null
        }
    }

    $captures = New-Object System.Collections.Generic.List[object]
    $burstSummary = $null
    $suppressedBecauseBurstCaptureActive = $false
    if (Test-UiCapabilityEnabled -CapabilityName "BurstCapture" -SessionState $session) {
        $suppressedBecauseBurstCaptureActive = $true
        $sessionForDecision = Get-UiCapabilitySessionState
        Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "suppressed" -Summary "رُصدت anomaly لكن BurstCapture كانت نشطة أصلًا، لذلك لم نكرر evidence إضافية." -Payload ([pscustomobject]@{
            Reasons = [object[]]@($reasons.ToArray())
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
    }
    else {
        $reactiveCapabilityRecord = [pscustomobject]@{
            Name = [string]$CapabilityRecord.Name
            Metadata = [pscustomobject]@{
                FrameCount = $profile.FrameCount
                IntervalMs = $profile.IntervalMs
                CreateContactSheet = $profile.CreateContactSheet
            }
        }

        $burstPayload = Save-UiCapabilityBurstSequence -Process $Process -CapabilityRecord $reactiveCapabilityRecord -ActionName $ActionName -CaptureReason "reactive-anomaly"
        foreach ($capture in @($burstPayload.Captures)) {
            [void]$captures.Add($capture)
        }
        $burstSummary = $burstPayload.Burst
        $sessionForDecision = Get-UiCapabilitySessionState
        Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName ([string]$CapabilityRecord.Name) -ActionName $ActionName -Decision "triggered" -Summary "فعّلت ReactiveAssist evidence بصرية خفيفة لأن anomaly الحالية تستحق متابعة." -Payload ([pscustomobject]@{
            Reasons = [object[]]@($reasons.ToArray())
            CaptureCount = $captures.Count
        }) | Out-Null
        Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
    }

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $payload = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        SessionId = $session.SessionId
        CapabilityName = [string]$CapabilityRecord.Name
        Action = $ActionName
        DurationMs = [math]::Round($DurationMs, 2)
        Reasons = [object[]]@($reasons.ToArray())
        Health = [pscustomobject]@{
            OpenWindowCount = $health.OpenWindowCount
            ActiveDialogTitle = $health.ActiveDialogTitle
            ExternalWindowTitle = if ($null -ne $health.ExternalForegroundWindow) { [string]$health.ExternalForegroundWindow.Name } else { $null }
        }
        SuppressedBecauseBurstCaptureActive = $suppressedBecauseBurstCaptureActive
        CaptureCount = $captures.Count
        ContactSheetPath = if ($null -ne $burstSummary) { $burstSummary.ContactSheetPath } else { $null }
    }

    Add-UiCapabilityObservationRecord -SessionState $session -CapabilityName ([string]$CapabilityRecord.Name) -Kind "reactive-trigger" -Payload $payload
    $session.UpdatedAt = (Get-Date).ToString("o")
    Save-UiCapabilitySessionState -SessionState $session | Out-Null

    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = [object[]]$captures.ToArray()
        Observation = $payload
    }
}

function Invoke-UiCapabilityHooksAfterAction {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        $Result = $null
    )

    $session = Invoke-UiCapabilityBrokerSweep -Persist
    if ($null -eq $session -or -not $session.IsActive) {
        return $null
    }

    $captures = New-Object System.Collections.Generic.List[object]
    if (Test-UiCapabilityEnabled -CapabilityName "ExplorationAssist" -SessionState $session) {
        $explorationAssistCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "ExplorationAssist", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($explorationAssistCapability.Count -gt 0) {
            $heuristicPayload = Invoke-UiExplorationAssistAfterAction -Process $Process -CapabilityRecord $explorationAssistCapability[0] -ActionName $ActionName -DurationMs $DurationMs -Result $Result
            foreach ($capture in @($heuristicPayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    if (Test-UiCapabilityEnabled -CapabilityName "BurstCapture" -SessionState $session) {
        $burstCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "BurstCapture", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($burstCapability.Count -gt 0) {
            $burstPayload = Save-UiCapabilityBurstSequence -Process $Process -CapabilityRecord $burstCapability[0] -ActionName $ActionName -CaptureReason "after-action"
            foreach ($capture in @($burstPayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    if (Test-UiCapabilityEnabled -CapabilityName "MouseTrace" -SessionState $session) {
        $mouseTraceCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "MouseTrace", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($mouseTraceCapability.Count -gt 0) {
            Save-UiMouseTraceObservation -Process $Process -CapabilityRecord $mouseTraceCapability[0] -ActionName $ActionName -Result $Result -CaptureReason "after-action" | Out-Null
        }
    }

    if (Test-UiCapabilityEnabled -CapabilityName "ReactiveAssist" -SessionState $session) {
        $reactiveAssistCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "ReactiveAssist", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($reactiveAssistCapability.Count -gt 0) {
            $reactivePayload = Invoke-UiReactiveAssistAfterAction -Process $Process -CapabilityRecord $reactiveAssistCapability[0] -ActionName $ActionName -DurationMs $DurationMs -Result $Result
            foreach ($capture in @($reactivePayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    if (Test-UiCapabilityEnabled -CapabilityName "FaultWatch" -SessionState $session) {
        $faultWatchCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "FaultWatch", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($faultWatchCapability.Count -gt 0) {
            $faultPayload = Invoke-UiFaultWatchAfterAction -ProcessId $Process.Id -CapabilityRecord $faultWatchCapability[0] -ActionName $ActionName -DurationMs $DurationMs -CaptureReason "after-action"
            foreach ($capture in @($faultPayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    Touch-UiCapabilitySession -SessionState (Get-UiCapabilitySessionState) -LastAction $ActionName -Reason "after-action" | Out-Null

    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = [object[]]$captures.ToArray()
    }
}

function Invoke-UiCapabilityHooksOnFailure {
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        [string]$ErrorMessage = ""
    )

    $session = Invoke-UiCapabilityBrokerSweep -Persist
    if ($null -eq $session -or -not $session.IsActive) {
        return $null
    }

    $process = Get-UiProcess

    if (Test-UiCapabilityEnabled -CapabilityName "MouseTrace" -SessionState $session) {
        $mouseTraceCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "MouseTrace", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($mouseTraceCapability.Count -gt 0) {
            try {
                Save-UiMouseTraceObservation -Process $(Get-UiProcess) -CapabilityRecord $mouseTraceCapability[0] -ActionName $ActionName -CaptureReason "failure" -ErrorMessage $ErrorMessage | Out-Null
            }
            catch {
            }
        }
    }

    $captures = New-Object System.Collections.Generic.List[object]
    if (Test-UiCapabilityEnabled -CapabilityName "ExplorationAssist" -SessionState $session) {
        $explorationAssistCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "ExplorationAssist", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($explorationAssistCapability.Count -gt 0) {
            $heuristicPayload = Invoke-UiExplorationAssistOnFailure -Process $process -CapabilityRecord $explorationAssistCapability[0] -ActionName $ActionName -DurationMs $DurationMs -ErrorMessage $ErrorMessage
            foreach ($capture in @($heuristicPayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    if (Test-UiCapabilityEnabled -CapabilityName "FaultWatch" -SessionState $session) {
        $faultWatchCapability = @($session.ActiveCapabilities | Where-Object {
            [string]::Equals($_.Name, "FaultWatch", [System.StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)

        if ($faultWatchCapability.Count -gt 0) {
            $faultPayload = Invoke-UiFaultWatchAfterAction -ProcessId ([int]$session.ProcessId) -CapabilityRecord $faultWatchCapability[0] -ActionName $ActionName -DurationMs $DurationMs -CaptureReason "failure" -ErrorMessage $ErrorMessage
            foreach ($capture in @($faultPayload.Captures)) {
                [void]$captures.Add($capture)
            }
        }
    }

    if (-not (Test-UiCapabilityEnabled -CapabilityName "AutoCaptureOnFailure" -SessionState $session)) {
        return [pscustomobject]@{
            Session = $session
            Captures = [object[]]$captures.ToArray()
        }
    }

    if ($null -eq $process) {
        return [pscustomobject]@{
            Session = $session
            Captures = [object[]]$captures.ToArray()
        }
    }

    $failureCapability = @($session.ActiveCapabilities | Where-Object {
        [string]::Equals($_.Name, "AutoCaptureOnFailure", [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)

    $failureCaptures = @()
    if ($failureCapability.Count -gt 0) {
        $profile = Resolve-UiFailureCaptureProfile -CapabilityRecord $failureCapability[0]
        $cooldown = Get-UiCapabilityCooldownState -SessionState $session -CapabilityName "AutoCaptureOnFailure" -CooldownMs $profile.CooldownMs -ActionName $ActionName
        if ($cooldown.IsActive) {
            Add-UiCapabilityDecisionRecord -SessionState $session -CapabilityName "AutoCaptureOnFailure" -ActionName $ActionName -Decision "suppressed" -Summary "تكرر الفشل نفسه سريعًا، لذلك تم suppress لالتقاط إضافي حتى لا تصبح الأداة مزعجة." -Payload ([pscustomobject]@{
                RemainingMs = $cooldown.RemainingMs
                Error = $ErrorMessage
            }) | Out-Null
            Save-UiCapabilitySessionState -SessionState $session | Out-Null
        }
        else {
            $failurePayload = Save-UiCapabilityBurstSequence -Process $process -CapabilityRecord $failureCapability[0] -ActionName $ActionName -CaptureReason "failure"
            $failureCaptures = @($failurePayload.Captures)
            $sessionForDecision = Get-UiCapabilitySessionState
            Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName "AutoCaptureOnFailure" -ActionName $ActionName -Decision "triggered" -Summary "فعّلت AutoCaptureOnFailure evidence متعددة لأن الفشل الحالي يستحق دليلًا أوضح." -Payload ([pscustomobject]@{
                Error = $ErrorMessage
                CaptureCount = $failureCaptures.Count
            }) | Out-Null
            Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
        }
    }

    $health = Get-UiCapabilityWindowHealth -Process $process
    $session = Get-UiCapabilitySessionState
    if ($null -ne $session) {
        Add-UiFailureBundleObservation -SessionState $session -CapabilityName "AutoCaptureOnFailure" -ActionName $ActionName -ErrorMessage $ErrorMessage -Captures $failureCaptures -Health $health | Out-Null
    }

    Touch-UiCapabilitySession -SessionState (Get-UiCapabilitySessionState) -LastAction $ActionName -Reason "failure" | Out-Null
    foreach ($capture in @($failureCaptures)) {
        [void]$captures.Add($capture)
    }
    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = [object[]]$captures.ToArray()
    }
}
