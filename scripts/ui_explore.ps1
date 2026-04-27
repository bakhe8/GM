param(
    [ValidateSet("Launch", "Windows", "Elements", "Sidebar", "Click", "SetField", "WaitWindow", "WaitWindowClosed", "Capture", "DialogAction", "State", "Diagnostics", "Probe", "Compare", "Events", "Key", "SendKeys", "HostState", "MediaState", "FaultState", "CapabilityOn", "CapabilityOff", "VideoOn", "VideoOff", "AudioOn", "AudioOff", "MouseMove", "MouseClick", "MouseRightClick", "MouseDoubleClick", "MouseHover", "MouseDrag", "MouseScroll")]
    [string]$Action = "Probe",
    [string]$WindowTitle = "",
    [string]$WindowAutomationId = "",
    [string]$Name = "",
    [string]$Label = "",
    [string]$Text = "",
    [AllowEmptyString()]
    [string]$Value = "",
    [string]$AutomationId = "",
    [string]$ControlType = "",
    [string]$Category = "",
    [string]$EventActionName = "",
    [string]$KeyName = "",
    [string]$CapabilityName = "",
    [string]$Reason = "",
    [int]$X = [int]::MinValue,
    [int]$Y = [int]::MinValue,
    [int]$OffsetX = 0,
    [int]$OffsetY = 0,
    [int]$DeltaX = 0,
    [int]$DeltaY = 0,
    [int]$HoverMilliseconds = 350,
    [int]$ScrollDelta = 120,
    [string]$OutputPath = ".\\Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-capture.png",
    [string]$ReferencePath = "",
    [string]$DiffOutputPath = ".\\Doc\\Assets\\Documentation\\Screenshots\\UIAcceptance\\latest\\interactive-diff.png",
    [int]$ProcessId = 0,
    [int]$MaxResults = 50,
    [int]$LeaseMilliseconds = 0,
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
. (Join-Path $modulesRoot "UiAutomation.Core.ps1")
. (Join-Path $modulesRoot "UiAutomation.Windows.ps1")
. (Join-Path $modulesRoot "UiAutomation.Session.ps1")
. (Join-Path $modulesRoot "UiAutomation.Diagnostics.ps1")
. (Join-Path $modulesRoot "UiAutomation.Host.ps1")
. (Join-Path $modulesRoot "UiAutomation.Media.ps1")
. (Join-Path $modulesRoot "UiAutomation.Capabilities.ps1")
. (Join-Path $modulesRoot "UiAutomation.Mouse.ps1")
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
        CapabilityName = $CapabilityName
        Reason = $Reason
        X = $X
        Y = $Y
        OffsetX = $OffsetX
        OffsetY = $OffsetY
        DeltaX = $DeltaX
        DeltaY = $DeltaY
        HoverMilliseconds = $HoverMilliseconds
        ScrollDelta = $ScrollDelta
        OutputPath = $OutputPath
        ReferencePath = $ReferencePath
        DiffOutputPath = $DiffOutputPath
        MaxResults = $MaxResults
        LeaseMilliseconds = $LeaseMilliseconds
        Tolerance = $Tolerance
        SampleStep = $SampleStep
        PartialMatch = [bool]$PartialMatch
        IncludeCapture = [bool]$IncludeCapture
    }
    $traceStopwatch.Stop()

    $hookPayload = $null
    $processForHooks = if ($null -ne $result -and $result.PSObject.Properties.Name -contains "ProcessId" -and [int]$result.ProcessId -gt 0) {
        Get-Process -Id ([int]$result.ProcessId) -ErrorAction SilentlyContinue
    }
    else {
        Get-UiProcess
    }

    if ($Action -notin @("HostState", "MediaState", "FaultState", "CapabilityOn", "CapabilityOff") -and $null -ne $processForHooks) {
        try {
            $hookPayload = Invoke-UiCapabilityHooksAfterAction -Process $processForHooks -RepoRoot $repoRoot -ActionName $Action -DurationMs $traceStopwatch.Elapsed.TotalMilliseconds -Result $result
            if ($null -ne $hookPayload) {
                Add-Member -InputObject $result -NotePropertyName "CapabilitySession" -NotePropertyValue $hookPayload.Session -Force
                Add-Member -InputObject $result -NotePropertyName "CapabilityCaptures" -NotePropertyValue ([object[]]@($hookPayload.Captures)) -Force
            }
        }
        catch {
            Add-Member -InputObject $result -NotePropertyName "CapabilityHookWarning" -NotePropertyValue $_.Exception.Message -Force
        }
    }
    elseif ($null -ne $result -and $result.PSObject.Properties.Name -notcontains "CapabilitySession") {
        Add-Member -InputObject $result -NotePropertyName "CapabilitySession" -NotePropertyValue (Get-UiCapabilitySessionState) -Force
    }

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
    $failureHooks = $null
    $failureCapturePaths = @()
    try {
        $failureHooks = Invoke-UiCapabilityHooksOnFailure -RepoRoot $repoRoot -ActionName $Action -DurationMs $traceStopwatch.Elapsed.TotalMilliseconds -ErrorMessage $_.Exception.Message
        if ($null -ne $failureHooks -and $null -ne $failureHooks.Captures) {
            $failureCapturePaths = @($failureHooks.Captures | ForEach-Object { $_.Path })
        }
    }
    catch {
    }

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
            CapabilityName = $CapabilityName
            FailureCapturePaths = $failureCapturePaths
            StartedAt = $traceStartedAt.ToString("o")
        }
    throw
}
