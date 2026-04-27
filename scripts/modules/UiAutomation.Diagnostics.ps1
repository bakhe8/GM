function Get-UiDiagnosticsPaths {
    $storageRoot = Get-UiStorageRoot
    $logsRoot = Join-Path $storageRoot "Logs"
    return [pscustomobject]@{
        StorageRoot = $storageRoot
        LogsRoot = $logsRoot
        ShellStatePath = Join-Path $logsRoot "ui-shell-state.json"
        EventLogPath = Join-Path $logsRoot "ui-events.jsonl"
        AppLogPath = Get-UiAppLogPath
    }
}

function Get-UiAppLogPath {
    $storageRoot = Get-UiStorageRoot
    $logsRoot = Join-Path $storageRoot "Logs"
    return Join-Path $logsRoot ("log_{0}.txt" -f (Get-Date -Format "yyyyMMdd"))
}

function Get-UiAcceptanceArtifactsRoot {
    $repoRoot = Get-UiAcceptanceRepoRoot
    return Join-Path $repoRoot "Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest"
}

function Get-UiTimelinePath {
    return Join-Path (Get-UiAcceptanceArtifactsRoot) "interactive-timeline.jsonl"
}

function Get-UiCalibrationPath {
    $scriptsRoot = Split-Path -Parent $PSScriptRoot
    return Join-Path $scriptsRoot "ui_human_calibration.json"
}

function Ensure-UiArtifactsRoot {
    $artifactsRoot = Get-UiAcceptanceArtifactsRoot
    if (-not (Test-Path -LiteralPath $artifactsRoot)) {
        New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    }

    return $artifactsRoot
}

function Ensure-UiCalibrationProfile {
    $path = Get-UiCalibrationPath
    if (Test-Path -LiteralPath $path) {
        return $path
    }

    $defaultProfile = [ordered]@{
        latencyThresholdsMs = [ordered]@{
            acceptable = 700
            slow       = 1500
        }
        actionThresholdsMs = [ordered]@{
            Launch = [ordered]@{
                acceptable = 5000
                slow       = 8000
            }
            Sidebar = [ordered]@{
                acceptable = 1400
                slow       = 2200
            }
            Click = [ordered]@{
                acceptable = 900
                slow       = 1600
            }
            DialogAction = [ordered]@{
                acceptable = 1600
                slow       = 2600
            }
        }
        notes = @(
            "افتح الحوارات المهمة ثم تأكد أن الإجراء المطلوب يعيد الفوكس إلى التطبيق الرئيسي بعد الإغلاق.",
            "إذا ظهر تأكيد إغلاق أو رسالة نظام، يجب حسمها قبل الانتقال إلى شاشة أخرى.",
            "راقب أي بطء يتجاوز الحد المقبول، خصوصًا في التنقل وفتح الحوارات الثقيلة."
        )
    } | ConvertTo-Json -Depth 6

    Set-Content -Path $path -Value $defaultProfile -Encoding UTF8
    return $path
}

function Get-UiCalibrationProfile {
    $path = Ensure-UiCalibrationProfile
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    $content = Get-Content -Path $path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    return $content | ConvertFrom-Json
}

function Get-UiLatencyThresholds {
    param(
        [string]$ActionName = "",
        $Calibration = $null
    )

    if ($null -eq $Calibration) {
        $Calibration = Get-UiCalibrationProfile
    }

    $acceptableMs = 700
    $slowMs = 1500

    if ($null -ne $Calibration -and $null -ne $Calibration.latencyThresholdsMs) {
        if ($null -ne $Calibration.latencyThresholdsMs.acceptable) {
            $acceptableMs = [int]$Calibration.latencyThresholdsMs.acceptable
        }

        if ($null -ne $Calibration.latencyThresholdsMs.slow) {
            $slowMs = [int]$Calibration.latencyThresholdsMs.slow
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ActionName) -and $null -ne $Calibration -and $null -ne $Calibration.actionThresholdsMs) {
        $property = $Calibration.actionThresholdsMs.PSObject.Properties[$ActionName]
        if ($null -ne $property) {
            $actionThresholds = $property.Value
            if ($null -ne $actionThresholds.acceptable) {
                $acceptableMs = [int]$actionThresholds.acceptable
            }

            if ($null -ne $actionThresholds.slow) {
                $slowMs = [int]$actionThresholds.slow
            }
        }
    }

    return [pscustomobject]@{
        AcceptableMs = $acceptableMs
        SlowMs = $slowMs
    }
}

