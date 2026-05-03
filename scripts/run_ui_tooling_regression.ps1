param(
    [string]$OutputRoot = ".\scratch\UIAcceptance\latest",
    [ValidateSet("Smoke", "Integration", "Unit", "Freedom", "All")]
    [string]$Suite = "Smoke",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$suiteScripts = switch ($Suite) {
    "Smoke" { @("UiAutomation.Tooling.Smoke.ps1") }
    "Integration" { @("UiAutomation.Tooling.Integration.ps1") }
    "Unit" { @("UiAutomation.Tooling.Unit.ps1") }
    "Freedom" { @("UiAutomation.Tooling.Freedom.ps1") }
    "All" { @("UiAutomation.Tooling.Unit.ps1", "UiAutomation.Tooling.Smoke.ps1", "UiAutomation.Tooling.Integration.ps1", "UiAutomation.Tooling.Freedom.ps1") }
}

foreach ($suiteScriptName in $suiteScripts) {
    $scriptPath = Join-Path $PSScriptRoot ("tests\" + $suiteScriptName)
    & $scriptPath -OutputRoot $OutputRoot -ReuseRunningSession:$ReuseRunningSession
}
