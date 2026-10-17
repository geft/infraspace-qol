param(
    [string]$ManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\InfraSpace\InfraSpace_Data\Managed'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($name in @('UnityEngine.CoreModule', 'DionicCore', 'DionicGame', 'InfraSpaceCore',
    'InfraSpaceGameExtensions', 'InfraSpaceGameFeatures', 'old', 'Assembly-CSharp')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $ManagedDir "$name.dll")) | Out-Null
}
$mod = [Reflection.Assembly]::LoadFrom((Join-Path $root 'bin/Release/net472/InfraSpaceQoL.dll'))
$speed = $mod.GetType('InfraSpaceQoL.SpeedPatches', $true)
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$options = $speed.GetField('Options', $staticFlags).GetValue($null)
$selectState = $speed.GetMethod('GetTimeStateForButton', $staticFlags)
$selectButton = $speed.GetMethod('GetButtonIndexForState', $staticFlags)
$manager = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TimeManager])
$scale = [TimeManager].GetMethod('GetScaleOfTimeState', [Reflection.BindingFlags]'Instance,Public,NonPublic')

function Assert-Equal($actual, $expected, $label) {
    if ($actual -ne $expected) { throw "$label`: expected $expected, got $actual" }
}

Assert-Equal $options.Length 2 'Two custom speed controls'
$savedIndices = @()
$hashes = @()
try {
    for ($i = 0; $i -lt $options.Length; $i++) {
        $option = $options[$i]
        $type = $option.GetType()
        $state = $type.GetField('State', $instanceFlags).GetValue($option)
        $multiplier = $type.GetField('Multiplier', $instanceFlags).GetValue($option)
        $index = $type.GetField('ButtonIndex', $instanceFlags)
        $savedIndices += $index.GetValue($option)
        Assert-Equal $multiplier (@(4, 8)[$i]) 'Configured multiplier'
        Assert-Equal ($scale.Invoke($manager, @($state))) ([single]$multiplier) 'Actual native game multiplier'
        Assert-Equal ($selectButton.Invoke($null, @($state))) -1 'No selection before dashboard initialization'

        $resource = $type.GetField('Resource', $instanceFlags).GetValue($option)
        $stream = $mod.GetManifestResourceStream($resource)
        if (!$stream) { throw "Missing embedded icon: $resource" }
        try {
            $buffer = [IO.MemoryStream]::new()
            try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $buffer.Dispose() }
        } finally { $stream.Dispose() }
        Assert-Equal ([BitConverter]::ToString($bytes[0..7])) '89-50-4E-47-0D-0A-1A-0A' 'Embedded icon is a PNG'
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hashes += [BitConverter]::ToString($sha.ComputeHash($bytes)) } finally { $sha.Dispose() }
    }
    if ($hashes[0] -eq $hashes[1]) { throw '4x and 8x must have different icon artwork' }

    # The sprite must retain button padding rather than stretch its ink to bar height.
    $render = $speed.GetMethod('BuildIconPixels', $staticFlags)
    $white = [UnityEngine.Color32]::new(255, 255, 255, 255)
    foreach ($size in @(@(64, 64), @(96, 64), @(128, 32))) {
        $width, $height = $size
        $mask = [Array]::CreateInstance([UnityEngine.Color32], $width * $height)
        for ($p = 0; $p -lt $mask.Length; $p++) { $mask[$p] = $white }
        $pixels = $render.Invoke($null, @($mask, $width, $height, $white))
        $minX = $width; $minY = $height; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                if ($pixels[$y * $width + $x].a -gt 0) {
                    $minX = [Math]::Min($minX, $x); $minY = [Math]::Min($minY, $y)
                    $maxX = [Math]::Max($maxX, $x); $maxY = [Math]::Max($maxY, $y)
                }
            }
        }
        $paintedWidth = $maxX - $minX + 1; $paintedHeight = $maxY - $minY + 1
        if ($paintedHeight -gt [Math]::Ceiling($height * 0.45)) { throw 'Icon exceeds native glyph height' }
        if ($paintedWidth -gt [Math]::Ceiling($width * 0.8)) { throw 'Icon exceeds padded button width' }
        if ($minY -le 0 -or $maxY -ge ($height - 1)) { throw 'Vertical button padding was removed' }
        if ([Math]::Abs($minX - ($width - 1 - $maxX)) -gt 1 -or
            [Math]::Abs($minY - ($height - 1 - $maxY)) -gt 1) { throw 'Icon is not centered' }
        if ([Math]::Abs($paintedWidth - $paintedHeight * $width / $height) -gt 2) { throw 'Icon aspect ratio changed' }
    }

    # Exercise dynamic placement, including when another mod adds controls before ours.
    foreach ($firstIndex in @(3, 6)) {
        for ($i = 0; $i -lt $options.Length; $i++) {
            $options[$i].GetType().GetField('ButtonIndex', $instanceFlags).SetValue($options[$i], $firstIndex + $i)
        }
        for ($i = 0; $i -lt $options.Length; $i++) {
            $state = $options[$i].GetType().GetField('State', $instanceFlags).GetValue($options[$i])
            Assert-Equal ($selectState.Invoke($null, @($firstIndex + $i))) $state 'Click selects matching native state'
            Assert-Equal ($selectButton.Invoke($null, @($state))) ($firstIndex + $i) 'Resume selects matching custom button'
        }
        foreach ($native in @([TimeState]::Paused, [TimeState]::Normal, [TimeState]::Fast)) {
            Assert-Equal ($selectButton.Invoke($null, @($native))) -1 'Native states retain native buttons'
        }
        foreach ($nativeIndex in @(-1, 0, 1, 2, 99)) {
            Assert-Equal ($selectState.Invoke($null, @($nativeIndex))) $null 'Unrelated controls keep native handling'
        }
    }
} finally {
    for ($i = 0; $i -lt $savedIndices.Count; $i++) {
        $options[$i].GetType().GetField('ButtonIndex', $instanceFlags).SetValue($options[$i], $savedIndices[$i])
    }
}
Write-Output 'PASS: native 4x/8x multipliers, dynamic button selection, resume selection, embedded icons, and padded glyph sizing.'