function Add-UiFileLineWithRetry {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Line,
        [int]$MaxAttempts = 6,
        [int]$DelayMilliseconds = 70
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $directory = Split-Path -Parent $Path
            if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Force -Path $directory | Out-Null
            }

            $fileStream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::ReadWrite)
            try {
                $writer = New-Object System.IO.StreamWriter($fileStream, [System.Text.Encoding]::UTF8)
                try {
                    $writer.WriteLine($Line)
                    $writer.Flush()
                    return
                }
                finally {
                    $writer.Dispose()
                }
            }
            finally {
                $fileStream.Dispose()
            }
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            Start-Sleep -Milliseconds ($DelayMilliseconds * $attempt)
        }
    }
}

function Write-UiTimelineEvent {
    param(
        [Parameter(Mandatory)]
        [string]$Action,
        [string]$Stage = "completed",
        [bool]$Success = $true,
        [double]$DurationMs = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [hashtable]$Payload = @{}
    )

    Ensure-UiArtifactsRoot | Out-Null
    $record = [ordered]@{
        Timestamp = (Get-Date).ToString("o")
        Action = $Action
        Stage = $Stage
        Success = $Success
        DurationMs = [math]::Round($DurationMs, 2)
        WindowTitle = $WindowTitle
        WindowAutomationId = $WindowAutomationId
        Payload = $Payload
    }

    $line = $record | ConvertTo-Json -Depth 8 -Compress
    Add-UiFileLineWithRetry -Path (Get-UiTimelinePath) -Line $line
}

