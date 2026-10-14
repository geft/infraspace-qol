# Loaded by Regression.ps1 after the game and mod assemblies are available.
$workforce = $mod.GetType('InfraSpaceQoL.WorkforcePatches', $true)
$releaseWorkers = $workforce.GetMethod('ShouldReleaseWorkers', $flags)
$employmentPrefix = $workforce.GetMethod('CheckEmploymentPrefix', [Reflection.BindingFlags]'Public,Static')
$employmentFinalizer = $workforce.GetMethod('CheckEmploymentFinalizer', [Reflection.BindingFlags]'Public,Static')
$marketFlags = [Reflection.BindingFlags]'Instance,NonPublic'

function Get-MarketField($market, $name) {
    return ,([Market].GetField($name, $marketFlags).GetValue($market))
}

function New-WorkforceFactory([Resource[]]$outputs, [int[]]$stock, [int]$capacity = 4, [int]$staff = 0) {
    $factory = [FactoryProductionLogic]::new()
    $factory.productionDefinition = [ProductionDefinition]::new()
    $factory.productionDefinition.producables = [System.Collections.Generic.List[ResourceCost]]::new()
    foreach ($output in $outputs) {
        $factory.productionDefinition.producables.Add([ResourceCost]::new($output, 1))
    }
    [FactoryProductionLogic].GetField('maxWorkersCached', $marketFlags).SetValue($factory, $capacity)
    $factory.consumerProducer = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ConsumerProducer])
    $factory.consumerProducer.productionLogic = $factory
    $factory.consumerProducer.outgoingStorage = $stock
    for ($i = 0; $i -lt $staff; $i++) {
        $worker = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Worker])
        $factory.AddWorker($worker)
    }
    return $factory
}

$water = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
$water.name = 'water'
$water.stackSize = 100
$carbon = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
$carbon.name = 'carbon'
$carbon.stackSize = 100
[Resource]::Init([Func[int]]{ 0 })
$dualOutput = New-WorkforceFactory @($water, $carbon) @(100, 100)
foreach ($case in @(
    @(100, 100, $true), @(100, 99, $false), @(99, 100, $false),
    @(0, 100, $false), @(100, 0, $false), @(101, 100, $true)
)) {
    $dualOutput.consumerProducer.outgoingStorage = [int[]]@($case[0], $case[1])
    Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $case[2] 'Every output must be completely full'
}
$dualOutput.consumerProducer.outgoingStorage = [int[]]@(100, 100)
[Resource]::Init([Func[int]]{ 100 })
Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $false 'Storage upgrades must also fill'
$dualOutput.consumerProducer.outgoingStorage = [int[]]@(200, 200)
Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $true 'Upgraded full buffers release workers'
[Resource]::Init([Func[int]]{ 0 })
$dualOutput.consumerProducer.outgoingStorage = [int[]]@(100, 100)
foreach ($override in @('spaceship', 'terraformer', 'other')) {
    $dualOutput.logicOverride = $override
    Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $false 'Special production remains staffed'
}
$dualOutput.logicOverride = $null
$dualOutput.productionDefinition.powerOutput = 1
Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $false 'Power production remains staffed'
$dualOutput.productionDefinition.powerOutput = 0
$dualOutput.consumerProducer.outgoingStorage = [int[]]@(100)
Assert-Equal ($releaseWorkers.Invoke($null, @($dualOutput))) $false 'Incomplete storage data is ignored'
$noOutput = New-WorkforceFactory @() @()
Assert-Equal ($releaseWorkers.Invoke($null, @($noOutput))) $false 'No outputs cannot trigger idle automation'
$noWorkers = New-WorkforceFactory @($water) @(100) 0
Assert-Equal ($releaseWorkers.Invoke($null, @($noWorkers))) $false 'Workerless production is unchanged'

