function Get-UiDescendants {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root
    )

    $collection = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)

    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    for ($index = 0; $index -lt $collection.Count; $index++) {
        [void]$items.Add($collection.Item($index))
    }

    return $items
}

function Get-UiBounds {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $emptyBounds = {
        return [pscustomobject]@{
            Left = 0
            Top = 0
            Right = 0
            Bottom = 0
            Width = 0
            Height = 0
            CenterX = 0
            CenterY = 0
        }
    }

    try {
        $rect = $Element.Current.BoundingRectangle
    }
    catch {
        return & $emptyBounds
    }

    $toInt = {
        param([double]$value)
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) {
            return 0
        }

        return [int][Math]::Round($value)
    }

    $propertyNames = @()
    try {
        $propertyNames = @($rect.GetType().GetProperties() | ForEach-Object { $_.Name })
    }
    catch {
        return & $emptyBounds
    }

    $leftValue = if ($propertyNames -contains "Left") { [double]$rect.Left } elseif ($propertyNames -contains "X") { [double]$rect.X } else { 0.0 }
    $topValue = if ($propertyNames -contains "Top") { [double]$rect.Top } elseif ($propertyNames -contains "Y") { [double]$rect.Y } else { 0.0 }
    $widthValue = if ($propertyNames -contains "Width") { [double]$rect.Width } else { 0.0 }
    $heightValue = if ($propertyNames -contains "Height") { [double]$rect.Height } else { 0.0 }
    $rightValue = if ($propertyNames -contains "Right") { [double]$rect.Right } else { $leftValue + $widthValue }
    $bottomValue = if ($propertyNames -contains "Bottom") { [double]$rect.Bottom } else { $topValue + $heightValue }

    return [pscustomobject]@{
        Left   = & $toInt $leftValue
        Top    = & $toInt $topValue
        Right  = & $toInt $rightValue
        Bottom = & $toInt $bottomValue
        Width  = & $toInt $widthValue
        Height = & $toInt $heightValue
        CenterX = & $toInt ($leftValue + ($widthValue / 2.0))
        CenterY = & $toInt ($topValue + ($heightValue / 2.0))
    }
}

function Get-UiElementSummary {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $bounds = Get-UiBounds -Element $Element
    $current = $null
    try {
        $current = $Element.Current
    }
    catch {
    }

    $controlTypeName = $null
    if ($null -ne $current) {
        try {
            if ($null -ne $current.ControlType) {
                $controlTypeName = $current.ControlType.ProgrammaticName
            }
        }
        catch {
        }
    }

    return [pscustomobject]@{
        Name = if ($null -ne $current) { try { $current.Name } catch { $null } } else { $null }
        AutomationId = if ($null -ne $current) { try { $current.AutomationId } catch { $null } } else { $null }
        ControlType = $controlTypeName
        ClassName = if ($null -ne $current) { try { $current.ClassName } catch { $null } } else { $null }
        HelpText = if ($null -ne $current) { try { $current.HelpText } catch { $null } } else { $null }
        ItemStatus = if ($null -ne $current) { try { $current.ItemStatus } catch { $null } } else { $null }
        ProcessId = if ($null -ne $current) { try { $current.ProcessId } catch { 0 } } else { 0 }
        NativeWindowHandle = if ($null -ne $current) { try { $current.NativeWindowHandle } catch { 0 } } else { 0 }
        IsOffscreen = if ($null -ne $current) { try { [bool]$current.IsOffscreen } catch { $false } } else { $false }
        Bounds = $bounds
    }
}

function Test-UiSummaryVisible {
    param(
        [Parameter(Mandatory)]
        $Summary,
        $ViewportBounds = $null
    )

    if ($null -eq $Summary) {
        return $false
    }

    if ($Summary.IsOffscreen) {
        return $false
    }

    if ($Summary.Bounds.Width -le 1 -or $Summary.Bounds.Height -le 1) {
        return $false
    }

    if ($null -eq $ViewportBounds) {
        return $true
    }

    return Test-UiBoundsOverlap -BoundsA $Summary.Bounds -BoundsB $ViewportBounds -MinimumCoverage 0.15
}

