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

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
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
        public static extern bool GetCursorPos(out POINT point);

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

function Ensure-UiWindowsFormsAssembly {
    if (-not ("System.Windows.Forms.SendKeys" -as [type])) {
        Add-Type -AssemblyName System.Windows.Forms
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

. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Core.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Mouse.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Windows.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Dialogs.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Capture.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Diagnostics.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Host.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Media.ps1")
. (Join-Path $script:UiAutomationModulesRoot "UiAutomation.Capabilities.ps1")

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
        [string]$WorkspaceLabel = "",
        [string]$SidebarAutomationId = "",
        [string]$WorkspaceKey = ""
    )

    $previousState = Get-UiShellStateSnapshot
    $button = if (-not [string]::IsNullOrWhiteSpace($SidebarAutomationId)) {
        Wait-UiElement -Root $MainWindow -AutomationId $SidebarAutomationId -ControlType $null -TimeoutSeconds 5
    }
    elseif (-not [string]::IsNullOrWhiteSpace($WorkspaceLabel)) {
        Get-UiSidebarButton -MainWindow $MainWindow -Label $WorkspaceLabel
    }
    else {
        throw "Invoke-UiSidebarNavigation requires either -WorkspaceLabel or -SidebarAutomationId."
    }

    Invoke-UiElement -Element $button
    $targetWorkspaceKey = if (-not [string]::IsNullOrWhiteSpace($WorkspaceKey)) {
        $WorkspaceKey
    }
    elseif (-not [string]::IsNullOrWhiteSpace($WorkspaceLabel)) {
        Get-UiWorkspaceKeyForLabel -WorkspaceLabel $WorkspaceLabel
    }
    elseif (-not [string]::IsNullOrWhiteSpace($SidebarAutomationId)) {
        Get-UiWorkspaceKeyForSidebarAutomationId -SidebarAutomationId $SidebarAutomationId
    }
    else {
        ""
    }

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

function Get-UiWorkspaceKeyForSidebarAutomationId {
    param(
        [Parameter(Mandatory)]
        [string]$SidebarAutomationId
    )

    switch ($SidebarAutomationId.Trim()) {
        "Shell.Sidebar.Dashboard" { return "Dashboard" }
        "Shell.Sidebar.Guarantees" { return "Guarantees" }
        "Shell.Sidebar.Requests" { return "Requests" }
        "Shell.Sidebar.Banks" { return "Banks" }
        "Shell.Sidebar.Reports" { return "Reports" }
        "Shell.Sidebar.Notifications" { return "Notifications" }
        "Shell.Sidebar.Settings" { return "Settings" }
        default { return "" }
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
            "Get-UiProcess",
            "Get-UiCapabilitySessionPath",
            "Get-UiCapabilitySessionState",
            "Start-UiCapabilitySession",
            "Stop-UiCapabilitySession")
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
        Category = "Mouse"
        Description = "التحكم الحر بالماوس عند الحاجة داخل نفس الاستكشاف."
        Commands = @(
            "Get-UiCursorPosition",
            "Move-UiMouse",
            "Invoke-UiMouseClick",
            "Invoke-UiMouseRightClick",
            "Invoke-UiMouseDoubleClick",
            "Invoke-UiMouseHover",
            "Invoke-UiMouseDrag",
            "Invoke-UiMouseScroll")
    },
    [pscustomobject]@{
        Category = "Capture"
        Description = "اللقطات والمقارنة البصرية وتجميع الشاشات."
        Commands = @(
            "Save-UiWindowScreenshot",
            "Save-UiDesktopScreenshot",
            "New-UiContactSheet",
            "Compare-UiImages")
    },
    [pscustomobject]@{
        Category = "Media"
        Description = "إدارة مزودي الفيديو والصوت عند الطلب، مع single-instance cleanup وscope attestation قابلة للقراءة."
        Commands = @(
            "Get-UiMediaSessionPath",
            "Get-UiMediaSessionState",
            "Get-UiMediaScopeView",
            "Get-UiMediaProviderCatalog",
            "Invoke-UiMediaBrokerSweep",
            "Start-UiVideoCaptureSidecar",
            "Stop-UiVideoCaptureSidecar",
            "Start-UiAudioCaptureSidecar",
            "Stop-UiAudioCaptureSidecar")
    },
    [pscustomobject]@{
        Category = "Capabilities"
        Description = "إدارة القدرات اللحظية الخفيفة وتفعيلها حسب الحاجة داخل نفس الاستكشاف."
        Commands = @(
            "Get-UiCapabilityDefinitions",
            "Get-UiCapabilityObservationPath",
            "Get-UiCapabilityObservationEntries",
            "Enable-UiCapability",
            "Disable-UiCapability",
            "Invoke-UiCapabilityBrokerSweep")
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
