[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$WorkspaceRoot = "",
    [string]$BackupRoot = ""
)

$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}
else {
    $PSScriptRoot
}

if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "GuaranteeManager_runtime_cleanup_backup"
}

$runtimeDirectories = @("Data", "Attachments", "Workflow", "Logs")
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupTarget = Join-Path $BackupRoot "legacy_runtime_tree_$timestamp"
$summary = [System.Collections.Generic.List[object]]::new()

foreach ($directoryName in $runtimeDirectories) {
    $sourcePath = Join-Path $WorkspaceRoot $directoryName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        $summary.Add([pscustomobject]@{
                Directory = $directoryName
                Action = "Skipped"
                Details = "Not present"
            })
        continue
    }

    $fileCount = (Get-ChildItem -LiteralPath $sourcePath -Recurse -Force -File -ErrorAction SilentlyContinue | Measure-Object).Count

    if ($fileCount -eq 0) {
        if ($PSCmdlet.ShouldProcess($sourcePath, "Remove empty runtime directory tree")) {
            Remove-Item -LiteralPath $sourcePath -Recurse -Force
        }

        $summary.Add([pscustomobject]@{
                Directory = $directoryName
                Action = "Removed"
                Details = "Empty directory tree"
            })
        continue
    }

    if (-not (Test-Path -LiteralPath $backupTarget)) {
        New-Item -ItemType Directory -Path $backupTarget -Force | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($sourcePath, "Move runtime directory to backup '$backupTarget'")) {
        Move-Item -LiteralPath $sourcePath -Destination $backupTarget -Force
    }

    $summary.Add([pscustomobject]@{
            Directory = $directoryName
            Action = "Moved"
            Details = $backupTarget
        })
}

$summary | Format-Table -AutoSize