function Get-UiTimelineEntries {
    param(
        [int]$MaxCount = 20
    )

    $path = Get-UiTimelinePath
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

function Get-UiPerformanceSummary {
    param(
        [int]$MaxCount = 30
    )

    $timeline = @(Get-UiTimelineEntries -MaxCount $MaxCount | Where-Object { $_.Stage -eq "completed" })
    $calibration = Get-UiCalibrationProfile
    $defaultThresholds = Get-UiLatencyThresholds -Calibration $calibration

    $lastAction = $timeline | Select-Object -Last 1
    $grouped = @($timeline | Group-Object Action | ForEach-Object {
        $durations = @($_.Group | ForEach-Object { [double]$_.DurationMs })
        $thresholds = Get-UiLatencyThresholds -ActionName $_.Name -Calibration $calibration
        [pscustomobject]@{
            Action = $_.Name
            Count = $durations.Count
            AverageMs = if ($durations.Count -gt 0) { [math]::Round((($durations | Measure-Object -Average).Average), 2) } else { 0 }
            MaxMs = if ($durations.Count -gt 0) { [math]::Round((($durations | Measure-Object -Maximum).Maximum), 2) } else { 0 }
            LastMs = if ($durations.Count -gt 0) { [math]::Round($durations[-1], 2) } else { 0 }
            Thresholds = $thresholds
            AboveAcceptableCount = @($_.Group | Where-Object { [double]$_.DurationMs -ge $thresholds.AcceptableMs }).Count
            SlowCount = @($_.Group | Where-Object { [double]$_.DurationMs -ge $thresholds.SlowMs }).Count
        }
    })

    return [pscustomobject]@{
        Thresholds = $defaultThresholds
        TotalActions = $timeline.Count
        SlowActionCount = @($timeline | Where-Object {
            $thresholds = Get-UiLatencyThresholds -ActionName $_.Action -Calibration $calibration
            [double]$_.DurationMs -ge $thresholds.SlowMs
        }).Count
        LastAction = if ($null -ne $lastAction) {
            $lastThresholds = Get-UiLatencyThresholds -ActionName $lastAction.Action -Calibration $calibration
            [pscustomobject]@{
                Action = $lastAction.Action
                DurationMs = [math]::Round([double]$lastAction.DurationMs, 2)
                Success = [bool]$lastAction.Success
                Thresholds = $lastThresholds
                IsSlow = ([double]$lastAction.DurationMs -ge $lastThresholds.SlowMs)
                IsAboveAcceptable = ([double]$lastAction.DurationMs -ge $lastThresholds.AcceptableMs)
            }
        }
        else {
            $null
        }
        Actions = [object[]]$grouped
    }
}

function Get-UiShellStateSnapshot {
    $paths = Get-UiDiagnosticsPaths
    if (-not (Test-Path -LiteralPath $paths.ShellStatePath)) {
        return $null
    }

    $content = Get-Content -Path $paths.ShellStatePath -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    return $content | ConvertFrom-Json
}

function Split-UiJsonObjects {
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $objects = New-Object System.Collections.Generic.List[string]
    $buffer = New-Object System.Text.StringBuilder
    $depth = 0
    $inString = $false
    $escaping = $false

    foreach ($character in $Content.ToCharArray()) {
        if ($depth -eq 0 -and [char]::IsWhiteSpace($character)) {
            continue
        }

        [void]$buffer.Append($character)

        if ($inString) {
            if ($escaping) {
                $escaping = $false
                continue
            }

            if ($character -eq '\') {
                $escaping = $true
                continue
            }

            if ($character -eq '"') {
                $inString = $false
            }

            continue
        }

        if ($character -eq '"') {
            $inString = $true
            continue
        }

        if ($character -eq '{') {
            $depth++
            continue
        }

        if ($character -eq '}') {
            $depth--
            if ($depth -eq 0) {
                $objects.Add($buffer.ToString())
                $buffer.Clear() | Out-Null
            }
        }
    }

    return $objects
}

function Get-UiRecentEvents {
    param(
        [int]$MaxCount = 20
    )

    $paths = Get-UiDiagnosticsPaths
    if (-not (Test-Path -LiteralPath $paths.EventLogPath)) {
        return @()
    }

    $content = Get-Content -Path $paths.EventLogPath -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) {
        return @()
    }

    $tailWindow = [Math]::Max($MaxCount * 3, 24)
    $tailLines = @(Get-Content -Path $paths.EventLogPath -Tail $tailWindow -Encoding UTF8 -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $fastEvents = New-Object System.Collections.Generic.List[object]
    foreach ($line in $tailLines) {
        try {
            $eventRecord = $line | ConvertFrom-Json
            if ($null -ne $eventRecord.Timestamp -and $null -ne $eventRecord.Category -and $null -ne $eventRecord.Action) {
                [void]$fastEvents.Add($eventRecord)
            }
        }
        catch {
        }
    }

    if ($fastEvents.Count -ge [Math]::Min($MaxCount, 3)) {
        return @($fastEvents | Select-Object -Last $MaxCount)
    }

    return @(Split-UiJsonObjects -Content $content |
        Select-Object -Last $MaxCount |
        ForEach-Object { $_ | ConvertFrom-Json })
}

function ConvertFrom-UiSimpleLoggerLine {
    param(
        [Parameter(Mandatory)]
        [string]$Line
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $null
    }

    $match = [regex]::Match($Line, '^(?<Timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(?<Level>[A-Z]+)\s*\] \[(?<Caller>[^\]]+)\](?<Message>.*)$')
    if (-not $match.Success) {
        return $null
    }

    $parsedTimestamp = $null
    try {
        $parsedTimestamp = [DateTimeOffset]::new([DateTime]::ParseExact($match.Groups["Timestamp"].Value, "yyyy-MM-dd HH:mm:ss", $null))
    }
    catch {
    }

    return [pscustomobject]@{
        Timestamp = $match.Groups["Timestamp"].Value
        ParsedTimestamp = $parsedTimestamp
        Level = $match.Groups["Level"].Value.Trim()
        Caller = $match.Groups["Caller"].Value.Trim()
        Message = $match.Groups["Message"].Value.Trim()
        RawLine = $Line
    }
}

function Get-UiRecentAppLogEntries {
    param(
        [int]$MaxCount = 20,
        [int]$LookbackSeconds = 0,
        [switch]$ErrorsOnly
    )

    $path = Get-UiAppLogPath
    if (-not (Test-Path -LiteralPath $path)) {
        return @()
    }

    $tailWindow = [Math]::Max($MaxCount * 4, 40)
    $lines = @(Get-Content -Path $path -Tail $tailWindow -Encoding UTF8 -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $entries = @($lines |
        ForEach-Object { ConvertFrom-UiSimpleLoggerLine -Line $_ } |
        Where-Object { $null -ne $_ })

    if ($ErrorsOnly) {
        $entries = @($entries | Where-Object {
            [string]::Equals([string]$_.Level, "ERROR", [System.StringComparison]::OrdinalIgnoreCase)
        })
    }

    if ($LookbackSeconds -gt 0) {
        $cutoff = [DateTimeOffset]::Now.AddSeconds(-1 * $LookbackSeconds)
        $entries = @($entries | Where-Object {
            $null -eq $_.ParsedTimestamp -or $_.ParsedTimestamp -ge $cutoff
        })
    }

    return @($entries | Select-Object -Last $MaxCount)
}

function Get-UiRecentFaultSignals {
    param(
        [int]$ProcessId = 0,
        [int]$MaxCount = 12,
        [int]$LookbackSeconds = 45,
        [switch]$IncludeProcessExit = $true
    )

    $signals = New-Object System.Collections.Generic.List[object]
    $cutoff = [DateTimeOffset]::Now.AddSeconds(-1 * [Math]::Max(1, $LookbackSeconds))

    $recentEvents = @(Get-UiRecentEvents -MaxCount ([Math]::Max($MaxCount * 4, 32)))
    foreach ($eventRecord in $recentEvents) {
        if (-not [string]::Equals([string]$eventRecord.Category, "runtime.fault", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $eventTimestamp = $null
        try {
            $eventTimestamp = [DateTimeOffset]::Parse([string]$eventRecord.Timestamp)
        }
        catch {
        }

        if ($null -ne $eventTimestamp -and $eventTimestamp -lt $cutoff) {
            continue
        }

        $payload = $eventRecord.Payload
        $title = if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains "Title") { [string]$payload.Title } else { [string]$eventRecord.Action }
        $message = if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains "Message") { [string]$payload.Message } else { "" }
        $severity = if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains "Severity") { [string]$payload.Severity } else { "Error" }
        $exceptionType = if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains "ExceptionType") { [string]$payload.ExceptionType } else { "" }
        $isTerminating = if ($null -ne $payload -and $payload.PSObject.Properties.Name -contains "IsTerminating") { [bool]$payload.IsTerminating } else { $false }
        $signature = "runtime-fault::{0}::{1}::{2}::{3}" -f ([string]$eventRecord.Action), $title, $exceptionType, $isTerminating

        [void]$signals.Add([pscustomobject]@{
            Timestamp = if ($null -ne $eventTimestamp) { $eventTimestamp.ToString("o") } else { [string]$eventRecord.Timestamp }
            Kind = "runtime-fault-event"
            Severity = $severity
            Title = $title
            Message = $message
            Signature = $signature
            Action = [string]$eventRecord.Action
            ExceptionType = $exceptionType
            IsTerminating = $isTerminating
            TrustedForReasoning = $true
            Scope = "program-diagnostics"
            Source = "ui-events.jsonl"
        })
    }

    $recentLogErrors = @(Get-UiRecentAppLogEntries -ErrorsOnly -MaxCount ([Math]::Max($MaxCount * 4, 32)) -LookbackSeconds $LookbackSeconds)
    foreach ($entry in $recentLogErrors) {
        $signature = "logger-error::{0}::{1}" -f ([string]$entry.Caller), ([string]$entry.Message)
        [void]$signals.Add([pscustomobject]@{
            Timestamp = if ($null -ne $entry.ParsedTimestamp) { $entry.ParsedTimestamp.ToString("o") } else { [string]$entry.Timestamp }
            Kind = "logger-error"
            Severity = "Error"
            Title = if ([string]::IsNullOrWhiteSpace([string]$entry.Caller)) { "سجل التطبيق" } else { [string]$entry.Caller }
            Message = [string]$entry.Message
            Signature = $signature
            Action = [string]$entry.Caller
            ExceptionType = ""
            IsTerminating = $false
            TrustedForReasoning = $true
            Scope = "program-log"
            Source = "log_yyyyMMdd.txt"
        })
    }

    if ($IncludeProcessExit -and $ProcessId -gt 0) {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            [void]$signals.Add([pscustomobject]@{
                Timestamp = (Get-Date).ToString("o")
                Kind = "process-exited"
                Severity = "Error"
                Title = "توقفت العملية المستهدفة"
                Message = "عملية التطبيق التي كانت الأداة تراقبها لم تعد موجودة."
                Signature = "process-exited::$ProcessId"
                Action = "process-exited"
                ExceptionType = ""
                IsTerminating = $true
                TrustedForReasoning = $true
                Scope = "process-liveness"
                Source = "process-state"
            })
        }
    }

    return @($signals |
        Group-Object Signature |
        ForEach-Object {
            $_.Group | Sort-Object {
                try { [DateTimeOffset]::Parse([string]$_.Timestamp) } catch { [DateTimeOffset]::MinValue }
            } -Descending | Select-Object -First 1
        } |
        Sort-Object {
            try { [DateTimeOffset]::Parse([string]$_.Timestamp) } catch { [DateTimeOffset]::MinValue }
        } -Descending |
        Select-Object -First $MaxCount)
}

