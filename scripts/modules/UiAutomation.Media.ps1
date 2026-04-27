function New-UiMediaChannelStateObject {
    param(
        [Parameter(Mandatory)]
        [string]$Kind
    )

    return [pscustomobject]@{
        Kind = $Kind
        IsActive = $false
        ProviderName = ""
        ProviderState = "inactive"
        Mode = ""
        Reason = ""
        StartedAt = $null
        UpdatedAt = $null
        StoppedAt = $null
        LeaseMilliseconds = 0
        ExpiresAt = $null
        OutputPath = $null
        ArchiveTargetPath = $null
        ArtifactPath = $null
        ArtifactStatus = "none"
        ProcessIds = @()
        LiveProcessCount = 0
        Notes = @()
    }
}

function New-UiMediaSessionStateObject {
    $timestamp = (Get-Date).ToString("o")
    return [pscustomobject]@{
        SessionId = [guid]::NewGuid().ToString("N")
        UpdatedAt = $timestamp
        LastSweepAt = $null
        VideoCapture = New-UiMediaChannelStateObject -Kind "Video"
        AudioCapture = New-UiMediaChannelStateObject -Kind "Audio"
        RecentArtifacts = @()
        RecentEvents = @()
    }
}

function Normalize-UiMediaSessionState {
    param(
        $SessionState
    )

    if ($null -eq $SessionState) {
        $SessionState = New-UiMediaSessionStateObject
    }

    foreach ($propertyName in @("RecentArtifacts", "RecentEvents")) {
        if ($SessionState.PSObject.Properties.Name -notcontains $propertyName -or $null -eq $SessionState.$propertyName) {
            Add-Member -InputObject $SessionState -NotePropertyName $propertyName -NotePropertyValue @() -Force
        }
    }

    foreach ($channelName in @("VideoCapture", "AudioCapture")) {
        if ($SessionState.PSObject.Properties.Name -notcontains $channelName -or $null -eq $SessionState.$channelName) {
            Add-Member -InputObject $SessionState -NotePropertyName $channelName -NotePropertyValue (New-UiMediaChannelStateObject -Kind $(if ($channelName -eq "VideoCapture") { "Video" } else { "Audio" })) -Force
        }

        $channel = $SessionState.$channelName
        foreach ($propertyName in @("ProcessIds", "Notes")) {
            if ($channel.PSObject.Properties.Name -notcontains $propertyName -or $null -eq $channel.$propertyName) {
                Add-Member -InputObject $channel -NotePropertyName $propertyName -NotePropertyValue @() -Force
            }
        }

        foreach ($propertyName in @("LiveProcessCount", "ProviderState", "ArchiveTargetPath", "ArtifactStatus")) {
            if ($channel.PSObject.Properties.Name -notcontains $propertyName) {
                $defaultValue = switch ($propertyName) {
                    "ProviderState" { "inactive" }
                    "ArchiveTargetPath" { $null }
                    "ArtifactStatus" { "none" }
                    default { 0 }
                }
                Add-Member -InputObject $channel -NotePropertyName $propertyName -NotePropertyValue $defaultValue -Force
            }
        }
    }

    return $SessionState
}

function Get-UiMediaSessionPath {
    return Join-Path (Get-UiAcceptanceArtifactsRoot) "interactive-media-session.json"
}

function Read-UiMediaSessionStateRaw {
    $path = Get-UiMediaSessionPath
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    $content = Get-Content -Path $path -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    return Normalize-UiMediaSessionState -SessionState ($content | ConvertFrom-Json)
}

function Save-UiMediaSessionState {
    param(
        [Parameter(Mandatory)]
        $SessionState
    )

    $normalized = Normalize-UiMediaSessionState -SessionState $SessionState
    $normalized.UpdatedAt = (Get-Date).ToString("o")

    $path = Get-UiMediaSessionPath
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $json = $normalized | ConvertTo-Json -Depth 10
    Set-Content -Path $path -Value $json -Encoding UTF8
    return $normalized
}

function Get-UiPsrCommandPath {
    $command = Get-Command psr.exe -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $null
    }

    return $command.Source
}

function Get-UiPsrProcesses {
    return @(Get-Process psr -ErrorAction SilentlyContinue)
}

