function Normalize-UiCapabilitySessionState {
    param(
        $SessionState
    )

    if ($null -eq $SessionState) {
        return $null
    }

    foreach ($propertyName in @("ActiveCapabilities", "CapabilityHistory", "RecentArtifacts", "RecentObservations", "RecentDecisions")) {
        if ($SessionState.PSObject.Properties.Name -notcontains $propertyName -or $null -eq $SessionState.$propertyName) {
            Add-Member -InputObject $SessionState -NotePropertyName $propertyName -NotePropertyValue @() -Force
        }
    }

    return $SessionState
}

function Get-UiCapabilitySessionPath {
    $repoRoot = Get-UiAcceptanceRepoRoot
    return Join-Path $repoRoot "Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-capability-session.json"
}

function Get-UiCapabilityObservationsPath {
    $repoRoot = Get-UiAcceptanceRepoRoot
    return Join-Path $repoRoot "Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-capability-observations.jsonl"
}

function Read-UiCapabilitySessionStateRaw {
    $path = Get-UiCapabilitySessionPath
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    $content = Get-Content -Path $path -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    return Normalize-UiCapabilitySessionState -SessionState ($content | ConvertFrom-Json)
}

function Save-UiCapabilitySessionState {
    param(
        [Parameter(Mandatory)]
        $SessionState
    )

    $path = Get-UiCapabilitySessionPath
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $json = $SessionState | ConvertTo-Json -Depth 10
    Set-Content -Path $path -Value $json -Encoding UTF8
    return $SessionState
}

function New-UiCapabilitySessionStateObject {
    param(
        [int]$ProcessId = 0,
        [string]$Mode = "free-explore",
        [string]$Reason = "manual-start"
    )

    $timestamp = (Get-Date).ToString("o")
    return [pscustomobject]@{
        SessionId = [guid]::NewGuid().ToString("N")
        IsActive = $true
        Mode = $Mode
        Reason = $Reason
        ProcessId = $ProcessId
        CreatedAt = $timestamp
        UpdatedAt = $timestamp
        LastTouchedAt = $timestamp
        LastAction = $null
        StopReason = $null
        StoppedAt = $null
        ActiveCapabilities = @()
        CapabilityHistory = @()
        RecentArtifacts = @()
        RecentObservations = @()
        RecentDecisions = @()
    }
}

function Start-UiCapabilitySession {
    param(
        [int]$ProcessId = 0,
        [string]$Mode = "free-explore",
        [string]$Reason = "manual-start",
        [switch]$ForceNew
    )

    $existing = Read-UiCapabilitySessionStateRaw
    if (-not $ForceNew -and $null -ne $existing -and $existing.IsActive) {
        if ($ProcessId -eq 0 -or [int]$existing.ProcessId -eq $ProcessId) {
            return Touch-UiCapabilitySession -SessionState $existing -LastAction "session-reuse" -Reason $Reason
        }
    }

    if ($ProcessId -eq 0) {
        $process = Get-UiProcess
        if ($null -ne $process) {
            $ProcessId = $process.Id
        }
    }

    $sessionState = New-UiCapabilitySessionStateObject -ProcessId $ProcessId -Mode $Mode -Reason $Reason
    Save-UiCapabilitySessionState -SessionState $sessionState | Out-Null

    Write-UiTimelineEvent -Action "CapabilitySession" -Stage "started" -Success $true -Payload @{
        SessionId = $sessionState.SessionId
        ProcessId = $sessionState.ProcessId
        Mode = $sessionState.Mode
        Reason = $Reason
    }

    return $sessionState
}

function Touch-UiCapabilitySession {
    param(
        $SessionState = $null,
        [string]$LastAction = "",
        [string]$Reason = "touch"
    )

    if ($null -eq $SessionState) {
        $SessionState = Read-UiCapabilitySessionStateRaw
    }

    if ($null -eq $SessionState) {
        return $null
    }

    $timestamp = (Get-Date).ToString("o")
    $SessionState.UpdatedAt = $timestamp
    $SessionState.LastTouchedAt = $timestamp
    if (-not [string]::IsNullOrWhiteSpace($LastAction)) {
        $SessionState.LastAction = $LastAction
    }

    Save-UiCapabilitySessionState -SessionState $SessionState | Out-Null
    return $SessionState
}

function Stop-UiCapabilitySession {
    param(
        [string]$Reason = "manual-stop"
    )

    $sessionState = Read-UiCapabilitySessionStateRaw
    if ($null -eq $sessionState) {
        return $null
    }

    $timestamp = (Get-Date).ToString("o")
    $sessionState.IsActive = $false
    $sessionState.StopReason = $Reason
    $sessionState.StoppedAt = $timestamp
    $sessionState.UpdatedAt = $timestamp
    $sessionState.LastTouchedAt = $timestamp
    $sessionState.ActiveCapabilities = @()
    Save-UiCapabilitySessionState -SessionState $sessionState | Out-Null

    Write-UiTimelineEvent -Action "CapabilitySession" -Stage "stopped" -Success $true -Payload @{
        SessionId = $sessionState.SessionId
        Reason = $Reason
    }

    return $sessionState
}

function Get-UiCapabilitySessionState {
    $sessionState = Read-UiCapabilitySessionStateRaw
    if ($null -eq $sessionState) {
        return $null
    }

    return $sessionState
}
