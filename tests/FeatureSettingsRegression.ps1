# Exercise the opt-outs against the runtime methods, independent of installer serialization.
$currentProperty = $settingsType.GetProperty('Current', $flags)
$originalSettings = $currentProperty.GetValue($null)
try {
    $configured = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{}', $settingsType)
    $currentProperty.SetValue($null, $configured)
    $logisticsOptions = $settingsType.GetField('Logistics').GetValue($configured)
    $monumentOptions = $settingsType.GetField('Terraforming').GetValue($configured)

    $resource.name = 'adamantine'
    $resource.stackSize = 48
    $producer.outgoingStorage[0] = 3
    Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $true 'Default adamantite exemption'
    $logisticsOptions.GetType().GetField('AdamantiteStorageExemption').SetValue($logisticsOptions, $false)
    Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $false 'Adamantite exemption opt-out'
    $logisticsOptions.GetType().GetField('OverflowOnlyStorage').SetValue($logisticsOptions, $false)
    Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $true 'Overflow policy opt-out'
    $producer.outgoingStorage[0] = 2
    Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $false 'Disabled overflow still needs stock for a full truck'
    Assert-Equal ($suggestPrefix.Invoke($null, @($storageSuggestion))) $true 'Native storage policy opt-out'

    $logisticsOptions.GetType().GetField('EnforceCollectionRadius').SetValue($logisticsOptions, $false)
    $dispatchArgs = [object[]]@($center, $producer, 3, $false)
    Assert-Equal ($receivePrefix.Invoke($null, $dispatchArgs)) $true 'Radius opt-out reaches native dispatch'

    $shipConsumer.productionLogic.logicOverride = 'spaceship'
    Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 2, 3))) 2 'Default partial final loads'
    $logisticsOptions.GetType().GetField('AllowPartialFinalLoads').SetValue($logisticsOptions, $false)
    Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 2, 3))) 0 'Partial final load opt-out'
    Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 3, 3))) 3 'Full loads remain available'

    # Both storage-related opt-outs together must leave the native candidate list alone.
    $logisticsOptions.GetType().GetField('PreferNearestStorehouse').SetValue($logisticsOptions, $false)
    $nativeConsumers.Clear()
    $nativeConsumers.Add([Pair[ConsumerProducer,single]]::new($storehouseConsumer, 0.25))
    $nativeConsumers.Add([Pair[ConsumerProducer,single]]::new($nearConsumer, 0.25))
    $logistics.GetMethod('InterestedConsumersPostfix').Invoke($null, @($resource, $nativeConsumers.PSObject.BaseObject, $producer))
    Assert-Equal $nativeConsumers.Count 2 'Native storage choices preserved when preference disabled'

    $description = [BuildingDescription]::new()
    $description.name = 'magneticFieldGenerator_Stage0'
    $description.costs = [System.Collections.Generic.List[ResourceCost]]::new()
    $savedCost = [Activator]::CreateInstance([ResourceCost])
    $savedCost.resourceName = 'adamantine'
    $savedCost.amount = 7500
    $description.costs.Add($savedCost)
    $monumentOptions.GetType().GetField('ReduceMagneticFieldGeneratorCost').SetValue($monumentOptions, $false)
    $applyMonumentCost.Invoke($null, @($description))
    Assert-Equal $description.costs[0].amount 25000 'Monument opt-out restores original placement cost'
    $monumentOptions.GetType().GetField('ReduceMagneticFieldGeneratorCost').SetValue($monumentOptions, $true)
    $applyMonumentCost.Invoke($null, @($description))
    Assert-Equal $description.costs[0].amount 7500 'Monument placement discount can be re-enabled'

    $powerField = $monumentOptions.GetType().GetField('MagneticFieldGeneratorPowerMW')
    foreach ($power in @(1, 1000, 2500, 5000)) {
        $powerField.SetValue($monumentOptions, [decimal]$power)
        $recipe = [ProductionDefinition]::new()
        $recipe.powerNeeded = 5000
        $monument.GetMethod('GeneratorPowerPostfix').Invoke($null, @('magneticFieldGenerator_Stage4', $recipe))
        Assert-Equal $recipe.powerNeeded ([single]$power) 'Configured generator power reaches runtime recipe'
    }
    foreach ($case in @(
        @('{"Monuments":{"ReduceMagneticFieldGeneratorCost":false}}', $false),
        @('{"Monuments":{"ReduceMagneticFieldGeneratorCost":false},"Terraforming":{"ReduceMagneticFieldGeneratorCost":true}}', $true))) {
        $normalized = $settingsType.GetMethod('NormalizeSettingsJson', $flags).Invoke($null, @($case[0]))
        $migrated = [Newtonsoft.Json.JsonConvert]::DeserializeObject($normalized, $settingsType)
        $options = $settingsType.GetField('Terraforming').GetValue($migrated)
        Assert-Equal $options.ReduceMagneticFieldGeneratorCost $case[1] 'Legacy monument preference migrates with new-key precedence'
        Assert-Equal $options.MagneticFieldGeneratorPowerMW ([decimal]1000) 'Older settings receive default generator power'
    }

    $packageSettings = Get-Content -LiteralPath (Join-Path $root 'settings.json') -Raw | ConvertFrom-Json -AsHashtable
    $defaults = [Newtonsoft.Json.JsonConvert]::DeserializeObject('{}', $settingsType)
    foreach ($section in $settingsType.GetFields()) {
        $values = $section.GetValue($defaults)
        foreach ($field in $section.FieldType.GetFields()) {
            Assert-Equal $packageSettings[$section.Name][$field.Name] ($field.GetValue($values)) "Package matches runtime default: $($section.Name).$($field.Name)"
        }
    }
}
finally {
    $currentProperty.SetValue($null, $originalSettings)
}
Write-Output 'PASS: configurable storage rules, radius enforcement, partial loads, saved monument costs, and package defaults.'
