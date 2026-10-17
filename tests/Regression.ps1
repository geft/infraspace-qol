param(
    [string]$ManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'bin\Release\net472'
foreach ($name in @('UnityEngine.CoreModule', 'Unity.Collections', 'Unity.Mathematics', 'DionicCore', 'DionicGame', 'InfraSpaceCore',
    'InfraSpaceGameExtensions', 'InfraSpaceGameFeatures', 'old', 'Assembly-CSharp', 'Newtonsoft.Json')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $ManagedDir "$name.dll")) | Out-Null
}
[Reflection.Assembly]::LoadFrom((Join-Path $output '0Harmony.dll')) | Out-Null
$mod = [Reflection.Assembly]::LoadFrom((Join-Path $output 'InfraSpaceQoL.dll'))
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$research = $mod.GetType('InfraSpaceQoL.ResearchPatches', $true)
$cost = $research.GetMethod('GetRedScienceCost', $flags)
$settingsType = $mod.GetType('InfraSpaceQoL.ModSettings', $true)

function Assert-Equal($actual, $expected, $label) {
    if ($actual -ne $expected) { throw "$label`: expected $expected, got $actual" }
}

. (Join-Path $PSScriptRoot 'MonumentCostRegression.ps1')

foreach ($rate in @('0', '0.07', '0.14', '0.5', '1')) {
    $settings = [Newtonsoft.Json.JsonConvert]::DeserializeObject(
        ('{"Research":{"RedScienceCostMultiplier":' + $rate + '}}'), $settingsType)
    $researchSettings = $settingsType.GetField('Research').GetValue($settings)
    $multiplier = $researchSettings.GetType().GetField('RedScienceCostMultiplier').GetValue($researchSettings)
    Assert-Equal $multiplier.GetType() ([decimal]) 'JSON multiplier type'
    foreach ($total in @(1, 50, 100, 200, 300, 500)) {
        $spent = 0L
        $previous = 0L
        for ($progress = 1L; $progress -le $total; $progress++) {
            $current = $cost.Invoke($null, @($progress, $multiplier))
            $delta = $current - $previous
            if ($delta -lt 0 -or $delta -gt 1) { throw 'Invalid per-point consumption' }
            $spent += $delta
            $previous = $current
        }
        Assert-Equal $spent ([decimal]::Ceiling($total * [decimal]::Parse($rate,
            [Globalization.CultureInfo]::InvariantCulture))) "Cost $total at $rate"
    }
}
Assert-Equal ($cost.Invoke($null, @([long]100, [decimal]0.07))) 7 'Floating-point regression'

# Exercise the native incoming-car bookkeeping used by immediate cancellation.
$consumer = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ConsumerProducer])
$consumer.incomingCars = New-Object 'System.Collections.Generic.HashSet[Car]'
$car = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Car])
$resource = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
$car.resource = $resource
$car.amount = 20
$consumer.AddIncomingCar($car)
Assert-Equal ($consumer.GetIncomingCarsResourceAmount($resource)) 20 'Reserved delivery'
$consumer.RemoveIncomingCar($car)
Assert-Equal ($consumer.GetIncomingCarsResourceAmount($resource)) 0 'Immediate reservation release'
$consumer.RemoveIncomingCar($car)
Assert-Equal ($consumer.GetIncomingCarsResourceAmount($resource)) 0 'Deferred cleanup is idempotent'

$entry = $mod.GetType('InfraSpaceQoL.InfraSpaceQoLMod', $true)
$requireField = $entry.GetMethod('RequireField', $flags)
foreach ($field in @(
    @([ConsumerProducer], 'requestStatusDirty', [bool]),
    @([ResearchManager], 'amountOfProgressToAdd', [int]),
    @([ResearchProductionLogic], 'consumables', [System.Collections.Generic.List[ResourceCost]])
)) {
    $requireField.Invoke($null, $field)
}
foreach ($field in @(
    @([ResearchManager], 'missingField', [int]),
    @([ResearchManager], 'amountOfProgressToAdd', [bool])
)) {
    $rejected = $false
    try { $requireField.Invoke($null, $field) }
    catch { $rejected = $_.Exception.InnerException -is [MissingFieldException] }
    Assert-Equal $rejected $true 'Incompatible field rejected'
}

