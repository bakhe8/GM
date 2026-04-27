Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ("GuaranteeManager.UiAcceptance.NativeMethods" -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GuaranteeManager.UiAcceptance
{
    public static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
"@
}

function Get-UiAcceptanceRepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Ensure-UiGraphicsAssemblies {
    if (-not ("System.Drawing.Bitmap" -as [type])) {
        Add-Type -AssemblyName System.Drawing
    }
}

function Ensure-UiWindowsFormsAssembly {
    if (-not ("System.Windows.Forms.SendKeys" -as [type])) {
        Add-Type -AssemblyName System.Windows.Forms
    }
}

function Get-UiPrimaryScreenBounds {
    Ensure-UiWindowsFormsAssembly
    $screenBounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    return [pscustomobject]@{
        Left = [int]$screenBounds.Left
        Top = [int]$screenBounds.Top
        Width = [int]$screenBounds.Width
        Height = [int]$screenBounds.Height
    }
}

function Send-UiSendKeys {
    param(
        [Parameter(Mandatory)]
        [string]$KeysText
    )

    Ensure-UiWindowsFormsAssembly
    [System.Windows.Forms.SendKeys]::SendWait($KeysText)
}

function Get-UiStorageRoot {
    $override = [Environment]::GetEnvironmentVariable("GUARANTEE_MANAGER_DATAROOT")
    if (-not [string]::IsNullOrWhiteSpace($override)) {
        return [System.IO.Path]::GetFullPath($override)
    }

    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrWhiteSpace($localAppData)) {
        return Get-UiAcceptanceRepoRoot
    }

    return Join-Path $localAppData "GuaranteeManager"
}

function Get-UiDiagnosticsPaths {
    $storageRoot = Get-UiStorageRoot
    $logsRoot = Join-Path $storageRoot "Logs"
    return [pscustomobject]@{
        StorageRoot = $storageRoot
        LogsRoot = $logsRoot
        ShellStatePath = Join-Path $logsRoot "ui-shell-state.json"
        EventLogPath = Join-Path $logsRoot "ui-events.jsonl"
    }
}

function Get-UiAcceptanceArtifactsRoot {
    $repoRoot = Get-UiAcceptanceRepoRoot
    return Join-Path $repoRoot "Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest"
}

function Get-UiTimelinePath {
    return Join-Path (Get-UiAcceptanceArtifactsRoot) "interactive-timeline.jsonl"
}

