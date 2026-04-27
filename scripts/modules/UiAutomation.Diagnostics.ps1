function Get-UiAssessment {
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$ProbePayload
    )

    $warnings = New-Object System.Collections.Generic.List[string]
    $observations = New-Object System.Collections.Generic.List[string]
    $performance = $ProbePayload.PerformanceSummary
    $health = $ProbePayload.Health

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

    return [pscustomobject]@{
        Warnings = [object[]]$warnings
        Observations = [object[]]$observations
        HumanNotes = if ($null -ne $ProbePayload.Calibration) { [object[]]$ProbePayload.Calibration.notes } else { @() }
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
        Calibration = $calibration
        PerformanceSummary = $performanceSummary
        Health = [pscustomobject]@{
            HasShellState = $null -ne $shellState
            EventCount = $recentEvents.Count
            TimelineCount = $recentTimeline.Count
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

    return $payload
}
