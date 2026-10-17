# Loaded by Regression.ps1 after the game and mod assemblies are available.
$terraform = $mod.GetType('InfraSpaceQoL.TerraformPatches', $true)
$expand = $terraform.GetMethod('GetExpandedRadius', $flags)
$terrain = [single[]]::new(81)
function Get-TestTerraformRadius {
    return $expand.Invoke($null, [object[]]@($terrain, 9, 9, [single]90, [single]0, [single]89,
        [single]40, [single]40, [single]15))
}
function Fill-TerraformCircle([single]$radius, [single]$value) {
    for ($row = 0; $row -lt 9; $row++) {
        for ($column = 0; $column -lt 9; $column++) {
            if (($row * 10 - 40) * ($row * 10 - 40) + ($column * 10 - 40) * ($column * 10 - 40) -le $radius * $radius) {
                $terrain[$row * 9 + $column] = $value
            }
        }
    }
}
Assert-Equal (Get-TestTerraformRadius) ([single]15) 'Unfinished local terrain retains normal range'
Fill-TerraformCircle 15 0.3
Assert-Equal (Get-TestTerraformRadius) ([single]15) 'One terraforming type cannot unlock map-wide range'
Fill-TerraformCircle 15 0.989
Assert-Equal (Get-TestTerraformRadius) ([single]15) 'Below saturation must not double range'
Fill-TerraformCircle 15 0.99
Assert-Equal (Get-TestTerraformRadius) ([single]30) 'Saturated local circle doubles range'
Fill-TerraformCircle 30 0.5
Fill-TerraformCircle 15 1
Assert-Equal (Get-TestTerraformRadius) ([single]30) 'Partially complete new area holds current doubled range'
Fill-TerraformCircle 30 1
Assert-Equal (Get-TestTerraformRadius) ([single]60) 'Second saturated circle doubles again'
$terrain[0] = 0
Assert-Equal (Get-TestTerraformRadius) ([single]60) 'Outer incomplete cells stop further doubling'
Fill-TerraformCircle 60 1
Assert-Equal (Get-TestTerraformRadius) ([single]60) 'Completed map caps range at map coverage'
$terrain[40] = 0
Assert-Equal (Get-TestTerraformRadius) ([single]15) 'Production loss or terrain decay removes local saturation'
$terrain[40] = [single]::NaN
Assert-Equal (Get-TestTerraformRadius) ([single]15) 'Invalid progress cannot unlock expansion'
# A hole exactly on the native radius is still local; outside the playable area is ignored.
$boundaryTerrain = [single[]](1..64 | ForEach-Object { 1 })
$boundaryTerrain[37] = 0
Assert-Equal ($expand.Invoke($null, [object[]]@($boundaryTerrain, 8, 8, [single]80, [single]0, [single]79,
    [single]40, [single]40, [single]10))) ([single]10) 'Native radius boundary must saturate'
$terrain = [single[]]::new(81)
$terrain[40] = 1
Assert-Equal ($expand.Invoke($null, [object[]]@($terrain, 9, 9, [single]90, [single]45, [single]1,
    [single]40, [single]40, [single]15))) ([single]15) 'Unplayable border does not block completed map'
Assert-Equal ($expand.Invoke($null, [object[]]@($terrain, 9, 9, [single]90, [single]0, [single]89,
    [single]40, [single]40, [single]0))) ([single]0) 'Disabled radius stays disabled'