function Get-UiCalibrationPath {
    return Join-Path $PSScriptRoot "ui_human_calibration.json"
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
    $acceptableMs = 700
    $slowMs = 1500
    if ($null -ne $calibration -and $null -ne $calibration.latencyThresholdsMs) {
        if ($null -ne $calibration.latencyThresholdsMs.acceptable) {
            $acceptableMs = [int]$calibration.latencyThresholdsMs.acceptable
        }

        if ($null -ne $calibration.latencyThresholdsMs.slow) {
            $slowMs = [int]$calibration.latencyThresholdsMs.slow
        }
    }

    $lastAction = $timeline | Select-Object -Last 1
    $grouped = @($timeline | Group-Object Action | ForEach-Object {
        $durations = @($_.Group | ForEach-Object { [double]$_.DurationMs })
        [pscustomobject]@{
            Action = $_.Name
            Count = $durations.Count
            AverageMs = if ($durations.Count -gt 0) { [math]::Round((($durations | Measure-Object -Average).Average), 2) } else { 0 }
            MaxMs = if ($durations.Count -gt 0) { [math]::Round((($durations | Measure-Object -Maximum).Maximum), 2) } else { 0 }
            LastMs = if ($durations.Count -gt 0) { [math]::Round($durations[-1], 2) } else { 0 }
        }
    })

    return [pscustomobject]@{
        Thresholds = [pscustomobject]@{
            AcceptableMs = $acceptableMs
            SlowMs = $slowMs
        }
        TotalActions = $timeline.Count
        SlowActionCount = @($timeline | Where-Object { [double]$_.DurationMs -ge $slowMs }).Count
        LastAction = if ($null -ne $lastAction) {
            [pscustomobject]@{
                Action = $lastAction.Action
                DurationMs = [math]::Round([double]$lastAction.DurationMs, 2)
                Success = [bool]$lastAction.Success
                IsSlow = ([double]$lastAction.DurationMs -ge $slowMs)
                IsAboveAcceptable = ([double]$lastAction.DurationMs -ge $acceptableMs)
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

function Get-UiProcessWindowDetails {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $candidates = @(Get-Process $ProcessName -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 0) {
        return @()
    }

    return @(foreach ($process in $candidates) {
        $windows = @(Get-UiTopLevelWindows -ProcessId $process.Id)
        if ($windows.Count -eq 0) {
            continue
        }

        $largest = $windows |
            Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
            Select-Object -First 1

        $bounds = Get-UiBounds -Element $largest
        [pscustomobject]@{
            Process = $process
            LargestWindow = $largest
            Area = $bounds.Width * $bounds.Height
            ClassName = $largest.Current.ClassName
            Title = $largest.Current.Name
        }
    })
}

function Get-UiProcess {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $ranked = @(Get-UiProcessWindowDetails -ProcessName $ProcessName)
    if ($ranked.Count -eq 0) {
        return $null
    }

    $preferred = $ranked |
        Sort-Object @{ Expression = { if ($_.ClassName -eq "Window") { 0 } else { 1 } } }, @{ Expression = { -$_.Area } }, @{ Expression = { -$_.Process.StartTime.Ticks } } |
        Select-Object -First 1

    return $preferred.Process
}

function Remove-UiTransientProcesses {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $ranked = @(Get-UiProcessWindowDetails -ProcessName $ProcessName)
    if ($ranked.Count -le 1) {
        return
    }

    $preferred = $ranked |
        Sort-Object @{ Expression = { if ($_.ClassName -eq "Window") { 0 } else { 1 } } }, @{ Expression = { -$_.Area } }, @{ Expression = { -$_.Process.StartTime.Ticks } } |
        Select-Object -First 1

    foreach ($item in $ranked) {
        if ($item.Process.Id -eq $preferred.Process.Id) {
            continue
        }

        if ($item.ClassName -eq "#32770" -or $item.Area -lt 150000 -or $item.ClassName -ne "Window") {
            try {
                Stop-Process -Id $item.Process.Id -Force -ErrorAction Stop
            }
            catch {
            }
        }
    }
}

function Get-UiDescendants {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root
    )

    $collection = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    for ($index = 0; $index -lt $collection.Count; $index++) {
        [void]$items.Add($collection.Item($index))
    }

    return $items
}

function Get-UiTopLevelWindows {
    param(
        [int]$ProcessId = 0
    )

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $callback = [GuaranteeManager.UiAcceptance.NativeMethods+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        if (-not [GuaranteeManager.UiAcceptance.NativeMethods]::IsWindowVisible($hWnd)) {
            return $true
        }

        [uint32]$windowProcessId = 0
        [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId) | Out-Null
        if ($ProcessId -ne 0 -and $windowProcessId -ne $ProcessId) {
            return $true
        }

        try {
            $element = [System.Windows.Automation.AutomationElement]::FromHandle($hWnd)
            if ($element -ne $null) {
                [void]$items.Add($element)
            }
        }
        catch {
        }

        return $true
    }

    [GuaranteeManager.UiAcceptance.NativeMethods]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

    return $items
}

function Get-UiForegroundWindowElement {
    $foregroundHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetForegroundWindow()
    if ($foregroundHandle -eq [IntPtr]::Zero) {
        return $null
    }

    try {
        return [System.Windows.Automation.AutomationElement]::FromHandle($foregroundHandle)
    }
    catch {
        return $null
    }
}

function Test-UiBoundsOverlap {
    param(
        [Parameter(Mandatory)]
        $BoundsA,
        [Parameter(Mandatory)]
        $BoundsB,
        [double]$MinimumCoverage = 0.12
    )

    if ($BoundsA.Width -le 0 -or $BoundsA.Height -le 0 -or $BoundsB.Width -le 0 -or $BoundsB.Height -le 0) {
        return $false
    }

    $left = [Math]::Max($BoundsA.Left, $BoundsB.Left)
    $top = [Math]::Max($BoundsA.Top, $BoundsB.Top)
    $right = [Math]::Min($BoundsA.Right, $BoundsB.Right)
    $bottom = [Math]::Min($BoundsA.Bottom, $BoundsB.Bottom)
    $width = $right - $left
    $height = $bottom - $top
    if ($width -le 0 -or $height -le 0) {
        return $false
    }

    $intersectionArea = [double]($width * $height)
    $smallerArea = [double]([Math]::Min(($BoundsA.Width * $BoundsA.Height), ($BoundsB.Width * $BoundsB.Height)))
    if ($smallerArea -le 0) {
        return $false
    }

    return ($intersectionArea / $smallerArea) -ge $MinimumCoverage
}

function Test-UiExternalWindowRelatedToProcess {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [int]$ProcessId = 0
    )

    if ($null -eq $Window -or $ProcessId -eq 0) {
        return $false
    }

    try {
        if ($Window.Current.ProcessId -eq $ProcessId) {
            return $false
        }
    }
    catch {
        return $false
    }

    $windowHandle = [IntPtr]::Zero
    $windowName = ""
    $targetProcess = $null
    try {
        $windowHandle = [IntPtr][int64]$Window.Current.NativeWindowHandle
        $windowName = $Window.Current.Name
    }
    catch {
        return $false
    }

    try {
        $targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
    }
    catch {
    }

    if ($windowHandle -ne [IntPtr]::Zero -and $null -ne $targetProcess -and $targetProcess.MainWindowHandle -ne 0) {
        $ownerHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindow($windowHandle, 4)
        if ($ownerHandle -ne [IntPtr]::Zero -and $ownerHandle -eq [IntPtr][int64]$targetProcess.MainWindowHandle) {
            return $true
        }
    }

    if ($null -ne $targetProcess -and -not [string]::IsNullOrWhiteSpace($windowName)) {
        if ($windowName.IndexOf($targetProcess.ProcessName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }

        if (-not [string]::IsNullOrWhiteSpace($targetProcess.MainWindowTitle) -and
            $windowName.IndexOf($targetProcess.MainWindowTitle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-UiRelatedTopLevelWindows {
    param(
        [int]$ProcessId = 0
    )

    if ($ProcessId -eq 0) {
        return @(Get-UiTopLevelWindows -ProcessId 0)
    }

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $seen = New-Object System.Collections.Generic.HashSet[int]
    $targetProcess = $null
    $targetHandle = [IntPtr]::Zero
    $targetProcessName = ""
    $targetTitle = ""

    try {
        $targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
        $targetHandle = [IntPtr][int64]$targetProcess.MainWindowHandle
        $targetProcessName = $targetProcess.ProcessName
        $targetTitle = $targetProcess.MainWindowTitle
    }
    catch {
    }

    foreach ($window in @(Get-UiTopLevelWindows -ProcessId $ProcessId)) {
        try {
            if ($window.Current.NativeWindowHandle -ne 0 -and $seen.Add([int]$window.Current.NativeWindowHandle)) {
                [void]$items.Add($window)
            }
        }
        catch {
        }
    }

    foreach ($window in @(Get-UiTopLevelWindows -ProcessId 0)) {
        try {
            if ($window.Current.ProcessId -eq $ProcessId) {
                continue
            }

            $windowHandle = [IntPtr][int64]$window.Current.NativeWindowHandle
            if ($windowHandle -eq [IntPtr]::Zero) {
                continue
            }

            $windowName = $window.Current.Name
            $relatedByOwner = $false
            if ($targetHandle -ne [IntPtr]::Zero) {
                $ownerHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindow($windowHandle, 4)
                $relatedByOwner = ($ownerHandle -ne [IntPtr]::Zero -and $ownerHandle -eq $targetHandle)
            }

            $relatedByName = -not [string]::IsNullOrWhiteSpace($windowName) -and (
                ((-not [string]::IsNullOrWhiteSpace($targetProcessName)) -and $windowName.IndexOf($targetProcessName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) -or
                ((-not [string]::IsNullOrWhiteSpace($targetTitle)) -and $windowName.IndexOf($targetTitle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
            )

            if (-not ($relatedByOwner -or $relatedByName)) {
                continue
            }

            if ($seen.Add([int]$window.Current.NativeWindowHandle)) {
                [void]$items.Add($window)
            }
        }
        catch {
        }
    }

    return $items
}

function Get-UiBounds {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $emptyBounds = {
        return [pscustomobject]@{
            Left = 0
            Top = 0
            Right = 0
            Bottom = 0
            Width = 0
            Height = 0
            CenterX = 0
            CenterY = 0
        }
    }

    try {
        $rect = $Element.Current.BoundingRectangle
    }
    catch {
        return & $emptyBounds
    }

    $toInt = {
        param([double]$value)
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) {
            return 0
        }

        return [int][Math]::Round($value)
    }

    $propertyNames = @()
    try {
        $propertyNames = @($rect.GetType().GetProperties() | ForEach-Object { $_.Name })
    }
    catch {
        return & $emptyBounds
    }

    $leftValue = if ($propertyNames -contains "Left") { [double]$rect.Left } elseif ($propertyNames -contains "X") { [double]$rect.X } else { 0.0 }
    $topValue = if ($propertyNames -contains "Top") { [double]$rect.Top } elseif ($propertyNames -contains "Y") { [double]$rect.Y } else { 0.0 }
    $widthValue = if ($propertyNames -contains "Width") { [double]$rect.Width } else { 0.0 }
    $heightValue = if ($propertyNames -contains "Height") { [double]$rect.Height } else { 0.0 }
    $rightValue = if ($propertyNames -contains "Right") { [double]$rect.Right } else { $leftValue + $widthValue }
    $bottomValue = if ($propertyNames -contains "Bottom") { [double]$rect.Bottom } else { $topValue + $heightValue }

    return [pscustomobject]@{
        Left   = & $toInt $leftValue
        Top    = & $toInt $topValue
        Right  = & $toInt $rightValue
        Bottom = & $toInt $bottomValue
        Width  = & $toInt $widthValue
        Height = & $toInt $heightValue
        CenterX = & $toInt ($leftValue + ($widthValue / 2.0))
        CenterY = & $toInt ($topValue + ($heightValue / 2.0))
    }
}

function Get-UiElementSummary {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $bounds = Get-UiBounds -Element $Element
    $current = $null
    try {
        $current = $Element.Current
    }
    catch {
    }

    $controlTypeName = $null
    if ($null -ne $current) {
        try {
            if ($null -ne $current.ControlType) {
                $controlTypeName = $current.ControlType.ProgrammaticName
            }
        }
        catch {
        }
    }

    return [pscustomobject]@{
        Name = if ($null -ne $current) { try { $current.Name } catch { $null } } else { $null }
        AutomationId = if ($null -ne $current) { try { $current.AutomationId } catch { $null } } else { $null }
        ControlType = $controlTypeName
        ClassName = if ($null -ne $current) { try { $current.ClassName } catch { $null } } else { $null }
        HelpText = if ($null -ne $current) { try { $current.HelpText } catch { $null } } else { $null }
        ItemStatus = if ($null -ne $current) { try { $current.ItemStatus } catch { $null } } else { $null }
        ProcessId = if ($null -ne $current) { try { $current.ProcessId } catch { 0 } } else { 0 }
        NativeWindowHandle = if ($null -ne $current) { try { $current.NativeWindowHandle } catch { 0 } } else { 0 }
        IsOffscreen = if ($null -ne $current) { try { [bool]$current.IsOffscreen } catch { $false } } else { $false }
        Bounds = $bounds
    }
}

function Test-UiSummaryVisible {
    param(
        [Parameter(Mandatory)]
        $Summary,
        $ViewportBounds = $null
    )

    if ($null -eq $Summary) {
        return $false
    }

    if ($Summary.IsOffscreen) {
        return $false
    }

    if ($Summary.Bounds.Width -le 1 -or $Summary.Bounds.Height -le 1) {
        return $false
    }

    if ($null -eq $ViewportBounds) {
        return $true
    }

    return Test-UiBoundsOverlap -BoundsA $Summary.Bounds -BoundsB $ViewportBounds -MinimumCoverage 0.05
}

function Get-UiElementRuntimeKey {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    try {
        $runtimeId = $Element.GetRuntimeId()
        if ($null -ne $runtimeId -and $runtimeId.Length -gt 0) {
            return ($runtimeId | ForEach-Object { [string]$_ }) -join "-"
        }
    }
    catch {
    }

    $summary = Get-UiElementSummary -Element $Element
    return "{0}|{1}|{2}|{3}" -f $summary.ProcessId, $summary.AutomationId, $summary.Name, $summary.Bounds.CenterX
}

function New-UiSearchCondition {
    param(
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType
    )

    $conditions = New-Object System.Collections.Generic.List[System.Windows.Automation.Condition]

    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name)
        [void]$conditions.Add($condition)
    }

    if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            $AutomationId)
        [void]$conditions.Add($condition)
    }

    if ($null -ne $ControlType) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            $ControlType)
        [void]$conditions.Add($condition)
    }

    if ($conditions.Count -eq 0) {
        return [System.Windows.Automation.Condition]::TrueCondition
    }

    if ($conditions.Count -eq 1) {
        return $conditions[0]
    }

    return New-Object System.Windows.Automation.AndCondition($conditions.ToArray())
}

function Resolve-UiControlType {
    param(
        [string]$ProgrammaticName
    )

    if ([string]::IsNullOrWhiteSpace($ProgrammaticName)) {
        return $null
    }

    $fieldName = $ProgrammaticName.Split('.')[-1]
    $field = [System.Windows.Automation.ControlType].GetField(
        $fieldName,
        [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)

    if ($null -eq $field) {
        return $null
    }

    return $field.GetValue($null)
}

function Find-UiElementsFast {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$MaxResults = 50
    )

    $condition = New-UiSearchCondition -Name $Name -AutomationId $AutomationId -ControlType $ControlType
    $collection = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    for ($index = 0; $index -lt $collection.Count -and $index -lt $MaxResults; $index++) {
        [void]$items.Add($collection.Item($index))
    }

    return $items
}

function Find-UiProcessElements {
    param(
        [int]$ProcessId = 0,
        [string]$Name,
        [string]$AutomationId,
        [string]$ControlType,
        [switch]$PartialMatch,
        [int]$MaxResults = 50
    )

    $resolvedControlType = Resolve-UiControlType -ProgrammaticName $ControlType
    $results = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $seen = New-Object System.Collections.Generic.HashSet[string]

    $roots = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    foreach ($root in @(Get-UiTopLevelWindows -ProcessId $ProcessId)) {
        [void]$roots.Add($root)
    }
    [void]$roots.Add([System.Windows.Automation.AutomationElement]::RootElement)

    foreach ($root in $roots) {
        if ($results.Count -ge $MaxResults) {
            break
        }

        $candidates = if (-not $PartialMatch -and -not [string]::IsNullOrWhiteSpace($ControlType) -and $null -ne $resolvedControlType) {
            @(Find-UiElementsFast -Root $root -Name $Name -AutomationId $AutomationId -ControlType $resolvedControlType -MaxResults $MaxResults)
        }
        elseif (-not $PartialMatch -and ([string]::IsNullOrWhiteSpace($ControlType) -or $null -eq $resolvedControlType)) {
            @(Find-UiElementsFast -Root $root -Name $Name -AutomationId $AutomationId -ControlType $null -MaxResults $MaxResults)
        }
        else {
            if ($root -eq [System.Windows.Automation.AutomationElement]::RootElement) {
                @()
            }
            else {
                @(Get-UiDescendants -Root $root | Where-Object {
                    ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
                    ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId) -and
                    ([string]::IsNullOrWhiteSpace($ControlType) -or $_.Current.ControlType.ProgrammaticName -eq $ControlType)
                } | Select-Object -First $MaxResults)
            }
        }

        foreach ($candidate in $candidates) {
            try {
                if ($ProcessId -ne 0 -and $candidate.Current.ProcessId -ne $ProcessId) {
                    continue
                }
            }
            catch {
                continue
            }

            $key = Get-UiElementRuntimeKey -Element $candidate
            if ($seen.Add($key)) {
                [void]$results.Add($candidate)
            }

            if ($results.Count -ge $MaxResults) {
                break
            }
        }
    }

    return $results
}

function Test-UiNameMatch {
    param(
        [string]$Candidate,
        [string]$Expected,
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $false
    }

    if ($PartialMatch) {
        return $Candidate.IndexOf($Expected, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    }

    return [string]::Equals($Candidate.Trim(), $Expected.Trim(), [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-UiWindowMatch {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Title,
        [string]$AutomationId,
        [switch]$PartialMatch
    )

    $matchesTitle = [string]::IsNullOrWhiteSpace($Title) -or
        (Test-UiNameMatch -Candidate $Window.Current.Name -Expected $Title -PartialMatch:$PartialMatch)
    $matchesAutomationId = [string]::IsNullOrWhiteSpace($AutomationId) -or
        [string]::Equals($Window.Current.AutomationId, $AutomationId, [System.StringComparison]::OrdinalIgnoreCase)

    return $matchesTitle -and $matchesAutomationId
}

function Resolve-UiWindow {
    param(
        [string]$Title,
        [string]$AutomationId,
        [int]$ProcessId = 0,
        [switch]$PartialMatch
    )

    if (-not [string]::IsNullOrWhiteSpace($Title) -or -not [string]::IsNullOrWhiteSpace($AutomationId)) {
        return Wait-UiWindow -Title $Title -AutomationId $AutomationId -ProcessId $ProcessId -PartialMatch:$PartialMatch
    }

    $process = if ($ProcessId -ne 0) {
        Get-Process -Id $ProcessId -ErrorAction Stop
    }
    else {
        Get-UiProcess
    }

    if ($null -eq $process -or $process.MainWindowHandle -eq 0) {
        $windows = @(
            if ($null -ne $process) {
                Get-UiTopLevelWindows -ProcessId $process.Id
            }
        )
        if ($windows.Count -eq 0) {
            throw "Could not resolve a main application window."
        }

        return $windows |
            Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
            Select-Object -First 1
    }

    $mainWindow = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr][int64]$process.MainWindowHandle)
    $mainBounds = Get-UiBounds -Element $mainWindow
    if ($mainBounds.Width -lt 400 -or $mainBounds.Height -lt 300) {
        $windows = @(Get-UiTopLevelWindows -ProcessId $process.Id)
        if ($windows.Count -gt 0) {
            return $windows |
                Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
                Select-Object -First 1
        }
    }

    return $mainWindow
}

function Get-UiClickableAncestor {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $Element
    while ($current -ne $null) {
        if ($current.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) {
            return $current
        }

        if ($current.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]([object]$null))) {
            return $current
        }

        $current = $walker.GetParent($current)
    }

    return $Element
}

function Get-UiTopLevelAncestor {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $Element
    $last = $Element
    while ($current -ne $null) {
        $last = $current
        $parent = $walker.GetParent($current)
        if ($parent -eq $null) {
            break
        }

        $current = $parent
    }

    return $last
}

function Show-UiWindow {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window
    )

    $handle = [IntPtr][int64]$Window.Current.NativeWindowHandle
    [GuaranteeManager.UiAcceptance.NativeMethods]::ShowWindowAsync($handle, 9) | Out-Null
    [GuaranteeManager.UiAcceptance.NativeMethods]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 200
}

