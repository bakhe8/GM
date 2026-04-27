param(
    [string]$OutputRoot = ".\Doc\Assets\Documentation\Screenshots\UIAcceptance\latest",
    [switch]$ReuseRunningSession = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "tests\UiAutomation.Tooling.Smoke.ps1"
& $scriptPath -OutputRoot $OutputRoot -ReuseRunningSession:$ReuseRunningSession
