param(
    [string]$OutputRoot = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest",
    [ValidateSet("Smoke", "Integration", "All")]
    [string]$Suite = "Smoke",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$suiteScripts = switch ($Suite) {
    "Smoke" { @("UiAutomation.Tooling.Smoke.ps1") }
    "Integration" { @("UiAutomation.Tooling.Integration.ps1") }
    "All" { @("UiAutomation.Tooling.Smoke.ps1", "UiAutomation.Tooling.Integration.ps1") }
}

foreach ($suiteScriptName in $suiteScripts) {
    $scriptPath = Join-Path $PSScriptRoot ("tests\" + $suiteScriptName)
    & $scriptPath -OutputRoot $OutputRoot -ReuseRunningSession:$ReuseRunningSession
}