function Send-UiVirtualKey {
    param(
        [Parameter(Mandatory)]
        [byte]$VirtualKey
    )

    [GuaranteeManager.UiAcceptance.NativeMethods]::keybd_event($VirtualKey, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [GuaranteeManager.UiAcceptance.NativeMethods]::keybd_event($VirtualKey, 0, 0x0002, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
}

function Wait-UiWindow {
    param(
        [string]$Title,
        [string]$AutomationId,
        [int]$ProcessId = 0,
        [int]$TimeoutSeconds = 15,
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($Title) -and [string]::IsNullOrWhiteSpace($AutomationId)) {
        throw "Wait-UiWindow requires either Title or AutomationId."
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $matches = @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId | Where-Object {
            ($ProcessId -eq 0 -or $_.Current.ProcessId -eq $ProcessId -or (Test-UiExternalWindowRelatedToProcess -Window $_ -ProcessId $ProcessId)) -and
            (Test-UiWindowMatch -Window $_ -Title $Title -AutomationId $AutomationId -PartialMatch:$PartialMatch)
        })

        if ($matches.Count -eq 0 -and $ProcessId -ne 0) {
            $matches = @(Get-UiTopLevelWindows -ProcessId 0 | Where-Object {
                (Test-UiWindowMatch -Window $_ -Title $Title -AutomationId $AutomationId -PartialMatch:$PartialMatch)
            })
        }

        if ($matches.Count -gt 0) {
            return $matches[0]
        }

        Start-Sleep -Milliseconds 200
    }

    $label = if (-not [string]::IsNullOrWhiteSpace($AutomationId)) { "automation id '$AutomationId'" } else { "window '$Title'" }
    throw "Timed out waiting for $label."
}

function Wait-UiElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$TimeoutSeconds = 10,
        [switch]$PartialMatch
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $elements = @(
            if ($PartialMatch) {
                Get-UiDescendants -Root $Root | Where-Object {
                    ($null -eq $ControlType -or $_.Current.ControlType -eq $ControlType) -and
                    ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
                    ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId)
                }
            }
            else {
                Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $ControlType -MaxResults 1
            }
        )

        if ($elements.Count -gt 0) {
            return $elements[0]
        }

        Start-Sleep -Milliseconds 200
    }

    $label = if ($Name) { "name '$Name'" } elseif ($AutomationId) { "automation id '$AutomationId'" } else { "requested pattern" }
    throw "Timed out waiting for element with $label."
}

