function Resolve-UiActionTarget {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [string]$Name = "",
        [string]$AutomationId = "",
        [string]$Text = "",
        [switch]$PartialMatch,
        [int]$InitialTimeoutSeconds = 1,
        [int]$FallbackTimeoutSeconds = 5
    )

    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        return Get-UiButtonByText -Root $Window -Text $Text -ProcessId $Process.Id -SearchProcessFallback -PartialMatch:$PartialMatch
    }

    try {
        return Wait-UiElement `
            -Root $Window `
            -Name $Name `
            -AutomationId $AutomationId `
            -ControlType $null `
            -TimeoutSeconds $InitialTimeoutSeconds `
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
        -Root $Window `
        -Name $Name `
        -AutomationId $AutomationId `
        -ControlType $null `
        -TimeoutSeconds $FallbackTimeoutSeconds `
        -PartialMatch:$PartialMatch
}

function Resolve-UiVirtualKey {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    switch ($Name.Trim().ToLowerInvariant()) {
        "enter" { return [byte]0x0D }
        "escape" { return [byte]0x1B }
        "esc" { return [byte]0x1B }
        "tab" { return [byte]0x09 }
        "down" { return [byte]0x28 }
        "up" { return [byte]0x26 }
        "left" { return [byte]0x25 }
        "right" { return [byte]0x27 }
        default { throw "Unsupported key '$Name'. Supported keys: Enter, Escape, Tab, Up, Down, Left, Right." }
    }
}

function Test-UiMouseUsesMainWindowFlow {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Options
    )

    $hasAbsolutePoint = ($Options.X -ne [int]::MinValue) -and ($Options.Y -ne [int]::MinValue)
    if ($hasAbsolutePoint) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($Options.WindowTitle) -and [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
        return $true
    }

    if ($Options.WindowAutomationId -eq "Shell.MainWindow") {
        return $true
    }

    return $false
}

function Invoke-UiExploreAction {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Options
    )

    switch ($Options.Action) {
        "HostState" {
            $sessionState = Invoke-UiCapabilityBrokerSweep -Persist
            $mediaSession = Invoke-UiMediaBrokerSweep -Persist
            return [pscustomobject]@{
                Action = "HostState"
                SessionPath = Get-UiCapabilitySessionPath
                ObservationsPath = Get-UiCapabilityObservationPath
                CapabilitySession = $sessionState
                MediaSessionPath = Get-UiMediaSessionPath
                MediaSession = $mediaSession
                MediaScopeView = Get-UiMediaScopeView -SessionState $mediaSession
                MediaProviders = [object[]]@(Get-UiMediaProviderCatalog)
                CapabilityDefinitions = [object[]]@(Get-UiCapabilityDefinitions)
                RecentCapabilityObservations = [object[]]@(Get-UiCapabilityObservationEntries -MaxCount $Options.MaxResults)
                RecentCapabilityDecisions = if ($null -ne $sessionState) { [object[]]@($sessionState.RecentDecisions | Select-Object -First $Options.MaxResults) } else { @() }
                CapabilityOperatorView = Get-UiCapabilityOperatorView -SessionState $sessionState
            }
        }

        "MediaState" {
            $mediaSession = Invoke-UiMediaBrokerSweep -Persist
            return [pscustomobject]@{
                Action = "MediaState"
                MediaSessionPath = Get-UiMediaSessionPath
                MediaSession = $mediaSession
                MediaScopeView = Get-UiMediaScopeView -SessionState $mediaSession
                MediaProviders = [object[]]@(Get-UiMediaProviderCatalog)
                PreferredVideoProvider = Get-UiPreferredMediaProvider -Kind "Video"
                PreferredAudioProvider = Get-UiPreferredMediaProvider -Kind "Audio"
            }
        }

        "CapabilityOn" {
            if ([string]::IsNullOrWhiteSpace($Options.CapabilityName)) {
                throw "CapabilityOn requires -CapabilityName."
            }

            $existingProcess = if ($Options.ProcessId -ne 0) {
                Get-Process -Id $Options.ProcessId -ErrorAction Stop
            }
            else {
                Get-UiProcess
            }

            $enabled = Enable-UiCapability `
                -CapabilityName $Options.CapabilityName `
                -ProcessId $(if ($null -ne $existingProcess) { $existingProcess.Id } else { 0 }) `
                -Reason $Options.Reason `
                -LeaseMilliseconds $Options.LeaseMilliseconds

            return [pscustomobject]@{
                Action = "CapabilityOn"
                ProcessId = if ($null -ne $existingProcess) { $existingProcess.Id } else { 0 }
                CapabilityName = $Options.CapabilityName
                Capability = $enabled.Capability
                CapabilitySession = $enabled.Session
            }
        }

        "CapabilityOff" {
            if ([string]::IsNullOrWhiteSpace($Options.CapabilityName)) {
                throw "CapabilityOff requires -CapabilityName."
            }

            $sessionState = Disable-UiCapability -CapabilityName $Options.CapabilityName -Reason $Options.Reason
            return [pscustomobject]@{
                Action = "CapabilityOff"
                CapabilityName = $Options.CapabilityName
                CapabilitySession = $sessionState
            }
        }
    }

    $process = Get-ResolvedProcess -RepoRoot $Options.RepoRoot -ProcessId $Options.ProcessId -ReuseRunningSession:$Options.ReuseRunningSession

    switch ($Options.Action) {
        "Launch" {
            $window = Resolve-UiWindow -ProcessId $process.Id
            Show-UiWindow -Window $window
            return [pscustomobject]@{
                ProcessId = $process.Id
                MainWindowHandle = $process.MainWindowHandle
                Window = Get-UiElementSummary -Element $window
            }
        }

        "VideoOn" {
            $sessionState = Start-UiVideoCaptureSidecar -ProcessId $process.Id -Reason $Options.Reason -LeaseMilliseconds $Options.LeaseMilliseconds
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "VideoOn"
                MediaSession = $sessionState
            }
        }

        "VideoOff" {
            $sessionState = Stop-UiVideoCaptureSidecar -Reason $Options.Reason
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "VideoOff"
                MediaSession = $sessionState
            }
        }

        "AudioOn" {
            $sessionState = Start-UiAudioCaptureSidecar -Reason $Options.Reason -LeaseMilliseconds $Options.LeaseMilliseconds
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "AudioOn"
                MediaSession = $sessionState
            }
        }

        "AudioOff" {
            $sessionState = Stop-UiAudioCaptureSidecar -Reason $Options.Reason
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "AudioOff"
                MediaSession = $sessionState
            }
        }

        "Windows" {
            $windows = Get-UiWindowsCatalog -ProcessId $process.Id -IncludeRelatedForeground
            return [pscustomobject]@{
                ProcessId = $process.Id
                Windows = [object[]]$windows
            }
        }

        "Elements" {
            $window = Resolve-UiScopeRoot -Process $process -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -PartialMatch:$Options.PartialMatch
            $elements = Find-UiElements -Root $window -ProcessId $process.Id -Name $Options.Name -AutomationId $Options.AutomationId -ControlType $Options.ControlType -SearchProcessFallback -PartialMatch:$Options.PartialMatch -MaxResults $Options.MaxResults
            return [pscustomobject]@{
                ProcessId = $process.Id
                Window = Get-UiElementSummary -Element $window
                Elements = [object[]]$elements
            }
        }

        "Sidebar" {
            Assert-UiNoBlockingWindows -Process $process
            $window = Resolve-UiWindow -ProcessId $process.Id
            if ([string]::IsNullOrWhiteSpace($Options.Label) -and [string]::IsNullOrWhiteSpace($Options.AutomationId)) {
                throw "Sidebar requires either -Label or -AutomationId."
            }

            Invoke-UiSidebarNavigation -MainWindow $window -WorkspaceLabel $Options.Label -SidebarAutomationId $Options.AutomationId
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Sidebar"
                Label = $Options.Label
                AutomationId = $Options.AutomationId
                Window = Get-UiElementSummary -Element $window
            }
        }

        "Click" {
            if ([string]::IsNullOrWhiteSpace($Options.WindowTitle) -and [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Assert-UiNoBlockingWindows -Process $process
            }
            elseif ($Options.WindowAutomationId -eq "Shell.MainWindow") {
                Assert-UiNoBlockingWindows -Process $process
            }

            $window = Resolve-UiScopeRoot -Process $process -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -PartialMatch:$Options.PartialMatch
            $target = Resolve-UiActionTarget -Process $process -Window $window -Name $Options.Name -AutomationId $Options.AutomationId -Text $Options.Text -PartialMatch:$Options.PartialMatch
            $targetSummary = Get-UiElementSummary -Element $target
            Invoke-UiElement -Element $target
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Click"
                Target = $targetSummary
            }
        }

        "SetField" {
            if ([string]::IsNullOrWhiteSpace($Options.WindowTitle) -and [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Assert-UiNoBlockingWindows -Process $process
            }
            elseif ($Options.WindowAutomationId -eq "Shell.MainWindow") {
                Assert-UiNoBlockingWindows -Process $process
            }

            $window = Resolve-UiScopeRoot -Process $process -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -PartialMatch:$Options.PartialMatch
            $field = if (-not [string]::IsNullOrWhiteSpace($Options.AutomationId)) {
                Wait-UiElement -Root $window -AutomationId $Options.AutomationId -ControlType $null -TimeoutSeconds 5 -PartialMatch:$Options.PartialMatch
            }
            elseif (-not [string]::IsNullOrWhiteSpace($Options.Label)) {
                Get-UiEditNearLabel -Window $window -Label $Options.Label
            }
            else {
                throw "SetField requires either -Label or -AutomationId."
            }

            Set-UiElementValue -Element $field -Value $Options.Value
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "SetField"
                Label = $Options.Label
                Value = $Options.Value
                Field = Get-UiElementSummary -Element $field
            }
        }

        "MouseMove" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Move-UiMouse `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseMove"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
            }
        }

        "MouseClick" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseClick `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseClick"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
                Button = $result.Button
                ClickCount = $result.ClickCount
            }
        }

        "MouseRightClick" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseRightClick `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseRightClick"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
                Button = $result.Button
                ClickCount = $result.ClickCount
            }
        }

        "MouseDoubleClick" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseDoubleClick `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseDoubleClick"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
                Button = $result.Button
                ClickCount = $result.ClickCount
            }
        }

        "MouseHover" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseHover `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -HoverMilliseconds $Options.HoverMilliseconds `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseHover"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
                HoverMilliseconds = $result.HoverMilliseconds
            }
        }

        "MouseDrag" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseDrag `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -DeltaX $Options.DeltaX `
                -DeltaY $Options.DeltaY `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseDrag"
                Window = $result.Window
                Target = $result.Target
                StartPosition = $result.StartPosition
                EndPosition = $result.EndPosition
                DeltaX = $result.DeltaX
                DeltaY = $result.DeltaY
            }
        }

        "MouseScroll" {
            if (Test-UiMouseUsesMainWindowFlow -Options $Options) {
                Assert-UiNoBlockingWindows -Process $process
            }

            $result = Invoke-UiMouseScroll `
                -ProcessId $process.Id `
                -WindowTitle $Options.WindowTitle `
                -WindowAutomationId $Options.WindowAutomationId `
                -Name $Options.Name `
                -AutomationId $Options.AutomationId `
                -Text $Options.Text `
                -X $Options.X `
                -Y $Options.Y `
                -OffsetX $Options.OffsetX `
                -OffsetY $Options.OffsetY `
                -ScrollDelta $Options.ScrollDelta `
                -PartialMatch:$Options.PartialMatch

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "MouseScroll"
                Window = $result.Window
                Target = $result.Target
                Position = $result.Position
                ScrollDelta = $result.ScrollDelta
            }
        }

        "WaitWindow" {
            $window = Resolve-UiWindow -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -ProcessId $process.Id -PartialMatch:$Options.PartialMatch
            Show-UiWindow -Window $window
            return [pscustomobject]@{
                ProcessId = $process.Id
                Window = Get-UiElementSummary -Element $window
            }
        }

        "WaitWindowClosed" {
            $closed = Wait-UiWindowMatchClosed `
                -Process $process `
                -Title $Options.WindowTitle `
                -AutomationId $Options.WindowAutomationId `
                -TimeoutSeconds 10 `
                -PartialMatch:$Options.PartialMatch

            if (-not $closed) {
                $windowLabel = if (-not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                    "automation id '$($Options.WindowAutomationId)'"
                }
                else {
                    "title '$($Options.WindowTitle)'"
                }

                throw "النافذة المستهدفة ($windowLabel) بقيت مفتوحة بعد مهلة الانتظار."
            }

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "WaitWindowClosed"
                WindowTitle = $Options.WindowTitle
                WindowAutomationId = $Options.WindowAutomationId
                Closed = $closed
            }
        }

        "Capture" {
            $window = if (-not [string]::IsNullOrWhiteSpace($Options.WindowTitle) -or -not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Resolve-UiWindow -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -ProcessId $process.Id -PartialMatch:$Options.PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            $resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $Options.RepoRoot $Options.OutputPath))
            Save-UiWindowScreenshot -Window $window -Path $resolvedOutput | Out-Null
            return [pscustomobject]@{
                ProcessId = $process.Id
                Capture = $resolvedOutput
                Window = Get-UiElementSummary -Element $window
            }
        }

        "DialogAction" {
            $dialog = if (-not [string]::IsNullOrWhiteSpace($Options.WindowTitle) -or -not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Resolve-UiWindow -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -ProcessId $process.Id -PartialMatch:$Options.PartialMatch
            }
            else {
                Get-UiActiveDialog -ProcessId $process.Id
            }

            $preferredLabels = if (-not [string]::IsNullOrWhiteSpace($Options.Text) -or -not [string]::IsNullOrWhiteSpace($Options.Name)) {
                @($Options.Text, $Options.Name, "إلغاء", "Cancel", "&Cancel", "موافق", "OK", "نعم", "Yes", "&Yes", "لا", "No", "&No")
            }
            else {
                @("Yes", "&Yes", "OK", "موافق", "نعم", "إلغاء", "Cancel", "&Cancel", "لا", "No", "&No")
            }

            $resolvedLabels = @($preferredLabels | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
            $button = Get-UiDialogActionButton -Dialog $dialog -PreferredLabels $resolvedLabels
            $dialogSummary = Get-UiElementSummary -Element $dialog
            $buttonSummary = Get-UiElementSummary -Element $button
            $result = Invoke-UiDialogActionButton -Dialog $dialog -PreferredLabels $resolvedLabels -ProcessId $process.Id -CloseTimeoutSeconds 3
            if (-not $result.Closed) {
                throw "تم الوصول إلى زر داخل الحوار '$($dialogSummary.Name)' لكن النافذة بقيت مفتوحة بعد $(if ($null -ne $result.Attempt) { $result.Attempt } else { 0 }) محاولات. آخر أسلوب: $($result.Strategy)."
            }

            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "DialogAction"
                Dialog = $dialogSummary
                Button = if ($null -ne $result.Button) { $result.Button } else { $buttonSummary }
                Closed = $result.Closed
                Strategy = $result.Strategy
                Attempt = $result.Attempt
            }
        }

        "Events" {
            $events = @(Get-UiRecentEvents -MaxCount $Options.MaxResults | Where-Object {
                ([string]::IsNullOrWhiteSpace($Options.Category) -or $_.Category -eq $Options.Category) -and
                ([string]::IsNullOrWhiteSpace($Options.EventActionName) -or $_.Action -eq $Options.EventActionName)
            })

            return [pscustomobject]@{
                ProcessId = $process.Id
                Category = $Options.Category
                EventActionName = $Options.EventActionName
                Events = [object[]]$events
            }
        }

        "Key" {
            $window = if (-not [string]::IsNullOrWhiteSpace($Options.WindowTitle) -or -not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Resolve-UiScopeRoot -Process $process -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -PartialMatch:$Options.PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            Show-UiWindow -Window $window
            $windowSummary = Get-UiElementSummary -Element $window
            $targetElement = $null
            if (-not [string]::IsNullOrWhiteSpace($Options.Text) -or -not [string]::IsNullOrWhiteSpace($Options.AutomationId) -or -not [string]::IsNullOrWhiteSpace($Options.Name)) {
                $targetElement = Resolve-UiActionTarget -Process $process -Window $window -Name $Options.Name -AutomationId $Options.AutomationId -Text $Options.Text -PartialMatch:$Options.PartialMatch
            }

            $targetSummary = if ($null -ne $targetElement) { Get-UiElementSummary -Element $targetElement } else { $null }
            if ($null -ne $targetElement) {
                Invoke-UiElement -Element $targetElement
                Start-Sleep -Milliseconds 100
            }

            $vk = Resolve-UiVirtualKey -Name $Options.KeyName
            Send-UiVirtualKey -VirtualKey $vk
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Key"
                KeyName = $Options.KeyName
                Window = $windowSummary
                Target = $targetSummary
            }
        }

        "SendKeys" {
            $window = if (-not [string]::IsNullOrWhiteSpace($Options.WindowTitle) -or -not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Resolve-UiWindow -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -ProcessId $process.Id -PartialMatch:$Options.PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            Show-UiWindow -Window $window
            $windowSummary = Get-UiElementSummary -Element $window
            $targetElement = $null
            if (-not [string]::IsNullOrWhiteSpace($Options.Text) -or -not [string]::IsNullOrWhiteSpace($Options.AutomationId) -or -not [string]::IsNullOrWhiteSpace($Options.Name)) {
                $targetElement = Resolve-UiActionTarget -Process $process -Window $window -Name $Options.Name -AutomationId $Options.AutomationId -Text $Options.Text -PartialMatch:$Options.PartialMatch
            }

            $targetSummary = if ($null -ne $targetElement) { Get-UiElementSummary -Element $targetElement } else { $null }
            if ($null -ne $targetElement) {
                Invoke-UiElement -Element $targetElement
                Start-Sleep -Milliseconds 100
            }

            $keysText = if (-not [string]::IsNullOrWhiteSpace($Options.Value)) { $Options.Value } else { $Options.Text }
            if ([string]::IsNullOrWhiteSpace($keysText)) {
                throw "SendKeys requires -Value or -Text."
            }

            Send-UiSendKeys -KeysText $keysText
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "SendKeys"
                KeysText = $keysText
                Window = $windowSummary
                Target = $targetSummary
            }
        }

        "State" {
            return Get-ProbePayload -Process $process -RepoRoot $Options.RepoRoot -OutputPath $Options.OutputPath -MaxResults $Options.MaxResults -WithCapture
        }

        "Diagnostics" {
            return Get-ProbePayload -Process $process -RepoRoot $Options.RepoRoot -OutputPath $Options.OutputPath -MaxResults $Options.MaxResults -WithCapture:$Options.IncludeCapture
        }

        "Probe" {
            return Get-ProbePayload -Process $process -RepoRoot $Options.RepoRoot -OutputPath $Options.OutputPath -MaxResults $Options.MaxResults -WithCapture:$Options.IncludeCapture
        }

        "Compare" {
            if ([string]::IsNullOrWhiteSpace($Options.ReferencePath)) {
                throw "Compare requires -ReferencePath."
            }

            $window = if (-not [string]::IsNullOrWhiteSpace($Options.WindowTitle) -or -not [string]::IsNullOrWhiteSpace($Options.WindowAutomationId)) {
                Resolve-UiWindow -Title $Options.WindowTitle -AutomationId $Options.WindowAutomationId -ProcessId $process.Id -PartialMatch:$Options.PartialMatch
            }
            else {
                Resolve-UiWindow -ProcessId $process.Id
            }

            $resolvedCapture = [System.IO.Path]::GetFullPath((Join-Path $Options.RepoRoot $Options.OutputPath))
            $resolvedReference = [System.IO.Path]::GetFullPath((Join-Path $Options.RepoRoot $Options.ReferencePath))
            $resolvedDiff = [System.IO.Path]::GetFullPath((Join-Path $Options.RepoRoot $Options.DiffOutputPath))
            Save-UiWindowScreenshot -Window $window -Path $resolvedCapture | Out-Null
            $comparison = Compare-UiImages -ReferencePath $resolvedReference -ActualPath $resolvedCapture -DiffPath $resolvedDiff -Tolerance $Options.Tolerance -SampleStep $Options.SampleStep
            return [pscustomobject]@{
                ProcessId = $process.Id
                Action = "Compare"
                Window = Get-UiElementSummary -Element $window
                Comparison = $comparison
            }
        }
    }
}