function Get-UiElementRuntimeKey {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    try {
        $runtime = $Element.GetRuntimeId()
        if ($null -ne $runtime -and $runtime.Length -gt 0) {
            return ($runtime -join ":")
        }
    }
    catch {
    }

    try {
        $current = $Element.Current
        return "{0}:{1}:{2}:{3}" -f $current.ProcessId, $current.AutomationId, $current.Name, $current.ControlType.ProgrammaticName
    }
    catch {
        return [guid]::NewGuid().ToString("N")
    }
}

function New-UiSearchCondition {
    param(
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType
    )

    $conditions = New-Object System.Collections.Generic.List[System.Windows.Automation.Condition]

    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name)
        [void]$conditions.Add($condition)
    }

    if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            $AutomationId)
        [void]$conditions.Add($condition)
    }

    if ($null -ne $ControlType) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            $ControlType)
        [void]$conditions.Add($condition)
    }

    if ($conditions.Count -eq 0) {
        return [System.Windows.Automation.Condition]::TrueCondition
    }

    if ($conditions.Count -eq 1) {
        return $conditions[0]
    }

    return New-Object System.Windows.Automation.AndCondition($conditions.ToArray())
}

function Resolve-UiControlType {
    param(
        [string]$ProgrammaticName
    )

    if ([string]::IsNullOrWhiteSpace($ProgrammaticName)) {
        return $null
    }

    $normalized = if ($ProgrammaticName.StartsWith("ControlType.", [System.StringComparison]::OrdinalIgnoreCase)) {
        $ProgrammaticName.Substring("ControlType.".Length)
    }
    else {
        $ProgrammaticName
    }

    $property = [System.Windows.Automation.ControlType].GetProperty($normalized, [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static -bor [System.Reflection.BindingFlags]::IgnoreCase)
    if ($null -eq $property) {
        return $null
    }

    return [System.Windows.Automation.ControlType]$property.GetValue($null, $null)
}

function Find-UiElementsFast {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$MaxResults = 50
    )

    $conditions = New-Object System.Collections.Generic.List[System.Windows.Automation.Condition]

    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        [void]$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)))
    }

    if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
        [void]$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)))
    }

    if ($null -ne $ControlType) {
        [void]$conditions.Add((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)))
    }

    $condition = if ($conditions.Count -eq 0) {
        [System.Windows.Automation.Condition]::TrueCondition
    }
    elseif ($conditions.Count -eq 1) {
        $conditions[0]
    }
    else {
        New-Object System.Windows.Automation.AndCondition($conditions.ToArray())
    }

    $collection = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $items = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    for ($index = 0; $index -lt $collection.Count -and $items.Count -lt $MaxResults; $index++) {
        [void]$items.Add($collection.Item($index))
    }

    return $items
}

function Find-UiProcessElements {
    param(
        [int]$ProcessId = 0,
        [string]$Name,
        [string]$AutomationId,
        [string]$ControlType,
        [switch]$PartialMatch,
        [int]$MaxResults = 50
    )

    $resolvedControlType = Resolve-UiControlType -ProgrammaticName $ControlType
    $results = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    $seen = New-Object System.Collections.Generic.HashSet[string]

    $roots = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]
    foreach ($root in @(Get-UiTopLevelWindows -ProcessId $ProcessId)) {
        [void]$roots.Add($root)
    }
    [void]$roots.Add([System.Windows.Automation.AutomationElement]::RootElement)

    foreach ($root in $roots) {
        if ($results.Count -ge $MaxResults) {
            break
        }

        $candidates = if (-not $PartialMatch -and -not [string]::IsNullOrWhiteSpace($ControlType) -and $null -ne $resolvedControlType) {
            @(Find-UiElementsFast -Root $root -Name $Name -AutomationId $AutomationId -ControlType $resolvedControlType -MaxResults $MaxResults)
        }
        elseif (-not $PartialMatch -and ([string]::IsNullOrWhiteSpace($ControlType) -or $null -eq $resolvedControlType)) {
            @(Find-UiElementsFast -Root $root -Name $Name -AutomationId $AutomationId -ControlType $null -MaxResults $MaxResults)
        }
        else {
            if ($root -eq [System.Windows.Automation.AutomationElement]::RootElement) {
                @()
            }
            else {
                @(Get-UiDescendants -Root $root | Where-Object {
                    ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
                    ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId) -and
                    ([string]::IsNullOrWhiteSpace($ControlType) -or $_.Current.ControlType.ProgrammaticName -eq $ControlType)
                } | Select-Object -First $MaxResults)
            }
        }

        foreach ($candidate in $candidates) {
            try {
                if ($ProcessId -ne 0 -and $candidate.Current.ProcessId -ne $ProcessId) {
                    continue
                }
            }
            catch {
                continue
            }

            $key = Get-UiElementRuntimeKey -Element $candidate
            if ($seen.Add($key)) {
                [void]$results.Add($candidate)
            }

            if ($results.Count -ge $MaxResults) {
                break
            }
        }
    }

    return $results
}