# Collection Center deliveries must reach factories and labs before surplus storage.
$logistics = $mod.GetType('InfraSpaceQoL.LogisticsPatches', $true)
$isSupportedDestination = $logistics.GetMethod('IsSupportedDestination', $flags)
$getDestinationPriority = $logistics.GetMethod('GetDestinationPriority', $flags)
function New-ConsumerForLogic($logicType) {
    $consumer = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ConsumerProducer])
    $consumer.productionLogic = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($logicType)
    return $consumer
}
$factoryConsumer = New-ConsumerForLogic ([FactoryProductionLogic])
$storehouseConsumer = New-ConsumerForLogic ([StorehouseProductionLogic])
$distributionConsumer = New-ConsumerForLogic ([DistributionProductionLogic])
$labConsumer = New-ConsumerForLogic ([ResearchProductionLogic])
$upgradeConsumer = New-ConsumerForLogic ([UpgradeProductionLogic])
Assert-Equal ($isSupportedDestination.Invoke($null, @($factoryConsumer))) $true 'Factory direct destination'
Assert-Equal ($isSupportedDestination.Invoke($null, @($storehouseConsumer))) $true 'Storehouse direct destination'
Assert-Equal ($isSupportedDestination.Invoke($null, @($labConsumer))) $true 'Research lab can receive collected science packs'
Assert-Equal ($isSupportedDestination.Invoke($null, @($upgradeConsumer))) $true 'Drill construction stages can receive Collection Center stock'
Assert-Equal ($isSupportedDestination.Invoke($null, @($distributionConsumer))) $false 'Distribution center excluded as direct destination'
Assert-Equal ($getDestinationPriority.Invoke($null, @($factoryConsumer))) 0 'Factory destination priority'
Assert-Equal ($getDestinationPriority.Invoke($null, @($labConsumer))) 0 'Research lab precedes surplus storage'
Assert-Equal ($getDestinationPriority.Invoke($null, @($upgradeConsumer))) 0 'Drill construction precedes surplus storage'
Assert-Equal ($getDestinationPriority.Invoke($null, @($storehouseConsumer))) 1 'Storehouse fallback priority'
$requestPriority = $logistics.GetMethod('GetRequestPriority', $flags)
Assert-Equal ($requestPriority.Invoke($null, @($labConsumer, 10, 10, 0))) ([single]1) 'Empty lab science input has native urgent priority'
Assert-Equal ($requestPriority.Invoke($null, @($labConsumer, 9, 10, 1))) ([single]0.8125) 'Lab priority decreases as packs arrive'
Assert-Equal ($requestPriority.Invoke($null, @($labConsumer, 6, 10, 4))) ([single]0.25) 'Stocked lab uses native minimum priority'
Assert-Equal ($requestPriority.Invoke($null, @($labConsumer, 0, 10, 10))) ([single]0) 'Full science input does not request more packs'
Assert-Equal ($requestPriority.Invoke($null, @($upgradeConsumer, 1, 1500, 0))) ([single]0.5) 'Construction uses native request priority'

