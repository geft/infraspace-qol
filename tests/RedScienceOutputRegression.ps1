$outputPostfix = $research.GetMethod('RedScienceOutputPostfix')
$savedOutputResources = [InfraSpaceCore]::resourceModule
try {
    $resources = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ResourceModule])
    $resources.resources = [System.Collections.Generic.Dictionary[string,Resource]]::new()
    foreach ($name in @('sciencePack4', 'steel')) {
        $item = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
        $item.name = $name
        $item.stackSize = 100
        $resources.resources.Add($name, $item)
    }
    [InfraSpaceCore]::resourceModule = $resources
    [Resource]::Init([Func[int]]{ 0 })
    $recipe = [ProductionDefinition]::new()
    $recipe.timeSteps = 12345
    $recipe.powerNeeded = 50
    $recipe.maxWorkers = 8
    $recipe.consumables.Add([ResourceCost]::new($resources.resources['steel'], 3))
    $recipe.producables.Add([ResourceCost]::new($resources.resources['sciencePack4'], 1))
    $recipe = [ProductionDefinition]::new($recipe)
    foreach ($lookup in 1..3) {
        $outputArgs = [object[]]@($recipe)
        $outputPostfix.Invoke($null, $outputArgs)
        $boosted = $outputArgs[0]
        Assert-Equal $boosted.producables[0].amount 2 'Two Red Science packs per batch'
        Assert-Equal $boosted.consumables[0].amount 3 'Normal ingredient cost'
        Assert-Equal $boosted.timeSteps 12345 'Normal batch time'
        Assert-Equal $boosted.powerNeeded ([single]50) 'Normal power'
        Assert-Equal $boosted.maxWorkers 8 'Normal workforce'
        Assert-Equal $recipe.producables[0].amount 1 'Repeated lookups do not mutate cached recipe'
    }
    $factory = [FactoryProductionLogic]::new()
    $factory.productionDefinition = $boosted
    $factory.consumerProducer = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ConsumerProducer])
    $factory.consumerProducer.building = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Building])
    $factory.consumerProducer.building.upgrades = [System.Collections.Generic.List[BuildingUpgrade]]::new()
    $factory.consumerProducer.incomingStorage = [int[]]@(12)
    $factory.consumerProducer.outgoingStorage = [int[]]@(0)
    foreach ($batch in 1..4) { $factory.Produce() }
    Assert-Equal $factory.consumerProducer.incomingStorage[0] 0 'Four batches use twelve ingredients'
    Assert-Equal $factory.consumerProducer.outgoingStorage[0] 8 'Four native batches produce eight packs'
    $factory.consumerProducer.outgoingStorage[0] = 99
    $factory.consumerProducer.incomingStorage[0] = 3
    $factory.Produce()
    Assert-Equal $factory.consumerProducer.outgoingStorage[0] 100 'Native storage limit preserved'
    $recipe.Boost('test upgrade', 'sciencePack4', 1)
    $outputArgs = [object[]]@($recipe)
    $outputPostfix.Invoke($null, $outputArgs)
    Assert-Equal $outputArgs[0].producables[0].amount 4 'Current upgraded yield doubles'
    $recipe.producables.Add([ResourceCost]::new($resources.resources['steel'], 5))
    $outputArgs = [object[]]@($recipe)
    $outputPostfix.Invoke($null, $outputArgs)
    Assert-Equal $outputArgs[0].producables[1].amount 5 'Other resource outputs unchanged'
    $other = [ProductionDefinition]::new()
    $other.producables.Add([ResourceCost]::new($resources.resources['steel'], 1))
    $outputArgs = [object[]]@($other)
    $outputPostfix.Invoke($null, $outputArgs)
    Assert-Equal ([object]::ReferenceEquals($outputArgs[0], $other)) $true 'Unrelated recipe unchanged'
}
finally {
    [InfraSpaceCore]::resourceModule = $savedOutputResources
}
foreach ($case in @(@('{}', $true), @('{"Research":{"DoubleRedScienceOutput":false}}', $false))) {
    $loaded = [Newtonsoft.Json.JsonConvert]::DeserializeObject($case[0], $settingsType)
    $section = $settingsType.GetField('Research').GetValue($loaded)
    Assert-Equal $section.GetType().GetField('DoubleRedScienceOutput').GetValue($section) $case[1] 'Output defaults and opt-out'
}
Write-Output 'PASS: native Red Science batches double output with normal ingredients, time, power, upgrades, and storage limits.'
