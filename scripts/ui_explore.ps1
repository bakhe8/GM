param(
    [ValidateSet("Launch", "Windows", "Elements", "Sidebar", "Click", "SetField", "WaitWindow", "Capture", "DialogAction", "State", "Diagnostics", "Probe", "Compare", "Events", "Key", "SendKeys")]
    [string]$Action = "Probe",
    [string]$WindowTitle = "",
    [string]$WindowAutomationId = "",
    [string]$Name = "",
    [string]$Label = "",
    [string]$Text = "",
    [string]$Value = "",
    [string]$AutomationId = "",
    [string]$ControlType = "",
    [string]$Category = "",
    [string]$EventActionName = "",
    [string]$KeyName = "",
    [string]$OutputPath = ".\\Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-capture.png",
    [string]$ReferencePath = "",
    [string]$DiffOutputPath = ".\\Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-diff.png",
    [int]$ProcessId = 0,
    [int]$MaxResults = 50,
    [int]$Tolerance = 18,
    [int]$SampleStep = 2,
    [switch]$PartialMatch,
    [switch]$ReuseRunningSession = $true,
    [switch]$IncludeCapture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modulePath = Join-Path $PSScriptRoot "UIAutomation.Acceptance.psm1"
Import-Module $modulePath -Force

$repoRoot = Get-UiAcceptanceRepoRoot

function Get-ResolvedProcess {
    if ($ProcessId -ne 0) {
        return Get-Process -Id $ProcessId -ErrorAction Stop
    }

    return Start-UiTargetApplication -RepoRoot $repoRoot -ReuseRunningSession:$ReuseRunningSession
}

function Get-UiBlockingWindows {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    return @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground | Where-Object {
        $_.AutomationId -ne "Shell.MainWindow" -and
        $_.ControlType -eq "ControlType.Window" -and
        -not [string]::IsNullOrWhiteSpace($_.Name)
    })
}

function Assert-UiNoBlockingWindows {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [string]$AllowedTitle = "",
        [string]$AllowedAutomationId = ""
    )

    $blocking = @(Get-UiBlockingWindows -Process $Process | Where-Object {
        (($AllowedTitle -eq "") -or $_.Name -ne $AllowedTitle) -and
        (($AllowedAutomationId -eq "") -or $_.AutomationId -ne $AllowedAutomationId)
    })

    if ($blocking.Count -eq 0) {
        return
    }

    $titles = ($blocking | ForEach-Object { $_.Name } | Select-Object -Unique) -join "، "
    throw "يوجد حوار أو رسالة مفتوحة فوق التطبيق: $titles. احسمها أولًا عبر DialogAction أو استهدفها مباشرة قبل متابعة التنقل أو التفاعل العام."
}

function Wait-UiWindowClosed {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [pscustomobject]$WindowSummary,
        [int]$TimeoutSeconds = 5
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $matches = @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground | Where-Object {
            if ($WindowSummary.NativeWindowHandle -ne 0) {
                $_.NativeWindowHandle -eq $WindowSummary.NativeWindowHandle
            }
            else {
                $sameAutomation = -not [string]::IsNullOrWhiteSpace($WindowSummary.AutomationId) -and $_.AutomationId -eq $WindowSummary.AutomationId
                $sameTitle = -not [string]::IsNullOrWhiteSpace($WindowSummary.Name) -and $_.Name -eq $WindowSummary.Name
                $sameAutomation -or $sameTitle
            }
        })

        if ($matches.Count -eq 0) {
            return $true
        }

        Start-Sleep -Milliseconds 200
    }

    return $false
}