# A newer, closer Storehouse must win even when an older distant one is listed first.
$routeType = $logistics.GetNestedType('StorehouseRoute', [Reflection.BindingFlags]'NonPublic')
$selectNearest = $logistics.GetMethod('SelectNearestStorehouseRoute', $flags)
$routeListType = [System.Collections.Generic.List``1].MakeGenericType($routeType)
function New-StorehouseRoute($consumer, [long]$distance, [long]$id) {
    $route = [Activator]::CreateInstance($routeType, $true)
    $routeType.GetField('Consumer', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($route, $consumer)
    $routeType.GetField('Distance', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($route, $distance)
    $routeType.GetField('BuildingId', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($route, $id)
    return $route
}
$nearConsumer = New-ConsumerForLogic ([StorehouseProductionLogic])
$farRoute = New-StorehouseRoute $storehouseConsumer 2000 1
$nearRoute = New-StorehouseRoute $nearConsumer 100 99
$routes = [Activator]::CreateInstance($routeListType)
$routes.Add($farRoute)
$routes.Add($nearRoute)
Assert-Equal ($selectNearest.Invoke($null, (, $routes))) $nearRoute 'Nearby newer storage wins'
$routes.Reverse()
Assert-Equal ($selectNearest.Invoke($null, (, $routes))) $nearRoute 'Storage ordering does not affect distance'
$tieRoute = New-StorehouseRoute $storehouseConsumer 100 5
$routes.Add($tieRoute)
Assert-Equal ($selectNearest.Invoke($null, (, $routes))) $tieRoute 'Equal routes use stable building ID'
$routes.Clear()
$routes.Add($farRoute)
Assert-Equal ($selectNearest.Invoke($null, (, $routes))) $farRoute 'Farther storage fallback when no nearer eligible route'
$routes.Clear()
Assert-Equal ($selectNearest.Invoke($null, (, $routes))) $null 'No eligible storage route'

# Filtering storage choices must preserve native consumers and their priorities.
$filterStores = $logistics.GetMethod('FilterStorehouseConsumers', $flags)
$nativeConsumers = New-Object 'System.Collections.Generic.List[Pair[ConsumerProducer,single]]'
$nativeConsumers.Add([Pair[ConsumerProducer,single]]::new($storehouseConsumer, 0.25))
$nativeConsumers.Add([Pair[ConsumerProducer,single]]::new($factoryConsumer, 1.0))
$nativeConsumers.Add([Pair[ConsumerProducer,single]]::new($nearConsumer, 0.25))
$filterStores.Invoke($null, @($nativeConsumers.PSObject.BaseObject, $nearConsumer))
Assert-Equal $nativeConsumers.Count 2 'Distant storage removed'
Assert-Equal $nativeConsumers[0].first $factoryConsumer 'Factory demand preserved'
Assert-Equal $nativeConsumers[0].second ([single]1.0) 'Factory priority preserved'
Assert-Equal $nativeConsumers[1].first $nearConsumer 'Nearest storage retained'
$filterStores.Invoke($null, @($nativeConsumers.PSObject.BaseObject, $null))
Assert-Equal $nativeConsumers.Count 1 'No eligible storage preserves other demand'

# Exercise the live collection eligibility and dispatch guard without Unity's simulation.
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
function New-LogisticsBuilding([single]$x, [single]$y, [single]$z) {
    $building = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Building])
    [Building].GetField('position', $instanceFlags).SetValue($building, [UnityEngine.Vector3]::new($x, $y, $z))
    [Building].GetField('isValid', $instanceFlags).SetValue($building, $true)
    [Building].GetField('isBuilt', $instanceFlags).SetValue($building, $true)
    return $building
}
$canCollect = $logistics.GetMethod('CanCollectFromProducer', $flags)
$pruneConnections = $logistics.GetMethod('PruneCollectionConnections', $flags)
$receivePrefix = $logistics.GetMethod('CollectionReceivePrefix', [Reflection.BindingFlags]'Static,Public')
$producer = New-ConsumerForLogic ([FactoryProductionLogic])
$producer.building = New-LogisticsBuilding 6 500 8
$producer.outgoingResources = [Resource[]]@($resource)
$producer.connectedCollectionCenters = [System.Collections.Generic.Dictionary[Resource,System.Collections.Generic.List[CollectionProductionLogic]]]::new()
$centerConsumer = New-ConsumerForLogic ([CollectionProductionLogic])
$centerConsumer.building = New-LogisticsBuilding 0 0 0
$center = $centerConsumer.productionLogic
$center.consumerProducer = $centerConsumer
$center.resource = $resource
$center.collectionRadius = 10
$center.connectedProducers = [System.Collections.Generic.HashSet[ConsumerProducer]]::new()
Assert-Equal ($canCollect.Invoke($null, @($center, $producer))) $true 'Radius boundary included and terrain height ignored'
$dispatchArgs = [object[]]@($center, $producer, 3, $false)
Assert-Equal ($receivePrefix.Invoke($null, $dispatchArgs)) $true 'In-radius delivery reaches native dispatch'
$center.collectionRadius = 9
Assert-Equal ($canCollect.Invoke($null, @($center, $producer))) $false 'Radius shrink rejects formerly connected producer'
$dispatchArgs = [object[]]@($center, $producer, 3, $true)
Assert-Equal ($receivePrefix.Invoke($null, $dispatchArgs)) $false 'Out-of-radius dispatch skipped'
Assert-Equal $dispatchArgs[3] $false 'Rejected delivery reports failure without spending stock'
$nearCenterConsumer = New-ConsumerForLogic ([CollectionProductionLogic])
$nearCenterConsumer.building = New-LogisticsBuilding 6 0 8
$nearCenter = $nearCenterConsumer.productionLogic
$nearCenter.consumerProducer = $nearCenterConsumer
$nearCenter.resource = $resource
$nearCenter.collectionRadius = 5
$nearCenter.connectedProducers = [System.Collections.Generic.HashSet[ConsumerProducer]]::new()
$centers = [System.Collections.Generic.List[CollectionProductionLogic]]::new()
$centers.Add($center)
$centers.Add($nearCenter)
$producer.connectedCollectionCenters.Add($resource, $centers)
$center.connectedProducers.Add($producer) | Out-Null
$nearCenter.connectedProducers.Add($producer) | Out-Null
$pruneConnections.Invoke($null, @($producer))
Assert-Equal $centers.Count 1 'Stale distant connection removed'
Assert-Equal $centers[0] $nearCenter 'Eligible local connection preserved'
Assert-Equal ($center.connectedProducers.Contains($producer)) $false 'Stale reverse connection removed'
Assert-Equal ($nearCenter.connectedProducers.Contains($producer)) $true 'Local reverse connection preserved'
[Building].GetField('isBuilt', $instanceFlags).SetValue($nearCenterConsumer.building, $false)
$pruneConnections.Invoke($null, @($producer))
Assert-Equal $centers.Count 0 'No eligible collection center leaves native delivery fallback available'
[Building].GetField('isBuilt', $instanceFlags).SetValue($nearCenterConsumer.building, $true)
$otherResource = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Resource])
$nearCenter.resource = $otherResource
Assert-Equal ($canCollect.Invoke($null, @($nearCenter, $producer))) $false 'Resource mismatch rejected'
$nearCenter.resource = $resource
foreach ($invalidRadius in @([single]::NaN, [single]::PositiveInfinity, [single]-1)) {
    $nearCenter.collectionRadius = $invalidRadius
    Assert-Equal ($canCollect.Invoke($null, @($nearCenter, $producer))) $false 'Invalid radius rejected'
}