function Invoke-UiElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $fallbackBounds = Get-UiBounds -Element $Element

    try {
        $target = Get-UiClickableAncestor -Element $Element
        $window = Get-UiTopLevelAncestor -Element $target
        if ($null -ne $window -and $window.Current.NativeWindowHandle -ne 0) {
            Show-UiWindow -Window $window
        }

        $pattern = $null
        if ($target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke()
            Start-Sleep -Milliseconds 250
            return
        }

        $legacyPatternType = "System.Windows.Automation.LegacyIAccessiblePattern" -as [type]
        if ($null -ne $legacyPatternType) {
            if ($target.TryGetCurrentPattern($legacyPatternType::Pattern, [ref]$pattern)) {
                try {
                    $pattern.DoDefaultAction()
                    Start-Sleep -Milliseconds 250
                    return
                }
                catch {
                }
            }
        }

        $targetHandle = [IntPtr][int64]$target.Current.NativeWindowHandle
        if ($targetHandle -ne [IntPtr]::Zero -and $target.Current.ClassName -eq "Button") {
            try {
                $target.SetFocus()
                Send-UiVirtualKey -VirtualKey 0x20
                Start-Sleep -Milliseconds 150
            }
            catch {
            }

            [GuaranteeManager.UiAcceptance.NativeMethods]::SendMessage($targetHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            Start-Sleep -Milliseconds 250
            return
        }

        if ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $pattern.Select()
            Start-Sleep -Milliseconds 250
            return
        }

        $fallbackBounds = Get-UiBounds -Element $target
    }
    catch {
    }

    if ($fallbackBounds.Width -le 0 -or $fallbackBounds.Height -le 0) {
        throw "تعذر التفاعل مع العنصر المحدد لأن حدود النقر غير متاحة."
    }

    [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($fallbackBounds.CenterX, $fallbackBounds.CenterY) | Out-Null
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Set-UiElementValue {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)]
        [string]$Value
    )

    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        if (-not $pattern.Current.IsReadOnly) {
            $pattern.SetValue($Value)
            Start-Sleep -Milliseconds 200
            return
        }
    }

    throw "Element does not support writable ValuePattern."
}