function Test-UiNameMatch {
    param(
        [string]$Candidate,
        [string]$Expected,
        [switch]$PartialMatch
    )

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $false
    }

    if ($PartialMatch) {
        return $Candidate.IndexOf($Expected, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    }

    return [string]::Equals($Candidate.Trim(), $Expected.Trim(), [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-UiWindowMatch {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Title,
        [string]$AutomationId,
        [switch]$PartialMatch
    )

    $matchesTitle = [string]::IsNullOrWhiteSpace($Title) -or
        (Test-UiNameMatch -Candidate $Window.Current.Name -Expected $Title -PartialMatch:$PartialMatch)
    $matchesAutomationId = [string]::IsNullOrWhiteSpace($AutomationId) -or
        [string]::Equals($Window.Current.AutomationId, $AutomationId, [System.StringComparison]::OrdinalIgnoreCase)

    return $matchesTitle -and $matchesAutomationId
}

function Get-UiClickableAncestor {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $Element
    while ($current -ne $null) {
        if ($current.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) {
            return $current
        }

        if ($current.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]([object]$null))) {
            return $current
        }

        $current = $walker.GetParent($current)
    }

    return $Element
}

function Get-UiTopLevelAncestor {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $Element
    $last = $Element
    while ($current -ne $null) {
        $last = $current
        $parent = $walker.GetParent($current)
        if ($parent -eq $null) {
            break
        }

        $current = $parent
    }

    return $last
}

function Test-UiElementHasAncestorControlType {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)]
        [System.Windows.Automation.ControlType]$ControlType
    )

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $current = $walker.GetParent($Element)
    while ($current -ne $null) {
        if ($current.Current.ControlType -eq $ControlType) {
            return $true
        }

        $current = $walker.GetParent($current)
    }

    return $false
}

function Send-UiVirtualKey {
    param(
        [Parameter(Mandatory)]
        [byte]$VirtualKey
    )

    [GuaranteeManager.UiAcceptance.NativeMethods]::keybd_event($VirtualKey, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [GuaranteeManager.UiAcceptance.NativeMethods]::keybd_event($VirtualKey, 0, 0x0002, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
}

function ConvertTo-UiSendKeysLiteral {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrEmpty($Value)) {
        return ""
    }

    $builder = New-Object System.Text.StringBuilder
    foreach ($character in $Value.ToCharArray()) {
        $escaped = switch ($character) {
            '+' { '{+}'; break }
            '^' { '{^}'; break }
            '%' { '{%}'; break }
            '~' { '{~}'; break }
            '(' { '{(}'; break }
            ')' { '{)}'; break }
            '[' { '{[}'; break }
            ']' { '{]}'; break }
            '{' { '{{}'; break }
            '}' { '{}}'; break }
            default { [string]$character }
        }

        [void]$builder.Append($escaped)
    }

    return $builder.ToString()
}

function Wait-UiElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Name,
        [string]$AutomationId,
        [System.Windows.Automation.ControlType]$ControlType,
        [int]$TimeoutSeconds = 10,
        [switch]$PartialMatch
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $elements = @(
            if ($PartialMatch) {
                Get-UiDescendants -Root $Root | Where-Object {
                    ($null -eq $ControlType -or $_.Current.ControlType -eq $ControlType) -and
                    ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
                    ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId)
                }
            }
            else {
                Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $ControlType -MaxResults 1
            }
        )

        if ($elements.Count -gt 0) {
            return $elements[0]
        }

        Start-Sleep -Milliseconds 200
    }

    $label = if ($Name) { "name '$Name'" } elseif ($AutomationId) { "automation id '$AutomationId'" } else { "requested pattern" }
    throw "Timed out waiting for element with $label."
}