# Drill construction tracks accumulated materials separately from its input buffer.
$upgradeConsumer.building = New-LogisticsBuilding 0 0 0
$upgradeConsumer.building.upgrades = [System.Collections.Generic.List[BuildingUpgrade]]::new()
$upgradeConsumer.incomingStorage = [int[]]@(0)
$upgradeConsumer.incomingCars = [System.Collections.Generic.HashSet[Car]]::new()
$upgradeLogic = $upgradeConsumer.productionLogic
$upgradeLogic.costs = [System.Collections.Generic.List[ResourceCost]]::new()
$upgradeLogic.costs.Add([ResourceCost]::new($resource, 1500))
$upgradeLogic.storage = [System.Collections.Generic.Dictionary[Resource,int]]::new()
$upgradeLogic.storage.Add($resource, 1497)
$remainingDemand = $logistics.GetMethod('GetRemainingDemand', $flags)
$directAmount = $logistics.GetMethod('GetDirectDeliveryAmount', $flags)
$hasRoom = $logistics.GetMethod('HasRoomForDelivery', $flags)
Assert-Equal ($remainingDemand.Invoke($null, @($upgradeConsumer, $upgradeLogic.costs[0], 0))) 3 'Construction subtracts accumulated materials'
$upgradeConsumer.incomingStorage[0] = 1
Assert-Equal ($remainingDemand.Invoke($null, @($upgradeConsumer, $upgradeLogic.costs[0], 0))) 2 'Construction also counts arrived input buffer'
$upgradeCar = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Car])
$upgradeCar.resource = $resource
$upgradeCar.amount = 1
$upgradeConsumer.AddIncomingCar($upgradeCar)
$upgradeRemaining = $remainingDemand.Invoke($null, @($upgradeConsumer, $upgradeLogic.costs[0], 0))
Assert-Equal $upgradeRemaining 1 'Construction also counts incoming reservations'
Assert-Equal ($directAmount.Invoke($null, @($upgradeConsumer, $upgradeRemaining, 3))) 1 'Final construction shipment sends one item'
$upgradeLogic.storage[$resource] = 1498
Assert-Equal ($remainingDemand.Invoke($null, @($upgradeConsumer, $upgradeLogic.costs[0], 0))) 0 'Fully committed construction requests nothing'
Assert-Equal ($hasRoom.Invoke($null, @($upgradeConsumer, $resource, 3))) $false 'No duplicate construction delivery when final materials are in transit'
$upgradeConsumer.RemoveIncomingCar($upgradeCar)
$upgradeConsumer.incomingStorage[0] = 0
$upgradeLogic.storage[$resource] = 1503
Assert-Equal ($remainingDemand.Invoke($null, @($upgradeConsumer, $upgradeLogic.costs[0], 0))) 0 'Previously overfilled construction input stays complete'

