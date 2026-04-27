param(
    [string]$OutputRoot = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$uiExplorePath = Join-Path $repoRoot "scripts\ui_explore.ps1"
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$summaryPath = Join-Path $resolvedOutputRoot "tooling-integration-summary.md"
$failureCapturePath = Join-Path $resolvedOutputRoot "tooling-integration-failure.png"
$relativeFailureCapturePath = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest\tooling-integration-failure.png"
if (Test-Path -LiteralPath $failureCapturePath) {
    Remove-Item -LiteralPath $failureCapturePath -Force
}
$results = New-Object System.Collections.Generic.List[object]
$suiteFailed = $false
$failureMessage = $null

function ConvertFrom-UiExploreOutput {
    param(
        [Parameter(Mandatory)]
        [object[]]$OutputLines
    )

    $text = (($OutputLines | ForEach-Object { "$_" }) -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

function Invoke-UiExploreJson {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Arguments
    )

    $invokeParameters = @{}
    foreach ($key in $Arguments.Keys) {
        $value = $Arguments[$key]
        if ($value -is [bool] -or $value -is [System.Management.Automation.SwitchParameter]) {
            $invokeParameters[$key] = [bool]$value
            continue
        }

        if ($null -eq $value) {
            continue
        }

        $stringValue = [string]$value
        if ([string]::IsNullOrWhiteSpace($stringValue)) {
            continue
        }

        $invokeParameters[$key] = $stringValue
    }

    $output = & $uiExplorePath @invokeParameters
    return ConvertFrom-UiExploreOutput -OutputLines @($output)
}

function Assert-RegressionCondition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,
        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Add-RegressionResult {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [bool]$Success,
        [double]$DurationMs = 0,
        [string]$Notes = ""
    )

    [void]$results.Add([pscustomobject]@{
        Name = $Name
        Success = $Success
        DurationMs = [math]::Round($DurationMs, 2)
        Notes = $Notes
    })
}

function Invoke-RegressionStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $result = & $ScriptBlock
        $stopwatch.Stop()
        Add-RegressionResult -Name $Name -Success $true -DurationMs $stopwatch.Elapsed.TotalMilliseconds
        return $result
    }
    catch {
        $stopwatch.Stop()
        Add-RegressionResult -Name $Name -Success $false -DurationMs $stopwatch.Elapsed.TotalMilliseconds -Notes $_.Exception.Message
        throw
    }
}

function Write-RegressionSummary {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# UI Tooling Integration Summary")
    [void]$lines.Add("")
    [void]$lines.Add("- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")")
    [void]$lines.Add("- ReuseRunningSession: $ReuseRunningSession")
    [void]$lines.Add("- Passed: $(@($results | Where-Object Success).Count)")
    [void]$lines.Add("- Failed: $(@($results | Where-Object { -not $_.Success }).Count)")
    [void]$lines.Add("")
    [void]$lines.Add("| Step | Result | Duration (ms) | Notes |")
    [void]$lines.Add("| --- | --- | ---: | --- |")

    foreach ($item in $results) {
        $resultLabel = if ($item.Success) { "PASS" } else { "FAIL" }
        $notes = if ([string]::IsNullOrWhiteSpace($item.Notes)) { "" } else { $item.Notes.Replace("|", "/") }
        [void]$lines.Add("| $($item.Name) | $resultLabel | $($item.DurationMs) | $notes |")
    }

    if ($suiteFailed -and -not [string]::IsNullOrWhiteSpace($failureMessage)) {
        [void]$lines.Add("")
        [void]$lines.Add("## Failure")
        [void]$lines.Add("")
        [void]$lines.Add($failureMessage)
        if (Test-Path -LiteralPath $failureCapturePath) {
            [void]$lines.Add("")
            [void]$lines.Add("- Failure capture: [tooling-integration-failure.png](./tooling-integration-failure.png)")
        }
    }

    [System.IO.File]::WriteAllLines($summaryPath, $lines)
}