function Get-UiAssessment {
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$ProbePayload
    )

    $warnings = New-Object System.Collections.Generic.List[string]
    $observations = New-Object System.Collections.Generic.List[string]
    $performance = $ProbePayload.PerformanceSummary
    $health = $ProbePayload.Health
    [object[]]$recentFaultSignals = @()
    if ($ProbePayload.PSObject.Properties.Name -contains "RecentFaultSignals" -and $null -ne $ProbePayload.RecentFaultSignals) {
        $recentFaultSignals = [object[]]@($ProbePayload.RecentFaultSignals | ForEach-Object { $_ })
    }
    $recentFaultSignalCount = @($recentFaultSignals | ForEach-Object { $_ }).Count

    if (-not $health.HasShellState) {
        [void]$warnings.Add("لا توجد لقطة تشخيصية حية من الـ Shell حاليًا.")
    }

    if ($health.OpenWindowCount -gt 1 -and -not [string]::IsNullOrWhiteSpace($health.ActiveDialogTitle)) {
        [void]$observations.Add("يوجد حوار نشط فوق التطبيق: $($health.ActiveDialogTitle)")
    }

    if ($null -ne $performance -and $null -ne $performance.LastAction) {
        $thresholds = if ($null -ne $performance.LastAction.Thresholds) { $performance.LastAction.Thresholds } else { $performance.Thresholds }
        if ($performance.LastAction.IsSlow) {
            [void]$warnings.Add("آخر إجراء UI أبطأ من الحد البشري البطيء ($($thresholds.SlowMs)ms).")
        }
        elseif ($performance.LastAction.IsAboveAcceptable) {
            [void]$observations.Add("آخر إجراء UI أعلى من الحد المقبول ($($thresholds.AcceptableMs)ms) لكنه ليس بطيئًا جدًا.")
        }
    }

    if ($recentFaultSignalCount -gt 0) {
        $topSignal = $recentFaultSignals[0]
        [void]$warnings.Add("رُصدت إشارة fault حديثة من داخل التطبيق: $([string]$topSignal.Title)")
    }

    if ($ProbePayload.PSObject.Properties.Name -contains "PrimaryHeuristicRecommendation" -and $null -ne $ProbePayload.PrimaryHeuristicRecommendation) {
        [void]$observations.Add("الـ heuristic الأوضح الآن: $([string]$ProbePayload.PrimaryHeuristicRecommendation.StrategyName)")
    }

    return [pscustomobject]@{
        Warnings = [object[]]$warnings
        Observations = [object[]]$observations
        HumanNotes = if ($null -ne $ProbePayload.Calibration) { [object[]]$ProbePayload.Calibration.notes } else { @() }
    }
}

