$monument = $mod.GetType('InfraSpaceQoL.MonumentCostPatches', $true)
$applyMonumentCost = $monument.GetMethod('ApplyCost', $flags)
# Placement costs from the shipped buildings asset are in hundredths.
foreach ($buildingName in @('magneticFieldGenerator_Stage0', 'magneticFieldGenerator_Stage3', 'monumentDrill_Stage0', 'factory')) {
    $description = [BuildingDescription]::new()
    $description.name = $buildingName
    $description.costs = [System.Collections.Generic.List[ResourceCost]]::new()
    foreach ($pair in @(@('concrete', 350000), @('steel', 150000), @('adamantine', 25000))) {
        $entryCost = [Activator]::CreateInstance([ResourceCost])
        $entryCost.resourceName = $pair[0]
        $entryCost.amount = $pair[1]
        $description.costs.Add($entryCost)
    }
    $originalCosts = $description.costs
    $monument.GetMethod('DescriptionInitPostfix').Invoke($null, @($description))
    $expected = if ($buildingName -eq 'magneticFieldGenerator_Stage0') { 7500 } else { 25000 }
    Assert-Equal $description.costs[2].amount $expected 'Generator-only upfront adamantite cost'
    Assert-Equal $description.costs[0].amount 350000 'Concrete placement cost preserved'
    Assert-Equal $description.costs[1].amount 150000 'Steel placement cost preserved'
    Assert-Equal $originalCosts[2].amount 25000 'Shared cost list preserved'
    $applyMonumentCost.Invoke($null, @($description))
    Assert-Equal $description.costs[2].amount $expected 'Repeated application is stable'
    Assert-Equal ($description.costs[2].amount / 100) ($expected / 100) 'Displayed adamantite units'
}
Write-Output 'PASS: magnetic generator placement costs, hundredth units, other buildings, and idempotence.'

# Base definitions have initialized before script mods are loaded. Exercise Start.
$savedModule = [Old]::buildingModule
try {
    $module = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([BuildingModule])
    $module.buildingDescriptions = [System.Collections.Generic.Dictionary[string,BuildingDescription]]::new()
    $description.name = 'magneticFieldGenerator_Stage0'
    $module.buildingDescriptions.Add($description.name, $description)
    [Old]::buildingModule = $module
    $entryType = $mod.GetType('InfraSpaceQoL.InfraSpaceQoLMod', $true)
    $instance = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($entryType)
    $entryType.GetMethod('Start').Invoke($instance, $null)
    Assert-Equal $description.costs[2].amount 7500 'Previously loaded definition updated at mod startup'
}
finally { [Old]::buildingModule = $savedModule }

# Saved factories refresh recipes through this manager; construction previews
# read the cached factory logic instead. Both must use the same power demand.
$generatorPower = $monument.GetMethod('GeneratorPowerPostfix')
$previewPower = $monument.GetMethod('GeneratorPreviewPostfix')
foreach ($name in @('magneticFieldGenerator_Stage4', 'ammoniaExtractor', 'magneticFieldGenerator_Stage0')) {
    $recipe = [ProductionDefinition]::new()
    $recipe.powerNeeded = 5000
    $recipe.maxWorkers = 200
    $recipe.timeSteps = 1500000
    $generatorPower.Invoke($null, @($name, $recipe))
    $expectedPower = if ($name -eq 'magneticFieldGenerator_Stage4') { 1000 } else { 5000 }
    Assert-Equal $recipe.powerNeeded ([single]$expectedPower) 'Generator-only operational power'
    Assert-Equal $recipe.maxWorkers 200 'Normal generator workforce'
    Assert-Equal $recipe.timeSteps 1500000 'Normal generator batch time'
    $factory = [FactoryProductionLogic]::new()
    $factory.productionDefinition = [ProductionDefinition]::new()
    $factory.productionDefinition.powerNeeded = 5000
    $factory.terraformRadius = 4096
    $description = [BuildingDescription]::new()
    $description.name = $name
    $previewPower.Invoke($null, @($description, $factory))
    Assert-Equal $factory.productionDefinition.powerNeeded ([single]$expectedPower) 'Preview matches operational power'
    Assert-Equal $factory.terraformRadius ([single]4096) 'Normal magnetic field coverage'
}
Write-Output 'PASS: generator power for saved factories and previews, other buildings, workforce, time, and radius.'
