function Get-UiProcessWindowDetails {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $candidates = @(Get-Process $ProcessName -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 0) {
        return @()
    }

    return @(foreach ($process in $candidates) {
        $windows = @(Get-UiTopLevelWindows -ProcessId $process.Id)
        if ($windows.Count -eq 0) {
            continue
        }

        $largest = $windows |
            Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
            Select-Object -First 1

        $bounds = Get-UiBounds -Element $largest
        [pscustomobject]@{
            Process = $process
            LargestWindow = $largest
            Area = $bounds.Width * $bounds.Height
            ClassName = $largest.Current.ClassName
            Title = $largest.Current.Name
        }
    })
}

function Get-UiProcess {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $ranked = @(Get-UiProcessWindowDetails -ProcessName $ProcessName)
    if ($ranked.Count -eq 0) {
        return $null
    }

    $preferred = $ranked |
        Sort-Object @{ Expression = { if ($_.ClassName -eq "Window") { 0 } else { 1 } } }, @{ Expression = { -$_.Area } }, @{ Expression = { -$_.Process.StartTime.Ticks } } |
        Select-Object -First 1

    return $preferred.Process
}

function Remove-UiTransientProcesses {
    param(
        [string]$ProcessName = "GuaranteeManager"
    )

    $ranked = @(Get-UiProcessWindowDetails -ProcessName $ProcessName)
    if ($ranked.Count -le 1) {
        return
    }

    $preferred = $ranked |
        Sort-Object @{ Expression = { if ($_.ClassName -eq "Window") { 0 } else { 1 } } }, @{ Expression = { -$_.Area } }, @{ Expression = { -$_.Process.StartTime.Ticks } } |
        Select-Object -First 1

    foreach ($item in $ranked) {
        if ($item.Process.Id -eq $preferred.Process.Id) {
            continue
        }

        if ($item.ClassName -eq "#32770" -or $item.Area -lt 150000 -or $item.ClassName -ne "Window") {
            try {
                Stop-Process -Id $item.Process.Id -Force -ErrorAction Stop
            }
            catch {
            }
        }
    }
}

function Get-UiTopLevelWindows {
    param(
        [int]$ProcessId = 0
    )

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $callback = [GuaranteeManager.UiAcceptance.NativeMethods+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        if (-not [GuaranteeManager.UiAcceptance.NativeMethods]::IsWindowVisible($hWnd)) {
            return $true
        }

        [uint32]$windowProcessId = 0
        [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId) | Out-Null
        if ($ProcessId -ne 0 -and $windowProcessId -ne $ProcessId) {
            return $true
        }

        try {
            $element = [System.Windows.Automation.AutomationElement]::FromHandle($hWnd)
            if ($element -ne $null) {
                [void]$items.Add($element)
            }
        }
        catch {
        }

        return $true
    }

    [GuaranteeManager.UiAcceptance.NativeMethods]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

    return $items
}

function Get-UiForegroundWindowElement {
    $foregroundHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetForegroundWindow()
    if ($foregroundHandle -eq [IntPtr]::Zero) {
        return $null
    }

    try {
        return [System.Windows.Automation.AutomationElement]::FromHandle($foregroundHandle)
    }
    catch {
        return $null
    }
}

function Test-UiBoundsOverlap {
    param(
        [Parameter(Mandatory)]
        $BoundsA,
        [Parameter(Mandatory)]
        $BoundsB,
        [double]$MinimumCoverage = 0.12
    )

    if ($BoundsA.Width -le 0 -or $BoundsA.Height -le 0 -or $BoundsB.Width -le 0 -or $BoundsB.Height -le 0) {
        return $false
    }

    $left = [Math]::Max($BoundsA.Left, $BoundsB.Left)
    $top = [Math]::Max($BoundsA.Top, $BoundsB.Top)
    $right = [Math]::Min($BoundsA.Right, $BoundsB.Right)
    $bottom = [Math]::Min($BoundsA.Bottom, $BoundsB.Bottom)
    $width = $right - $left
    $height = $bottom - $top
    if ($width -le 0 -or $height -le 0) {
        return $false
    }

    $intersectionArea = [double]($width * $height)
    $smallerArea = [double]([Math]::Min(($BoundsA.Width * $BoundsA.Height), ($BoundsB.Width * $BoundsB.Height)))
    if ($smallerArea -le 0) {
        return $false
    }

    return ($intersectionArea / $smallerArea) -ge $MinimumCoverage
}

