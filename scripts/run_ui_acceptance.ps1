param(
    [ValidateSet("SmokeNavigation", "NewGuaranteeDiscard", "All")]
    [string]$Scenario = "All",
    [string]$OutputRoot = ".\\scratch\\UIAcceptance",
    [switch]$ReuseRunningSession = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modulePath = Join-Path $PSScriptRoot "UIAutomation.Acceptance.psm1"
Import-Module $modulePath -Force

$repoRoot = Get-UiAcceptanceRepoRoot
$resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
$sessionName = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionDirectory = Join-Path $resolvedOutputRoot $sessionName
New-Item -ItemType Directory -Force -Path $sessionDirectory | Out-Null

function Get-MainWindowElement {
    param([System.Diagnostics.Process]$Process)

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne 0) {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr][int64]$Process.MainWindowHandle)
            Show-UiWindow -Window $window
            return $window
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for the main application window handle."
}

function Save-ScenarioStep {
    param(
        [string]$Name,
        [System.Windows.Automation.AutomationElement]$Window
    )

    $path = Join-Path $sessionDirectory ("{0}.png" -f $Name)
    Save-UiWindowScreenshot -Window $Window -Path $path | Out-Null
    return $path
}

function Wait-UiWindowClosed {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Title = "",
        [string]$AutomationId = "",
        [int]$TimeoutSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $windows = @(Get-UiWindowsCatalog -ProcessId $Process.Id)
        $matching = @($windows | Where-Object {
            (($Title -and $_.Name -eq $Title) -or ($AutomationId -and $_.AutomationId -eq $AutomationId))
        })

        if ($matching.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 200
    }

    $label = if ($AutomationId) { "automation id '$AutomationId'" } else { "title '$Title'" }
    throw "Timed out waiting for dialog closure by $label."
}

function Complete-UiConfirmationDialog {
    param(
        [System.Windows.Automation.AutomationElement]$Dialog,
        [string[]]$PreferredLabels,
        [System.Diagnostics.Process]$Process,
        [int]$MaxAttempts = 3
    )

    $result = Invoke-UiDialogActionButton -Dialog $Dialog -PreferredLabels $PreferredLabels -ProcessId $Process.Id -MaxAttempts $MaxAttempts -CloseTimeoutSeconds 3
    if (-not $result.Closed) {
        throw "تعذر حسم حوار '$($result.Dialog.Name)' بعد $($result.Attempt) محاولات. آخر أسلوب: $($result.Strategy)."
    }

    return $result
}

function Invoke-SmokeNavigationScenario {
    param([System.Windows.Automation.AutomationElement]$MainWindow)

    $screens = @(
        @{ Label = "اليوم"; Slug = "dashboard" },
        @{ Label = "الضمانات"; Slug = "guarantees" },
        @{ Label = "البنوك"; Slug = "banks" },
        @{ Label = "التقارير"; Slug = "reports" },
        @{ Label = "الإعدادات"; Slug = "settings" }
    )

    $captures = New-Object System.Collections.Generic.List[string]
    foreach ($screen in $screens) {
        Invoke-UiSidebarNavigation -MainWindow $MainWindow -WorkspaceLabel $screen.Label
        $capture = Save-ScenarioStep -Name ("smoke-{0}" -f $screen.Slug) -Window $MainWindow
        [void]$captures.Add($capture)
    }

    $contactSheet = Join-Path $sessionDirectory "smoke-contactsheet.png"
    New-UiContactSheet -ImagePaths $captures.ToArray() -DestinationPath $contactSheet | Out-Null

    return [pscustomobject]@{
        Scenario = "SmokeNavigation"
        Captures = $captures.ToArray()
        ContactSheet = $contactSheet
    }
}