function Get-UiFaultStatePayload {
    param(
        [int]$ProcessId = 0,
        [int]$MaxResults = 20
    )

    [object[]]$recentFaultSignals = @(Get-UiRecentFaultSignals -ProcessId $ProcessId -MaxCount $MaxResults)
    $processRunning = if ($ProcessId -gt 0) { $null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) } else { $null -ne (Get-UiProcess) }

    return [pscustomobject]@{
        ProcessId = $ProcessId
        DiagnosticsPaths = Get-UiDiagnosticsPaths
        AppLogPath = Get-UiAppLogPath
        RecentFaultSignals = [object[]]$recentFaultSignals
        FaultSummary = [pscustomobject]@{
            SignalCount = @($recentFaultSignals).Count
            TrustedSignalCount = @($recentFaultSignals | Where-Object TrustedForReasoning).Count
            ProcessRunning = $processRunning
            LatestSignal = if (@($recentFaultSignals).Count -gt 0) { $recentFaultSignals[0] } else { $null }
        }
    }
}

function Get-ProbePayload {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [Parameter(Mandatory)]
        [string]$OutputPath,
        [int]$MaxResults = 50,
        [switch]$WithCapture
    )

    $window = Resolve-UiWindow -ProcessId $Process.Id
    $windows = @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground)
    $diagnosticsPaths = Get-UiDiagnosticsPaths
    $shellState = Get-UiShellStateSnapshot
    $recentEvents = @(Get-UiRecentEvents -MaxCount $MaxResults)
    $recentTimeline = @(Get-UiTimelineEntries -MaxCount $MaxResults)
    $capabilitySession = Invoke-UiCapabilityBrokerSweep -Persist
    $mediaSession = Invoke-UiMediaBrokerSweep -Persist
    $mediaScopeView = Get-UiMediaScopeView -SessionState $mediaSession
    $audioScopePolicy = Get-UiAudioScopePolicy
    $mediaProviders = @(Get-UiMediaProviderCatalog)
    $heuristicState = Get-UiHeuristicStatePayload -ProcessId $Process.Id -MaxResults $MaxResults
    $recentCapabilityObservations = @(Get-UiCapabilityObservationEntries -MaxCount $MaxResults)
    $recentCapabilityDecisions = if ($null -ne $capabilitySession) { @($capabilitySession.RecentDecisions | Select-Object -First $MaxResults) } else { @() }
    $capabilityOperatorView = Get-UiCapabilityOperatorView -SessionState $capabilitySession
    [object[]]$recentFaultSignals = @(Get-UiRecentFaultSignals -ProcessId $Process.Id -MaxCount $MaxResults)
    $calibration = Get-UiCalibrationProfile
    $performanceSummary = Get-UiPerformanceSummary -MaxCount $MaxResults
    $meaningfulWindows = @($windows | Where-Object {
        $_.AutomationId -eq "Shell.MainWindow" -or
        -not [string]::IsNullOrWhiteSpace($_.Name)
    })
    $dialogWindow = $meaningfulWindows |
        Where-Object { $_.AutomationId -ne "Shell.MainWindow" -and $_.ProcessId -eq $Process.Id } |
        Select-Object -First 1
    $externalForegroundWindow = $meaningfulWindows |
        Where-Object { $_.AutomationId -ne "Shell.MainWindow" -and $_.ProcessId -ne $Process.Id } |
        Select-Object -First 1

    $payload = [ordered]@{
        ProcessId = $Process.Id
        MainWindow = Get-UiElementSummary -Element $window
        OpenWindows = [object[]]$windows
        ForegroundWindow = if ($null -ne $externalForegroundWindow) { $externalForegroundWindow } elseif ($null -ne $dialogWindow) { $dialogWindow } else { $null }
        DiagnosticsPaths = $diagnosticsPaths
        TimelinePath = Get-UiTimelinePath
        CalibrationPath = Get-UiCalibrationPath
        ShellState = $shellState
        RecentEvents = [object[]]$recentEvents
        RecentTimeline = [object[]]$recentTimeline
        CapabilitySession = $capabilitySession
        MediaSession = $mediaSession
        MediaScopeView = $mediaScopeView
        AudioScopePolicy = $audioScopePolicy
        MediaProviders = [object[]]$mediaProviders
        AppLogPath = Get-UiAppLogPath
        RecentFaultSignals = [object[]]$recentFaultSignals
        HeuristicDefinitions = [object[]]$heuristicState.HeuristicDefinitions
        HeuristicRecommendations = [object[]]$heuristicState.Recommendations
        PrimaryHeuristicRecommendation = $heuristicState.PrimaryRecommendation
        RecentHeuristicDecisions = [object[]]$heuristicState.RecentHeuristicDecisions
        HeuristicOperatorView = $heuristicState.HeuristicOperatorView
        RecentCapabilityObservations = [object[]]$recentCapabilityObservations
        RecentCapabilityDecisions = [object[]]$recentCapabilityDecisions
        CapabilityOperatorView = $capabilityOperatorView
        Calibration = $calibration
        PerformanceSummary = $performanceSummary
        Health = [pscustomobject]@{
            HasShellState = $null -ne $shellState
            EventCount = $recentEvents.Count
            TimelineCount = $recentTimeline.Count
            ActiveCapabilityCount = if ($null -ne $capabilitySession -and $null -ne $capabilitySession.ActiveCapabilities) { @($capabilitySession.ActiveCapabilities).Count } else { 0 }
            ActiveMediaCount = @(
                if ($null -ne $mediaSession -and $null -ne $mediaSession.VideoCapture -and $mediaSession.VideoCapture.IsActive) { "Video" }
                if ($null -ne $mediaSession -and $null -ne $mediaSession.AudioCapture -and $mediaSession.AudioCapture.IsActive) { "Audio" }
            ).Count
            MediaScopeStatus = if ($null -ne $mediaScopeView -and $null -ne $mediaScopeView.VideoCapture) { [string]$mediaScopeView.VideoCapture.ScopeStatus } else { "" }
            MediaEvidenceIsolation = if ($null -ne $mediaScopeView -and $null -ne $mediaScopeView.VideoCapture) { [string]$mediaScopeView.VideoCapture.EvidenceIsolation } else { "" }
            MediaTrustedForReasoning = if ($null -ne $mediaScopeView -and $null -ne $mediaScopeView.VideoCapture) { [bool]$mediaScopeView.VideoCapture.TrustedForReasoning } else { $false }
            HasScopedMediaContamination = if ($null -ne $mediaScopeView -and $null -ne $mediaScopeView.VideoCapture) { [bool]$mediaScopeView.VideoCapture.ContaminationDetected } else { $false }
            AudioProviderReadiness = if ($null -ne $audioScopePolicy) { [string]$audioScopePolicy.ProviderReadiness } else { "" }
            AudioSystemMixFallbackAllowed = if ($null -ne $audioScopePolicy) { [bool]$audioScopePolicy.AcceptsSystemMixFallback } else { $false }
            FaultSignalCount = @($recentFaultSignals).Count
            HasTrustedFaultSignal = @($recentFaultSignals | Where-Object TrustedForReasoning).Count -gt 0
            HeuristicRecommendationCount = @($heuristicState.Recommendations).Count
            HasActiveHeuristicRecommendation = $null -ne $heuristicState.PrimaryRecommendation
            OpenWindowCount = $meaningfulWindows.Count
            RawOpenWindowCount = $windows.Count
            ActiveDialogTitle = if ($null -ne $externalForegroundWindow) { $externalForegroundWindow.Name } elseif ($null -ne $dialogWindow) { $dialogWindow.Name } else { $null }
            HasExternalForegroundWindow = $null -ne $externalForegroundWindow
        }
    }

    if ($WithCapture) {
        $capturePath = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputPath))
        if ($null -ne $externalForegroundWindow) {
            Save-UiDesktopScreenshot -Path $capturePath | Out-Null
            $payload["CaptureMode"] = "Desktop"
        }
        else {
            Save-UiWindowScreenshot -Window $window -Path $capturePath | Out-Null
            $payload["CaptureMode"] = "Window"
        }
        $payload["Capture"] = $capturePath
    }

    $payload["Assessment"] = Get-UiAssessment -ProbePayload ([pscustomobject]$payload)
    return [pscustomobject]$payload
}

