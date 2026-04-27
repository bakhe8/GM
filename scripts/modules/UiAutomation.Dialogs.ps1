function Get-UiDialogActionButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Dialog,
        [string[]]$PreferredLabels
    )

    foreach ($label in $PreferredLabels) {
        $buttons = @(Find-UiElementsFast -Root $Dialog -Name $label -ControlType ([System.Windows.Automation.ControlType]::Button) -MaxResults 3)

        $buttonMatches = @($buttons)
        if ($buttonMatches.Count -gt 0) {
            return $buttonMatches[0]
        }

        $namedDescendants = @(Get-UiDescendants -Root $Dialog | Where-Object {
            Test-UiNameMatch -Candidate $_.Current.Name -Expected $label
        })

        foreach ($item in $namedDescendants) {
            $clickable = Get-UiClickableAncestor -Element $item
            if ($clickable -ne $null) {
                return $clickable
            }
        }
    }

    throw "Could not find any of the dialog buttons: $($PreferredLabels -join ', ')."
}

function Test-UiElementReady {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element
    )

    try {
        if ($Element.Current.IsOffscreen) {
            return $false
        }

        if (-not $Element.Current.IsEnabled) {
            return $false
        }
    }
    catch {
        return $false
    }

    $bounds = Get-UiBounds -Element $Element
    return $bounds.Width -gt 1 -and $bounds.Height -gt 1
}

function Wait-UiElementReady {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Element,
        [int]$TimeoutSeconds = 3
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-UiElementReady -Element $Element) {
            return $true
        }

        Start-Sleep -Milliseconds 100
    }

    return $false
}

function Test-UiWindowSummaryPresent {
    param(
        [Parameter(Mandatory)]
        $WindowSummary,
        [int]$ProcessId = 0
    )

    $windows = @(Get-UiWindowsCatalog -ProcessId $ProcessId -IncludeRelatedForeground)
    $matches = @($windows | Where-Object {
        if ($WindowSummary.NativeWindowHandle -ne 0) {
            $_.NativeWindowHandle -eq $WindowSummary.NativeWindowHandle
        }
        else {
            $sameAutomation = -not [string]::IsNullOrWhiteSpace($WindowSummary.AutomationId) -and $_.AutomationId -eq $WindowSummary.AutomationId
            $sameTitle = -not [string]::IsNullOrWhiteSpace($WindowSummary.Name) -and $_.Name -eq $WindowSummary.Name
            $sameAutomation -or $sameTitle
        }
    })

    return $matches.Count -gt 0
}

function Wait-UiWindowGone {
    param(
        [Parameter(Mandatory)]
        $WindowSummary,
        [int]$ProcessId = 0,
        [int]$TimeoutSeconds = 5
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-UiWindowSummaryPresent -WindowSummary $WindowSummary -ProcessId $ProcessId)) {
            return $true
        }

        Start-Sleep -Milliseconds 200
    }

    return $false
}

function Invoke-UiDialogActionButton {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Dialog,
        [Parameter(Mandatory)]
        [string[]]$PreferredLabels,
        [int]$ProcessId = 0,
        [int]$MaxAttempts = 4,
        [int]$CloseTimeoutSeconds = 3
    )

    $dialogSummary = Get-UiElementSummary -Element $Dialog
    $lastButtonSummary = $null
    $lastError = $null
    $strategies = @("invoke", "space", "enter", "mouse")

    for ($attempt = 0; $attempt -lt [Math]::Min($MaxAttempts, $strategies.Count); $attempt++) {
        if (-not (Test-UiWindowSummaryPresent -WindowSummary $dialogSummary -ProcessId $ProcessId)) {
            return [pscustomobject]@{
                Closed = $true
                Dialog = $dialogSummary
                Button = $lastButtonSummary
                Attempt = $attempt
                Strategy = "already-closed"
                Error = $null
            }
        }

        Show-UiWindow -Window $Dialog
        Start-Sleep -Milliseconds 250

        try {
            $button = Get-UiDialogActionButton -Dialog $Dialog -PreferredLabels $PreferredLabels
            $lastButtonSummary = Get-UiElementSummary -Element $button
            [void](Wait-UiElementReady -Element $button -TimeoutSeconds 2)

            try {
                $button.SetFocus()
                Start-Sleep -Milliseconds 150
            }
            catch {
            }

            switch ($strategies[$attempt]) {
                "invoke" {
                    Invoke-UiElement -Element $button
                }
                "space" {
                    Send-UiVirtualKey -VirtualKey 0x20
                }
                "enter" {
                    Send-UiVirtualKey -VirtualKey 0x0D
                }
                "mouse" {
                    $bounds = Get-UiBounds -Element $button
                    [GuaranteeManager.UiAcceptance.NativeMethods]::SetCursorPos($bounds.CenterX, $bounds.CenterY) | Out-Null
                    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                    [GuaranteeManager.UiAcceptance.NativeMethods]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                    Start-Sleep -Milliseconds 300
                }
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        if (Wait-UiWindowGone -WindowSummary $dialogSummary -ProcessId $ProcessId -TimeoutSeconds $CloseTimeoutSeconds) {
            return [pscustomobject]@{
                Closed = $true
                Dialog = $dialogSummary
                Button = $lastButtonSummary
                Attempt = ($attempt + 1)
                Strategy = $strategies[$attempt]
                Error = $lastError
            }
        }

        Start-Sleep -Milliseconds 200
    }

    return [pscustomobject]@{
        Closed = $false
        Dialog = $dialogSummary
        Button = $lastButtonSummary
        Attempt = [Math]::Min($MaxAttempts, $strategies.Count)
        Strategy = ($strategies[([Math]::Min($MaxAttempts, $strategies.Count) - 1)])
        Error = $lastError
    }
}

function Get-UiActiveDialog {
    param(
        [int]$ProcessId = 0
    )

    $foregroundWindow = Get-UiForegroundWindowElement
    if ($null -ne $foregroundWindow) {
        $isSameProcess = $false
        $isRelatedExternal = $false
        try {
            $isSameProcess = ($ProcessId -eq 0 -or $foregroundWindow.Current.ProcessId -eq $ProcessId)
            if (-not $isSameProcess -and $ProcessId -ne 0) {
                $isRelatedExternal = Test-UiExternalWindowRelatedToProcess -Window $foregroundWindow -ProcessId $ProcessId
            }
        }
        catch {
        }

        try {
            if (($isSameProcess -or $isRelatedExternal) -and
                $foregroundWindow.Current.AutomationId -ne "Shell.MainWindow" -and
                -not [string]::IsNullOrWhiteSpace($foregroundWindow.Current.Name)) {
                return $foregroundWindow
            }
        }
        catch {
        }
    }

    $windows = @(Get-UiRelatedTopLevelWindows -ProcessId $ProcessId)
    if ($windows.Count -eq 0) {
        throw "No top-level windows were found for the target process."
    }

    $dialogs = @($windows | Where-Object {
        $_.Current.NativeWindowHandle -ne 0 -and
        $_.Current.AutomationId -ne "Shell.MainWindow" -and
        -not [string]::IsNullOrWhiteSpace($_.Current.Name)
    } | Sort-Object { (Get-UiBounds -Element $_).Width })

    if ($dialogs.Count -gt 0) {
        return $dialogs[0]
    }

    throw "No active dialog is currently open for the target process."
}