function Get-UiEditNearLabel {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [Parameter(Mandatory)]
        [string]$Label
    )

    $labelElement = Wait-UiElement -Root $Window -Name $Label -ControlType ([System.Windows.Automation.ControlType]::Text) -TimeoutSeconds 5
    $labelBounds = Get-UiBounds -Element $labelElement
    $edits = Get-UiDescendants -Root $Window | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit }

    $scored = foreach ($edit in $edits) {
        $bounds = Get-UiBounds -Element $edit
        if ($bounds.Top -lt ($labelBounds.Top - 6)) {
            continue
        }

        [pscustomobject]@{
            Element = $edit
            VerticalDistance = [Math]::Abs($bounds.Top - $labelBounds.Bottom)
            HorizontalDistance = [Math]::Abs($bounds.CenterX - $labelBounds.CenterX)
        }
    }

    $candidate = $scored |
        Sort-Object VerticalDistance, HorizontalDistance |
        Select-Object -First 1

    if ($null -eq $candidate) {
        throw "Could not find an editable field near label '$Label'."
    }

    return $candidate.Element
}

function Get-UiSidebarButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)]
        [string]$Label
    )

    $windowBounds = Get-UiBounds -Element $MainWindow
    $descendants = Get-UiDescendants -Root $MainWindow | Where-Object {
        Test-UiNameMatch -Candidate $_.Current.Name -Expected $Label
    }

    $candidates = foreach ($item in $descendants) {
        $button = Get-UiClickableAncestor -Element $item
        $bounds = Get-UiBounds -Element $button
        if ($bounds.CenterX -lt ($windowBounds.Left + ($windowBounds.Width * 0.82))) {
            continue
        }

        [pscustomobject]@{
            Element = $button
            Bounds = $bounds
        }
    }

    $match = $candidates |
        Sort-Object { $_.Bounds.Top } |
        Select-Object -First 1

    if ($null -eq $match) {
        throw "Could not find sidebar button '$Label'."
    }

    return $match.Element
}

function Get-UiWindowsCatalog {
    param(
        [int]$ProcessId = 0,
        [switch]$IncludeRelatedForeground
    )

    $windows = if ($IncludeRelatedForeground) {
        @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId)
    }
    else {
        @(Get-UiTopLevelWindows -ProcessId $ProcessId)
    }

    return @($windows | ForEach-Object {
        Get-UiElementSummary -Element $_
    })
}

function Get-UiButtonByText {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [string]$Text,
        [int]$ProcessId = 0,
        [switch]$SearchProcessFallback,
        [switch]$PartialMatch
    )

    $buttons = if ($PartialMatch) {
        @(Get-UiDescendants -Root $Root | Where-Object {
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
            (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Text -PartialMatch:$PartialMatch) -and
            -not $_.Current.IsOffscreen
        })
    }
    else {
        @((Find-UiElementsFast -Root $Root -Name $Text -ControlType ([System.Windows.Automation.ControlType]::Button) -MaxResults 8) | Where-Object {
            try { -not $_.Current.IsOffscreen } catch { $true }
        })
    }

    $buttonMatches = @($buttons)
    if ($buttonMatches.Count -gt 0) {
        return $buttonMatches[0]
    }

    $namedDescendants = @(Get-UiDescendants -Root $Root | Where-Object {
        (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Text -PartialMatch:$PartialMatch) -and
        -not $_.Current.IsOffscreen
    })

    foreach ($item in $namedDescendants) {
        $clickable = Get-UiClickableAncestor -Element $item
        if ($clickable -ne $null) {
            return $clickable
        }
    }

    if ($SearchProcessFallback -and $ProcessId -ne 0) {
        $globalMatches = @(Find-UiProcessElements -ProcessId $ProcessId -Name $Text -PartialMatch:$PartialMatch -MaxResults 12)
        foreach ($item in $globalMatches) {
            $clickable = Get-UiClickableAncestor -Element $item
            if ($clickable -ne $null) {
                return $clickable
            }
        }
    }

    throw "Could not find button '$Text'."
}

