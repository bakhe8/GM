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

$summaryPath = Join-Path $resolvedOutputRoot "tooling-regression-summary.md"
$failureCapturePath = Join-Path $resolvedOutputRoot "tooling-regression-failure.png"
$relativeFailureCapturePath = ".\scratch\UIAcceptance\latest\tooling-regression-failure.png"
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
    [void]$lines.Add("# UI Tooling Regression Summary")
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
            [void]$lines.Add("- Failure capture: [tooling-regression-failure.png](./tooling-regression-failure.png)")
        }
    }

    [System.IO.File]::WriteAllLines($summaryPath, $lines)
}

try {
    if (-not $ReuseRunningSession) {
        Get-Process GuaranteeManager -ErrorAction SilentlyContinue | Stop-Process -Force
    }

    $probe = Invoke-RegressionStep -Name "probe-main-window" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $ReuseRunningSession
        }

        Assert-RegressionCondition ($payload.MainWindow.AutomationId -eq "Shell.MainWindow") "Probe did not return Shell.MainWindow."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -ge 1) "Probe did not report any open application window."
        return $payload
    }

    Invoke-RegressionStep -Name "sidebar-guarantees" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Sidebar"
            Label = "الضمانات"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Action -eq "Sidebar") "Sidebar action payload was not returned."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-guarantees-workspace" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.ShellState.CurrentWorkspaceKey -eq "Guarantees") "ShellState did not switch to Guarantees after sidebar navigation."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "elements-global-search" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Elements"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Shell.GlobalSearchBox"
            MaxResults = 5
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Elements.Count -ge 1) "Elements query did not return Shell.GlobalSearchBox."
        Assert-RegressionCondition ($payload.Elements[0].AutomationId -eq "Shell.GlobalSearchBox") "Elements query returned an unexpected first element."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "open-new-guarantee-dialog" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Shell.MainWindow"
            AutomationId = "Guarantees.Toolbar.CreateNew"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowAutomationId = "Dialog.NewGuarantee"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.AutomationId -eq "Dialog.NewGuarantee") "WaitWindow did not resolve Dialog.NewGuarantee."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "set-guarantee-number" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "SetField"
            WindowAutomationId = "Dialog.NewGuarantee"
            AutomationId = "Dialog.NewGuarantee.GuaranteeNoInput"
            Value = "REG-SMOKE-001"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Field.AutomationId -eq "Dialog.NewGuarantee.GuaranteeNoInput") "SetField did not target the guarantee number input."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "trigger-discard-confirmation" -ScriptBlock {
        Invoke-UiExploreJson -Arguments @{
            Action = "Click"
            WindowAutomationId = "Dialog.NewGuarantee"
            AutomationId = "Dialog.NewGuarantee.CancelButton"
            ReuseRunningSession = $true
        } | Out-Null

        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "WaitWindow"
            WindowTitle = "تأكيد الإغلاق"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Window.Name -eq "تأكيد الإغلاق") "Discard confirmation did not appear."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "dialogaction-yes" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "DialogAction"
            Text = "Yes"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Closed -eq $true) "DialogAction did not close the confirmation dialog."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "probe-clean-session" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Probe"
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ([string]::IsNullOrWhiteSpace([string]$payload.Health.ActiveDialogTitle)) "A dialog remained open after confirming discard."
        Assert-RegressionCondition ($payload.Health.OpenWindowCount -eq 1) "Session did not return to a single main window after dialog closure."
        return $payload
    } | Out-Null

    Invoke-RegressionStep -Name "events-shell-navigation" -ScriptBlock {
        $payload = Invoke-UiExploreJson -Arguments @{
            Action = "Events"
            Category = "shell.navigation"
            MaxResults = 10
            ReuseRunningSession = $true
        }

        Assert-RegressionCondition ($payload.Events.Count -ge 1) "Events query did not return shell.navigation records."
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
    throw "UI tooling regression suite failed. See $summaryPath"
}

[pscustomobject]@{
    Summary = $summaryPath
    FailureCapture = if (Test-Path -LiteralPath $failureCapturePath) { $failureCapturePath } else { $null }
    Passed = @($results | Where-Object Success).Count
    Failed = @($results | Where-Object { -not $_.Success }).Count
} | Format-List