# Real native heaps, worker ownership, and balancing methods. Only PreUpdate's
# Unity-dependent district lookup/rounding is represented by headless bookkeeping.
$market = [Market]::new()
$industries = Get-MarketField $market 'industries'
$minHeaps = Get-MarketField $market 'industriesMinHeap'
$maxHeaps = Get-MarketField $market 'industriesMaxHeap'
$working = Get-MarketField $market 'currentlyWorkingWorkersPerIndustry'
$unemployed = Get-MarketField $market 'unemployedWorkers'
$jobs = Get-MarketField $market 'totalJobsPerIndustry'
$requested = Get-MarketField $market 'requestedWorkersPerIndustry'
$satisfied = Get-MarketField $market 'satisfiedPercentageOfWorkersPerIndustry'
$minComparer = [Activator]::CreateInstance([Market].GetNestedType('FactoryMinHeapComparer', $marketFlags), $true)
$maxComparer = [Activator]::CreateInstance([Market].GetNestedType('FactoryMaxHeapComparer', $marketFlags), $true)
$idleFactory = New-WorkforceFactory @($water, $carbon) @(100, 100) 4 4
$activeFactory = New-WorkforceFactory @($water, $carbon) @(100, 0) 4 0
$otherIdle = New-WorkforceFactory @($carbon) @(100) 4 2
$idleWorkers = @($idleFactory.workers.ToArray()) + @($otherIdle.workers.ToArray())
foreach ($industry in @($water, $carbon)) {
    $industries.Add($industry, [System.Collections.Generic.List[FactoryProductionLogic]]::new())
    $minHeaps.Add($industry, [Heap[FactoryProductionLogic]]::new($minComparer))
    $maxHeaps.Add($industry, [Heap[FactoryProductionLogic]]::new($maxComparer))
    $working.Add($industry, 0)
}
foreach ($pair in @(@($water, $idleFactory), @($water, $activeFactory), @($carbon, $otherIdle))) {
    $industries[$pair[0]].Add($pair[1])
    $minHeaps[$pair[0]].Add($pair[1])
    $maxHeaps[$pair[0]].Add($pair[1])
    $working[$pair[0]] += $pair[1].workers.Count
}
$market.SetResourcePriority($water, [single]0.5)
$market.SetResourcePriority($carbon, [single]1)
$savedResourceModule = [InfraSpaceCore]::resourceModule
$testResources = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ResourceModule])
$testResources.resources = [System.Collections.Generic.Dictionary[string,Resource]]::new()
$empty = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
$empty.name = 'empty'
$testResources.resources.Add('empty', $empty)
[InfraSpaceCore]::resourceModule = $testResources