# Spaceship repair must finish at 348/350 even when a truck normally carries three.
$shipConsumer = New-ConsumerForLogic ([FactoryProductionLogic])
$shipConsumer.productionLogic.logicOverride = 'spaceship'
$shipConsumer.building = New-LogisticsBuilding 0 0 0
$shipConsumer.building.upgrades = [System.Collections.Generic.List[BuildingUpgrade]]::new()
$shipConsumer.incomingStorage = [int[]]@(348)
$shipConsumer.incomingCars = [System.Collections.Generic.HashSet[Car]]::new()
$shipCosts = [System.Collections.Generic.List[ResourceCost]]::new()
$shipCosts.Add([ResourceCost]::new($resource, 350))
[FactoryProductionLogic].GetField('cachedConsumables', $instanceFlags).SetValue($shipConsumer.productionLogic, $shipCosts)
$directAmount = $logistics.GetMethod('GetDirectDeliveryAmount', $flags)
$hasRoom = $logistics.GetMethod('HasRoomForDelivery', $flags)
Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 2, 3))) 2 'Direct repair shipment carries final two bars'
Assert-Equal ($directAmount.Invoke($null, @($factoryConsumer, 2, 3))) 0 'Ordinary factory still requires full load'
Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 5, 3))) 3 'Repair normally uses full loads'
Assert-Equal ($directAmount.Invoke($null, @($shipConsumer, 0, 3))) 0 'Completed repair requests nothing'
Assert-Equal ($hasRoom.Invoke($null, @($shipConsumer, $resource, 3))) $true 'Native distribution final load allowed'
$finalCar = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Car])
$finalCar.resource = $resource
$finalCar.amount = 2
$shipConsumer.AddIncomingCar($finalCar)
Assert-Equal ($hasRoom.Invoke($null, @($shipConsumer, $resource, 3))) $false 'Final bars already in transit prevent duplicate load'
$shipConsumer.RemoveIncomingCar($finalCar)
$shipConsumer.incomingStorage[0] = 350
Assert-Equal ($hasRoom.Invoke($null, @($shipConsumer, $resource, 3))) $false 'Completed repair blocks extra native load'
$shipConsumer.incomingStorage[0] = 348
$shipConsumer.productionLogic.logicOverride = $null
Assert-Equal ($hasRoom.Invoke($null, @($shipConsumer, $resource, 3))) $false 'Ordinary input capacity remains enforced'