function Test-UiExternalWindowRelatedToProcess {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [int]$ProcessId = 0
    )

    if ($null -eq $Window -or $ProcessId -eq 0) {
        return $false
    }

    try {
        if ($Window.Current.ProcessId -eq $ProcessId) {
            return $false
        }
    }
    catch {
        return $false
    }

    $windowHandle = [IntPtr]::Zero
    $windowName = ""
    $targetProcess = $null
    try {
        $windowHandle = [IntPtr][int64]$Window.Current.NativeWindowHandle
        $windowName = $Window.Current.Name
    }
    catch {
        return $false
    }

    try {
        $targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
    }
    catch {
    }

    if ($windowHandle -ne [IntPtr]::Zero -and $null -ne $targetProcess -and $targetProcess.MainWindowHandle -ne 0) {
        $ownerHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindow($windowHandle, 4)
        if ($ownerHandle -ne [IntPtr]::Zero -and $ownerHandle -eq [IntPtr][int64]$targetProcess.MainWindowHandle) {
            return $true
        }
    }

    if ($null -ne $targetProcess -and -not [string]::IsNullOrWhiteSpace($windowName)) {
        if ($windowName.IndexOf($targetProcess.ProcessName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }

        if (-not [string]::IsNullOrWhiteSpace($targetProcess.MainWindowTitle) -and
            $windowName.IndexOf($targetProcess.MainWindowTitle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-UiRelatedTopLevelWindows {
    param(
        [int]$ProcessId = 0
    )

    if ($ProcessId -eq 0) {
        return @(Get-UiTopLevelWindows -ProcessId 0)
    }

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $seen = New-Object System.Collections.Generic.HashSet[int]
    $targetProcess = $null
    $targetHandle = [IntPtr]::Zero
    $targetProcessName = ""
    $targetTitle = ""

    try {
        $targetProcess = Get-Process -Id $ProcessId -ErrorAction Stop
        $targetHandle = [IntPtr][int64]$targetProcess.MainWindowHandle
        $targetProcessName = $targetProcess.ProcessName
        $targetTitle = $targetProcess.MainWindowTitle
    }
    catch {
    }

    foreach ($window in @(Get-UiTopLevelWindows -ProcessId $ProcessId)) {
        try {
            if ($window.Current.NativeWindowHandle -ne 0 -and $seen.Add([int]$window.Current.NativeWindowHandle)) {
                [void]$items.Add($window)
            }
        }
        catch {
        }
    }

    foreach ($window in @(Get-UiTopLevelWindows -ProcessId 0)) {
        try {
            if ($window.Current.ProcessId -eq $ProcessId) {
                continue
            }

            $windowHandle = [IntPtr][int64]$window.Current.NativeWindowHandle
            if ($windowHandle -eq [IntPtr]::Zero) {
                continue
            }

            $windowName = $window.Current.Name
            $relatedByOwner = $false
            if ($targetHandle -ne [IntPtr]::Zero) {
                $ownerHandle = [GuaranteeManager.UiAcceptance.NativeMethods]::GetWindow($windowHandle, 4)
                $relatedByOwner = ($ownerHandle -ne [IntPtr]::Zero -and $ownerHandle -eq $targetHandle)
            }

            $relatedByName = -not [string]::IsNullOrWhiteSpace($windowName) -and (
                ((-not [string]::IsNullOrWhiteSpace($targetProcessName)) -and $windowName.IndexOf($targetProcessName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) -or
                ((-not [string]::IsNullOrWhiteSpace($targetTitle)) -and $windowName.IndexOf($targetTitle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
            )

            if (-not ($relatedByOwner -or $relatedByName)) {
                continue
            }

            if ($seen.Add([int]$window.Current.NativeWindowHandle)) {
                [void]$items.Add($window)
            }
        }
        catch {
        }
    }

    return $items
}

function Resolve-UiWindow {
    param(
        [string]$Title,
        [string]$AutomationId,
        [int]$ProcessId = 0,
        [switch]$PartialMatch
    )

    if (-not [string]::IsNullOrWhiteSpace($Title) -or -not [string]::IsNullOrWhiteSpace($AutomationId)) {
        return Wait-UiWindow -Title $Title -AutomationId $AutomationId -ProcessId $ProcessId -PartialMatch:$PartialMatch
    }

    $process = if ($ProcessId -ne 0) {
        Get-Process -Id $ProcessId -ErrorAction Stop
    }
    else {
        Get-UiProcess
    }

    if ($null -eq $process -or $process.MainWindowHandle -eq 0) {
        $windows = @(
            if ($null -ne $process) {
                Get-UiTopLevelWindows -ProcessId $process.Id
            }
        )
        if ($windows.Count -eq 0) {
            throw "Could not resolve a main application window."
        }

        return $windows |
            Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
            Select-Object -First 1
    }

    $mainWindow = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr][int64]$process.MainWindowHandle)
    $mainBounds = Get-UiBounds -Element $mainWindow
    if ($mainBounds.Width -lt 400 -or $mainBounds.Height -lt 300) {
        $windows = @(Get-UiTopLevelWindows -ProcessId $process.Id)
        if ($windows.Count -gt 0) {
            return $windows |
                Sort-Object { (Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height } -Descending |
                Select-Object -First 1
        }
    }

    return $mainWindow
}

function Show-UiWindow {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window
    )

    try {
        if ($Window.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]([object]$pattern))) {
            $windowPattern = [System.Windows.Automation.WindowPattern]$pattern
            if ($windowPattern.Current.WindowVisualState -eq [System.Windows.Automation.WindowVisualState]::Minimized) {
                $windowPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
            }
        }
    }
    catch {
    }

    $handle = [IntPtr][int64]$Window.Current.NativeWindowHandle
    if ($handle -ne [IntPtr]::Zero) {
        [GuaranteeManager.UiAcceptance.NativeMethods]::ShowWindowAsync($handle, 9) | Out-Null
        [GuaranteeManager.UiAcceptance.NativeMethods]::SetForegroundWindow($handle) | Out-Null
    }

    try {
        $Window.SetFocus()
    }
    catch {
    }
}

function Wait-UiWindow {
    param(
        [string]$Title,
        [string]$AutomationId,
        [int]$ProcessId = 0,
        [int]$TimeoutSeconds = 10,
        [switch]$PartialMatch
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $windows = @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId)
        $matchedWindows = @($windows | Where-Object {
            Test-UiWindowMatch -Window $_ -Title $Title -AutomationId $AutomationId -PartialMatch:$PartialMatch
        })
        if ($matchedWindows.Count -gt 0) {
            $ordered = $matchedWindows |
                Sort-Object @{ Expression = { if ($_.Current.NativeWindowHandle -eq 0) { 1 } else { 0 } } }, @{ Expression = { -((Get-UiBounds -Element $_).Width * (Get-UiBounds -Element $_).Height) } }

            $window = $ordered | Select-Object -First 1
            Show-UiWindow -Window $window
            return $window
        }

        Start-Sleep -Milliseconds 200
    }

    $message = if ($Title) { "title '$Title'" } else { "automation id '$AutomationId'" }
    throw "Timed out waiting for window with $message."
}

function Get-UiWindowsCatalog {
    param(
        [int]$ProcessId = 0,
        [switch]$IncludeRelatedForeground
    )

    $windows = if ($IncludeRelatedForeground) {
        @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId)
    }
    else {
        @(Get-UiTopLevelWindows -ProcessId $ProcessId)
    }

    return @($windows | ForEach-Object {
        Get-UiElementSummary -Element $_
    })
}
