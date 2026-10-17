$monument = $mod.GetType('InfraSpaceQoL.MonumentCostPatches', $true)
$applyMonumentCost = $monument.GetMethod('ApplyCost', $flags)
foreach ($buildingName in @('magneticFieldGenerator_Stage3', 'monumentDrill_Stage3', 'factory')) {
    $upgrade = [UpgradeProductionLogic]::new()
    $upgrade.costs = [System.Collections.Generic.List[ResourceCost]]::new()
    foreach ($pair in @(@('adamantine', 250), @('steel', 250), @('adamantine', 10))) {
        $entryCost = [Activator]::CreateInstance([ResourceCost])
        $entryCost.resourceName = $pair[0]
        $entryCost.amount = $pair[1]
        $upgrade.costs.Add($entryCost)
    }
    $originalCosts = $upgrade.costs
    $description = [BuildingDescription]::new()
    $description.name = $buildingName
    $monument.GetMethod('NewProductionLogicPostfix').Invoke($null, @($description, $upgrade))
    $expected = if ($buildingName -eq 'magneticFieldGenerator_Stage3') { 75 } else { 250 }
    Assert-Equal $upgrade.costs[0].amount $expected 'Generator-only adamantite cost'
    Assert-Equal $upgrade.costs[1].amount 250 'Other ingredients preserved'
    Assert-Equal $upgrade.costs[2].amount 10 'Other stage amounts preserved'
    Assert-Equal $originalCosts[0].amount 250 'Shared recipe preserved'
    $applyMonumentCost.Invoke($null, @($buildingName, $upgrade))
    Assert-Equal $upgrade.costs[0].amount $expected 'Repeated application is stable'

    # A saved stage has old costs even when newly created recipes are patched.
    $upgrade.costs = $originalCosts
    $consumer = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ConsumerProducer])
    $consumer.building = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Building])
    $consumer.building.buildingName = $buildingName
    $savedStorage = $upgrade.storage
    $monument.GetMethod('UpgradeInitPrefix').Invoke($null, @($upgrade, $consumer))
    Assert-Equal $upgrade.costs[0].amount $expected 'Saved-stage cost updated'
    Assert-Equal ([object]::ReferenceEquals($savedStorage, $upgrade.storage)) $true 'Delivered material storage preserved'
}
Write-Output 'PASS: magnetic generator costs, saved stages, other ingredients, shared recipes, and idempotence.'