# Storage is an overflow outlet, based on arrived stock in the sending buffer.
$nearlyFull = $logistics.GetMethod('IsNearlyFull', $flags)
$overflowStock = $logistics.GetMethod('HasOverflowStock', $flags)
foreach ($case in @(
    @(89, 100, 3, $false), @(90, 100, 3, $true), @(100, 100, 3, $true),
    @(8, 9, 3, $false), @(9, 9, 3, $true), @(2, 2, 3, $false),
    @(0, 0, 3, $false), @(100, 100, 0, $false)
)) {
    Assert-Equal ($nearlyFull.Invoke($null, [object[]]@($case[0], $case[1], $case[2]))) $case[3] 'Overflow threshold and full-truck availability'
}
[Resource]::Init([Func[int]]{ 0 })
$resource.stackSize = 100
$producer.outgoingStorage = [int[]]@(89)
Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $false 'Producer below threshold retains output'
$producer.outgoingStorage[0] = 90
Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $true 'Producer at threshold can send overflow'
[Resource]::Init([Func[int]]{ 100 })
Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $false 'Producer threshold includes upgraded output capacity'
[Resource]::Init([Func[int]]{ 0 })
$center.playerStorageLimit = 200
$centerConsumer.incomingStorage = [int[]]@(179)
Assert-Equal ($overflowStock.Invoke($null, @($centerConsumer, $resource, 3))) $false 'Collection Center below threshold retains stock'
$centerConsumer.incomingStorage[0] = 180
Assert-Equal ($overflowStock.Invoke($null, @($centerConsumer, $resource, 3))) $true 'Collection Center uses configured capacity'
Assert-Equal ($overflowStock.Invoke($null, @($centerConsumer, $otherResource, 3))) $false 'Other resource cannot use collection stock'
$distribution = $distributionConsumer.productionLogic
$distribution.resource = $resource
$distribution.playerStorageLimit = 200
$distributionConsumer.incomingStorage = [int[]]@(179)
Assert-Equal ($overflowStock.Invoke($null, @($distributionConsumer, $resource, 3))) $false 'Distribution Center cannot bypass overflow policy'
$distributionConsumer.incomingStorage[0] = 180
Assert-Equal ($overflowStock.Invoke($null, @($distributionConsumer, $resource, 3))) $true 'Distribution overflow allowed at threshold'
$suggestPrefix = $logistics.GetMethod('DeliverySuggestionPrefix', [Reflection.BindingFlags]'Static,Public')
$producer.outgoingStorage[0] = 89
$storageSuggestion = [DeliverySuggestion]::new($producer, $storehouseConsumer, $resource, 3, $null)
Assert-Equal ($suggestPrefix.Invoke($null, @($storageSuggestion))) $false 'Native storage suggestion blocked below threshold'
$factorySuggestion = [DeliverySuggestion]::new($producer, $factoryConsumer, $resource, 3, $null)
$labSuggestion = [DeliverySuggestion]::new($producer, $labConsumer, $resource, 3, $null)
Assert-Equal ($suggestPrefix.Invoke($null, @($factorySuggestion))) $true 'Factory delivery unaffected by overflow rule'
Assert-Equal ($suggestPrefix.Invoke($null, @($labSuggestion))) $true 'Research delivery unaffected by overflow rule'
$producer.outgoingStorage[0] = 90
Assert-Equal ($suggestPrefix.Invoke($null, @($storageSuggestion))) $true 'Native storage suggestion allowed for overflow'
$producer.outgoingStorage[0] -= 3
Assert-Equal ($suggestPrefix.Invoke($null, @($storageSuggestion))) $false 'Storage stops draining once source falls below threshold'