function Start-EmploymentPass {
    $script:employmentArgs = [object[]]@((Get-MarketField $market 'industries'), $minHeaps, $maxHeaps, $working, $unemployed, $null)
    $employmentPrefix.Invoke($null, $script:employmentArgs)
    [Market].GetField('industries', $marketFlags).SetValue($market, $script:employmentArgs[0])
}
function Stop-EmploymentPass($exception = $null) {
    $endArgs = [object[]]@($exception, (Get-MarketField $market 'industries'), $script:employmentArgs[5])
    $returned = $employmentFinalizer.Invoke($null, $endArgs)
    [Market].GetField('industries', $marketFlags).SetValue($market, $endArgs[1])
    Assert-Equal $returned $exception 'Finalizer preserves original exceptions'
    Assert-Equal ([object]::ReferenceEquals((Get-MarketField $market 'industries'), $industries)) $true 'Industry registry restored'
    foreach ($industry in @($water, $carbon)) {
        Assert-Equal ($minHeaps[$industry].Verify()) $true 'Restored minimum heap is valid'
        Assert-Equal ($maxHeaps[$industry].Verify()) $true 'Restored maximum heap is valid'
        Assert-Equal $minHeaps[$industry].Count() $industries[$industry].Count 'No factories lost or duplicated'
    }
}
function Invoke-NativeEmploymentBalance {
    $active = Get-MarketField $market 'industries'
    foreach ($industry in $active.Keys) {
        $total = 0
        foreach ($factory in $active[$industry]) { $total += $factory.GetMaxWorkers() }
        $jobs[$industry] = $total
        $requested[$industry] = [single][Math]::Round($market.GetResourcePriority($industry) * $total)
        $satisfied[$industry] = [Market].GetMethod('GetSatisfiedPercentage', $marketFlags).Invoke($market, @($industry, $working[$industry]))
    }
    for ($i = 0; $i -lt 100; $i++) {
        $cut = [Market].GetMethod('CutOverPopulatedIndustries', $marketFlags).Invoke($market, @())
        $between = [Market].GetMethod('BalanceBetweenIndustries', $marketFlags).Invoke($market, @())
        $within = [Market].GetMethod('BalanceWithinIndustry', $marketFlags).Invoke($market, @())
        if (!$cut -and $between -eq -1 -and !$within) { break }
    }
}
try {
    Start-EmploymentPass
    Assert-Equal $idleFactory.workers.Count 0 'Full dual-output factory releases all workers'
    Assert-Equal $otherIdle.workers.Count 0 'Fully idle industry releases all workers'
    Assert-Equal $unemployed.Count 6 'All released workers enter the unemployment pool'
    foreach ($worker in $idleWorkers) { Assert-Equal $worker.workplace $null 'Released worker has no workplace' }
    Assert-Equal $employmentArgs[0][$water].Count 1 'Partially full factory stays eligible'
    Assert-Equal $employmentArgs[0][$carbon].Count 0 'Entirely idle industry has zero active demand'
    Invoke-NativeEmploymentBalance
    Assert-Equal $activeFactory.workers.Count 2 'Manual 50 percent priority applies to active capacity'
    Assert-Equal $idleFactory.workers.Count 0 'Native balancing cannot refill idle factory'
    Assert-Equal $working[$water] 2 'Native working count follows reassignment'
    Stop-EmploymentPass
    Assert-Equal $idleFactory.GetMaxWorkers() 4 'Normal worker capacity preserved'
    Assert-Equal $market.GetResourcePriority($water) ([single]0.5) 'Manual priority preserved'

    # Change the priority while idle, then drain ONE output and resume normally.
    $market.SetResourcePriority($water, [single]0.25)
    $idleFactory.consumerProducer.outgoingStorage[1] = 99
    Start-EmploymentPass
    Assert-Equal $employmentArgs[0][$water].Count 2 'Either output draining restores eligibility'
    Invoke-NativeEmploymentBalance
    Assert-Equal $requested[$water] ([single]2) 'Latest manual priority applies when resuming'
    Assert-Equal ($idleFactory.workers.Count + $activeFactory.workers.Count) 2 'Resuming does not force full staffing'
    Stop-EmploymentPass

    # Zero priority must keep a resumed factory unstaffed.
    $market.SetResourcePriority($water, [single]0)
    Start-EmploymentPass
    Invoke-NativeEmploymentBalance
    Assert-Equal $working[$water] 0 'Manual zero priority keeps production unstaffed'
    Stop-EmploymentPass

    # Repeated pauses and exceptional exits must restore the native heaps/registry.
    $idleFactory.consumerProducer.outgoingStorage[1] = 100
    Start-EmploymentPass
    Stop-EmploymentPass ([InvalidOperationException]::new('simulated employment failure'))
    Start-EmploymentPass
    Stop-EmploymentPass
}
finally {
    [InfraSpaceCore]::resourceModule = $savedResourceModule
}

foreach ($field in @(
    @('industries', [System.Collections.Generic.Dictionary[Resource,System.Collections.Generic.List[FactoryProductionLogic]]]),
    @('industriesMinHeap', [System.Collections.Generic.Dictionary[Resource,Heap[FactoryProductionLogic]]]),
    @('industriesMaxHeap', [System.Collections.Generic.Dictionary[Resource,Heap[FactoryProductionLogic]]]),
    @('currentlyWorkingWorkersPerIndustry', [System.Collections.Generic.Dictionary[Resource,int]]),
    @('unemployedWorkers', [System.Collections.Generic.List[Worker]])
)) {
    $requireField.Invoke($null, @([Market], $field[0], $field[1]))
}
foreach ($json in @('{}', '{"Workforce":{}}')) {
    $loaded = [Newtonsoft.Json.JsonConvert]::DeserializeObject($json, $settingsType)
    $workforceSettings = $settingsType.GetField('Workforce').GetValue($loaded)
    Assert-Equal $workforceSettings.GetType().GetField('ReleaseWorkersWhenAllOutputsFull').GetValue($workforceSettings) $true 'Older settings enable automation by default'
}
$loaded = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{"Workforce":{"ReleaseWorkersWhenAllOutputsFull":false}}', $settingsType)
$workforceSettings = $settingsType.GetField('Workforce').GetValue($loaded)
Assert-Equal $workforceSettings.GetType().GetField('ReleaseWorkersWhenAllOutputsFull').GetValue($workforceSettings) $false 'Worker automation can be disabled'
Write-Output 'PASS: all-output worker release, native redistribution, manual priorities, resumption, and exception cleanup.'
