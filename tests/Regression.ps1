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
Write-Output 'PASS: decimal costs, JSON parsing, incoming reservations, field compatibility, and factory-first Storehouse routing checks.'
