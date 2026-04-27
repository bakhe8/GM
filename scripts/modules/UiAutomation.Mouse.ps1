function Test-UiCoordinateProvided {
    param(
        [int]$Value
    )

    return $Value -ne [int]::MinValue
}

function Test-UiMouseSelectorProvided {
    param(
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue
    )

    return (
        ((Test-UiCoordinateProvided -Value $X) -and (Test-UiCoordinateProvided -Value $Y)) -or
        -not [string]::IsNullOrWhiteSpace($WindowTitle) -or
        -not [string]::IsNullOrWhiteSpace($WindowAutomationId) -or
        -not [string]::IsNullOrWhiteSpace($Name) -or
        -not [string]::IsNullOrWhiteSpace($AutomationId) -or
        -not [string]::IsNullOrWhiteSpace($Text)
    )
}

function Convert-UiSignedIntToUInt32 {
    param(
        [int]$Value
    )

    return [uint32]([BitConverter]::ToUInt32([BitConverter]::GetBytes($Value), 0))
}

function Get-UiCursorPosition {
    $point = New-Object GuaranteeManager.UiAcceptance.NativeMethods+POINT
    $succeeded = [GuaranteeManager.UiAcceptance.NativeMethods]::GetCursorPos([ref]$point)
    if (-not $succeeded) {
        throw "تعذر قراءة موضع المؤشر الحالي."
    }

    return [pscustomobject]@{
        X = [int]$point.X
        Y = [int]$point.Y
    }
}

