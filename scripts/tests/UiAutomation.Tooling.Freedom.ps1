param(
    [string]$OutputRoot = ".\scratch\UIAcceptance\latest",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$uiExplorePath = Join-Path $repoRoot "scripts\ui_explore.ps1"
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$summaryPath = Join-Path $resolvedOutputRoot "tooling-freedom-summary.md"
$failureCapturePath = Join-Path $resolvedOutputRoot "tooling-freedom-failure.png"
$relativeFailureCapturePath = ".\scratch\UIAcceptance\latest\tooling-freedom-failure.png"
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
    [void]$lines.Add("# UI Tooling Freedom Summary")
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
            [void]$lines.Add("- Failure capture: [tooling-freedom-failure.png](./tooling-freedom-failure.png)")
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
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "workspace-settings-via-label" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "الإعدادات"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Settings") "Sidebar by label did not switch to Settings."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "workspace-reports-via-automationid" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            AutomationId = "Shell.Sidebar.Reports"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Reports") "Sidebar by AutomationId did not switch to Reports."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "workspace-guarantees-via-mouseclick" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "MouseClick"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Guarantees") "MouseClick did not bring us back to Guarantees."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "resolve-global-search-via-elements" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Elements"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.GlobalSearchBox"
            MaxResults = 3
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.Elements).Count -ge 1) "Elements did not resolve the global search box by AutomationId."
        Assert-RegressionCondition ([string]$payload.Elements[0].AutomationId -eq "Shell.GlobalSearchBox") "Elements returned an unexpected first element for the global search box."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "resolve-main-window-via-windows-catalog" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition (@($payload.Windows | Where-Object AutomationId -eq "Shell.MainWindow").Count -eq 1) "Windows catalog did not expose Shell.MainWindow."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "dialog-open-via-click-close-via-escape" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Guarantees.Toolbar.CreateNew"
            ReuseRunningSession = $true
        } | Out-Null

        Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowAutomationId = "Dialog.NewGuarantee"
            ReuseRunningSession = $true
        } | Out-Null

        Invoke-UiExploreJson -Arguments @{
            Action = "Key"
            WindowAutomationId = "Dialog.NewGuarantee"
            KeyName = "Escape"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindowClosed"
            WindowAutomationId = "Dialog.NewGuarantee"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "Escape did not close NewGuaranteeDialog."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "manual-evidence-via-burstcapture" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "BurstCapture"
            LeaseMilliseconds = 2500
            Reason = "freedom-manual-evidence"
            ReuseRunningSession = $true
        } | Out-Null

        $windowsPayload = Invoke-UiExploreJson -Arguments @{
            Action = "Windows"
            ReuseRunningSession = $true
        }

        $hostPayload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
            ReuseRunningSession = $true
        }

        $observation = @($hostPayload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "BurstCapture" -and $_.Kind -eq "burst-sequence" -and $_.Payload.Action -eq "Windows"
        } | Select-Object -First 1)

        Assert-RegressionCondition (@($windowsPayload.Windows).Count -ge 1) "Windows action did not return any visible window entries."
        Assert-RegressionCondition ($observation.Count -eq 1) "Explicit BurstCapture did not record a burst-sequence observation."

        Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "BurstCapture"
            Reason = "freedom-manual-evidence-complete"
            ReuseRunningSession = $true
        } | Out-Null

        return $hostPayload
    } | Out-Null

    Invoke-RegressionStep -Name "adaptive-evidence-via-reactiveassist" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOn"
            CapabilityName = "ReactiveAssist"
            LeaseMilliseconds = 3500
            Reason = "freedom-reactive-evidence"
            ReuseRunningSession = $true
        } | Out-Null

        Invoke-UiExploreJson -Arguments @{
            Action = "MouseHover"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.Sidebar.Guarantees"
            HoverMilliseconds = 900
            ReuseRunningSession = $true
        } | Out-Null

        $hostPayload = Invoke-UiExploreJson -Arguments @{
            Action = "HostState"
            ReuseRunningSession = $true
        }

        $observation = @($hostPayload.RecentCapabilityObservations | Where-Object {
            $_.CapabilityName -eq "ReactiveAssist" -and $_.Kind -eq "reactive-trigger" -and $_.Payload.Action -eq "MouseHover"
        } | Select-Object -First 1)

        Assert-RegressionCondition ($observation.Count -eq 1) "ReactiveAssist did not record a reactive-trigger observation."
        Assert-RegressionCondition ([string]$hostPayload.CapabilityOperatorView.Status -in @("intervened", "cooling-down")) "CapabilityOperatorView did not surface the reactive intervention clearly."

        Invoke-UiExploreJson -Arguments @{
            Action = "CapabilityOff"
            CapabilityName = "ReactiveAssist"
            Reason = "freedom-reactive-evidence-complete"
            ReuseRunningSession = $true
        } | Out-Null

        return $hostPayload
    } | Out-Null
}
catch {
    $suiteFailed = $true
    $failureMessage = $_.Exception.Message
    try {
        & $uiExplorePath -Action Probe -IncludeCapture -OutputPath $relativeFailureCapturePath -ReuseRunningSession:$true | Out-Null
    }
    catch {
    }
}
finally {
    Write-RegressionSummary
}

if ($suiteFailed) {
    throw "UI tooling freedom suite failed. See $summaryPath"
}

[pscustomobject]@{
    Summary = $summaryPath
    FailureCapture = if (Test-Path -LiteralPath $failureCapturePath) { $failureCapturePath } else { $null }
    Passed = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
} | Format-List
