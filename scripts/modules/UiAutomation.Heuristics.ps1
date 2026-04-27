function Get-UiExplorationHeuristicDefinitions {
    return @(
        [pscustomobject]@{
            Name = "runtime-fault"
            Priority = 100
            DefaultCooldownMs = 3200
            RecommendedCapability = "FaultWatch"
            Summary = "عند ظهور fault موثوقة من داخل التطبيق، نعطي الأولوية لإشارة البرنامج نفسها بدل أي أثر جانبي عام."
            SuggestedActions = @("FaultState", "HostState", "Windows")
        },
        [pscustomobject]@{
            Name = "external-window"
            Priority = 90
            DefaultCooldownMs = 2400
            RecommendedCapability = "BurstCapture"
            Summary = "عند ظهور نافذة خارجية مرتبطة بالبرنامج، نحتاج evidence بصرية سريعة وحكمًا أوضح على من أخذ foreground."
            SuggestedActions = @("Windows", "WaitWindow", "WaitWindowClosed", "MouseClick")
        },
        [pscustomobject]@{
            Name = "stubborn-control"
            Priority = 80
            DefaultCooldownMs = 2600
            RecommendedCapability = "MouseTrace"
            Summary = "عند عناد عنصر أو فشل مسار UIA، نتحول إلى سلوك أقرب للتفاعل البشري ونراقب اليد نفسها."
            SuggestedActions = @("MouseMove", "MouseClick", "MouseHover", "Elements")
        },
        [pscustomobject]@{
            Name = "visual-anomaly"
            Priority = 65
            DefaultCooldownMs = 2200
            RecommendedCapability = "BurstCapture"
            Summary = "عند بطء أو غموض بصري، نلتقط burst خفيفة بدل الانتقال مباشرة إلى وسائط أثقل."
            SuggestedActions = @("Probe", "Compare", "CapabilityOn")
        }
    )
}

