function Get-UiCapabilityDefinitions {
    return @(
        [pscustomobject]@{
            Name = "BurstCapture"
            Category = "Visual"
            ProviderState = "available"
            DefaultLeaseMs = 1800
            Description = "يلتقط لقطات سريعة خفيفة أثناء نفس الاستكشاف الحر عند الحاجة."
        },
        [pscustomobject]@{
            Name = "AutoCaptureOnFailure"
            Category = "Visual"
            ProviderState = "available"
            DefaultLeaseMs = 3000
            Description = "يلتقط لقطة تلقائية عند فشل الفعل أثناء الاستكشاف."
        },
        [pscustomobject]@{
            Name = "VideoCapture"
            Category = "Media"
            ProviderState = "planned"
            DefaultLeaseMs = 4000
            Description = "تسجيل فيديو قصير عند الطلب أو عند trigger."
        },
        [pscustomobject]@{
            Name = "AudioCapture"
            Category = "Media"
            ProviderState = "planned"
            DefaultLeaseMs = 4000
            Description = "التقاط صوت قصير عند الحاجة فقط."
        },
        [pscustomobject]@{
            Name = "MouseTrace"
            Category = "Input"
            ProviderState = "planned"
            DefaultLeaseMs = 2500
            Description = "تتبع/تعزيز استخدام الماوس أثناء المسارات الحساسة."
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
        return $SessionState
    }

    $now = [DateTimeOffset]::Now
    $kept = New-Object System.Collections.Generic.List[object]
    $changed = $false
    foreach ($capability in $activeCapabilities) {
        $expiresAtText = [string]$capability.ExpiresAt
        if (-not [string]::IsNullOrWhiteSpace($expiresAtText) -and [DateTimeOffset]::Parse($expiresAtText) -le $now) {
            $changed = $true
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
        throw "Capability '$CapabilityName' is not implemented yet. Current state: $($definition.ProviderState)."
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

function Save-UiCapabilityActionCapture {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$CapabilityName,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$CaptureReason
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
    $capturePath = Join-Path $artifactRoot ($timestamp + "-" + $safeAction + ".png")

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

function Invoke-UiCapabilityHooksAfterAction {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [Parameter(Mandatory)]
        [string]$ActionName,
        $Result = $null
    )

    $session = Invoke-UiCapabilityBrokerSweep -Persist
    if ($null -eq $session -or -not $session.IsActive) {
        return $null
    }

    $captures = New-Object System.Collections.Generic.List[object]
    if (Test-UiCapabilityEnabled -CapabilityName "BurstCapture" -SessionState $session) {
        $capture = Save-UiCapabilityActionCapture -Process $Process -CapabilityName "BurstCapture" -ActionName $ActionName -CaptureReason "after-action"
        if ($null -ne $capture) {
            [void]$captures.Add($capture)
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
        [string]$ErrorMessage = ""
    )

    $session = Invoke-UiCapabilityBrokerSweep -Persist
    if ($null -eq $session -or -not $session.IsActive) {
        return $null
    }

    if (-not (Test-UiCapabilityEnabled -CapabilityName "AutoCaptureOnFailure" -SessionState $session)) {
        return [pscustomobject]@{
            Session = $session
            Captures = @()
        }
    }

    $process = Get-UiProcess
    if ($null -eq $process) {
        return [pscustomobject]@{
            Session = $session
            Captures = @()
        }
    }

    $capture = Save-UiCapabilityActionCapture -Process $process -CapabilityName "AutoCaptureOnFailure" -ActionName $ActionName -CaptureReason "failure"
    Touch-UiCapabilitySession -SessionState (Get-UiCapabilitySessionState) -LastAction $ActionName -Reason "failure" | Out-Null
    return [pscustomobject]@{
        Session = Get-UiCapabilitySessionState
        Captures = if ($null -ne $capture) { @($capture) } else { @() }
    }
}