function Invoke-NewGuaranteeDiscardScenario {
    param(
        [System.Diagnostics.Process]$Process,
        [System.Windows.Automation.AutomationElement]$MainWindow
    )

    Invoke-UiSidebarNavigation -MainWindow $MainWindow -WorkspaceLabel "الضمانات"
    $toolbarButton = Wait-UiElement -Root $MainWindow -AutomationId "Guarantees.Toolbar.CreateNew" -TimeoutSeconds 10
    Invoke-UiElement -Element $toolbarButton

    $dialog = Wait-UiWindow -Title "إجراء جديد" -ProcessId $Process.Id -TimeoutSeconds 10
    Show-UiWindow -Window $dialog

    $openCapture = Save-ScenarioStep -Name "dialog-new-guarantee-open" -Window $dialog
    $guaranteeNoField = Get-UiEditNearLabel -Window $dialog -Label "رقم الضمان"
    Set-UiElementValue -Element $guaranteeNoField -Value "UI-AUTO-001"
    $filledCapture = Save-ScenarioStep -Name "dialog-new-guarantee-filled" -Window $dialog

    $cancelButton = Wait-UiElement -Root $dialog -AutomationId "Dialog.NewGuarantee.CancelButton" -TimeoutSeconds 10
    Invoke-UiElement -Element $cancelButton

    $confirmation = Wait-UiWindow -Title "تأكيد الإغلاق" -ProcessId $Process.Id -TimeoutSeconds 10
    Show-UiWindow -Window $confirmation
    $confirmCapture = Save-ScenarioStep -Name "dialog-discard-confirmation" -Window $confirmation

    Complete-UiConfirmationDialog -Dialog $confirmation -PreferredLabels @("Yes", "&Yes", "نعم", "موافق", "OK") -Process $Process | Out-Null
    Wait-UiWindowClosed -Process $Process -Title "تأكيد الإغلاق" -TimeoutSeconds 10
    Wait-UiWindowClosed -Process $Process -AutomationId "Dialog.NewGuarantee" -TimeoutSeconds 10

    return [pscustomobject]@{
        Scenario = "NewGuaranteeDiscard"
        Captures = @($openCapture, $filledCapture, $confirmCapture)
    }
}

$process = Start-UiTargetApplication -RepoRoot $repoRoot -ReuseRunningSession:$ReuseRunningSession
$mainWindow = Get-MainWindowElement -Process $process

$results = New-Object System.Collections.Generic.List[object]

if ($Scenario -in @("SmokeNavigation", "All")) {
    [void]$results.Add((Invoke-SmokeNavigationScenario -MainWindow $mainWindow))
}

if ($Scenario -in @("NewGuaranteeDiscard", "All")) {
    [void]$results.Add((Invoke-NewGuaranteeDiscardScenario -Process $process -MainWindow $mainWindow))
}

$summaryPath = Join-Path $sessionDirectory "summary.md"
$summaryLines = New-Object System.Collections.Generic.List[string]
[void]$summaryLines.Add("# UI Acceptance Run")
[void]$summaryLines.Add("")
[void]$summaryLines.Add("- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")")
[void]$summaryLines.Add("- ProcessId: $($process.Id)")
[void]$summaryLines.Add("- Scenario: $Scenario")
[void]$summaryLines.Add("")
[void]$summaryLines.Add("## Results")
[void]$summaryLines.Add("")

foreach ($result in $results) {
    [void]$summaryLines.Add("### $($result.Scenario)")
    [void]$summaryLines.Add("")
    foreach ($capture in $result.Captures) {
        $fileName = [System.IO.Path]::GetFileName($capture)
        [void]$summaryLines.Add("- [$fileName](./$fileName)")
    }
    if ($result.PSObject.Properties.Name -contains "ContactSheet") {
        $contactFile = [System.IO.Path]::GetFileName($result.ContactSheet)
        [void]$summaryLines.Add("- [$contactFile](./$contactFile)")
    }
    [void]$summaryLines.Add("")
}

[System.IO.File]::WriteAllLines($summaryPath, $summaryLines)

$latestDirectory = Join-Path $resolvedOutputRoot "latest"
if (Test-Path -LiteralPath $latestDirectory) {
    Remove-Item -LiteralPath $latestDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $latestDirectory | Out-Null
Copy-Item -Path (Join-Path $sessionDirectory "*") -Destination $latestDirectory -Recurse -Force

[pscustomobject]@{
    SessionDirectory = $sessionDirectory
    LatestDirectory = $latestDirectory
    Summary = $summaryPath
    ProcessId = $process.Id
    Scenario = $Scenario
} | Format-List