function Resolve-UiMouseTargetElement {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [switch]$PartialMatch
    )

    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        return Get-UiButtonByText -Root $Root -Text $Text -ProcessId $Process.Id -SearchProcessFallback -PartialMatch:$PartialMatch
    }

    try {
        return Wait-UiElement `
            -Root $Root `
            -Name $Name `
            -AutomationId $AutomationId `
            -ControlType $null `
            -TimeoutSeconds 1 `
            -PartialMatch:$PartialMatch
    }
    catch {
        $processWide = @(
            Find-UiProcessElements `
                -ProcessId $Process.Id `
                -Name $Name `
                -AutomationId $AutomationId `
                -MaxResults 6 `
                -PartialMatch:$PartialMatch
        )

        if ($processWide.Count -gt 0) {
            return $processWide[0]
        }
    }

    return Wait-UiElement `
        -Root $Root `
        -Name $Name `
        -AutomationId $AutomationId `
        -ControlType $null `
        -TimeoutSeconds 5 `
        -PartialMatch:$PartialMatch
}

function Resolve-UiMouseScopeRoot {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($WindowTitle) -and [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
        return Resolve-UiWindow -ProcessId $Process.Id
    }

    try {
        return Resolve-UiWindow -Title $WindowTitle -AutomationId $WindowAutomationId -ProcessId $Process.Id -PartialMatch:$PartialMatch
    }
    catch {
        $mainWindow = Resolve-UiWindow -ProcessId $Process.Id
        if (-not [string]::IsNullOrWhiteSpace($WindowAutomationId)) {
            return Wait-UiElement -Root $mainWindow -AutomationId $WindowAutomationId -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        if (-not [string]::IsNullOrWhiteSpace($WindowTitle)) {
            return Wait-UiElement -Root $mainWindow -Name $WindowTitle -ControlType $null -TimeoutSeconds 3 -PartialMatch:$PartialMatch
        }

        throw
    }
}

function Resolve-UiMousePoint {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [switch]$PartialMatch,
        [switch]$AllowCurrentCursor
    )

    $hasAbsolutePoint = (Test-UiCoordinateProvided -Value $X) -and (Test-UiCoordinateProvided -Value $Y)
    if ($hasAbsolutePoint) {
        return [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = [pscustomobject]@{
                X = [int]($X + $OffsetX)
                Y = [int]($Y + $OffsetY)
            }
        }
    }

    if ((Test-UiCoordinateProvided -Value $X) -xor (Test-UiCoordinateProvided -Value $Y)) {
        throw "يجب تمرير X و Y معًا عند استخدام الإحداثيات المطلقة."
    }

    $hasTargetSelector =
        -not [string]::IsNullOrWhiteSpace($WindowTitle) -or
        -not [string]::IsNullOrWhiteSpace($WindowAutomationId) -or
        -not [string]::IsNullOrWhiteSpace($Name) -or
        -not [string]::IsNullOrWhiteSpace($AutomationId) -or
        -not [string]::IsNullOrWhiteSpace($Text)

    if (-not $hasTargetSelector -and $AllowCurrentCursor) {
        return [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = Get-UiCursorPosition
        }
    }

    $process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction Stop } else { Get-UiProcess }
    if ($null -eq $process) {
        throw "تعذر العثور على عملية التطبيق الحالية لاستهداف الماوس."
    }

    $scopeRoot = Resolve-UiMouseScopeRoot -Process $process -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -PartialMatch:$PartialMatch
    $targetElement = $null
    if (-not [string]::IsNullOrWhiteSpace($Name) -or -not [string]::IsNullOrWhiteSpace($AutomationId) -or -not [string]::IsNullOrWhiteSpace($Text)) {
        $targetElement = Resolve-UiMouseTargetElement -Process $process -Root $scopeRoot -Name $Name -AutomationId $AutomationId -Text $Text -PartialMatch:$PartialMatch
    }
    else {
        $targetElement = $scopeRoot
    }

    $bounds = Get-UiBounds -Element $targetElement
    return [pscustomobject]@{
        Process = $process
        Window = Get-UiElementSummary -Element $scopeRoot
        Target = Get-UiElementSummary -Element $targetElement
        Position = [pscustomobject]@{
            X = [int]($bounds.CenterX + $OffsetX)
            Y = [int]($bounds.CenterY + $OffsetY)
        }
    }
}

function Set-UiMousePosition {
    param(
        [Parameter(Mandatory)]
        [int]$X,
        [Parameter(Mandatory)]
        [int]$Y,
        [int]$StepCount = 1,
        [int]$StepDelayMilliseconds = 10
    )

    $stepCount = [Math]::Max(1, $StepCount)
    if ($stepCount -le 1) {
        [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($X, $Y) | Out-Null
        return Get-UiCursorPosition
    }

    $start = Get-UiCursorPosition
    for ($step = 1; $step -le $stepCount; $step++) {
        $ratio = $step / [double]$stepCount
        $nextX = [int][Math]::Round($start.X + (($X - $start.X) * $ratio))
        $nextY = [int][Math]::Round($start.Y + (($Y - $start.Y) * $ratio))
        [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($nextX, $nextY) | Out-Null
        if ($step -lt $stepCount -and $StepDelayMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $StepDelayMilliseconds
        }
    }

    return Get-UiCursorPosition
}

function Invoke-UiMouseButtonClick {
    param(
        [ValidateSet("Left", "Right", "Middle")]
        [string]$Button = "Left",
        [int]$ClickCount = 1,
        [int]$PauseMilliseconds = 55
    )

    switch ($Button) {
        "Left" {
            $down = 0x0002
            $up = 0x0004
        }
        "Right" {
            $down = 0x0008
            $up = 0x0010
        }
        "Middle" {
            $down = 0x0020
            $up = 0x0040
        }
    }

    for ($index = 0; $index -lt [Math]::Max(1, $ClickCount); $index++) {
        [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 20
        [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
        if ($index -lt ($ClickCount - 1) -and $PauseMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $PauseMilliseconds
        }
    }
}

function Move-UiMouse {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [int]$StepCount = 4,
        [int]$StepDelayMilliseconds = 12,
        [switch]$PartialMatch
    )

    $resolved = Resolve-UiMousePoint -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    $position = Set-UiMousePosition -X $resolved.Position.X -Y $resolved.Position.Y -StepCount $StepCount -StepDelayMilliseconds $StepDelayMilliseconds
    return [pscustomobject]@{
        Process = $resolved.Process
        Window = $resolved.Window
        Target = $resolved.Target
        Position = $position
    }
}

function Invoke-UiMouseClick {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [ValidateSet("Left", "Right", "Middle")]
        [string]$Button = "Left",
        [switch]$PartialMatch
    )

    $resolved = if (Test-UiMouseSelectorProvided -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y) {
        Move-UiMouse -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    }
    else {
        $position = Get-UiCursorPosition
        [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = $position
        }
    }

    Invoke-UiMouseButtonClick -Button $Button -ClickCount 1
    Start-Sleep -Milliseconds 90
    return [pscustomobject]@{
        Process = $resolved.Process
        Window = $resolved.Window
        Target = $resolved.Target
        Position = Get-UiCursorPosition
        Button = $Button
        ClickCount = 1
    }
}

function Invoke-UiMouseRightClick {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [switch]$PartialMatch
    )

    return Invoke-UiMouseClick -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -Button Right -PartialMatch:$PartialMatch
}

function Invoke-UiMouseDoubleClick {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [switch]$PartialMatch
    )

    $resolved = if (Test-UiMouseSelectorProvided -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y) {
        Move-UiMouse -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    }
    else {
        $position = Get-UiCursorPosition
        [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = $position
        }
    }

    Invoke-UiMouseButtonClick -Button Left -ClickCount 2
    Start-Sleep -Milliseconds 110
    return [pscustomobject]@{
        Process = $resolved.Process
        Window = $resolved.Window
        Target = $resolved.Target
        Position = Get-UiCursorPosition
        Button = "Left"
        ClickCount = 2
    }
}

function Invoke-UiMouseHover {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [int]$HoverMilliseconds = 350,
        [switch]$PartialMatch
    )

    $resolved = if (Test-UiMouseSelectorProvided -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y) {
        Move-UiMouse -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    }
    else {
        $position = Get-UiCursorPosition
        [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = $position
        }
    }

    if ($HoverMilliseconds -gt 0) {
        Start-Sleep -Milliseconds $HoverMilliseconds
    }

    return [pscustomobject]@{
        Process = $resolved.Process
        Window = $resolved.Window
        Target = $resolved.Target
        Position = Get-UiCursorPosition
        HoverMilliseconds = [Math]::Max(0, $HoverMilliseconds)
    }
}

function Invoke-UiMouseDrag {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [int]$DeltaX = 0,
        [int]$DeltaY = 0,
        [int]$StepCount = 8,
        [int]$StepDelayMilliseconds = 14,
        [switch]$PartialMatch
    )

    if ($DeltaX -eq 0 -and $DeltaY -eq 0) {
        throw "MouseDrag requires a non-zero -DeltaX or -DeltaY."
    }

    $start = if (Test-UiMouseSelectorProvided -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y) {
        Move-UiMouse -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    }
    else {
        $position = Get-UiCursorPosition
        [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = $position
        }
    }

    $startPosition = [pscustomobject]@{
        X = [int]$start.Position.X
        Y = [int]$start.Position.Y
    }
    $endX = [int]($startPosition.X + $DeltaX)
    $endY = [int]($startPosition.Y + $DeltaY)

    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
    $endPosition = Set-UiMousePosition -X $endX -Y $endY -StepCount ([Math]::Max(2, $StepCount)) -StepDelayMilliseconds $StepDelayMilliseconds
    Start-Sleep -Milliseconds 30
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 90

    return [pscustomobject]@{
        Process = $start.Process
        Window = $start.Window
        Target = $start.Target
        StartPosition = $startPosition
        EndPosition = $endPosition
        DeltaX = $DeltaX
        DeltaY = $DeltaY
    }
}

function Invoke-UiMouseScroll {
    param(
        [int]$ProcessId = 0,
        [string]$WindowTitle = "",
        [string]$WindowAutomationId = "",
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [int]$X = [int]::MinValue,
        [int]$Y = [int]::MinValue,
        [int]$OffsetX = 0,
        [int]$OffsetY = 0,
        [int]$ScrollDelta = 120,
        [switch]$PartialMatch
    )

    $resolved = if (Test-UiMouseSelectorProvided -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y) {
        Move-UiMouse -ProcessId $ProcessId -WindowTitle $WindowTitle -WindowAutomationId $WindowAutomationId -Name $Name -AutomationId $AutomationId -Text $Text -X $X -Y $Y -OffsetX $OffsetX -OffsetY $OffsetY -PartialMatch:$PartialMatch
    }
    else {
        $position = Get-UiCursorPosition
        [pscustomobject]@{
            Process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
            Window = $null
            Target = $null
            Position = $position
        }
    }

    $wheelValue = Convert-UiSignedIntToUInt32 -Value $ScrollDelta
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0800, 0, 0, $wheelValue, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 90

    return [pscustomobject]@{
        Process = $resolved.Process
        Window = $resolved.Window
        Target = $resolved.Target
        Position = Get-UiCursorPosition
        ScrollDelta = $ScrollDelta
    }
}
