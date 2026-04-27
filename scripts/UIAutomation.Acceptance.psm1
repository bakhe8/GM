Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$script:UiAutomationModulesRoot = Join-Path $PSScriptRoot "modules"

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

. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Core.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Windows.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Dialogs.ps1")

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

        Start-Sleep -Milliseconds 100
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

    $previousState = Get-UiShellStateSnapshot
    $button = Get-UiSidebarButton -MainWindow $MainWindow -Label $WorkspaceLabel
    Invoke-UiElement -Element $button
    $targetWorkspaceKey = Get-UiWorkspaceKeyForLabel -WorkspaceLabel $WorkspaceLabel
    if ([string]::IsNullOrWhiteSpace($targetWorkspaceKey)) {
        Start-Sleep -Milliseconds 200
        return
    }

    $ready = Wait-UiShellWorkspace `
        -WorkspaceKey $targetWorkspaceKey `
        -PreviousWorkspaceKey $(if ($null -ne $previousState) { [string]$previousState.CurrentWorkspaceKey } else { "" }) `
        -PreviousTimestamp $(if ($null -ne $previousState) { [string]$previousState.Timestamp } else { "" }) `
        -TimeoutMilliseconds 2500

    if (-not $ready) {
        Start-Sleep -Milliseconds 200
    }
}

function Get-UiWorkspaceKeyForLabel {
    param(
        [Parameter(Mandatory)]
        [string]$WorkspaceLabel
    )

    switch ($WorkspaceLabel.Trim()) {
        "لوحة التحكم" { return "Dashboard" }
        "الضمانات" { return "Guarantees" }
        "الطلبات" { return "Requests" }
        "البنوك" { return "Banks" }
        "التقارير" { return "Reports" }
        "التنبيهات" { return "Notifications" }
        "الإعدادات" { return "Settings" }
        default { return "" }
    }
}

function Wait-UiShellWorkspace {
    param(
        [Parameter(Mandatory)]
        [string]$WorkspaceKey,
        [string]$PreviousWorkspaceKey = "",
        [string]$PreviousTimestamp = "",
        [int]$TimeoutMilliseconds = 2500
    )

    $deadline = (Get-Date).AddMilliseconds($TimeoutMilliseconds)
    while ((Get-Date) -lt $deadline) {
        $state = Get-UiShellStateSnapshot
        if ($null -ne $state -and [string]$state.CurrentWorkspaceKey -eq $WorkspaceKey) {
            if ([string]::IsNullOrWhiteSpace($PreviousTimestamp)) {
                return $true
            }

            if ([string]$state.Timestamp -ne $PreviousTimestamp -or [string]$PreviousWorkspaceKey -ne $WorkspaceKey) {
                return $true
            }
        }

        Start-Sleep -Milliseconds 75
    }

    return $false
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