function Get-UiMediaProviderCatalog {
    $psrPath = Get-UiPsrCommandPath
    $psrProcesses = @(Get-UiPsrProcesses)

    return @(
        [pscustomobject]@{
            Name = "Psr.ScreenTrace"
            Kind = "Video"
            Availability = if (-not [string]::IsNullOrWhiteSpace($psrPath)) { "available" } else { "unavailable" }
            CommandPath = $psrPath
            OutputFormat = "zip"
            CaptureStyle = "screen-trace"
            SupportsAudio = $false
            SupportsSingleFramePreview = $false
            RunningInstanceCount = $psrProcesses.Count
            Notes = @(
                "مزود مدمج في ويندوز، مناسب كتتبع بصري خفيف عند الطلب.",
                "ليس فيديو full-motion، لكنه كافٍ كبنية sidecar أولى إذا أدير بحالة واحدة صحيحة."
            )
        },
        [pscustomobject]@{
            Name = "Audio.None"
            Kind = "Audio"
            Availability = "unavailable"
            CommandPath = $null
            OutputFormat = $null
            CaptureStyle = "none"
            SupportsAudio = $false
            SupportsSingleFramePreview = $false
            RunningInstanceCount = 0
            Notes = @(
                "لا يوجد provider صوت موصول رسميًا حتى الآن.",
                "سيبقى AudioCapture غير متاح حتى نوصل sidecar صوت مستقلة."
            )
        }
    )
}