# Adamantine can reach the global stockpile without clipping slow drill batches.
$resource.name = 'adamantine'
$resource.stackSize = 48
foreach ($case in @(@(0, $false), @(2, $false), @(3, $true), @(30, $true), @(42, $true))) {
    $producer.outgoingStorage[0] = $case[0]
    $centerConsumer.incomingStorage[0] = $case[0]
    $distributionConsumer.incomingStorage[0] = $case[0]
    foreach ($source in @($producer, $centerConsumer, $distributionConsumer)) {
        Assert-Equal ($overflowStock.Invoke($null, @($source, $resource, 3))) $case[1] 'Adamantine requires a full load but no overflow threshold for all suppliers'
    }
    Assert-Equal ($suggestPrefix.Invoke($null, @($storageSuggestion))) $case[1] 'Native adamantine storage suggestions use the exemption'
}
Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 0))) $false 'Adamantine rejects zero-sized deliveries'
Assert-Equal ($overflowStock.Invoke($null, @($centerConsumer, $otherResource, 3))) $false 'Adamantine exemption preserves resource matching'
$resource.name = 'steel'
Assert-Equal ($overflowStock.Invoke($null, @($producer, $resource, 3))) $false 'Other resources still retain stock below 90 percent'
. (Join-Path $PSScriptRoot 'FeatureSettingsRegression.ps1')
. (Join-Path $PSScriptRoot 'WorkforceRegression.ps1')
. (Join-Path $PSScriptRoot 'TerraformRegression.ps1')
. (Join-Path $PSScriptRoot 'RedScienceOutputRegression.ps1')
foreach ($case in @(@('{}', $true), @('{"Laser":{}}', $true), @('{"Laser":{"AutoClearObstacles":false}}', $false))) {
    $loaded = [Newtonsoft.Json.JsonConvert]::DeserializeObject($case[0], $settingsType)
    $laserSettings = $settingsType.GetField('Laser').GetValue($loaded)
    Assert-Equal $laserSettings.GetType().GetField('AutoClearObstacles').GetValue($laserSettings) $case[1] 'Laser automation defaults and opt-out'
    Assert-Equal $laserSettings.GetType().GetField('SpeedMultiplier').GetValue($laserSettings) ([decimal]3) 'Older settings default to triple laser speed independently of automation'
}
$laserSpeed = $mod.GetType('InfraSpaceQoL.LaserPatches', $true).GetMethod('LaserSpeedBonusPostfix')
$currentSettingsProperty = $settingsType.GetProperty('Current', $flags)
$savedSpeedSettings = $currentSettingsProperty.GetValue($null)
try {
    foreach ($multiplier in @(1, 3, 10)) {
        $speedSettings = [Newtonsoft.Json.JsonConvert]::DeserializeObject(('{"Laser":{"AutoClearObstacles":false,"SpeedMultiplier":' + $multiplier + '}}'), $settingsType)
        $currentSettingsProperty.SetValue($null, $speedSettings)
        foreach ($bonus in @([single]0, [single]0.5, [single]2)) {
            $speedArgs = [object[]]@('laserSpeed', $bonus)
            $laserSpeed.Invoke($null, $speedArgs)
            Assert-Equal (1 + $speedArgs[1]) ((1 + $bonus) * $multiplier) 'Laser rate multiplies the complete upgraded rate'
        }
        foreach ($tech in @('carSpeed', 'productionTimeUpgrade', $null)) {
            $speedArgs = [object[]]@($tech, [single]0.5)
            $laserSpeed.Invoke($null, $speedArgs)
            Assert-Equal $speedArgs[1] ([single]0.5) 'Other upgrade bonuses unchanged'
        }
    }
}
finally {
    $currentSettingsProperty.SetValue($null, $savedSpeedSettings)
}
Write-Output 'PASS: laser speed, upgrade multiplication, automation independence, defaults, and normal-speed opt-out.'
# Harmony runs in the game's .NET Framework runtime, independently of PowerShell's CLR.
$smokeExe = Join-Path ([IO.Path]::GetTempPath()) ('InfraSpaceWorkforceSmoke-' + [guid]::NewGuid().ToString('N') + '.exe')
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $references = @('old', 'DionicCore', 'DionicGame', 'InfraSpaceCore', 'UnityEngine.CoreModule', 'netstandard') | ForEach-Object {
        '/reference:' + (Join-Path $ManagedDir ($_ + '.dll'))
    }
    & $compiler /nologo /target:exe /optimize+ "/out:$smokeExe" @references "/reference:$(Join-Path $output '0Harmony.dll')" (Join-Path $PSScriptRoot 'WorkforcePatchSmoke.cs') (Join-Path $PSScriptRoot 'LaserAutomationSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Workforce patch smoke compilation failed.' }
    & $smokeExe $output $ManagedDir
    if ($LASTEXITCODE -ne 0) { throw 'Workforce Harmony smoke test failed.' }
}
finally {
    if (Test-Path -LiteralPath $smokeExe) { Remove-Item -LiteralPath $smokeExe }
}
Write-Output 'PASS: research supply, costs, reservations, compatibility, radius enforcement, nearest storage, and overflow-only storage.'