function Find-UiElements {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [int]$ProcessId = 0,
        [string]$Name,
        [string]$AutomationId,
        [string]$ControlType,
        [switch]$SearchProcessFallback,
        [switch]$PartialMatch,
        [int]$MaxResults = 50
    )

    $resolvedControlType = Resolve-UiControlType -ProgrammaticName $ControlType

    $elements = if (-not $PartialMatch -and -not [string]::IsNullOrWhiteSpace($ControlType) -and $null -ne $resolvedControlType) {
        @(Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $resolvedControlType -MaxResults $MaxResults)
    }
    elseif (-not $PartialMatch -and ([string]::IsNullOrWhiteSpace($ControlType) -or $null -eq $resolvedControlType)) {
        @(Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $null -MaxResults $MaxResults)
    }
    else {
        @(Get-UiDescendants -Root $Root | Where-Object {
            ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
            ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId) -and
            ([string]::IsNullOrWhiteSpace($ControlType) -or $_.Current.ControlType.ProgrammaticName -eq $ControlType)
        } | Select-Object -First $MaxResults)
    }

    $elements = @($elements)

    if ($elements.Count -eq 0 -and $SearchProcessFallback -and $ProcessId -ne 0) {
        $elements = @(Find-UiProcessElements -ProcessId $ProcessId -Name $Name -AutomationId $AutomationId -ControlType $ControlType -PartialMatch:$PartialMatch -MaxResults $MaxResults)
    }

    $summaries = @($elements | ForEach-Object {
        Get-UiElementSummary -Element $_
    })

    $viewportBounds = Get-UiBounds -Element $Root
    $visibleSummaries = @($summaries | Where-Object { Test-UiSummaryVisible -Summary $_ -ViewportBounds $viewportBounds })
    if ($visibleSummaries.Count -gt 0) {
        return $visibleSummaries
    }

    return $summaries
}

function Get-UiDialogActionButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Dialog,
        [string[]]$PreferredLabels
    )

    foreach ($label in $PreferredLabels) {
        $buttons = @(Find-UiElementsFast -Root $Dialog -Name $label -ControlType ([System.Windows.Automation.ControlType]::Button) -MaxResults 3)

        $buttonMatches = @($buttons)
        if ($buttonMatches.Count -gt 0) {
            return $buttonMatches[0]
        }

        $namedDescendants = @(Get-UiDescendants -Root $Dialog | Where-Object {
            Test-UiNameMatch -Candidate $_.Current.Name -Expected $label
        })

        foreach ($item in $namedDescendants) {
            $clickable = Get-UiClickableAncestor -Element $item
            if ($clickable -ne $null) {
                return $clickable
            }
        }
    }

    throw "Could not find any of the dialog buttons: $($PreferredLabels -join ', ')."
}

function Test-UiElementReady {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    try {
        if ($Element.Current.IsOffscreen) {
            return $false
        }

        if (-not $Element.Current.IsEnabled) {
            return $false
        }
    }
    catch {
        return $false
    }

    $bounds = Get-UiBounds -Element $Element
    return $bounds.Width -gt 1 -and $bounds.Height -gt 1
}

function Wait-UiElementReady {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element,
        [int]$TimeoutSeconds = 3
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-UiElementReady -Element $Element) {
            return $true
        }

        Start-Sleep -Milliseconds 100
    }

    return $false
}

function Test-UiWindowSummaryPresent {
    param(
        [Parameter(Mandatory)]
        $WindowSummary,
        [int]$ProcessId = 0
    )

    $windows = @(Get-UiWindowsCatalog -ProcessId $ProcessId -IncludeRelatedForeground)
    $matches = @($windows | Where-Object {
        if ($WindowSummary.NativeWindowHandle -ne 0) {
            $_.NativeWindowHandle -eq $WindowSummary.NativeWindowHandle
        }
        else {
            $sameAutomation = -not [string]::IsNullOrWhiteSpace($WindowSummary.AutomationId) -and $_.AutomationId -eq $WindowSummary.AutomationId
            $sameTitle = -not [string]::IsNullOrWhiteSpace($WindowSummary.Name) -and $_.Name -eq $WindowSummary.Name
            $sameAutomation -or $sameTitle
        }
    })

    return $matches.Count -gt 0
}

function Wait-UiWindowGone {
    param(
        [Parameter(Mandatory)]
        $WindowSummary,
        [int]$ProcessId = 0,
        [int]$TimeoutSeconds = 5
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-UiWindowSummaryPresent -WindowSummary $WindowSummary -ProcessId $ProcessId)) {
            return $true
        }

        Start-Sleep -Milliseconds 200
    }

    return $false
}

function Invoke-UiDialogActionButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Dialog,
        [Parameter(Mandatory)]
        [string[]]$PreferredLabels,
        [int]$ProcessId = 0,
        [int]$MaxAttempts = 4,
        [int]$CloseTimeoutSeconds = 3
    )

    $dialogSummary = Get-UiElementSummary -Element $Dialog
    $lastButtonSummary = $null
    $lastError = $null
    $strategies = @("invoke", "space", "enter", "mouse")

    for ($attempt = 0; $attempt -lt [Math]::Min($MaxAttempts, $strategies.Count); $attempt++) {
        if (-not (Test-UiWindowSummaryPresent -WindowSummary $dialogSummary -ProcessId $ProcessId)) {
            return [pscustomobject]@{
                Closed = $true
                Dialog = $dialogSummary
                Button = $lastButtonSummary
                Attempt = $attempt
                Strategy = "already-closed"
                Error = $null
            }
        }

        Show-UiWindow -Window $Dialog
        Start-Sleep -Milliseconds 250

        try {
            $button = Get-UiDialogActionButton -Dialog $Dialog -PreferredLabels $PreferredLabels
            $lastButtonSummary = Get-UiElementSummary -Element $button
            [void](Wait-UiElementReady -Element $button -TimeoutSeconds 2)

            try {
                $button.SetFocus()
                Start-Sleep -Milliseconds 150
            }
            catch {
            }

            switch ($strategies[$attempt]) {
                "invoke" {
                    Invoke-UiElement -Element $button
                }
                "space" {
                    Send-UiVirtualKey -VirtualKey 0x20
                }
                "enter" {
                    Send-UiVirtualKey -VirtualKey 0x0D
                }
                "mouse" {
                    $bounds = Get-UiBounds -Element $button
                    [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($bounds.CenterX, $bounds.CenterY) | Out-Null
                    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                    Start-Sleep -Milliseconds 300
                }
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        if (Wait-UiWindowGone -WindowSummary $dialogSummary -ProcessId $ProcessId -TimeoutSeconds $CloseTimeoutSeconds) {
            return [pscustomobject]@{
                Closed = $true
                Dialog = $dialogSummary
                Button = $lastButtonSummary
                Attempt = ($attempt + 1)
                Strategy = $strategies[$attempt]
                Error = $lastError
            }
        }

        Start-Sleep -Milliseconds 200
    }

    return [pscustomobject]@{
        Closed = $false
        Dialog = $dialogSummary
        Button = $lastButtonSummary
        Attempt = [Math]::Min($MaxAttempts, $strategies.Count)
        Strategy = ($strategies[([Math]::Min($MaxAttempts, $strategies.Count) - 1)])
        Error = $lastError
    }
}

function Get-UiActiveDialog {
    param(
        [int]$ProcessId = 0
    )

    $foregroundWindow = Get-UiForegroundWindowElement
    if ($null -ne $foregroundWindow) {
        $isSameProcess = $false
        $isRelatedExternal = $false
        try {
            $isSameProcess = ($ProcessId -eq 0 -or $foregroundWindow.Current.ProcessId -eq $ProcessId)
            if (-not $isSameProcess -and $ProcessId -ne 0) {
                $isRelatedExternal = Test-UiExternalWindowRelatedToProcess -Window $foregroundWindow -ProcessId $ProcessId
            }
        }
        catch {
        }

        try {
            if (($isSameProcess -or $isRelatedExternal) -and
                $foregroundWindow.Current.AutomationId -ne "Shell.MainWindow" -and
                -not [string]::IsNullOrWhiteSpace($foregroundWindow.Current.Name)) {
                return $foregroundWindow
            }
        }
        catch {
        }
    }

    $windows = @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId)
    if ($windows.Count -eq 0) {
        throw "No top-level windows were found for the target process."
    }

    $dialogs = @($windows | Where-Object {
        $_.Current.NativeWindowHandle -ne 0 -and
        $_.Current.AutomationId -ne "Shell.MainWindow" -and
        -not [string]::IsNullOrWhiteSpace($_.Current.Name)
    } | Sort-Object { (Get-UiBounds -Element $_).Width })

    if ($dialogs.Count -gt 0) {
        return $dialogs[0]
    }

    throw "No active dialog is currently open for the target process."
}

function Save-UiWindowScreenshot {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [Parameter(Mandatory)]
        [string]$Path
    )

    Ensure-UiGraphicsAssemblies

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $bounds = Get-UiBounds -Element $Window
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return $Path
}