function Resolve-UiActionTarget {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [switch]$PartialMatch,
        [int]$InitialTimeoutSeconds = 1,
        [int]$FallbackTimeoutSeconds = 5
    )

    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        return Get-UiButtonByText -Root $Window -Text $Text -ProcessId $Process.Id -SearchProcessFallback -PartialMatch:$PartialMatch
    }

    try {
        return Wait-UiElement `
            -Root $Window `
            -Name $Name `
            -AutomationId $AutomationId `
            -ControlType $null `
            -TimeoutSeconds $InitialTimeoutSeconds `
            -PartialMatch:$PartialMatch
    }
    catch {
        $processWide = @(
            Find-UiProcessElements `
                -ProcessId $Process.Id `
                -Name $Name `
                -AutomationId $AutomationId `
                -MaxResults 6 `
                -PartialMatch:$PartialMatch
        )

        if ($processWide.Count -gt 0) {
            return $processWide[0]
        }
    }

    return Wait-UiElement `
        -Root $Window `
        -Name $Name `
        -AutomationId $AutomationId `
        -ControlType $null `
        -TimeoutSeconds $FallbackTimeoutSeconds `
        -PartialMatch:$PartialMatch
}

function Write-UiObject {
    param($InputObject)
    $InputObject | ConvertTo-Json -Depth 10
}

function Resolve-UiVirtualKey {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    switch ($Name.Trim().ToLowerInvariant()) {
        "enter" { return [byte]0x0D }
        "escape" { return [byte]0x1B }
        "esc" { return [byte]0x1B }
        "tab" { return [byte]0x09 }
        "down" { return [byte]0x28 }
        "up" { return [byte]0x26 }
        "left" { return [byte]0x25 }
        "right" { return [byte]0x27 }
        default { throw "Unsupported key '$Name'. Supported keys: Enter, Escape, Tab, Up, Down, Left, Right." }
    }
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
        $Process,
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
        $capturePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
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

function Resolve-UiScopeRoot {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [string]$Title = "",
        [string]$AutomationId = "",
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($Title) -and [string]::IsNullOrWhiteSpace($AutomationId)) {
        return Resolve-UiWindow -ProcessId $Process.Id
    }

    try {
        return Resolve-UiWindow -Title $Title -AutomationId $AutomationId -ProcessId $Process.Id -PartialMatch:$PartialMatch
    }
    catch {
        $mainWindow = Resolve-UiWindow -ProcessId $Process.Id
        if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
            return Wait-UiElement -Root $mainWindow -AutomationId $AutomationId -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        if (-not [string]::IsNullOrWhiteSpace($Title)) {
            return Wait-UiElement -Root $mainWindow -Name $Title -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        throw
    }
}

function Invoke-UiAction {
    $process = Get-ResolvedProcess

    switch ($Action) {
        "Launch" {
            $window = Resolve-UiWindow -ProcessId $process.Id
            Show-UiWindow -Window $window
            return [pscustomobject]@{
                ProcessId = $process.Id
                MainWindowHandle = $process.MainWindowHandle
                Window = Get-UiElementSummary -Element $window
            }
        }

        "Windows" {
            $windows = Get-UiWindowsCatalog -ProcessId $process.Id -IncludeRelatedForeground
            return [pscustomobject]@{
                ProcessId = $process.Id
                Windows = [object[]]$windows
            }
        }

        "Elements" {
            $window = Resolve-UiScopeRoot -Process $process -Title $WindowTitle -AutomationId $WindowAutomationId -PartialMatch:$PartialMatch
            $elements = Find-UiElements -Root $window -ProcessId $process.Id -Name $Name -AutomationId $AutomationId -ControlType $ControlType -SearchProcessFallback -PartialMatch:$PartialMatch -MaxResults $MaxResults
            return [pscustomobject]@{
                ProcessId = $process.Id
                Window = Get-UiElementSummary -Element $window
                Elements = [object[]]$elements
            }
        }

        "Sidebar" {
            Assert-UiNoBlockingWindows -Process $process
            $window = Resolve-UiWindow -ProcessId $process.Id
            Invoke-UiSidebarNavigation -MainWindow $window -WorkspaceLabel $Label
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Sidebar"
                Label = $Label
                Window = Get-UiElementSummary -Element $window
            }
        }

        "Click" {
            if ([string]::IsNullOrWhiteSpace($WindowTitle) -and [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Assert-UiNoBlockingWindows -Process $process
            }
            elseif ($WindowAutomationId -eq "Shell.MainWindow") {
                Assert-UiNoBlockingWindows -Process $process
            }
            $window = Resolve-UiScopeRoot -Process $process -Title $WindowTitle -AutomationId $WindowAutomationId -PartialMatch:$PartialMatch
            $target = Resolve-UiActionTarget `
                -Process $process `
                -Window $window `
                -Name $Name `
                -AutomationId $AutomationId `
                -Text $Text `
                -PartialMatch:$PartialMatch

            $targetSummary = Get-UiElementSummary -Element $target
            Invoke-UiElement -Element $target
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Click"
                Target = $targetSummary
            }
        }

        "SetField" {
            if ([string]::IsNullOrWhiteSpace($WindowTitle) -and [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Assert-UiNoBlockingWindows -Process $process
            }
            elseif ($WindowAutomationId -eq "Shell.MainWindow") {
                Assert-UiNoBlockingWindows -Process $process
            }
            $window = Resolve-UiScopeRoot -Process $process -Title $WindowTitle -AutomationId $WindowAutomationId -PartialMatch:$PartialMatch
            $field = if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
                Wait-UiElement -Root $window -AutomationId $AutomationId -ControlType $null -TimeoutSeconds 5 -PartialMatch:$PartialMatch
            }
            elseif (-not [string]::IsNullOrWhiteSpace($Label)) {
                Get-UiEditNearLabel -Window $window -Label $Label
            }
            else {
                throw "SetField requires either -Label or -AutomationId."
            }
            Set-UiElementValue -Element $field -Value $Value
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "SetField"
                Label = $Label
                Value = $Value
                Field = Get-UiElementSummary -Element $field
            }
        }

        "WaitWindow" {
            $window = Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $process.Id -PartialMatch:$PartialMatch
            Show-UiWindow -Window $window
            return [pscustomobject]@{
                ProcessId = $process.Id
                Window = Get-UiElementSummary -Element $window
            }
        }

        "Capture" {
            $window = if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -or -not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $process.Id -PartialMatch:$PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            $resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
            Save-UiWindowScreenshot -Window $window -Path $resolvedOutput | Out-Null
            return [pscustomobject]@{
                ProcessId = $process.Id
                Capture = $resolvedOutput
                Window = Get-UiElementSummary -Element $window
            }
        }

        "DialogAction" {
            $dialog = if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -or -not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $process.Id -PartialMatch:$PartialMatch
            }
            else {
                Get-UiActiveDialog -ProcessId $process.Id
            }

            $preferredLabels = if (-not [string]::IsNullOrWhiteSpace($Text) -or -not [string]::IsNullOrWhiteSpace($Name)) {
                @($Text, $Name, "إلغاء", "Cancel", "&Cancel", "موافق", "OK", "نعم", "Yes", "&Yes", "لا", "No", "&No")
            }
            else {
                @("Yes", "&Yes", "OK", "موافق", "نعم", "إلغاء", "Cancel", "&Cancel", "لا", "No", "&No")
            }

            $button = Get-UiDialogActionButton -Dialog $dialog -PreferredLabels @(
                $preferredLabels |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Select-Object -Unique
            )
            $dialogSummary = Get-UiElementSummary -Element $dialog
            $buttonSummary = Get-UiElementSummary -Element $button
            $result = Invoke-UiDialogActionButton -Dialog $dialog -PreferredLabels @(
                $preferredLabels |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Select-Object -Unique
            ) -ProcessId $process.Id -CloseTimeoutSeconds 3
            if (-not $result.Closed) {
                throw "تم الوصول إلى زر داخل الحوار '$($dialogSummary.Name)' لكن النافذة بقيت مفتوحة بعد $(if ($null -ne $result.Attempt) { $result.Attempt } else { 0 }) محاولات. آخر أسلوب: $($result.Strategy)."
            }
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "DialogAction"
                Dialog = $dialogSummary
                Button = if ($null -ne $result.Button) { $result.Button } else { $buttonSummary }
                Closed = $result.Closed
                Strategy = $result.Strategy
                Attempt = $result.Attempt
            }
        }

        "Events" {
            $events = @(Get-UiRecentEvents -MaxCount $MaxResults | Where-Object {
                ([string]::IsNullOrWhiteSpace($Category) -or $_.Category -eq $Category) -and
                ([string]::IsNullOrWhiteSpace($EventActionName) -or $_.Action -eq $EventActionName)
            })

            return [pscustomobject]@{
                ProcessId = $process.Id
                Category = $Category
                EventActionName = $EventActionName
                Events = [object[]]$events
            }
        }

        "Key" {
            $window = if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -or -not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Resolve-UiScopeRoot -Process $process -Title $WindowTitle -AutomationId $WindowAutomationId -PartialMatch:$PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            Show-UiWindow -Window $window
            $windowSummary = Get-UiElementSummary -Element $window
            $targetElement = $null
            if (-not [string]::IsNullOrWhiteSpace($Text) -or -not [string]::IsNullOrWhiteSpace($AutomationId) -or -not [string]::IsNullOrWhiteSpace($Name)) {
                $targetElement = Resolve-UiActionTarget `
                    -Process $process `
                    -Window $window `
                    -Name $Name `
                    -AutomationId $AutomationId `
                    -Text $Text `
                    -PartialMatch:$PartialMatch
            }

            $targetSummary = if ($null -ne $targetElement) { Get-UiElementSummary -Element $targetElement } else { $null }

            if ($null -ne $targetElement) {
                Invoke-UiElement -Element $targetElement
                Start-Sleep -Milliseconds 100
            }

            $vk = Resolve-UiVirtualKey -Name $KeyName
            Send-UiVirtualKey -VirtualKey $vk
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Key"
                KeyName = $KeyName
                Window = $windowSummary
                Target = $targetSummary
            }
        }

        "SendKeys" {
            $window = if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -or -not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $process.Id -PartialMatch:$PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            Show-UiWindow -Window $window
            $windowSummary = Get-UiElementSummary -Element $window
            $targetElement = $null

            if (-not [string]::IsNullOrWhiteSpace($Text) -or -not [string]::IsNullOrWhiteSpace($AutomationId) -or -not [string]::IsNullOrWhiteSpace($Name)) {
                $targetElement = Resolve-UiActionTarget `
                    -Process $process `
                    -Window $window `
                    -Name $Name `
                    -AutomationId $AutomationId `
                    -Text $Text `
                    -PartialMatch:$PartialMatch
            }

            $targetSummary = if ($null -ne $targetElement) { Get-UiElementSummary -Element $targetElement } else { $null }

            if ($null -ne $targetElement) {
                Invoke-UiElement -Element $targetElement
                Start-Sleep -Milliseconds 100
            }

            $keysText = if (-not [string]::IsNullOrWhiteSpace($Value)) { $Value } else { $Text }
            if ([string]::IsNullOrWhiteSpace($keysText)) {
                throw "SendKeys requires -Value or -Text."
            }

            Send-UiSendKeys -KeysText $keysText
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "SendKeys"
                KeysText = $keysText
                Window = $windowSummary
                Target = $targetSummary
            }
        }

        "State" {
            return Get-ProbePayload -Process $process -WithCapture
        }

        "Diagnostics" {
            return Get-ProbePayload -Process $process -WithCapture:$IncludeCapture
        }

        "Probe" {
            return Get-ProbePayload -Process $process -WithCapture:$IncludeCapture
        }

        "Compare" {
            if ([string]::IsNullOrWhiteSpace($ReferencePath)) {
                throw "Compare requires -ReferencePath."
            }

            $window = if (-not [string]::IsNullOrWhiteSpace($WindowTitle) -or -not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
                Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $process.Id -PartialMatch:$PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            $resolvedCapture = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
            $resolvedReference = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReferencePath))
            $resolvedDiff = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $DiffOutputPath))
            Save-UiWindowScreenshot -Window $window -Path $resolvedCapture | Out-Null
            $comparison = Compare-UiImages -ReferencePath $resolvedReference -ActualPath $resolvedCapture -DiffPath $resolvedDiff -Tolerance $Tolerance -SampleStep $SampleStep
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Compare"
                Window = Get-UiElementSummary -Element $window
                Comparison = $comparison
            }
        }
    }
}

$traceStartedAt = Get-Date
$traceStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$result = $null

try {
    $result = Invoke-UiAction
    $traceStopwatch.Stop()

    $payload = Get-TracePayloadFromResult -Result $result
    Write-UiTimelineEvent `
        -Action $Action `
        -Stage "completed" `
        -Success $true `
        -DurationMs $traceStopwatch.Elapsed.TotalMilliseconds `
        -WindowTitle $WindowTitle `
        -WindowAutomationId $WindowAutomationId `
        -Payload $payload

    Write-UiObject $result
}
catch {
    $traceStopwatch.Stop()
    Write-UiTimelineEvent `
        -Action $Action `
        -Stage "failed" `
        -Success $false `
        -DurationMs $traceStopwatch.Elapsed.TotalMilliseconds `
        -WindowTitle $WindowTitle `
        -WindowAutomationId $WindowAutomationId `
        -Payload @{
            Error = $_.Exception.Message
            Name = $Name
            Label = $Label
            Text = $Text
            AutomationId = $AutomationId
            StartedAt = $traceStartedAt.ToString("o")
        }
    throw
}