try {
    if (-not $ReuseRunningSession) {
        Get-Process GuaranteeManager -ErrorAction SilentlyContinue | Stop-Process -Force
    }

    Invoke-RegressionStep -Name "probe-clean-launch" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $ReuseRunningSession
        }

        Assert-RegressionCondition ($payload.MainWindow.AutomationId -eq "Shell.MainWindow") "Probe did not return Shell.MainWindow."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Clean launch did not start from a single main window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-adaptive-snapshot" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        Assert-RegressionCondition ($null -ne $payload.CapabilityDefinitions) "HostState did not return capability definitions."
        Assert-RegressionCondition (@($payload.CapabilityDefinitions | Where-Object Name -eq "BurstCapture").Count -eq 1) "HostState did not include BurstCapture definition."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-burst-capture" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "BurstCapture"
            LeaseMilliseconds = 8000
            Reason = "integration-burst-check"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "BurstCapture") "CapabilityOn did not activate BurstCapture."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "BurstCapture").Count -eq 1) "BurstCapture was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "windows-burst-capture-evidence" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $captures = @($payload.CapabilityCaptures)
        Assert-RegressionCondition ($captures.Count -ge 4) "BurstCapture did not produce the expected multi-frame evidence."
        foreach ($capture in $captures) {
            Assert-RegressionCondition (Test-Path -LiteralPath $capture.Path) "A burst capture frame path did not exist on disk."
        }

        $hostState = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }
        $burstSummary = @($hostState.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "BurstCapture" -and $_.Kind -eq "burst-sequence"
        } | Select-Object -First 1)
        Assert-RegressionCondition ($burstSummary.Count -eq 1) "HostState did not record a burst-sequence observation."
        Assert-RegressionCondition ([int]$burstSummary[0].Payload.FrameCount -ge 4) "burst-sequence observation reported too few frames."
        Assert-RegressionCondition (-not [string]::IsNullOrWhiteSpace([string]$burstSummary[0].Payload.ContactSheetPath)) "burst-sequence observation did not include a contact sheet path."
        Assert-RegressionCondition (Test-Path -LiteralPath ([string]$burstSummary[0].Payload.ContactSheetPath)) "Burst contact sheet was not created on disk."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-burst-capture" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "BurstCapture"
            Reason = "integration-burst-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "BurstCapture").Count -eq 0) "BurstCapture remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "enable-mouse-trace" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "MouseTrace"
            LeaseMilliseconds = 8000
            Reason = "integration-mouse-trace"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Capability.Name -eq "MouseTrace") "CapabilityOn did not activate MouseTrace."
        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "MouseTrace").Count -eq 1) "MouseTrace was not present in the active session state."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-move-settings-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseMove"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Settings"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Shell.Sidebar.Settings") "MouseMove did not resolve the Settings sidebar target."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "hoststate-mouse-trace-after-move" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
        }

        $trace = @($payload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "MouseTrace" -and $_.Kind -eq "mouse-trace" -and $_.Payload.Action -eq "MouseMove"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($trace.Count -eq 1) "MouseTrace did not record a mouse-trace observation after MouseMove."
        Assert-RegressionCondition ([int]$trace[0].Payload.SampleCount -ge 2) "MouseTrace observation reported too few samples."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-click-settings-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseClick"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Settings"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Button -eq "Left") "MouseClick did not report a left-button action."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-settings-after-mouse-click" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Settings") "MouseClick on Settings sidebar did not switch to Settings workspace."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-hover-guarantees-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 150
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Shell.Sidebar.Guarantees") "MouseHover did not resolve the Guarantees sidebar target."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-doubleclick-guarantees-sidebar" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseDoubleClick"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.ClickCount -eq 2) "MouseDoubleClick did not report a double-click."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-guarantees-after-mouse-doubleclick" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Guarantees") "MouseDoubleClick on Guarantees sidebar did not return to Guarantees workspace."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-scroll-main-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseScroll"
            WindowAutomationId = "Shell.MainWindow"
            ScrollDelta = -120
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.ScrollDelta -eq -120) "MouseScroll did not echo the expected delta."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-drag-global-search" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseDrag"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.GlobalSearchBox"
            DeltaX = 24
            DeltaY = 0
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([int]$payload.DeltaX -eq 24) "MouseDrag did not report the expected horizontal delta."
        Assert-RegressionCondition ([int]$payload.EndPosition.X -gt [int]$payload.StartPosition.X) "MouseDrag did not move the cursor to the right."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "mouse-rightclick-main-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "MouseRightClick"
            WindowAutomationId = "Shell.MainWindow"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Button -eq "Right") "MouseRightClick did not report a right-button action."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-clean-after-mouse-actions" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([string]::IsNullOrWhiteSpace([string]$payload.Health.ActiveDialogTitle)) "A dialog remained open after mouse actions."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Mouse actions left the session with more than one visible window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "disable-mouse-trace" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "MouseTrace"
            Reason = "integration-mouse-trace-complete"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.CapabilitySession.ActiveCapabilities | Where-Object Name -eq "MouseTrace").Count -eq 0) "MouseTrace remained active after CapabilityOff."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "sidebar-settings" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "الإعدادات"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "Sidebar") "Sidebar action payload was not returned for Settings."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-settings-tools-menu" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Settings.Toolbar.ToolsMenu"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.AutomationId -eq "Settings.Toolbar.ToolsMenu") "Failed to target Settings tools menu button."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "popup-click-copy-path-summary" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            Name = "نسخ ملخص المسارات"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Target.ControlType -eq "ControlType.MenuItem") "Popup targeting did not resolve the Settings menu item."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "observe-settings-quiet-status" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Elements"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Status.Primary"
            MaxResults = 1
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Elements.Count -ge 1) "Could not read Shell.Status.Primary after popup action."
        $status = $payload.Elements[0]
        $message = @($status.HelpText, $status.ItemStatus, $status.Name) -join " "
        Assert-RegressionCondition ($message.Contains("تم نسخ ملخص مسارات التشغيل.")) "Settings popup action did not produce the expected quiet status message."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "sidebar-guarantees" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "الضمانات"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "Sidebar") "Sidebar action payload was not returned for Guarantees."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-guarantee-history" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "GuaranteeDetailPanel.Action.History"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.AutomationId -eq "Dialog.GuaranteeHistory") "WaitWindow did not resolve HistoryDialog."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "close-history-and-wait" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            AutomationId = "Dialog.GuaranteeHistory.CloseFooterButton"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindowClosed"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "HistoryDialog did not close within the expected wait window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "reopen-history-for-print" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "GuaranteeDetailPanel.Action.History"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.AutomationId -eq "Dialog.GuaranteeHistory") "HistoryDialog did not reopen for print integration."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-history-print-window" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            AutomationId = "Dialog.GuaranteeHistory.PrintButton"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowTitle = "GuaranteeManager - Print"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.Name -eq "GuaranteeManager - Print") "WaitWindow did not resolve the external print window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "windows-include-external-print" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $printWindow = @($payload.Windows | Where-Object { $_.Name -eq "GuaranteeManager - Print" } | Select-Object -First 1)
        Assert-RegressionCondition ($printWindow.Count -eq 1) "Windows catalog did not include the external print window."
        Assert-RegressionCondition ($printWindow[0].ProcessId -ne $payload.ProcessId) "External print window was not detected as a cross-process window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "close-print-and-wait" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowTitle = "GuaranteeManager - Print"
            Name = "Cancel"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindowClosed"
            WindowTitle = "GuaranteeManager - Print"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "External print window did not close within the expected wait window."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "close-history-after-print" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            AutomationId = "Dialog.GuaranteeHistory.CloseFooterButton"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindowClosed"
            WindowAutomationId = "Dialog.GuaranteeHistory"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "HistoryDialog did not close after the print integration path."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-clean-after-integration" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([string]::IsNullOrWhiteSpace([string]$payload.Health.ActiveDialogTitle)) "A dialog remained open after integration flow."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Session did not return to a single main window after integration flow."
        return $payload
    } | Out-Null
}
catch {
    $suiteFailed = $true
    $failureMessage = $_.Exception.Message
    try {
        Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            IncludeCapture = $true
            OutputPath = $relativeFailureCapturePath
            ReuseRunningSession = $true
        } | Out-Null
    }
    catch {
    }
}
finally {
    Write-RegressionSummary
}

if ($suiteFailed) {
    throw "UI tooling integration suite failed. See $summaryPath"
}

[pscustomobject]@{
    Summary = $summaryPath
    FailureCapture = if (Test-Path -LiteralPath $failureCapturePath) { $failureCapturePath } else { $null }
    Passed = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
} | Format-List