# Reproduce a completed circle with a thin incomplete edge. The native grid is
# sampled at whole coordinates here, avoiding float boundary ambiguity.
$edgeTerrain = [single[]]::new(4096)
$innerCells = [System.Collections.Generic.List[int]]::new()
$outerCells = [System.Collections.Generic.List[int]]::new()
for ($row = 0; $row -lt 64; $row++) {
    for ($column = 0; $column -lt 64; $column++) {
        $d2 = ($row - 32) * ($row - 32) + ($column - 32) * ($column - 32)
        $index = $row * 64 + $column
        if ($d2 -le 100) { $innerCells.Add($index); $edgeTerrain[$index] = 1 }
        elseif ($d2 -le 400) { $outerCells.Add($index) }
    }
}
function Get-EdgeTerraformRadius {
    return $expand.Invoke($null, [object[]]@($edgeTerrain, 64, 64, [single]64, [single]0, [single]63,
        [single]32, [single]32, [single]10))
}
$allowedInnerHoles = [int][Math]::Floor($innerCells.Count * 0.05)
for ($i = 0; $i -lt $allowedInnerHoles; $i++) { $edgeTerrain[$innerCells[$i]] = 0 }
Assert-Equal (Get-EdgeTerraformRadius) ([single]20) 'Small unfinished edge allows first doubling'
Assert-Equal (Get-EdgeTerraformRadius) ([single]20) 'Saved progress reconstructs the same expanded stage'
$edgeTerrain[$innerCells[$allowedInnerHoles]] = 0
Assert-Equal (Get-EdgeTerraformRadius) ([single]10) 'More than five percent unfinished blocks expansion'
$edgeTerrain[$innerCells[$allowedInnerHoles]] = [single]::NaN
Assert-Equal (Get-EdgeTerraformRadius) ([single]10) 'Invalid cells count as unfinished'
foreach ($index in $innerCells) { $edgeTerrain[$index] = 1 }
foreach ($index in $outerCells) { $edgeTerrain[$index] = 1 }
$allowedOuterHoles = [int][Math]::Floor(($innerCells.Count + $outerCells.Count) * 0.05)
for ($i = 0; $i -lt $allowedOuterHoles; $i++) { $edgeTerrain[$outerCells[$i]] = 0 }
Assert-Equal (Get-EdgeTerraformRadius) ([single]40) 'Second circle uses its own area threshold'
$edgeTerrain[$outerCells[$allowedOuterHoles]] = 0
Assert-Equal (Get-EdgeTerraformRadius) ([single]20) 'Unfinished outer circle holds existing expansion'

# Exactly 19 of 20 local cells saturated must pass the inclusive 95% threshold.
$exactTerrain = [single[]](1..21 | ForEach-Object { 1 })
$exactTerrain[0] = 0
$exactTerrain[20] = 0
$exactArgs = [object[]]@($exactTerrain, 21, 1, [single]21, [single]0, [single]20,
    [single]0, [single]0, [single]19)
Assert-Equal ($expand.Invoke($null, $exactArgs)) ([single]38) 'Exactly 95 percent saturation expands'
$exactTerrain[1] = 0
Assert-Equal ($expand.Invoke($null, $exactArgs)) ([single]19) 'Below 95 percent does not expand'

$original = [System.Collections.Generic.Dictionary[long,Terraformer]]::new()
$temporary = [System.Collections.Generic.Dictionary[long,Terraformer]]::new()
$failure = [InvalidOperationException]::new('simulated scheduling failure')
$cleanup = [object[]]@($failure, $temporary, $original)
$result = $terraform.GetMethod('TerraformFinalizer').Invoke($null, $cleanup)
Assert-Equal ([object]::ReferenceEquals($cleanup[1], $original)) $true 'Scheduling failure restores original building registry'
Assert-Equal $result $failure 'Terraform finalizer preserves scheduling exception'

foreach ($case in @(@('{}', $true), @('{"Terraforming":{}}', $true),
    @('{"Terraforming":{"ExpandWhenLocalAreaSaturated":false}}', $false))) {
    $loaded = [Newtonsoft.Json.JsonConvert]::DeserializeObject($case[0], $settingsType)
    $section = $settingsType.GetField('Terraforming').GetValue($loaded)
    Assert-Equal $section.GetType().GetField('ExpandWhenLocalAreaSaturated').GetValue($section) $case[1] 'Terraform setting defaults and opt-out'
}
foreach ($field in @(
    @('terraformers', [System.Collections.Generic.Dictionary[long,Terraformer]]),
    @('progress', [Unity.Collections.NativeArray[single]]),
    @('progressDim', [Unity.Mathematics.int2]),
    @('mapCorner', [single]), @('mapSize', [single]), @('worldSize', [single]),
    @('jobSequentialScheduler', [JobSequentialScheduler])
)) {
    $requireField.Invoke($null, @([TerraformBuildingsManager], $field[0], $field[1]))
}
Write-Output 'PASS: terraform 95-percent area saturation, edge holes, staged doubling, playable bounds, settings, and registry cleanup.'
