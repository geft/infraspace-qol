param(
    [string]$ManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'bin\Release\net472'
foreach ($name in @('UnityEngine.CoreModule', 'DionicCore', 'DionicGame', 'InfraSpaceCore',
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

# Keep Collection Center truck targets limited to factories and storage, with factories first.
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
Assert-Equal ($isSupportedDestination.Invoke($null, @($factoryConsumer))) $true 'Factory direct destination'
Assert-Equal ($isSupportedDestination.Invoke($null, @($storehouseConsumer))) $true 'Storehouse direct destination'
Assert-Equal ($isSupportedDestination.Invoke($null, @($distributionConsumer))) $false 'Distribution center excluded as direct destination'
Assert-Equal ($getDestinationPriority.Invoke($null, @($factoryConsumer))) 0 'Factory destination priority'
Assert-Equal ($getDestinationPriority.Invoke($null, @($storehouseConsumer))) 1 'Storehouse fallback priority'

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
Write-Output 'PASS: research costs, reservations, compatibility, storage routing, and live Collection Center radius enforcement.'