function Resolve-UiExplorationHeuristicDefinition {
    param(
        [Parameter(Mandatory)]
        [string]$HeuristicName
    )

    $definition = @(Get-UiExplorationHeuristicDefinitions | Where-Object {
        [string]::Equals([string]$_.Name, $HeuristicName, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)

    if ($definition.Count -eq 0) {
        throw "Unknown exploration heuristic '$HeuristicName'."
    }

    return $definition[0]
}

function Get-UiRecentHeuristicDecisions {
    param(
        $SessionState = $null,
        [int]$MaxCount = 8
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiCapabilitySessionState
    }

    if ($null -eq $SessionState) {
        return @()
    }

    return @($SessionState.RecentDecisions | Where-Object {
        [string]::Equals([string]$_.CapabilityName, "ExplorationAssist", [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First $MaxCount)
}

function Get-UiRecentHeuristicObservations {
    param(
        $SessionState = $null,
        [int]$MaxCount = 8
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiCapabilitySessionState
    }

    if ($null -eq $SessionState) {
        return @()
    }

    return @($SessionState.RecentObservations | Where-Object {
        [string]::Equals([string]$_.CapabilityName, "ExplorationAssist", [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First $MaxCount)
}

function Get-UiRecentHeuristicDecision {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$HeuristicName,
        [string[]]$DecisionKinds = @("triggered", "guided")
    )

    return @((Get-UiRecentHeuristicDecisions -SessionState $SessionState -MaxCount 12) | Where-Object {
        ($DecisionKinds -contains [string]$_.Decision) -and
        ($null -ne $_.Payload) -and
        [string]::Equals([string]$_.Payload.StrategyName, $HeuristicName, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1)
}

function Get-UiHeuristicCooldownState {
    param(
        [Parameter(Mandatory)]
        $SessionState,
        [Parameter(Mandatory)]
        [string]$HeuristicName,
        [int]$CooldownMs = 0
    )

    if ($CooldownMs -le 0) {
        return [pscustomobject]@{
            IsActive = $false
            RemainingMs = 0
            LastDecision = $null
        }
    }

    $lastDecision = @(Get-UiRecentHeuristicDecision -SessionState $SessionState -HeuristicName $HeuristicName | Select-Object -First 1)
    if ($lastDecision.Count -eq 0) {
        return [pscustomobject]@{
            IsActive = $false
            RemainingMs = 0
            LastDecision = $null
        }
    }

    $elapsedMs = ([DateTimeOffset]::Now - [DateTimeOffset]::Parse([string]$lastDecision[0].Timestamp)).TotalMilliseconds
    $remainingMs = [Math]::Max(0, [int][Math]::Ceiling($CooldownMs - $elapsedMs))
    return [pscustomobject]@{
        IsActive = $remainingMs -gt 0
        RemainingMs = $remainingMs
        LastDecision = $lastDecision[0]
    }
}

function Test-UiStubbornControlFailure {
    param(
        [Parameter(Mandatory)]
        [string]$ActionName,
        [string]$ErrorMessage = ""
    )

    if ([string]::IsNullOrWhiteSpace($ErrorMessage)) {
        return $false
    }

    $interactiveActions = @("Click", "SetField", "Key", "SendKeys", "Elements", "MouseClick", "MouseDoubleClick")
    if ($ActionName -notin $interactiveActions) {
        return $false
    }

    return $true
}

function New-UiHeuristicRecommendation {
    param(
        [Parameter(Mandatory)]
        [string]$HeuristicName,
        [Parameter(Mandatory)]
        [string]$Summary,
        [Parameter(Mandatory)]
        [string]$Rationale,
        [Parameter(Mandatory)]
        [string[]]$SuggestedActions,
        [Parameter(Mandatory)]
        [string]$RecommendedCapability,
        [hashtable]$Signals = @{},
        [string]$AutoIntervention = "none"
    )

    $definition = Resolve-UiExplorationHeuristicDefinition -HeuristicName $HeuristicName
    return [pscustomobject]@{
        StrategyName = $definition.Name
        Priority = [int]$definition.Priority
        DefaultCooldownMs = [int]$definition.DefaultCooldownMs
        Summary = $Summary
        Rationale = $Rationale
        SuggestedActions = [object[]]$SuggestedActions
        RecommendedCapability = $RecommendedCapability
        AutoIntervention = $AutoIntervention
        Signals = [pscustomobject]$Signals
    }
}

function Get-UiExplorationRecommendations {
    param(
        [int]$ProcessId = 0,
        [string]$ActionName = "",
        [double]$DurationMs = 0,
        [string]$ErrorMessage = "",
        [switch]$OnFailure
    )

    $process = if ($ProcessId -gt 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
    $health = if ($null -ne $process) { Get-UiCapabilityWindowHealth -Process $process } else { $null }
    $recentFaultSignals = @()
    if ($null -ne $process) {
        $recentFaultSignals = @(Get-UiRecentFaultSignals -ProcessId $process.Id -MaxCount 6)
    }

    $recommendations = New-Object System.Collections.Generic.List[object]
    $trustedFaultSignals = New-Object System.Collections.Generic.List[object]
    foreach ($faultSignal in @($recentFaultSignals)) {
        if (-not [bool]$faultSignal.TrustedForReasoning) {
            continue
        }

        $isRecent = $true
        try {
            $isRecent = (([DateTimeOffset]::Now - [DateTimeOffset]::Parse([string]$faultSignal.Timestamp)).TotalSeconds -le 12)
        }
        catch {
            $isRecent = $true
        }

        if ($isRecent) {
            [void]$trustedFaultSignals.Add($faultSignal)
        }
    }

    $trustedFaultSignals = @($trustedFaultSignals.ToArray())
    if ($trustedFaultSignals.Count -gt 0) {
        $topSignal = $trustedFaultSignals[0]
        [void]$recommendations.Add((New-UiHeuristicRecommendation `
                -HeuristicName "runtime-fault" `
                -Summary "ظهرت fault موثوقة من داخل البرنامج، لذلك هذا هو المسار الأوضح لفهم ما حدث." `
                -Rationale "الإشارة جاءت من التطبيق نفسه أو من log scoped، وهذا أنظف من انتظار side effect عامة." `
                -SuggestedActions @("FaultState", "HostState", "Windows") `
                -RecommendedCapability "FaultWatch" `
                -AutoIntervention "fault-watch-direct" `
                -Signals @{
                    Title = [string]$topSignal.Title
                    Kind = [string]$topSignal.Kind
                    SignalCount = $trustedFaultSignals.Count
                }))
    }

    if ($null -ne $health -and $null -ne $health.ExternalForegroundWindow) {
        [void]$recommendations.Add((New-UiHeuristicRecommendation `
                -HeuristicName "external-window" `
                -Summary "ظهرت نافذة خارجية مرتبطة بالبرنامج، لذلك نحتاج حكمًا بصريًا أسرع على foreground والسياق." `
                -Rationale "وجود نافذة cross-process فوق المسار الحالي قد يغير ما نراه أو ما يتلقى الإدخال." `
                -SuggestedActions @("Windows", "WaitWindow", "WaitWindowClosed", "MouseClick") `
                -RecommendedCapability "BurstCapture" `
                -AutoIntervention "burst-capture-direct" `
                -Signals @{
                    WindowTitle = [string]$health.ExternalForegroundWindow.Name
                    WindowAutomationId = [string]$health.ExternalForegroundWindow.AutomationId
                }))
    }

    if ($OnFailure -and (Test-UiStubbornControlFailure -ActionName $ActionName -ErrorMessage $ErrorMessage)) {
        [void]$recommendations.Add((New-UiHeuristicRecommendation `
                -HeuristicName "stubborn-control" `
                -Summary "هذا المسار يبدو عنيدًا أو خارج تغطية UIA المريحة، لذلك الأفضل أن نقرّب التفاعل من يد المستخدم." `
                -Rationale "عندما يفشل المسار النصي أو العنصري، يكون تتبع اليد والانتقال إلى الماوس أوضح من تكرار نفس الأسلوب." `
                -SuggestedActions @("MouseMove", "MouseClick", "MouseHover", "Elements") `
                -RecommendedCapability "MouseTrace" `
                -AutoIntervention "mouse-trace-arm" `
                -Signals @{
                    Error = $ErrorMessage
                    Action = $ActionName
                }))
    }

    $thresholds = Get-UiLatencyThresholds -ActionName $ActionName
    if ($DurationMs -ge [double]$thresholds.AcceptableMs) {
        [void]$recommendations.Add((New-UiHeuristicRecommendation `
                -HeuristicName "visual-anomaly" `
                -Summary "الإجراء الحالي أبطأ من الإيقاع المقبول، لذلك burst خفيفة الآن أرجح من فتح وسائط أثقل." `
                -Rationale "البطء أو الغموض البصري غالبًا يحتاج sequence قصيرة مرتبطة بالفعل نفسه." `
                -SuggestedActions @("Probe", "Compare", "CapabilityOn") `
                -RecommendedCapability "BurstCapture" `
                -AutoIntervention "burst-capture-direct" `
                -Signals @{
                    Action = $ActionName
                    DurationMs = [math]::Round($DurationMs, 2)
                    AcceptableMs = [int]$thresholds.AcceptableMs
                    SlowMs = [int]$thresholds.SlowMs
                }))
    }

    return @($recommendations | Sort-Object Priority -Descending)
}

function Get-UiHeuristicOperatorView {
    param(
        $SessionState = $null
    )

    if ($null -eq $SessionState) {
        $SessionState = Get-UiCapabilitySessionState
    }

    $activeExplorationAssist = $false
    if ($null -ne $SessionState) {
        $activeExplorationAssist = @($SessionState.ActiveCapabilities | Where-Object {
            [string]::Equals([string]$_.Name, "ExplorationAssist", [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
    }

    $recentDecisions = @(Get-UiRecentHeuristicDecisions -SessionState $SessionState -MaxCount 5)
    $lastDecision = if ($recentDecisions.Count -gt 0) { $recentDecisions[0] } else { $null }

    if (-not $activeExplorationAssist -and $null -eq $lastDecision) {
        return [pscustomobject]@{
            Status = "idle"
            Summary = "طبقة heuristics غير نشطة الآن؛ القرارات كلها يدوية أو صادرة من capabilities نفسها."
            SecondarySummary = "فعّل ExplorationAssist فقط عندما تريد من الأداة أن تختار modality أنسب أثناء نفس الاستكشاف."
            Guidance = "استخدم CapabilityOn -CapabilityName ExplorationAssist عندما تريد تنسيقًا أهدأ بين النظر واليد وfault sensing."
            Active = $false
            LastDecision = $null
            RecentDecisions = @()
            DecisionDigest = @()
        }
    }

    $status = "monitoring"
    if ($null -ne $lastDecision) {
        switch ([string]$lastDecision.Decision) {
            "triggered" { $status = "intervened"; break }
            "guided" { $status = "guided"; break }
            "suppressed" { $status = "cooling-down"; break }
            default { $status = if ($activeExplorationAssist) { "monitoring" } else { "calm" } }
        }
    }
    elseif (-not $activeExplorationAssist) {
        $status = "calm"
    }

    $summary = ""
    $secondarySummary = ""
    $guidance = ""
    switch ($status) {
        "intervened" {
            $summary = "طبقة heuristics اختارت modality أنسب لهذا الفرع وتدخلت بلطف من غير أن تقطع الاستكشاف."
            $secondarySummary = if ($null -ne $lastDecision) { [string]$lastDecision.Summary } else { "حدث تدخل heuristic حديث." }
            $guidance = "راجع آخر decision سريعًا، ثم واصل من نفس المسار ولا تغيّر أسلوبك إلا إذا بقي الغموض قائمًا."
            break
        }
        "guided" {
            $summary = "طبقة heuristics رأت أن أفضل خطوة الآن هي تغيير الأسلوب، لا تكرار نفس المسار."
            $secondarySummary = if ($null -ne $lastDecision) { [string]$lastDecision.Summary } else { "هناك guidance heuristic حديثة." }
            $guidance = "اتبع الـ suggested actions الظاهرة في القرار الأخير إذا بقي العنصر أو المسار عنيدًا."
            break
        }
        "cooling-down" {
            $summary = "طبقة heuristics في تهدئة قصيرة حتى لا تعيد نفس التدخل بشكل مزعج."
            $secondarySummary = if ($null -ne $lastDecision) { [string]$lastDecision.Summary } else { "هناك suppression heuristic حديثة." }
            $guidance = "أكمل المسار طبيعيًا؛ إذا استمرت العلامة نفسها بعد المهلة سنعيد النظر من جديد."
            break
        }
        "monitoring" {
            $summary = "طبقة heuristics تراقب الآن بهدوء وتنتظر لحظة تحتاج فيها لتبديل modality."
            $secondarySummary = "لا يوجد تدخل قسري الآن؛ فقط تنسيق خفيف بين القدرات إذا لزم."
            $guidance = "واصل الاستكشاف بحرية؛ الطبقة ستتدخل فقط عندما يصبح التبديل بين الأساليب أوضح من الاستمرار على نفس النمط."
            break
        }
        default {
            $summary = "لا توجد guidance heuristics حديثة الآن."
            $secondarySummary = "الوضع الحالي calm ولا توجد حاجة لتدخل تكتيكي إضافي."
            $guidance = "استمر على نفس الإيقاع الخفيف ما دامت الأدلة الحالية كافية."
        }
    }

    $decisionDigest = @($recentDecisions | ForEach-Object {
        $strategyName = if ($null -ne $_.Payload -and $_.Payload.PSObject.Properties.Name -contains "StrategyName") { [string]$_.Payload.StrategyName } else { "" }
        [pscustomobject]@{
            StrategyName = $strategyName
            Decision = [string]$_.Decision
            Action = [string]$_.Action
            Summary = [string]$_.Summary
            Headline = ("$strategyName -> $([string]$_.Decision): $([string]$_.Summary)")
        }
    })

    return [pscustomobject]@{
        Status = $status
        Summary = $summary
        SecondarySummary = $secondarySummary
        Guidance = $guidance
        Active = $activeExplorationAssist
        LastDecision = $lastDecision
        RecentDecisions = [object[]]$recentDecisions
        DecisionDigest = [object[]]$decisionDigest
    }
}

function Get-UiHeuristicStatePayload {
    param(
        [int]$ProcessId = 0,
        [int]$MaxResults = 10
    )

    $process = if ($ProcessId -gt 0) { Get-Process -Id $ProcessId -ErrorAction SilentlyContinue } else { Get-UiProcess }
    $effectiveProcessId = if ($null -ne $process) { $process.Id } else { 0 }
    $sessionState = Get-UiCapabilitySessionState
    $recommendations = @(Get-UiExplorationRecommendations -ProcessId $effectiveProcessId)

    return [pscustomobject]@{
        ProcessId = $effectiveProcessId
        HeuristicDefinitions = [object[]]@(Get-UiExplorationHeuristicDefinitions)
        Recommendations = [object[]]@($recommendations | Select-Object -First $MaxResults)
        PrimaryRecommendation = if ($recommendations.Count -gt 0) { $recommendations[0] } else { $null }
        RecentHeuristicDecisions = [object[]]@(Get-UiRecentHeuristicDecisions -SessionState $sessionState -MaxCount $MaxResults)
        RecentHeuristicObservations = [object[]]@(Get-UiRecentHeuristicObservations -SessionState $sessionState -MaxCount $MaxResults)
        HeuristicOperatorView = Get-UiHeuristicOperatorView -SessionState $sessionState
        FaultSummary = (Get-UiFaultStatePayload -ProcessId $effectiveProcessId -MaxResults $MaxResults).FaultSummary
        WindowHealth = if ($null -ne $process) { Get-UiCapabilityWindowHealth -Process $process } else { $null }
        CapabilitySession = $sessionState
    }
}

function Invoke-UiHeuristicBurstIntervention {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [Parameter(Mandatory)]
        [string]$CaptureReason,
        [int]$FrameCount = 3,
        [int]$IntervalMs = 70,
        [switch]$ArmBurstCapture
    )

    $captures = New-Object System.Collections.Generic.List[object]
    if (-not (Test-UiCapabilityEnabled -CapabilityName "BurstCapture")) {
        $pseudoCapability = [pscustomobject]@{
            Name = "ExplorationAssist"
            Metadata = [pscustomobject]@{
                FrameCount = $FrameCount
                IntervalMs = $IntervalMs
                CreateContactSheet = $true
            }
        }

        $burstPayload = Save-UiCapabilityBurstSequence -Process $Process -CapabilityRecord $pseudoCapability -ActionName $ActionName -CaptureReason $CaptureReason
        foreach ($capture in @($burstPayload.Captures)) {
            [void]$captures.Add($capture)
        }
    }

    if ($ArmBurstCapture -and -not (Test-UiCapabilityEnabled -CapabilityName "BurstCapture")) {
        Enable-UiCapability -CapabilityName "BurstCapture" -ProcessId $Process.Id -Reason "heuristic-$CaptureReason" -LeaseMilliseconds 1800 | Out-Null
    }

    return [object[]]$captures.ToArray()
}

function Invoke-UiExplorationAssistAfterAction {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        $Result = $null
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $recommendations = @(Get-UiExplorationRecommendations -ProcessId $Process.Id -ActionName $ActionName -DurationMs $DurationMs)
    if ($recommendations.Count -eq 0) {
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Decision = $null
        }
    }

    $primary = $recommendations[0]
    $cooldown = Get-UiHeuristicCooldownState -SessionState $session -HeuristicName ([string]$primary.StrategyName) -CooldownMs ([int]$primary.DefaultCooldownMs)
    if ($cooldown.IsActive) {
        $sessionForDecision = Get-UiCapabilitySessionState
        $decision = Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -ActionName $ActionName -Decision "suppressed" -Summary "رأت heuristics نفس النمط مجددًا، لكنها تجاهلت إعادة التدخل مؤقتًا حتى يبقى الإيقاع هادئًا." -Payload ([pscustomobject]@{
                StrategyName = [string]$primary.StrategyName
                RecommendedCapability = [string]$primary.RecommendedCapability
                SuggestedActions = [object[]]$primary.SuggestedActions
                RemainingMs = [int]$cooldown.RemainingMs
            })
        Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
        Add-UiCapabilityObservationRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -Kind "heuristic-suppressed" -Payload ([pscustomobject]@{
                Action = $ActionName
                StrategyName = [string]$primary.StrategyName
                RemainingMs = [int]$cooldown.RemainingMs
            })
        Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
        return [pscustomobject]@{
            Session = $sessionForDecision
            Captures = @()
            Decision = $decision
        }
    }

    $captures = New-Object System.Collections.Generic.List[object]
    $decisionKind = "guided"
    $summary = [string]$primary.Summary
    $autoIntervention = [string]$primary.AutoIntervention

    switch ([string]$primary.StrategyName) {
        "runtime-fault" {
            if (-not (Test-UiCapabilityEnabled -CapabilityName "FaultWatch")) {
                Enable-UiCapability -CapabilityName "FaultWatch" -ProcessId $Process.Id -Reason "heuristic-runtime-fault" -LeaseMilliseconds 3600 | Out-Null
                $faultPayload = Invoke-UiFaultWatchAfterAction -ProcessId $Process.Id -CapabilityRecord ([pscustomobject]@{ Name = "FaultWatch"; Metadata = $null }) -ActionName $ActionName -DurationMs $DurationMs -CaptureReason "heuristic-runtime-fault"
                foreach ($capture in @($faultPayload.Captures)) {
                    [void]$captures.Add($capture)
                }
                $decisionKind = "triggered"
                $summary = "اختارت heuristics مسار runtime-fault وفعّلت FaultWatch لأن الإشارة جاءت من التطبيق نفسه."
            }
            else {
                $summary = "اختارت heuristics مسار runtime-fault، لكن FaultWatch كانت نشطة أصلًا وتغطي هذا الفرع بالفعل."
            }
            break
        }
        "external-window" {
            if (-not (Test-UiCapabilityEnabled -CapabilityName "BurstCapture")) {
                foreach ($capture in @(Invoke-UiHeuristicBurstIntervention -Process $Process -ActionName $ActionName -CaptureReason "heuristic-external-window" -FrameCount 3 -IntervalMs 70 -ArmBurstCapture)) {
                    [void]$captures.Add($capture)
                }
                $decisionKind = "triggered"
                $summary = "اختارت heuristics مسار external-window وجمعت burst خفيفة لأن foreground خرجت إلى نافذة مرتبطة خارج العملية."
            }
            else {
                $summary = "اختارت heuristics مسار external-window، لكن BurstCapture كانت جاهزة أصلًا لذلك لم تحتج الأداة لتدخل إضافي."
            }
            break
        }
        "visual-anomaly" {
            if (-not (Test-UiCapabilityEnabled -CapabilityName "BurstCapture") -and -not (Test-UiCapabilityEnabled -CapabilityName "ReactiveAssist")) {
                foreach ($capture in @(Invoke-UiHeuristicBurstIntervention -Process $Process -ActionName $ActionName -CaptureReason "heuristic-visual-anomaly" -FrameCount 3 -IntervalMs 70 -ArmBurstCapture)) {
                    [void]$captures.Add($capture)
                }
                $decisionKind = "triggered"
                $summary = "اختارت heuristics مسار visual-anomaly وجمعت burst خفيفة لأن الإجراء خرج عن الإيقاع المقبول."
            }
            else {
                $summary = "اختارت heuristics مسار visual-anomaly، لكن الطبقة البصرية كانت مفعلة أصلًا لذلك لم تكرر evidence."
            }
            break
        }
    }

    $sessionForDecision = Get-UiCapabilitySessionState
    $decision = Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -ActionName $ActionName -Decision $decisionKind -Summary $summary -Payload ([pscustomobject]@{
            StrategyName = [string]$primary.StrategyName
            RecommendedCapability = [string]$primary.RecommendedCapability
            SuggestedActions = [object[]]$primary.SuggestedActions
            AutoIntervention = $autoIntervention
            CaptureCount = $captures.Count
            Signals = $primary.Signals
        })
    Add-UiCapabilityObservationRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -Kind "heuristic-intervention" -Payload ([pscustomobject]@{
            Action = $ActionName
            StrategyName = [string]$primary.StrategyName
            Decision = $decisionKind
            CaptureCount = $captures.Count
            Signals = $primary.Signals
        })
    Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null

    return [pscustomobject]@{
        Session = $sessionForDecision
        Captures = [object[]]$captures.ToArray()
        Decision = $decision
    }
}

function Invoke-UiExplorationAssistOnFailure {
    param(
        [System.Diagnostics.Process]$Process = $null,
        [Parameter(Mandatory)]
        $CapabilityRecord,
        [Parameter(Mandatory)]
        [string]$ActionName,
        [double]$DurationMs = 0,
        [string]$ErrorMessage = ""
    )

    $session = Get-UiCapabilitySessionState
    if ($null -eq $session) {
        return $null
    }

    $effectiveProcess = $Process
    if ($null -eq $effectiveProcess -and [int]$session.ProcessId -gt 0) {
        $effectiveProcess = Get-Process -Id ([int]$session.ProcessId) -ErrorAction SilentlyContinue
    }
    if ($null -eq $effectiveProcess) {
        $effectiveProcess = Get-UiProcess
    }
    if ($null -eq $effectiveProcess) {
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Decision = $null
        }
    }

    $recommendations = @(Get-UiExplorationRecommendations -ProcessId $effectiveProcess.Id -ActionName $ActionName -DurationMs $DurationMs -ErrorMessage $ErrorMessage -OnFailure)
    $primary = @($recommendations | Where-Object { $_.StrategyName -eq "stubborn-control" } | Select-Object -First 1)
    if ($primary.Count -eq 0) {
        return [pscustomobject]@{
            Session = $session
            Captures = @()
            Decision = $null
        }
    }

    $cooldown = Get-UiHeuristicCooldownState -SessionState $session -HeuristicName "stubborn-control" -CooldownMs ([int]$primary[0].DefaultCooldownMs)
    if ($cooldown.IsActive) {
        $sessionForDecision = Get-UiCapabilitySessionState
        $decision = Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -ActionName $ActionName -Decision "suppressed" -Summary "رأت heuristics أن العنصر ما زال عنيدًا، لكنها لم تكرر نفس التوجيه مباشرة حتى لا يصبح المسار مشتتًا." -Payload ([pscustomobject]@{
                StrategyName = "stubborn-control"
                RecommendedCapability = "MouseTrace"
                SuggestedActions = [object[]]$primary[0].SuggestedActions
                RemainingMs = [int]$cooldown.RemainingMs
                Error = $ErrorMessage
            })
        Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null
        return [pscustomobject]@{
            Session = $sessionForDecision
            Captures = @()
            Decision = $decision
        }
    }

    $captures = New-Object System.Collections.Generic.List[object]
    $mouseTraceRecord = [pscustomobject]@{
        Name = "MouseTrace"
        Metadata = [pscustomobject]@{
            SampleCount = 4
            IntervalMs = 35
        }
    }
    try {
        Save-UiMouseTraceObservation -Process $effectiveProcess -CapabilityRecord $mouseTraceRecord -ActionName $ActionName -CaptureReason "heuristic-stubborn-control" -ErrorMessage $ErrorMessage | Out-Null
    }
    catch {
    }

    if (-not (Test-UiCapabilityEnabled -CapabilityName "MouseTrace")) {
        Enable-UiCapability -CapabilityName "MouseTrace" -ProcessId $effectiveProcess.Id -Reason "heuristic-stubborn-control" -LeaseMilliseconds 2600 | Out-Null
    }

    $sessionForDecision = Get-UiCapabilitySessionState
    $decision = Add-UiCapabilityDecisionRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -ActionName $ActionName -Decision "guided" -Summary "اختارت heuristics مسار stubborn-control: الأفضل الآن التحول إلى يد أوضح بدل إعادة نفس المسار العنصري." -Payload ([pscustomobject]@{
            StrategyName = "stubborn-control"
            RecommendedCapability = "MouseTrace"
            SuggestedActions = [object[]]$primary[0].SuggestedActions
            AutoIntervention = "mouse-trace-arm"
            Error = $ErrorMessage
        })
    Add-UiCapabilityObservationRecord -SessionState $sessionForDecision -CapabilityName "ExplorationAssist" -Kind "heuristic-guidance" -Payload ([pscustomobject]@{
            Action = $ActionName
            StrategyName = "stubborn-control"
            Error = $ErrorMessage
        })
    Save-UiCapabilitySessionState -SessionState $sessionForDecision | Out-Null

    return [pscustomobject]@{
        Session = $sessionForDecision
        Captures = [object[]]$captures.ToArray()
        Decision = $decision
    }
}