function Get-TracePayloadFromResult {
    param($Result)

    $payload = [ordered]@{}
    if ($null -eq $Result) {
        return $payload
    }

    foreach ($propertyName in @("Action", "Label", "Value", "Capture", "TimelinePath", "CalibrationPath")) {
        if ($Result.PSObject.Properties.Name -contains $propertyName) {
            $payload[$propertyName] = $Result.$propertyName
        }
    }

    if ($Result.PSObject.Properties.Name -contains "Comparison" -and $null -ne $Result.Comparison) {
        $payload["DifferenceRatio"] = $Result.Comparison.DifferenceRatio
        $payload["DiffPath"] = $Result.Comparison.DiffPath
    }

    if ($Result.PSObject.Properties.Name -contains "Window" -and $null -ne $Result.Window) {
        $payload["WindowTitle"] = $Result.Window.Name
        $payload["WindowAutomationId"] = $Result.Window.AutomationId
    }

    if ($Result.PSObject.Properties.Name -contains "Position" -and $null -ne $Result.Position) {
        $payload["Position"] = $Result.Position
    }

    if ($Result.PSObject.Properties.Name -contains "StartPosition" -and $null -ne $Result.StartPosition) {
        $payload["StartPosition"] = $Result.StartPosition
    }

    if ($Result.PSObject.Properties.Name -contains "EndPosition" -and $null -ne $Result.EndPosition) {
        $payload["EndPosition"] = $Result.EndPosition
    }

    foreach ($propertyName in @("Button", "ClickCount", "HoverMilliseconds", "ScrollDelta", "DeltaX", "DeltaY")) {
        if ($Result.PSObject.Properties.Name -contains $propertyName) {
            $payload[$propertyName] = $Result.$propertyName
        }
    }

    if ($Result.PSObject.Properties.Name -contains "CapabilityName" -and -not [string]::IsNullOrWhiteSpace([string]$Result.CapabilityName)) {
        $payload["CapabilityName"] = [string]$Result.CapabilityName
    }

    if ($Result.PSObject.Properties.Name -contains "CapabilitySession" -and $null -ne $Result.CapabilitySession) {
        $payload["CapabilitySessionId"] = [string]$Result.CapabilitySession.SessionId
        $payload["ActiveCapabilities"] = @($Result.CapabilitySession.ActiveCapabilities | ForEach-Object { $_.Name })
    }

    if ($Result.PSObject.Properties.Name -contains "CapabilityCaptures" -and $null -ne $Result.CapabilityCaptures) {
        $captures = @($Result.CapabilityCaptures)
        $payload["CapabilityCaptureCount"] = $captures.Count
        $payload["CapabilityCapturePaths"] = @($captures | ForEach-Object { $_.Path })
    }

    if ($Result.PSObject.Properties.Name -contains "CapabilityHookWarning" -and -not [string]::IsNullOrWhiteSpace([string]$Result.CapabilityHookWarning)) {
        $payload["CapabilityHookWarning"] = [string]$Result.CapabilityHookWarning
    }

    return $payload
}