function Invoke-UiElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    $fallbackBounds = Get-UiBounds -Element $Element

    try {
        $target = Get-UiClickableAncestor -Element $Element
        $window = Get-UiTopLevelAncestor -Element $target
        if ($null -ne $window -and $window.Current.NativeWindowHandle -ne 0) {
            Show-UiWindow -Window $window
        }

        $pattern = $null
        if ($target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke()
            Start-Sleep -Milliseconds 120
            return
        }

        $legacyPatternType = "System.Windows.Automation.LegacyIAccessiblePattern" -as [type]
        if ($null -ne $legacyPatternType) {
            if ($target.TryGetCurrentPattern($legacyPatternType::Pattern, [ref]$pattern)) {
                try {
                    $pattern.DoDefaultAction()
                    Start-Sleep -Milliseconds 120
                    return
                }
                catch {
                }
            }
        }

        $targetHandle = [IntPtr][int64]$target.Current.NativeWindowHandle
        if ($targetHandle -ne [IntPtr]::Zero -and $target.Current.ClassName -eq "Button") {
            try {
                $target.SetFocus()
                Send-UiVirtualKey -VirtualKey 0x20
                Start-Sleep -Milliseconds 80
            }
            catch {
            }

            [GuaranteeManager.UiAcceptance.NativeMethods]::SendMessage($targetHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            Start-Sleep -Milliseconds 120
            return
        }

        if ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $pattern.Select()
            Start-Sleep -Milliseconds 120
            return
        }

        $fallbackBounds = Get-UiBounds -Element $target
    }
    catch {
    }

    if ($fallbackBounds.Width -le 0 -or $fallbackBounds.Height -le 0) {
        throw "تعذر التفاعل مع العنصر المحدد لأن حدود النقر غير متاحة."
    }

    [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($fallbackBounds.CenterX, $fallbackBounds.CenterY) | Out-Null
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
}

function Set-UiElementValue {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    $isTextBox = [string]::Equals([string]$Element.Current.ClassName, "TextBox", [System.StringComparison]::OrdinalIgnoreCase)
    $isComboTextBox = $isTextBox -and (Test-UiElementHasAncestorControlType -Element $Element -ControlType ([System.Windows.Automation.ControlType]::ComboBox))
    if ($isTextBox -and -not $isComboTextBox) {
        $window = Get-UiTopLevelAncestor -Element $Element
        if ($null -ne $window -and $window.Current.NativeWindowHandle -ne 0) {
            Show-UiWindow -Window $window
        }

        $Element.SetFocus()
        Start-Sleep -Milliseconds 80
        Send-UiSendKeys -KeysText "^a{BACKSPACE}"
        Start-Sleep -Milliseconds 80
        if (-not [string]::IsNullOrEmpty($Value)) {
            Send-UiSendKeys -KeysText (ConvertTo-UiSendKeysLiteral -Value $Value)
        }

        Start-Sleep -Milliseconds 220
        return
    }

    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        if (-not $pattern.Current.IsReadOnly) {
            $pattern.SetValue($Value)
            Start-Sleep -Milliseconds 200
            return
        }
    }

    throw "Element does not support writable ValuePattern."
}

function Get-UiEditNearLabel {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [Parameter(Mandatory)]
        [string]$Label
    )

    $labelElement = Wait-UiElement -Root $Window -Name $Label -ControlType ([System.Windows.Automation.ControlType]::Text) -TimeoutSeconds 5
    $labelBounds = Get-UiBounds -Element $labelElement
    $edits = Get-UiDescendants -Root $Window | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit }

    $scored = foreach ($edit in $edits) {
        $bounds = Get-UiBounds -Element $edit
        if ($bounds.Top -lt ($labelBounds.Top - 6)) {
            continue
        }

        [pscustomobject]@{
            Element = $edit
            VerticalDistance = [Math]::Abs($bounds.Top - $labelBounds.Bottom)
            HorizontalDistance = [Math]::Abs($bounds.CenterX - $labelBounds.CenterX)
        }
    }

    $candidate = $scored |
        Sort-Object VerticalDistance, HorizontalDistance |
        Select-Object -First 1

    if ($null -eq $candidate) {
        throw "Could not find an editable field near label '$Label'."
    }

    return $candidate.Element
}

