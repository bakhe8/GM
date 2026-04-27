function Get-ResolvedProcess {
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,
        [int]$ProcessId = 0,
        [switch]$ReuseRunningSession = $true
    )

    if ($ProcessId -ne 0) {
        return Get-Process -Id $ProcessId -ErrorAction Stop
    }

    return Start-UiTargetApplication -RepoRoot $RepoRoot -ReuseRunningSession:$ReuseRunningSession
}

function Get-UiBlockingWindows {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    return @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground | Where-Object {
        $_.AutomationId -ne "Shell.MainWindow" -and
        $_.ControlType -eq "ControlType.Window" -and
        -not [string]::IsNullOrWhiteSpace($_.Name)
    })
}

function Assert-UiNoBlockingWindows {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [string]$AllowedTitle = "",
        [string]$AllowedAutomationId = ""
    )

    $blocking = @(Get-UiBlockingWindows -Process $Process | Where-Object {
        (($AllowedTitle -eq "") -or $_.Name -ne $AllowedTitle) -and
        (($AllowedAutomationId -eq "") -or $_.AutomationId -ne $AllowedAutomationId)
    })

    if ($blocking.Count -eq 0) {
        return
    }

    $titles = ($blocking | ForEach-Object { $_.Name } | Select-Object -Unique) -join "، "
    throw "يوجد حوار أو رسالة مفتوحة فوق التطبيق: $titles. احسمها أولًا عبر DialogAction أو استهدفها مباشرة قبل متابعة التنقل أو التفاعل العام."
}

function Wait-UiWindowClosed {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [pscustomobject]$WindowSummary,
        [int]$TimeoutSeconds = 5
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $matches = @(Get-UiWindowsCatalog -ProcessId $Process.Id -IncludeRelatedForeground | Where-Object {
            if ($WindowSummary.NativeWindowHandle -ne 0) {
                $_.NativeWindowHandle -eq $WindowSummary.NativeWindowHandle
            }
            else {
                $sameAutomation = -not [string]::IsNullOrWhiteSpace($WindowSummary.AutomationId) -and $_.AutomationId -eq $WindowSummary.AutomationId
                $sameTitle = -not [string]::IsNullOrWhiteSpace($WindowSummary.Name) -and $_.Name -eq $WindowSummary.Name
                $sameAutomation -or $sameTitle
            }
        })

        if ($matches.Count -eq 0) {
            return $true
        }

        Start-Sleep -Milliseconds 200
    }

    return $false
}

function Resolve-UiScopeRoot {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [string]$Title = "",
        [string]$AutomationId = "",
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($Title) -and [string]::IsNullOrWhiteSpace($AutomationId)) {
        return Resolve-UiWindow -ProcessId $Process.Id
    }

    try {
        return Resolve-UiWindow -Title $Title -AutomationId $AutomationId -ProcessId $Process.Id -PartialMatch:$PartialMatch
    }
    catch {
        $mainWindow = Resolve-UiWindow -ProcessId $Process.Id
        if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
            return Wait-UiElement -Root $mainWindow -AutomationId $AutomationId -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        if (-not [string]::IsNullOrWhiteSpace($Title)) {
            return Wait-UiElement -Root $mainWindow -Name $Title -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        throw
    }
}