function Save-UiDesktopScreenshot {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Ensure-UiGraphicsAssemblies
    Ensure-UiWindowsFormsAssembly

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $screenBounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $screenBounds.Width, $screenBounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($screenBounds.Left, $screenBounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return $Path
}

function New-UiContactSheet {
    param(
        [Parameter(Mandatory)]
        [string[]]$ImagePaths,
        [Parameter(Mandatory)]
        [string]$DestinationPath,
        [int]$Columns = 3
    )

    Ensure-UiGraphicsAssemblies

    $images = foreach ($path in $ImagePaths) {
        [pscustomobject]@{
            Path = $path
            Name = [System.IO.Path]::GetFileNameWithoutExtension($path)
            Bitmap = [System.Drawing.Image]::FromFile($path)
        }
    }

    try {
        $tileWidth = [int](($images | ForEach-Object { $_.Bitmap.Width } | Measure-Object -Maximum).Maximum)
        $tileHeight = [int](($images | ForEach-Object { $_.Bitmap.Height } | Measure-Object -Maximum).Maximum)
        $rows = [int][Math]::Ceiling($images.Count / [double]$Columns)
        $padding = 16
        $labelHeight = 28
        $sheetWidth = [int](($Columns * ($tileWidth + $padding)) + $padding)
        $sheetHeight = [int](($rows * ($tileHeight + $labelHeight + $padding)) + $padding)

        $sheet = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        $font = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Regular)
        $textBrush = [System.Drawing.Brushes]::Black
        try {
            $graphics.Clear([System.Drawing.Color]::White)
            for ($index = 0; $index -lt $images.Count; $index++) {
                $row = [int]($index / $Columns)
                $column = $index % $Columns
                $x = $padding + ($column * ($tileWidth + $padding))
                $y = $padding + ($row * ($tileHeight + $labelHeight + $padding))
                $graphics.DrawImage($images[$index].Bitmap, $x, $y, $images[$index].Bitmap.Width, $images[$index].Bitmap.Height)
                $graphics.DrawString($images[$index].Name, $font, $textBrush, $x, $y + $tileHeight + 4)
            }

            $directory = Split-Path -Parent $DestinationPath
            if (-not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Force -Path $directory | Out-Null
            }

            $sheet.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $font.Dispose()
            $graphics.Dispose()
            $sheet.Dispose()
        }
    }
    finally {
        foreach ($image in $images) {
            $image.Bitmap.Dispose()
        }
    }

    return $DestinationPath
}

