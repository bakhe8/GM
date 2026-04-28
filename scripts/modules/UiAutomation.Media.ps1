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
        ScopeContract = New-UiDefaultMediaScopeContract -Kind $Kind
        Notes = @()
    }
}

function New-UiDefaultMediaScopeContract {
    param(
        [string]$Kind = "Video",
        [string]$ProviderName = "",
        [string]$ScopeModel = "unscoped",
        [string]$ProviderScopeLevel = "unknown",
        [bool]$SupportsProcessIsolation = $false,
        [bool]$SupportsWindowIsolation = $false,
        [bool]$SupportsForegroundAttestation = $false
    )

    return [pscustomobject]@{
        Kind = $Kind
        ProviderName = $ProviderName
        ScopeModel = $ScopeModel
        ProviderScopeLevel = $ProviderScopeLevel
        SupportsProcessIsolation = $SupportsProcessIsolation
        SupportsWindowIsolation = $SupportsWindowIsolation
        SupportsForegroundAttestation = $SupportsForegroundAttestation
        SourcePolicy = if ($Kind -eq "Audio") { "per-app-attested-required" } else { "" }
        AcceptsSystemMixFallback = if ($Kind -eq "Audio") { $false } else { $false }
        SupportsPerAppAudioIsolation = $false
        SupportsSystemMixCapture = $false
        SupportsDeviceLoopbackCapture = $false
        TargetProcessId = 0
        TargetProcessName = ""
        TargetMainWindowTitle = ""
        TargetWindow = $null
        StartSnapshot = $null
        StopSnapshot = $null
        ScopeStatus = if ($Kind -eq "Audio") { "unavailable" } else { "inactive" }
        EvidenceIsolation = if ($Kind -eq "Audio") { "no-provider" } else { "none" }
        TrustedForReasoning = $false
        ContaminationDetected = $false
        ScopeNotes = if ($Kind -eq "Audio") {
            @(
                "لا يوجد مزود صوت فعلي حتى الآن، لذلك لا توجد hearing evidence قابلة للاعتماد.",
                "السياسة المقصودة للصوت هي per-app attested audio، لا system mix عام."
            )
        }
        else {
            @()
        }
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

        if ($channel.PSObject.Properties.Name -notcontains "ScopeContract" -or $null -eq $channel.ScopeContract) {
            Add-Member -InputObject $channel -NotePropertyName "ScopeContract" -NotePropertyValue (New-UiDefaultMediaScopeContract -Kind $channel.Kind -ProviderName ([string]$channel.ProviderName)) -Force
        }
        else {
            $defaultScopeContract = New-UiDefaultMediaScopeContract -Kind $channel.Kind -ProviderName ([string]$channel.ProviderName) -ScopeModel ([string]$channel.ScopeContract.ScopeModel) -ProviderScopeLevel ([string]$channel.ScopeContract.ProviderScopeLevel) -SupportsProcessIsolation ([bool]$channel.ScopeContract.SupportsProcessIsolation) -SupportsWindowIsolation ([bool]$channel.ScopeContract.SupportsWindowIsolation) -SupportsForegroundAttestation ([bool]$channel.ScopeContract.SupportsForegroundAttestation)
            foreach ($scopeProperty in $defaultScopeContract.PSObject.Properties) {
                if ($channel.ScopeContract.PSObject.Properties.Name -notcontains $scopeProperty.Name) {
                    Add-Member -InputObject $channel.ScopeContract -NotePropertyName $scopeProperty.Name -NotePropertyValue $scopeProperty.Value -Force
                }
            }

            if ([string]::Equals([string]$channel.Kind, "Audio", [System.StringComparison]::OrdinalIgnoreCase) -and
                [string]::IsNullOrWhiteSpace([string]$channel.ProviderName)) {
                $audioDefaults = New-UiDefaultMediaScopeContract -Kind "Audio" -ProviderName "Audio.None" -ScopeModel "planned-per-app-attested" -ProviderScopeLevel "none"
                foreach ($propertyName in @("ProviderName", "ScopeModel", "ProviderScopeLevel", "SourcePolicy", "AcceptsSystemMixFallback", "SupportsPerAppAudioIsolation", "SupportsSystemMixCapture", "SupportsDeviceLoopbackCapture")) {
                    $channel.ScopeContract.$propertyName = $audioDefaults.$propertyName
                }

                if ([string]$channel.ScopeContract.ScopeStatus -eq "inactive") {
                    $channel.ScopeContract.ScopeStatus = "unavailable"
                }

                if ([string]$channel.ScopeContract.EvidenceIsolation -eq "none") {
                    $channel.ScopeContract.EvidenceIsolation = "no-provider"
                }

                if (@($channel.ScopeContract.ScopeNotes).Count -eq 0) {
                    $channel.ScopeContract.ScopeNotes = [object[]]$audioDefaults.ScopeNotes
                }
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

    try {
        return Normalize-UiMediaSessionState -SessionState ($content | ConvertFrom-Json)
    }
    catch {
        $corruptPath = "{0}.corrupt-{1}.json" -f $path, (Get-Date -Format "yyyyMMdd-HHmmss-fff")
        try {
            Move-Item -LiteralPath $path -Destination $corruptPath -Force
        }
        catch {
        }

        return $null
    }
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
    $saved = $false
    $tempPath = "{0}.{1}.{2}.tmp" -f $path, $PID, ([guid]::NewGuid().ToString("N"))
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Set-Content -LiteralPath $tempPath -Value $json -Encoding UTF8
            Move-Item -LiteralPath $tempPath -Destination $path -Force
            $saved = $true
            break
        }
        catch {
            if (Test-Path -LiteralPath $tempPath) {
                Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
            }

            if ($attempt -ge 6) {
                throw
            }

            Start-Sleep -Milliseconds (60 * $attempt)
        }
    }

    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    }

    if (-not $saved) {
        throw "Failed to persist media session state."
    }

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
            ScopeModel = "global-session-attested"
            ProviderScopeLevel = "global"
            SupportsProcessIsolation = $false
            SupportsWindowIsolation = $false
            SupportsForegroundAttestation = $true
            SupportsAudio = $false
            SupportsSingleFramePreview = $false
            RunningInstanceCount = $psrProcesses.Count
            Notes = @(
                "مزود مدمج في ويندوز، مناسب كتتبع بصري خفيف عند الطلب.",
                "ليس process-bound أو window-bound؛ لذلك نعزله عبر attestation لحالة foreground والنافذة المستهدفة.",
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
            ScopeModel = "planned-per-app-attested"
            ProviderScopeLevel = "none"
            SupportsProcessIsolation = $false
            SupportsWindowIsolation = $false
            SupportsForegroundAttestation = $false
            SupportsAudio = $false
            SupportsPerAppAudioIsolation = $false
            SupportsSystemMixCapture = $false
            SupportsDeviceLoopbackCapture = $false
            SupportsSingleFramePreview = $false
            RunningInstanceCount = 0
            Notes = @(
                "لا يوجد provider صوت موصول رسميًا حتى الآن.",
                "سيبقى AudioCapture غير متاح حتى نوصل sidecar صوت مستقلة.",
                "السياسة المستهدفة مستقبلًا هي per-app attested audio من دون system-mix fallback افتراضي."
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

function Get-UiMediaProviderByName {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return (Get-UiMediaProviderCatalog | Where-Object {
        [string]::Equals([string]$_.Name, $Name, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)
}

function Get-UiAudioScopePolicy {
    $preferredProvider = Get-UiPreferredMediaProvider -Kind "Audio"
    $catalogProvider = Get-UiMediaProviderByName -Name $(if ($null -ne $preferredProvider) { [string]$preferredProvider.Name } else { "Audio.None" })
    $providerName = if ($null -ne $catalogProvider) { [string]$catalogProvider.Name } else { "Audio.None" }
    $providerReadiness = if ($null -ne $preferredProvider) { "available" } else { "unavailable" }

    return [pscustomobject]@{
        PolicyName = "AIFirstScopedAudio"
        ProviderReadiness = $providerReadiness
        CurrentProviderName = $providerName
        DesiredCaptureSource = "per-app-audio"
        DesiredScopeModel = "per-app-attested"
        RequiresTargetProcessBinding = $true
        RequiresForegroundAttestation = $true
        AcceptsSystemMixFallback = $false
        AcceptsDesktopMixFallback = $false
        TrustedForReasoning = $false
        StartBlockedReason = if ($providerReadiness -eq "available") { "" } else { "لا يوجد مزود صوت يحقق سياسة per-app attested audio حتى الآن." }
        Notes = @(
            "الصوت المقبول مستقبلاً يجب أن يرتبط بالتطبيق نفسه، لا بخليط النظام العام.",
            "Foreground وحدها لا تكفي للصوت؛ نحتاج provider تقبل binding على process/session قبل أن نعتبر hearing evidence موثوقة.",
            $(if ($providerReadiness -eq "available") { "يوجد provider صوت متاح مبدئيًا، لكن يجب التحقق من نطاقه قبل الثقة به." } else { "حتى الآن لا يوجد provider صوت فعلي، لذلك تبقى hearing evidence غير متاحة." })
        )
    }
}

function Get-UiMediaTargetProcess {
    param(
        [int]$ProcessId = 0
    )

    if ($ProcessId -le 0) {
        return $null
    }

    try {
        return Get-Process -Id $ProcessId -ErrorAction Stop
    }
    catch {
        return $null
    }
}

function Get-UiMediaTargetWindowSummary {
    param(
        [int]$ProcessId = 0
    )

    if ($ProcessId -le 0) {
        return $null
    }

    try {
        $window = Resolve-UiWindow -ProcessId $ProcessId
        if ($null -ne $window) {
            return Get-UiElementSummary -Element $window
        }
    }
    catch {
    }

    $fallback = @(Get-UiWindowsCatalog -ProcessId $ProcessId | Sort-Object {
        if ($null -eq $_) { return 0 }
        return [int]$_.Bounds.Width * [int]$_.Bounds.Height
    } -Descending | Select-Object -First 1)

    if ($fallback.Count -gt 0) {
        return $fallback[0]
    }

    return $null
}

function Get-UiMediaForegroundRelation {
    param(
        $ForegroundSummary,
        [int]$ProcessId = 0,
        $TargetWindow = $null
    )

    if ($null -eq $ForegroundSummary) {
        return "none"
    }

    if ($ProcessId -gt 0 -and [int]$ForegroundSummary.ProcessId -eq $ProcessId) {
        return "target-process"
    }

    $windowHandle = [IntPtr]::Zero
    try {
        $windowHandle = [IntPtr][int64]$ForegroundSummary.NativeWindowHandle
    }
    catch {
    }

    if ($windowHandle -ne [IntPtr]::Zero) {
        try {
            $element = [System.Windows.Automation.AutomationElement]::FromHandle($windowHandle)
            if ($null -ne $element -and (Test-UiExternalWindowRelatedToProcess -Window $element -ProcessId $ProcessId)) {
                return "related-external"
            }
        }
        catch {
        }
    }

    return "unrelated-external"
}

function New-UiMediaScopeSnapshot {
    param(
        [ValidateSet("Video", "Audio")]
        [string]$Kind = "Video",
        [string]$ProviderName = "",
        [string]$ScopeModel = "unscoped",
        [string]$Phase = "start",
        [int]$ProcessId = 0
    )

    $targetProcess = Get-UiMediaTargetProcess -ProcessId $ProcessId
    $targetWindow = Get-UiMediaTargetWindowSummary -ProcessId $ProcessId
    $foregroundElement = Get-UiForegroundWindowElement
    $foregroundSummary = if ($null -ne $foregroundElement) { Get-UiElementSummary -Element $foregroundElement } else { $null }
    $relatedWindows = @(
        if ($ProcessId -gt 0) {
            Get-UiWindowsCatalog -ProcessId $ProcessId -IncludeRelatedForeground | Select-Object -First 8
        }
    )

    $relation = Get-UiMediaForegroundRelation -ForegroundSummary $foregroundSummary -ProcessId $ProcessId -TargetWindow $targetWindow

    return [pscustomobject]@{
        Kind = $Kind
        ProviderName = $ProviderName
        ScopeModel = $ScopeModel
        Phase = $Phase
        Timestamp = (Get-Date).ToString("o")
        TargetProcessId = if ($null -ne $targetProcess) { $targetProcess.Id } else { $ProcessId }
        TargetProcessName = if ($null -ne $targetProcess) { $targetProcess.ProcessName } else { "" }
        TargetMainWindowTitle = if ($null -ne $targetProcess) { $targetProcess.MainWindowTitle } else { "" }
        HasTargetWindow = $null -ne $targetWindow
        TargetWindow = $targetWindow
        ForegroundWindow = $foregroundSummary
        ForegroundRelation = $relation
        ForegroundMatchesTargetProcess = ($relation -eq "target-process")
        ForegroundMatchesRelatedWindow = ($relation -eq "related-external")
        RelatedWindows = [object[]]$relatedWindows
        OpenWindowCount = $relatedWindows.Count
    }
}

function Get-UiMediaScopeAssessment {
    param(
        $StartSnapshot = $null,
        $StopSnapshot = $null
    )

    $notes = New-Object System.Collections.Generic.List[string]
    $relations = New-Object System.Collections.Generic.List[string]
    foreach ($snapshot in @($StartSnapshot, $StopSnapshot)) {
        if ($null -eq $snapshot) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$snapshot.ForegroundRelation)) {
            [void]$relations.Add([string]$snapshot.ForegroundRelation)
        }
    }

    $hasTargetWindow = ($null -ne $StartSnapshot -and [bool]$StartSnapshot.HasTargetWindow) -or ($null -ne $StopSnapshot -and [bool]$StopSnapshot.HasTargetWindow)
    $hasTargetProcess = $relations -contains "target-process"
    $hasRelatedExternal = $relations -contains "related-external"
    $hasUnrelated = $relations -contains "unrelated-external"
    $hasUnknown = $relations -contains "unknown"
    $hasNone = $relations -contains "none"

    if (-not $hasTargetWindow) {
        [void]$notes.Add("لم تتمكن الأداة من تثبيت نافذة هدف صريحة وقت الالتقاط.")
    }

    if ($null -eq $StopSnapshot) {
        if ($hasUnrelated) {
            [void]$notes.Add("الالتقاط بدأ بينما نافذة foreground لا تبدو مرتبطة مباشرة بالبرنامج المستهدف.")
            return [pscustomobject]@{
                ScopeStatus = "monitoring-external"
                EvidenceIsolation = "pending-contaminated"
                TrustedForReasoning = $false
                ContaminationDetected = $true
                ScopeNotes = [object[]]$notes
            }
        }

        if ($hasTargetProcess -or $hasRelatedExternal) {
            [void]$notes.Add("الالتقاط في وضع مراقبة مرتبط بالبرنامج، لكن العزل النهائي ينتظر لحظة الإيقاف.")
            return [pscustomobject]@{
                ScopeStatus = "monitoring"
                EvidenceIsolation = if ($hasRelatedExternal) { "pending-program-plus-related-window" } else { "pending-program-window" }
                TrustedForReasoning = $false
                ContaminationDetected = $false
                ScopeNotes = [object[]]$notes
            }
        }

        [void]$notes.Add("الالتقاط بدأ دون evidence كافية بعد لتأكيد foreground الصحيحة.")
        return [pscustomobject]@{
            ScopeStatus = "monitoring-unknown"
            EvidenceIsolation = "pending-unknown"
            TrustedForReasoning = $false
            ContaminationDetected = $false
            ScopeNotes = [object[]]$notes
        }
    }

    if ($hasUnrelated -and ($hasTargetProcess -or $hasRelatedExternal)) {
        [void]$notes.Add("ظهر foreground غير مرتبط بالبرنامج في جزء من زمن الالتقاط، لذلك الدليل مختلط.")
        return [pscustomobject]@{
            ScopeStatus = "mixed"
            EvidenceIsolation = "contaminated"
            TrustedForReasoning = $false
            ContaminationDetected = $true
            ScopeNotes = [object[]]$notes
        }
    }

    if ($hasUnrelated) {
        [void]$notes.Add("foreground خلال الالتقاط كانت خارج نطاق البرنامج، لذلك لا يمكن اعتبار الدليل معزولًا.")
        return [pscustomobject]@{
            ScopeStatus = "external"
            EvidenceIsolation = "contaminated"
            TrustedForReasoning = $false
            ContaminationDetected = $true
            ScopeNotes = [object[]]$notes
        }
    }

    if (-not $hasTargetWindow -or $hasUnknown -or $hasNone -or $relations.Count -eq 0) {
        [void]$notes.Add("المعطيات المتاحة لا تكفي لتأكيد عزل الالتقاط على نافذة البرنامج.")
        return [pscustomobject]@{
            ScopeStatus = "unknown"
            EvidenceIsolation = "unknown"
            TrustedForReasoning = $false
            ContaminationDetected = $false
            ScopeNotes = [object[]]$notes
        }
    }

    if ($hasRelatedExternal) {
        [void]$notes.Add("الالتقاط بقي ضمن نطاق البرنامج، لكنه مرّ أيضًا عبر نافذة خارجية مرتبطة به مثل حوار مملوك أو مزود نظام.")
        return [pscustomobject]@{
            ScopeStatus = "clean"
            EvidenceIsolation = "program-plus-related-window"
            TrustedForReasoning = $true
            ContaminationDetected = $false
            ScopeNotes = [object[]]$notes
        }
    }

    [void]$notes.Add("الالتقاط بقي على نوافذ البرنامج المستهدف نفسها دون مؤشرات تلوث خارجية.")
    return [pscustomobject]@{
        ScopeStatus = "clean"
        EvidenceIsolation = "program-window"
        TrustedForReasoning = $true
        ContaminationDetected = $false
        ScopeNotes = [object[]]$notes
    }
}

function New-UiMediaScopeContract {
    param(
        [ValidateSet("Video", "Audio")]
        [string]$Kind = "Video",
        $Provider = $null,
        $StartSnapshot = $null,
        $StopSnapshot = $null
    )

    $providerName = if ($null -ne $Provider) { [string]$Provider.Name } else { "" }
    $scopeModel = if ($null -ne $Provider) { [string]$Provider.ScopeModel } else { "unscoped" }
    $scopeLevel = if ($null -ne $Provider) { [string]$Provider.ProviderScopeLevel } else { "unknown" }
    $supportsProcessIsolation = if ($null -ne $Provider) { [bool]$Provider.SupportsProcessIsolation } else { $false }
    $supportsWindowIsolation = if ($null -ne $Provider) { [bool]$Provider.SupportsWindowIsolation } else { $false }
    $supportsForegroundAttestation = if ($null -ne $Provider) { [bool]$Provider.SupportsForegroundAttestation } else { $false }
    $assessment = Get-UiMediaScopeAssessment -StartSnapshot $StartSnapshot -StopSnapshot $StopSnapshot
    $targetSource = if ($null -ne $StartSnapshot) { $StartSnapshot } else { $StopSnapshot }

    return [pscustomobject]@{
        Kind = $Kind
        ProviderName = $providerName
        ScopeModel = $scopeModel
        ProviderScopeLevel = $scopeLevel
        SupportsProcessIsolation = $supportsProcessIsolation
        SupportsWindowIsolation = $supportsWindowIsolation
        SupportsForegroundAttestation = $supportsForegroundAttestation
        TargetProcessId = if ($null -ne $targetSource) { [int]$targetSource.TargetProcessId } else { 0 }
        TargetProcessName = if ($null -ne $targetSource) { [string]$targetSource.TargetProcessName } else { "" }
        TargetMainWindowTitle = if ($null -ne $targetSource) { [string]$targetSource.TargetMainWindowTitle } else { "" }
        TargetWindow = if ($null -ne $targetSource) { $targetSource.TargetWindow } else { $null }
        StartSnapshot = $StartSnapshot
        StopSnapshot = $StopSnapshot
        ScopeStatus = [string]$assessment.ScopeStatus
        EvidenceIsolation = [string]$assessment.EvidenceIsolation
        TrustedForReasoning = [bool]$assessment.TrustedForReasoning
        ContaminationDetected = [bool]$assessment.ContaminationDetected
        ScopeNotes = [object[]]$assessment.ScopeNotes
    }
}

function Get-UiMediaScopeView {
    param(
        $SessionState = $null
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiMediaSessionState
    }

    $mapChannel = {
        param($Channel)

        $scope = if ($null -ne $Channel -and $null -ne $Channel.ScopeContract) {
            $Channel.ScopeContract
        }
        else {
            New-UiDefaultMediaScopeContract -Kind $(if ($null -ne $Channel) { [string]$Channel.Kind } else { "Unknown" })
        }

        return [pscustomobject]@{
            Kind = if ($null -ne $Channel) { [string]$Channel.Kind } else { [string]$scope.Kind }
            ProviderName = [string]$scope.ProviderName
            ScopeModel = [string]$scope.ScopeModel
            ProviderScopeLevel = [string]$scope.ProviderScopeLevel
            SourcePolicy = [string]$scope.SourcePolicy
            AcceptsSystemMixFallback = [bool]$scope.AcceptsSystemMixFallback
            SupportsPerAppAudioIsolation = [bool]$scope.SupportsPerAppAudioIsolation
            SupportsSystemMixCapture = [bool]$scope.SupportsSystemMixCapture
            SupportsDeviceLoopbackCapture = [bool]$scope.SupportsDeviceLoopbackCapture
            ScopeStatus = [string]$scope.ScopeStatus
            EvidenceIsolation = [string]$scope.EvidenceIsolation
            TrustedForReasoning = [bool]$scope.TrustedForReasoning
            ContaminationDetected = [bool]$scope.ContaminationDetected
            TargetProcessId = [int]$scope.TargetProcessId
            TargetProcessName = [string]$scope.TargetProcessName
            TargetWindowAutomationId = if ($null -ne $scope.TargetWindow) { [string]$scope.TargetWindow.AutomationId } else { "" }
            TargetWindowTitle = if ($null -ne $scope.TargetWindow) { [string]$scope.TargetWindow.Name } else { "" }
            ForegroundRelationAtStart = if ($null -ne $scope.StartSnapshot) { [string]$scope.StartSnapshot.ForegroundRelation } else { "" }
            ForegroundRelationAtStop = if ($null -ne $scope.StopSnapshot) { [string]$scope.StopSnapshot.ForegroundRelation } else { "" }
            ScopeNotes = [object[]]$scope.ScopeNotes
        }
    }

    return [pscustomobject]@{
        VideoCapture = & $mapChannel $SessionState.VideoCapture
        AudioCapture = & $mapChannel $SessionState.AudioCapture
    }
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

function Refresh-UiActiveMediaLeases {
    param(
        $SessionState = $null,
        [switch]$Persist
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiMediaSessionState
    }

    if ($null -eq $SessionState) {
        return $null
    }

    $now = [DateTimeOffset]::Now
    $changed = $false
    foreach ($channelName in @("VideoCapture", "AudioCapture")) {
        $channel = $SessionState.$channelName
        if ($null -eq $channel -or -not [bool]$channel.IsActive) {
            continue
        }

        $expiresAtText = [string]$channel.ExpiresAt
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

        $leaseMs = [int]$channel.LeaseMilliseconds
        if ($leaseMs -le 0) {
            continue
        }

        $newExpiry = $now.AddMilliseconds($leaseMs).ToString("o")
        if ($newExpiry -ne $expiresAtText) {
            $channel.ExpiresAt = $newExpiry
            $changed = $true
        }
    }

    if ($Persist -and $changed) {
        $SessionState = Save-UiMediaSessionState -SessionState $SessionState
    }

    return $SessionState
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
    $startScopeSnapshot = New-UiMediaScopeSnapshot -Kind "Video" -ProviderName ([string]$provider.Name) -ScopeModel ([string]$provider.ScopeModel) -Phase "start" -ProcessId $ProcessId
    $scopeContract = New-UiMediaScopeContract -Kind "Video" -Provider $provider -StartSnapshot $startScopeSnapshot
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
        ScopeContract = $scopeContract
        Notes = @(
            "التقاط الفيديو الحالي يعتمد على Steps Recorder كتتبع بصري لحظي خفيف.",
            "الأداة ستبقي المزود single-instance وتغلقه بهدوء عند الإيقاف أو انتهاء lease.",
            "العزل هنا يعتمد على attestation لنافذة البرنامج والـ foreground، لا على provider process-bound فعلية."
        )
    }

        Add-UiMediaRecentEvent -SessionState $sessionState -Kind "video-started" -Summary "بدأت الأداة sidecar فيديو خفيفة عبر Steps Recorder مع ربط scope بالبرنامج الحالي." -Payload @{
        ProviderName = $provider.Name
        OutputPath = $workingOutputPath
        ArchiveTargetPath = $archiveTargetPath
        LeaseMilliseconds = $effectiveLeaseMs
        ProcessId = $ProcessId
        Reason = $Reason
        ScopeStatus = $scopeContract.ScopeStatus
        EvidenceIsolation = $scopeContract.EvidenceIsolation
        ForegroundRelationAtStart = $startScopeSnapshot.ForegroundRelation
    }

    $sessionState = Save-UiMediaSessionState -SessionState $sessionState
    Write-UiTimelineEvent -Action "Media.VideoCapture" -Stage "started" -Success $true -Payload @{
        ProviderName = $provider.Name
        OutputPath = $workingOutputPath
        ArchiveTargetPath = $archiveTargetPath
        LeaseMilliseconds = $effectiveLeaseMs
        Reason = $Reason
        ProcessId = $ProcessId
        ScopeStatus = $scopeContract.ScopeStatus
        EvidenceIsolation = $scopeContract.EvidenceIsolation
        ForegroundRelationAtStart = $startScopeSnapshot.ForegroundRelation
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
    $existingScopeContract = $sessionState.VideoCapture.ScopeContract
    $targetProcessId = if ($null -ne $existingScopeContract) { [int]$existingScopeContract.TargetProcessId } else { 0 }

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
    $provider = if (-not [string]::IsNullOrWhiteSpace($providerName)) { Get-UiMediaProviderByName -Name $providerName } else { $null }
    $stopScopeSnapshot = if ($targetProcessId -gt 0) {
        New-UiMediaScopeSnapshot -Kind "Video" -ProviderName $providerName -ScopeModel $(if ($null -ne $provider) { [string]$provider.ScopeModel } else { "unscoped" }) -Phase "stop" -ProcessId $targetProcessId
    }
    else {
        $null
    }
    $scopeContract = New-UiMediaScopeContract -Kind "Video" -Provider $provider -StartSnapshot $(if ($null -ne $existingScopeContract) { $existingScopeContract.StartSnapshot } else { $null }) -StopSnapshot $stopScopeSnapshot
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
        ScopeContract = $scopeContract
        Notes = @(
            "تم إيقاف sidecar الفيديو وعادت الأداة إلى وضعها الخفيف.",
            $(if ($artifactStatus -eq "saved") { "المزود حفظ artifact فعلية ويمكن الرجوع إليها الآن." } else { "المزود لم يخرج artifact محفوظة هذه المرة؛ الإيقاف نجح لكن لا يوجد ملف بصري محفوظ." }),
            $(if ([bool]$scopeContract.TrustedForReasoning) { "العزل المقروء من الأداة يسمح بالاعتماد على هذا الدليل في الاستنتاج." } else { "الأداة لم تعتبر العزل كافيًا بالكامل؛ اقرأ ScopeStatus وEvidenceIsolation قبل الاعتماد على الدليل." })
        )
    }

    if ($wasActive -or $cleanedCount -gt 0) {
        Add-UiMediaRecentEvent -SessionState $sessionState -Kind "video-stopped" -Summary "أوقفت الأداة sidecar الفيديو وأعادت المزود إلى وضعه الطبيعي." -Payload @{
            ProviderName = $providerName
            ArtifactPath = $artifactPath
            ArtifactStatus = $artifactStatus
            Reason = $Reason
            CleanedProcessCount = $cleanedCount
            ScopeStatus = $scopeContract.ScopeStatus
            EvidenceIsolation = $scopeContract.EvidenceIsolation
            ContaminationDetected = $scopeContract.ContaminationDetected
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
            ScopeStatus = $scopeContract.ScopeStatus
            EvidenceIsolation = $scopeContract.EvidenceIsolation
            ContaminationDetected = $scopeContract.ContaminationDetected
        }
    }

    return Save-UiMediaSessionState -SessionState $sessionState
}

function Start-UiAudioCaptureSidecar {
    param(
        [string]$Reason = "manual-audio-start",
        [int]$LeaseMilliseconds = 0
    )

    $policy = Get-UiAudioScopePolicy
    $sessionState = Get-UiMediaSessionState
    $timestamp = (Get-Date).ToString("o")
    $sessionState.AudioCapture = [pscustomobject]@{
        Kind = "Audio"
        IsActive = $false
        ProviderName = [string]$policy.CurrentProviderName
        ProviderState = [string]$policy.ProviderReadiness
        Mode = "blocked-no-provider"
        Reason = $Reason
        StartedAt = $sessionState.AudioCapture.StartedAt
        UpdatedAt = $timestamp
        StoppedAt = $timestamp
        LeaseMilliseconds = 0
        ExpiresAt = $null
        OutputPath = $null
        ArchiveTargetPath = $null
        ArtifactPath = $null
        ArtifactStatus = "blocked"
        ProcessIds = @()
        LiveProcessCount = 0
        ScopeContract = [pscustomobject]@{
            Kind = "Audio"
            ProviderName = [string]$policy.CurrentProviderName
            ScopeModel = "planned-per-app-attested"
            ProviderScopeLevel = "none"
            SupportsProcessIsolation = $false
            SupportsWindowIsolation = $false
            SupportsForegroundAttestation = $false
            SourcePolicy = "per-app-attested-required"
            AcceptsSystemMixFallback = $false
            SupportsPerAppAudioIsolation = $false
            SupportsSystemMixCapture = $false
            SupportsDeviceLoopbackCapture = $false
            TargetProcessId = 0
            TargetProcessName = ""
            TargetMainWindowTitle = ""
            TargetWindow = $null
            StartSnapshot = $null
            StopSnapshot = $null
            ScopeStatus = "blocked"
            EvidenceIsolation = "no-provider"
            TrustedForReasoning = $false
            ContaminationDetected = $false
            ScopeNotes = [object[]]@(
                "تم حجب بدء sidecar الصوت لأن سياسة السمع الحالية تتطلب مزود per-app attested غير موصول بعد.",
                [string]$policy.StartBlockedReason
            )
        }
        Notes = @(
            "لم تبدأ sidecar الصوت لأن الأداة لا تملك مزودًا يحقق سياسة السمع الحالية.",
            [string]$policy.StartBlockedReason
        )
    }

    Add-UiMediaRecentEvent -SessionState $sessionState -Kind "audio-start-blocked" -Summary "رفضت الأداة بدء sidecar صوت لأن سياسة السمع الحالية لا تملك مزودًا موثوقًا بعد." -Payload @{
        Reason = $Reason
        LeaseMilliseconds = $LeaseMilliseconds
        PolicyName = $policy.PolicyName
        ProviderReadiness = $policy.ProviderReadiness
        StartBlockedReason = $policy.StartBlockedReason
        DesiredScopeModel = $policy.DesiredScopeModel
        AcceptsSystemMixFallback = $policy.AcceptsSystemMixFallback
    }

    $null = Save-UiMediaSessionState -SessionState $sessionState
    throw "No available audio provider is wired right now. $($policy.StartBlockedReason)"
}

function Stop-UiAudioCaptureSidecar {
    param(
        [string]$Reason = "manual-audio-stop"
    )

    $sessionState = Get-UiMediaSessionState
    $policy = Get-UiAudioScopePolicy
    $timestamp = (Get-Date).ToString("o")
    $sessionState.AudioCapture = [pscustomobject]@{
        Kind = "Audio"
        IsActive = $false
        ProviderName = [string]$policy.CurrentProviderName
        ProviderState = [string]$policy.ProviderReadiness
        Mode = "policy-unavailable"
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
        ScopeContract = New-UiDefaultMediaScopeContract -Kind "Audio" -ProviderName ([string]$policy.CurrentProviderName) -ScopeModel "planned-per-app-attested" -ProviderScopeLevel "none"
        Notes = @(
            "لا توجد sidecar صوت مفعلة حاليًا.",
            [string]$policy.StartBlockedReason
        )
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