function Get-UiPreferredMediaProvider {
    param(
        [ValidateSet("Video", "Audio")]
        [string]$Kind = "Video"
    )

    $providers = @(Get-UiMediaProviderCatalog | Where-Object {
        [string]::Equals($_.Kind, $Kind, [System.StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals($_.Availability, "available", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($providers.Count -eq 0) {
        return $null
    }

    return $providers[0]
}

function Add-UiMediaRecentEvent {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$Kind,
        [Parameter(Mandatory)]
        [string]$Summary,
        $Payload = $null
    )

    $entry = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        Kind = $Kind
        Summary = $Summary
        Payload = $Payload
    }

    $SessionState.RecentEvents = @($entry) + @($SessionState.RecentEvents | Select-Object -First 15)
}

function Add-UiMediaRecentArtifact {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        $Artifact
    )

    $SessionState.RecentArtifacts = @($Artifact) + @($SessionState.RecentArtifacts | Select-Object -First 15)
}

function Get-UiMediaArtifactsRoot {
    param(
        $SessionState = $null
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiMediaSessionState
    }

    return Join-Path (Get-UiAcceptanceArtifactsRoot) ("media\" + $sessionState.SessionId)
}

function Get-UiMediaWorkingRoot {
    return Join-Path (Get-UiStorageRoot) "UiMedia"
}

function Get-UiVideoArtifactsRoot {
    param(
        $SessionState = $null
    )

    return Join-Path (Get-UiMediaArtifactsRoot -SessionState $SessionState) "video"
}

function Get-UiAudioArtifactsRoot {
    param(
        $SessionState = $null
    )

    return Join-Path (Get-UiMediaArtifactsRoot -SessionState $SessionState) "audio"
}

function New-UiMediaOutputPath {
    param(
        [ValidateSet("Video", "Audio")]
        [string]$Kind = "Video",
        [string]$Extension = "zip",
        $SessionState = $null
    )

    $root = if ($Kind -eq "Video") { Get-UiVideoArtifactsRoot -SessionState $SessionState } else { Get-UiAudioArtifactsRoot -SessionState $SessionState }
    if (-not (Test-Path -LiteralPath $root)) {
        New-Item -ItemType Directory -Force -Path $root | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    return Join-Path $root ($timestamp + "." + $Extension.TrimStart("."))
}

function New-UiMediaWorkingOutputPath {
    param(
        [ValidateSet("Video", "Audio")]
        [string]$Kind = "Video",
        [string]$Extension = "zip"
    )

    $root = Join-Path (Get-UiMediaWorkingRoot) $Kind.ToLowerInvariant()
    if (-not (Test-Path -LiteralPath $root)) {
        New-Item -ItemType Directory -Force -Path $root | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    return Join-Path $root ($timestamp + "." + $Extension.TrimStart("."))
}

function Stop-UiPsrProcessQuietly {
    param(
        [string]$Reason = "cleanup",
        [int]$WaitMilliseconds = 5000
    )

    $running = @(Get-UiPsrProcesses)
    if ($running.Count -eq 0) {
        return 0
    }

    $commandPath = Get-UiPsrCommandPath
    if (-not [string]::IsNullOrWhiteSpace($commandPath)) {
        try {
            $null = & $commandPath /stop
        }
        catch {
        }

        Start-Sleep -Milliseconds ([Math]::Min(900, [Math]::Max(150, $WaitMilliseconds / 2)))
    }

    $deadline = (Get-Date).AddMilliseconds($WaitMilliseconds)
    do {
        $remaining = @(Get-UiPsrProcesses)
        if ($remaining.Count -eq 0) {
            return $running.Count
        }

        Start-Sleep -Milliseconds 140
    } while ((Get-Date) -lt $deadline)

    foreach ($process in @(Get-UiPsrProcesses)) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
        catch {
        }
    }

    Start-Sleep -Milliseconds 220
    return $running.Count
}

function Update-UiMediaLiveProcessCounts {
    param(
        [Parameter(Mandatory)]
        $SessionState
    )

    $psrCount = @(Get-UiPsrProcesses).Count
    $SessionState.VideoCapture.LiveProcessCount = $psrCount
    if ($SessionState.VideoCapture.IsActive -and $psrCount -gt 0) {
        $SessionState.VideoCapture.ProcessIds = @((Get-UiPsrProcesses | Select-Object -ExpandProperty Id))
    }

    return $SessionState
}

function Get-UiMediaSessionState {
    $sessionState = Normalize-UiMediaSessionState -SessionState (Read-UiMediaSessionStateRaw)
    return Update-UiMediaLiveProcessCounts -SessionState $sessionState
}

function Start-UiVideoCaptureSidecar {
    param(
        [int]$ProcessId = 0,
        [string]$Reason = "manual-video-start",
        [int]$LeaseMilliseconds = 0
    )

    $provider = Get-UiPreferredMediaProvider -Kind "Video"
    if ($null -eq $provider) {
        throw "No available video provider is wired right now."
    }

    $sessionState = Get-UiMediaSessionState
    if ($sessionState.VideoCapture.IsActive) {
        $sessionState = Stop-UiVideoCaptureSidecar -Reason "restart-before-start"
    }

    $cleanedCount = Stop-UiPsrProcessQuietly -Reason "preflight-cleanup"
    if ($cleanedCount -gt 0) {
        Add-UiMediaRecentEvent -SessionState $sessionState -Kind "provider-preflight-cleanup" -Summary "أغلقت الأداة نسخة قديمة من Steps Recorder قبل بدء تتبع جديد." -Payload @{
            ProviderName = $provider.Name
            CleanedProcessCount = $cleanedCount
        }
    }

    $workingOutputPath = [System.IO.Path]::GetFullPath((New-UiMediaWorkingOutputPath -Kind "Video" -Extension "zip"))
    $archiveTargetPath = [System.IO.Path]::GetFullPath((New-UiMediaOutputPath -Kind "Video" -Extension "zip" -SessionState $sessionState))
    if (Test-Path -LiteralPath $workingOutputPath) {
        Remove-Item -LiteralPath $workingOutputPath -Force
    }

    $commandPath = [string]$provider.CommandPath
    $null = & $commandPath /start /output $workingOutputPath /sc 1 /gui 0

    $deadline = (Get-Date).AddSeconds(4)
    do {
        $processes = @(Get-UiPsrProcesses)
        if ($processes.Count -gt 0) {
            break
        }

        Start-Sleep -Milliseconds 120
    } while ((Get-Date) -lt $deadline)

    $processes = @(Get-UiPsrProcesses)
    if ($processes.Count -eq 0) {
        throw "Steps Recorder did not start cleanly."
    }

    $effectiveLeaseMs = if ($LeaseMilliseconds -gt 0) { $LeaseMilliseconds } else { 5000 }
    $timestamp = (Get-Date).ToString("o")
    $sessionState.VideoCapture = [pscustomobject]@{
        Kind = "Video"
        IsActive = $true
        ProviderName = [string]$provider.Name
        ProviderState = [string]$provider.Availability
        Mode = "single-instance-screen-trace"
        Reason = $Reason
        StartedAt = $timestamp
        UpdatedAt = $timestamp
        StoppedAt = $null
        LeaseMilliseconds = $effectiveLeaseMs
        ExpiresAt = [DateTimeOffset]::Now.AddMilliseconds($effectiveLeaseMs).ToString("o")
        OutputPath = $workingOutputPath
        ArchiveTargetPath = $archiveTargetPath
        ArtifactPath = $null
        ArtifactStatus = "pending"
        ProcessIds = @($processes | Select-Object -ExpandProperty Id)
        LiveProcessCount = $processes.Count
        Notes = @(
            "التقاط الفيديو الحالي يعتمد على Steps Recorder كتتبع بصري لحظي خفيف.",
            "الأداة ستبقي المزود single-instance وتغلقه بهدوء عند الإيقاف أو انتهاء lease."
        )
    }

        Add-UiMediaRecentEvent -SessionState $sessionState -Kind "video-started" -Summary "بدأت الأداة sidecar فيديو خفيفة عبر Steps Recorder." -Payload @{
        ProviderName = $provider.Name
        OutputPath = $workingOutputPath
        ArchiveTargetPath = $archiveTargetPath
        LeaseMilliseconds = $effectiveLeaseMs
        ProcessId = $ProcessId
        Reason = $Reason
    }

    $sessionState = Save-UiMediaSessionState -SessionState $sessionState
    Write-UiTimelineEvent -Action "Media.VideoCapture" -Stage "started" -Success $true -Payload @{
        ProviderName = $provider.Name
        OutputPath = $workingOutputPath
        ArchiveTargetPath = $archiveTargetPath
        LeaseMilliseconds = $effectiveLeaseMs
        Reason = $Reason
        ProcessId = $ProcessId
    }

    return $sessionState
}

function Stop-UiVideoCaptureSidecar {
    param(
        [string]$Reason = "manual-video-stop"
    )

    $sessionState = Get-UiMediaSessionState
    $wasActive = [bool]$sessionState.VideoCapture.IsActive
    $outputPath = [string]$sessionState.VideoCapture.OutputPath
    $archiveTargetPath = [string]$sessionState.VideoCapture.ArchiveTargetPath
    $providerName = [string]$sessionState.VideoCapture.ProviderName

    $cleanedCount = Stop-UiPsrProcessQuietly -Reason $Reason

    $artifactPath = $null
    if (-not [string]::IsNullOrWhiteSpace($outputPath)) {
        $artifactDeadline = (Get-Date).AddSeconds(4)
        do {
            if (Test-Path -LiteralPath $outputPath) {
                $resolvedOutputPath = [System.IO.Path]::GetFullPath($outputPath)
                if (-not [string]::IsNullOrWhiteSpace($archiveTargetPath)) {
                    $archiveDirectory = Split-Path -Parent $archiveTargetPath
                    if (-not (Test-Path -LiteralPath $archiveDirectory)) {
                        New-Item -ItemType Directory -Force -Path $archiveDirectory | Out-Null
                    }

                    try {
                        Move-Item -LiteralPath $resolvedOutputPath -Destination $archiveTargetPath -Force
                        $artifactPath = [System.IO.Path]::GetFullPath($archiveTargetPath)
                    }
                    catch {
                        $artifactPath = $resolvedOutputPath
                    }
                }
                else {
                    $artifactPath = $resolvedOutputPath
                }
                break
            }

            Start-Sleep -Milliseconds 180
        } while ((Get-Date) -lt $artifactDeadline)
    }

    $timestamp = (Get-Date).ToString("o")
    $artifactStatus = if (-not [string]::IsNullOrWhiteSpace($artifactPath)) { "saved" } else { "missing" }
    $sessionState.VideoCapture = [pscustomobject]@{
        Kind = "Video"
        IsActive = $false
        ProviderName = $providerName
        ProviderState = if (-not [string]::IsNullOrWhiteSpace($providerName)) { "stopped" } else { "inactive" }
        Mode = "single-instance-screen-trace"
        Reason = $Reason
        StartedAt = $sessionState.VideoCapture.StartedAt
        UpdatedAt = $timestamp
        StoppedAt = $timestamp
        LeaseMilliseconds = 0
        ExpiresAt = $null
        OutputPath = $outputPath
        ArchiveTargetPath = $archiveTargetPath
        ArtifactPath = $artifactPath
        ArtifactStatus = $artifactStatus
        ProcessIds = @()
        LiveProcessCount = 0
        Notes = @(
            "تم إيقاف sidecar الفيديو وعادت الأداة إلى وضعها الخفيف.",
            $(if ($artifactStatus -eq "saved") { "المزود حفظ artifact فعلية ويمكن الرجوع إليها الآن." } else { "المزود لم يخرج artifact محفوظة هذه المرة؛ الإيقاف نجح لكن لا يوجد ملف بصري محفوظ." })
        )
    }

    if ($wasActive -or $cleanedCount -gt 0) {
        Add-UiMediaRecentEvent -SessionState $sessionState -Kind "video-stopped" -Summary "أوقفت الأداة sidecar الفيديو وأعادت المزود إلى وضعه الطبيعي." -Payload @{
            ProviderName = $providerName
            ArtifactPath = $artifactPath
            ArtifactStatus = $artifactStatus
            Reason = $Reason
            CleanedProcessCount = $cleanedCount
        }

        if (-not [string]::IsNullOrWhiteSpace($artifactPath)) {
            Add-UiMediaRecentArtifact -SessionState $sessionState -Artifact ([pscustomobject]@{
                Timestamp = $timestamp
                Kind = "Video"
                ProviderName = $providerName
                Path = $artifactPath
                Reason = $Reason
            })
        }

        Write-UiTimelineEvent -Action "Media.VideoCapture" -Stage "stopped" -Success $true -Payload @{
            ProviderName = $providerName
            ArtifactPath = $artifactPath
            ArtifactStatus = $artifactStatus
            Reason = $Reason
            CleanedProcessCount = $cleanedCount
        }
    }

    return Save-UiMediaSessionState -SessionState $sessionState
}

function Start-UiAudioCaptureSidecar {
    param(
        [string]$Reason = "manual-audio-start",
        [int]$LeaseMilliseconds = 0
    )

    throw "No available audio provider is wired right now."
}

function Stop-UiAudioCaptureSidecar {
    param(
        [string]$Reason = "manual-audio-stop"
    )

    $sessionState = Get-UiMediaSessionState
    $timestamp = (Get-Date).ToString("o")
    $sessionState.AudioCapture = [pscustomobject]@{
        Kind = "Audio"
        IsActive = $false
        ProviderName = "Audio.None"
        ProviderState = "inactive"
        Mode = "none"
        Reason = $Reason
        StartedAt = $sessionState.AudioCapture.StartedAt
        UpdatedAt = $timestamp
        StoppedAt = $timestamp
        LeaseMilliseconds = 0
        ExpiresAt = $null
        OutputPath = $null
        ArtifactPath = $null
        ProcessIds = @()
        LiveProcessCount = 0
        Notes = @("لا يوجد sidecar صوت مفعلة حاليًا.")
    }

    return Save-UiMediaSessionState -SessionState $sessionState
}

function Invoke-UiMediaBrokerSweep {
    param(
        [switch]$Persist,
        [switch]$ForceCleanup,
        [string]$Reason = "broker-sweep"
    )

    $sessionState = Get-UiMediaSessionState
    $changed = $false
    $sessionState.LastSweepAt = (Get-Date).ToString("o")

    if ($sessionState.VideoCapture.IsActive -and -not [string]::IsNullOrWhiteSpace([string]$sessionState.VideoCapture.ExpiresAt)) {
        if ([DateTimeOffset]::Parse([string]$sessionState.VideoCapture.ExpiresAt) -le [DateTimeOffset]::Now) {
            $sessionState = Stop-UiVideoCaptureSidecar -Reason "lease-expired"
            $changed = $true
        }
    }

    if ($ForceCleanup -and -not $sessionState.VideoCapture.IsActive) {
        $cleanedCount = Stop-UiPsrProcessQuietly -Reason $Reason
        if ($cleanedCount -gt 0) {
            Add-UiMediaRecentEvent -SessionState $sessionState -Kind "orphan-cleanup" -Summary "أغلقت الأداة instance قديمة من Steps Recorder خارج أي sidecar نشطة." -Payload @{
                CleanedProcessCount = $cleanedCount
                Reason = $Reason
            }
            $changed = $true
        }
    }

    $sessionState = Update-UiMediaLiveProcessCounts -SessionState $sessionState
    if ($Persist -or $changed) {
        $sessionState = Save-UiMediaSessionState -SessionState $sessionState
    }

    return $sessionState
}