function Compare-UiImages {
    param(
        [Parameter(Mandatory)]
        [string]$ReferencePath,
        [Parameter(Mandatory)]
        [string]$ActualPath,
        [string]$DiffPath,
        [int]$Tolerance = 18,
        [int]$SampleStep = 2
    )

    Ensure-UiGraphicsAssemblies

    if (-not (Test-Path -LiteralPath $ReferencePath)) {
        throw "Reference image was not found: $ReferencePath"
    }

    if (-not (Test-Path -LiteralPath $ActualPath)) {
        throw "Actual image was not found: $ActualPath"
    }

    $referenceImage = [System.Drawing.Image]::FromFile($ReferencePath)
    $actualImage = [System.Drawing.Image]::FromFile($ActualPath)
    $referenceBitmap = $null
    $actualBitmap = $null
    $resizedReference = $null
    $diffBitmap = $null

    try {
        $referenceBitmap = New-Object System.Drawing.Bitmap $referenceImage
        $actualBitmap = New-Object System.Drawing.Bitmap $actualImage

        if ($referenceBitmap.Width -ne $actualBitmap.Width -or $referenceBitmap.Height -ne $actualBitmap.Height) {
            $resizedReference = New-Object System.Drawing.Bitmap $actualBitmap.Width, $actualBitmap.Height
            $graphics = [System.Drawing.Graphics]::FromImage($resizedReference)
            try {
                $graphics.DrawImage($referenceBitmap, 0, 0, $actualBitmap.Width, $actualBitmap.Height)
            }
            finally {
                $graphics.Dispose()
            }

            $referenceBitmap.Dispose()
            $referenceBitmap = $resizedReference
            $resizedReference = $null
        }

        $width = $actualBitmap.Width
        $height = $actualBitmap.Height
        $step = [Math]::Max(1, $SampleStep)
        $sampledPixels = 0
        $changedPixels = 0
        $minX = $width
        $minY = $height
        $maxX = -1
        $maxY = -1

        if (-not [string]::IsNullOrWhiteSpace($DiffPath)) {
            $diffBitmap = New-Object System.Drawing.Bitmap $width, $height
            $graphics = [System.Drawing.Graphics]::FromImage($diffBitmap)
            try {
                $graphics.DrawImage($actualBitmap, 0, 0, $width, $height)
            }
            finally {
                $graphics.Dispose()
            }
        }

        for ($y = 0; $y -lt $height; $y += $step) {
            for ($x = 0; $x -lt $width; $x += $step) {
                $sampledPixels++
                $expected = $referenceBitmap.GetPixel($x, $y)
                $actual = $actualBitmap.GetPixel($x, $y)
                $delta = [Math]::Abs($expected.R - $actual.R) + [Math]::Abs($expected.G - $actual.G) + [Math]::Abs($expected.B - $actual.B)
                if ($delta -gt $Tolerance) {
                    $changedPixels++
                    if ($x -lt $minX) { $minX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -gt $maxY) { $maxY = $y }

                    if ($null -ne $diffBitmap) {
                        $markColor = [System.Drawing.Color]::FromArgb(220, 255, 59, 48)
                        for ($offsetY = 0; $offsetY -lt $step -and ($y + $offsetY) -lt $height; $offsetY++) {
                            for ($offsetX = 0; $offsetX -lt $step -and ($x + $offsetX) -lt $width; $offsetX++) {
                                $diffBitmap.SetPixel($x + $offsetX, $y + $offsetY, $markColor)
                            }
                        }
                    }
                }
            }
        }

        $differenceRatio = if ($sampledPixels -gt 0) { [math]::Round(($changedPixels / [double]$sampledPixels) * 100.0, 2) } else { 0 }
        if ($null -ne $diffBitmap -and -not [string]::IsNullOrWhiteSpace($DiffPath)) {
            $directory = Split-Path -Parent $DiffPath
            if (-not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Force -Path $directory | Out-Null
            }

            $diffBitmap.Save($DiffPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        return [pscustomobject]@{
            ReferencePath = [System.IO.Path]::GetFullPath($ReferencePath)
            ActualPath = [System.IO.Path]::GetFullPath($ActualPath)
            DiffPath = if (-not [string]::IsNullOrWhiteSpace($DiffPath) -and (Test-Path -LiteralPath $DiffPath)) { [System.IO.Path]::GetFullPath($DiffPath) } else { $null }
            Width = $width
            Height = $height
            SampleStep = $step
            Tolerance = $Tolerance
            SampledPixels = $sampledPixels
            ChangedPixels = $changedPixels
            DifferenceRatio = $differenceRatio
            DifferenceBounds = if ($changedPixels -gt 0) {
                [pscustomobject]@{
                    Left = $minX
                    Top = $minY
                    Right = $maxX
                    Bottom = $maxY
                    Width = ($maxX - $minX) + 1
                    Height = ($maxY - $minY) + 1
                }
            } else {
                $null
            }
        }
    }
    finally {
        if ($null -ne $diffBitmap) { $diffBitmap.Dispose() }
        if ($null -ne $referenceBitmap) { $referenceBitmap.Dispose() }
        if ($null -ne $actualBitmap) { $actualBitmap.Dispose() }
        if ($null -ne $resizedReference) { $resizedReference.Dispose() }
        $referenceImage.Dispose()
        $actualImage.Dispose()
    }
}

function Start-UiTargetApplication {
    param(
        [string]$RepoRoot = (Get-UiAcceptanceRepoRoot),
        [switch]$ReuseRunningSession
    )

    Remove-UiTransientProcesses
    $existing = Get-UiProcess

    if ($ReuseRunningSession -and $null -ne $existing) {
        return $existing
    }

    $exeCandidates = @(Get-ChildItem -Path (Join-Path $RepoRoot "bin") -Recurse -Filter "GuaranteeManager.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\Debug\*" } |
        Sort-Object LastWriteTime -Descending)

    if ($exeCandidates.Count -eq 0) {
        Push-Location $RepoRoot
        try {
            dotnet build .\my_work.sln | Out-Host
        }
        finally {
            Pop-Location
        }

        $exeCandidates = @(Get-ChildItem -Path (Join-Path $RepoRoot "bin") -Recurse -Filter "GuaranteeManager.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like "*\Debug\*" } |
            Sort-Object LastWriteTime -Descending)
    }

    if ($exeCandidates.Count -eq 0) {
        throw "Could not locate GuaranteeManager.exe under bin\\Debug."
    }

    $process = Start-Process -FilePath $exeCandidates[0].FullName -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) {
            return $process
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for GuaranteeManager main window."
}

function Invoke-UiSidebarNavigation {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)]
        [string]$WorkspaceLabel
    )

    $button = Get-UiSidebarButton -MainWindow $MainWindow -Label $WorkspaceLabel
    Invoke-UiElement -Element $button
    Start-Sleep -Milliseconds 500
}

Export-ModuleMember -Function `
    Get-UiAcceptanceRepoRoot, `
    Get-UiAcceptanceArtifactsRoot, `
    Get-UiStorageRoot, `
    Get-UiDiagnosticsPaths, `
    Get-UiTimelinePath, `
    Get-UiCalibrationPath, `
    Get-UiCalibrationProfile, `
    Get-UiTimelineEntries, `
    Get-UiPerformanceSummary, `
    Write-UiTimelineEvent, `
    Get-UiShellStateSnapshot, `
    Get-UiRecentEvents, `
    Get-UiProcess, `
    Wait-UiWindow, `
    Wait-UiElement, `
    Resolve-UiWindow, `
    Send-UiVirtualKey, `
    Send-UiSendKeys, `
    Get-UiEditNearLabel, `
    Get-UiSidebarButton, `
    Get-UiTopLevelWindows, `
    Get-UiWindowsCatalog, `
    Get-UiButtonByText, `
    Find-UiProcessElements, `
    Get-UiDialogActionButton, `
    Wait-UiElementReady, `
    Wait-UiWindowGone, `
    Invoke-UiDialogActionButton, `
    Get-UiBounds, `
    Get-UiDescendants, `
    Get-UiElementSummary, `
    Find-UiElements, `
    Get-UiActiveDialog, `
    Show-UiWindow, `
    Invoke-UiElement, `
    Set-UiElementValue, `
    Save-UiWindowScreenshot, `
    Save-UiDesktopScreenshot, `
    New-UiContactSheet, `
    Compare-UiImages, `
    Start-UiTargetApplication, `
    Invoke-UiSidebarNavigation