function Get-UiSidebarButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)]
        [string]$Label
    )

    $windowBounds = Get-UiBounds -Element $MainWindow
    $descendants = Get-UiDescendants -Root $MainWindow | Where-Object {
        Test-UiNameMatch -Candidate $_.Current.Name -Expected $Label
    }

    $candidates = foreach ($item in $descendants) {
        $button = Get-UiClickableAncestor -Element $item
        $bounds = Get-UiBounds -Element $button
        if ($bounds.CenterX -lt ($windowBounds.Left + ($windowBounds.Width * 0.82))) {
            continue
        }

        [pscustomobject]@{
            Element = $button
            Bounds = $bounds
        }
    }

    $match = $candidates |
        Sort-Object { $_.Bounds.Top } |
        Select-Object -First 1

    if ($null -eq $match) {
        throw "Could not find sidebar button '$Label'."
    }

    return $match.Element
}

function Get-UiButtonByText {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [string]$Text,
        [int]$ProcessId = 0,
        [switch]$SearchProcessFallback,
        [switch]$PartialMatch
    )

    $buttons = if ($PartialMatch) {
        @(Get-UiDescendants -Root $Root | Where-Object {
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
            (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Text -PartialMatch:$PartialMatch) -and
            -not $_.Current.IsOffscreen
        })
    }
    else {
        @((Find-UiElementsFast -Root $Root -Name $Text -ControlType ([System.Windows.Automation.ControlType]::Button) -MaxResults 8) | Where-Object {
            try { -not $_.Current.IsOffscreen } catch { $true }
        })
    }

    $buttonMatches = @($buttons)
    if ($buttonMatches.Count -gt 0) {
        return $buttonMatches[0]
    }

    $namedDescendants = @(Get-UiDescendants -Root $Root | Where-Object {
        (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Text -PartialMatch:$PartialMatch) -and
        -not $_.Current.IsOffscreen
    })

    foreach ($item in $namedDescendants) {
        $clickable = Get-UiClickableAncestor -Element $item
        if ($clickable -ne $null) {
            return $clickable
        }
    }

    if ($SearchProcessFallback -and $ProcessId -ne 0) {
        $globalMatches = @(Find-UiProcessElements -ProcessId $ProcessId -Name $Text -PartialMatch:$PartialMatch -MaxResults 12)
        foreach ($item in $globalMatches) {
            $clickable = Get-UiClickableAncestor -Element $item
            if ($clickable -ne $null) {
                return $clickable
            }
        }
    }

    throw "Could not find button '$Text'."
}

function Find-UiElements {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [int]$ProcessId = 0,
        [string]$Name,
        [string]$AutomationId,
        [string]$ControlType,
        [switch]$SearchProcessFallback,
        [switch]$PartialMatch,
        [int]$MaxResults = 50
    )

    $resolvedControlType = Resolve-UiControlType -ProgrammaticName $ControlType

    $elements = if (-not $PartialMatch -and -not [string]::IsNullOrWhiteSpace($ControlType) -and $null -ne $resolvedControlType) {
        @(Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $resolvedControlType -MaxResults $MaxResults)
    }
    elseif (-not $PartialMatch -and ([string]::IsNullOrWhiteSpace($ControlType) -or $null -eq $resolvedControlType)) {
        @(Find-UiElementsFast -Root $Root -Name $Name -AutomationId $AutomationId -ControlType $null -MaxResults $MaxResults)
    }
    else {
        @(Get-UiDescendants -Root $Root | Where-Object {
            ([string]::IsNullOrWhiteSpace($Name) -or (Test-UiNameMatch -Candidate $_.Current.Name -Expected $Name -PartialMatch:$PartialMatch)) -and
            ([string]::IsNullOrWhiteSpace($AutomationId) -or $_.Current.AutomationId -eq $AutomationId) -and
            ([string]::IsNullOrWhiteSpace($ControlType) -or $_.Current.ControlType.ProgrammaticName -eq $ControlType)
        } | Select-Object -First $MaxResults)
    }

    $elements = @($elements)

    if ($elements.Count -eq 0 -and $SearchProcessFallback -and $ProcessId -ne 0) {
        $elements = @(Find-UiProcessElements -ProcessId $ProcessId -Name $Name -AutomationId $AutomationId -ControlType $ControlType -PartialMatch:$PartialMatch -MaxResults $MaxResults)
    }

    $summaries = @($elements | ForEach-Object {
        Get-UiElementSummary -Element $_
    })

    $viewportBounds = Get-UiBounds -Element $Root
    $visibleSummaries = @($summaries | Where-Object { Test-UiSummaryVisible -Summary $_ -ViewportBounds $viewportBounds })
    if ($visibleSummaries.Count -gt 0) {
        return $visibleSummaries
    }

    return $summaries
}
