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
$modulesRoot = Join-Path $PSScriptRoot "modules"
. (Join-Path $modulesRoot "UiAutomation.Session.ps1")
. (Join-Path $modulesRoot "UiAutomation.Diagnostics.ps1")
. (Join-Path $modulesRoot "UiAutomation.Actions.ps1")

function Write-UiObject {
    param($InputObject)
    $InputObject | ConvertTo-Json -Depth 10
}

$traceStartedAt = Get-Date
$traceStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$result = $null

try {
    $result = Invoke-UiExploreAction -Options @{
        Action = $Action
        RepoRoot = $repoRoot
        ProcessId = $ProcessId
        ReuseRunningSession = $ReuseRunningSession
        WindowTitle = $WindowTitle
        WindowAutomationId = $WindowAutomationId
        Name = $Name
        Label = $Label
        Text = $Text
        Value = $Value
        AutomationId = $AutomationId
        ControlType = $ControlType
        Category = $Category
        EventActionName = $EventActionName
        KeyName = $KeyName
        OutputPath = $OutputPath
        ReferencePath = $ReferencePath
        DiffOutputPath = $DiffOutputPath
        MaxResults = $MaxResults
        Tolerance = $Tolerance
        SampleStep = $SampleStep
        PartialMatch = [bool]$PartialMatch
        IncludeCapture = [bool]$IncludeCapture
    }
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
