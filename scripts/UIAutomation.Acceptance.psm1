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

. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Diagnostics.ps1")
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

$script:UiSupportedApiCatalog = @(
    [pscustomobject]@{
        Category = "Session"
        Description = "بدء جلسة التطبيق والوصول إلى الجذور والمسارات المرجعية."
        Commands = @(
            "Get-UiAcceptanceRepoRoot",
            "Get-UiAcceptanceArtifactsRoot",
            "Get-UiStorageRoot",
            "Start-UiTargetApplication",
            "Get-UiProcess")
    },
    [pscustomobject]@{
        Category = "Diagnostics"
        Description = "قراءة التشخيص، السجل الزمني، ومقاييس الأداء."
        Commands = @(
            "Get-UiDiagnosticsPaths",
            "Get-UiTimelinePath",
            "Get-UiCalibrationPath",
            "Get-UiCalibrationProfile",
            "Get-UiTimelineEntries",
            "Get-UiPerformanceSummary",
            "Write-UiTimelineEvent",
            "Get-UiShellStateSnapshot",
            "Get-UiRecentEvents")
    },
    [pscustomobject]@{
        Category = "Windows"
        Description = "اكتشاف النوافذ وإظهارها وانتظارها."
        Commands = @(
            "Get-UiTopLevelWindows",
            "Get-UiWindowsCatalog",
            "Resolve-UiWindow",
            "Show-UiWindow",
            "Wait-UiWindow")
    },
    [pscustomobject]@{
        Category = "Elements"
        Description = "البحث عن العناصر، قراءتها، والتفاعل معها مباشرة."
        Commands = @(
            "Wait-UiElement",
            "Find-UiElements",
            "Find-UiProcessElements",
            "Get-UiElementSummary",
            "Get-UiBounds",
            "Get-UiDescendants",
            "Invoke-UiElement",
            "Set-UiElementValue",
            "Get-UiEditNearLabel",
            "Get-UiButtonByText",
            "Get-UiSidebarButton")
    },
    [pscustomobject]@{
        Category = "Dialogs"
        Description = "اكتشاف الحوارات وأزرارها وحسمها بموثوقية."
        Commands = @(
            "Get-UiActiveDialog",
            "Get-UiDialogActionButton",
            "Wait-UiElementReady",
            "Wait-UiWindowGone",
            "Invoke-UiDialogActionButton")
    },
    [pscustomobject]@{
        Category = "InputAndNavigation"
        Description = "المفاتيح والتنقل السياقي داخل التطبيق."
        Commands = @(
            "Send-UiVirtualKey",
            "Send-UiSendKeys",
            "Invoke-UiSidebarNavigation")
    },
    [pscustomobject]@{
        Category = "Capture"
        Description = "اللقطات والمقارنة البصرية وتجميع الشاشات."
        Commands = @(
            "Save-UiWindowScreenshot",
            "Save-UiDesktopScreenshot",
            "New-UiContactSheet",
            "Compare-UiImages")
    }
)

function Get-UiSupportedApi {
    return @($script:UiSupportedApiCatalog | ForEach-Object {
        [pscustomobject]@{
            Category = $_.Category
            Description = $_.Description
            Commands = [object[]]@($_.Commands)
        }
    })
}

$script:UiSupportedFunctionNames = New-Object System.Collections.Generic.List[string]
[void]$script:UiSupportedFunctionNames.Add("Get-UiSupportedApi")
foreach ($category in $script:UiSupportedApiCatalog) {
    foreach ($commandName in @($category.Commands)) {
        if (-not $script:UiSupportedFunctionNames.Contains($commandName)) {
            [void]$script:UiSupportedFunctionNames.Add($commandName)
        }
    }
}

Export-ModuleMember -Function $script:UiSupportedFunctionNames
