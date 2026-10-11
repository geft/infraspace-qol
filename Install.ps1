$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$requiredFiles = @('InfraSpaceQoL.dll', '0Harmony.dll', 'config.json', 'settings.json')
$packageRoot = $PSScriptRoot
$buildOutput = Join-Path $PSScriptRoot 'bin\Release\net472'
$targetPath = Join-Path $env:USERPROFILE 'AppData\LocalLow\Dionic Software\InfraSpace\mods\InfraSpaceQoL'

function Test-PackageFolder([string] $Path) {
    foreach ($fileName in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $fileName) -PathType Leaf)) {
            return $false
        }
    }
    return $true
}

if (-not (Test-PackageFolder $packageRoot)) {
    if (Test-PackageFolder $buildOutput) {
        $packageRoot = $buildOutput
    }
    else {
        [System.Windows.Forms.MessageBox]::Show(
            "Mod files were not found. Extract the release ZIP, or build the project in Release configuration first.",
            'InfraSpace QoL Installer',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        exit 1
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'InfraSpace QoL Installer'
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ClientSize = New-Object System.Drawing.Size(560, 310)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

$title = New-Object System.Windows.Forms.Label
$title.Text = 'Install InfraSpace QoL'
$title.Font = New-Object System.Drawing.Font('Segoe UI', 15, [System.Drawing.FontStyle]::Bold)
$title.Location = New-Object System.Drawing.Point(18, 16)
$title.Size = New-Object System.Drawing.Size(520, 32)
$form.Controls.Add($title)

$description = New-Object System.Windows.Forms.Label
$description.Text = "Install the mod files in InfraSpace's local mods folder. Existing settings are preserved."
$description.Location = New-Object System.Drawing.Point(20, 55)
$description.Size = New-Object System.Drawing.Size(520, 36)
$form.Controls.Add($description)

$packageLabel = New-Object System.Windows.Forms.Label
$packageLabel.Text = 'Package:'
$packageLabel.Location = New-Object System.Drawing.Point(20, 104)
$packageLabel.Size = New-Object System.Drawing.Size(80, 20)
$form.Controls.Add($packageLabel)

$packageValue = New-Object System.Windows.Forms.Label
$packageValue.Text = $packageRoot
$packageValue.Location = New-Object System.Drawing.Point(102, 104)
$packageValue.Size = New-Object System.Drawing.Size(438, 36)
$packageValue.AutoEllipsis = $true
$form.Controls.Add($packageValue)

$destinationLabel = New-Object System.Windows.Forms.Label
$destinationLabel.Text = 'Install location:'
$destinationLabel.Location = New-Object System.Drawing.Point(20, 151)
$destinationLabel.Size = New-Object System.Drawing.Size(110, 20)
$form.Controls.Add($destinationLabel)

$destinationValue = New-Object System.Windows.Forms.TextBox
$destinationValue.Text = $targetPath
$destinationValue.Location = New-Object System.Drawing.Point(20, 174)
$destinationValue.Size = New-Object System.Drawing.Size(520, 24)
$destinationValue.ReadOnly = $true
$form.Controls.Add($destinationValue)

$openFolder = New-Object System.Windows.Forms.CheckBox
$openFolder.Text = 'Open the mod folder after installation'
$openFolder.Checked = $true
$openFolder.Location = New-Object System.Drawing.Point(20, 210)
$openFolder.Size = New-Object System.Drawing.Size(300, 24)
$form.Controls.Add($openFolder)

$status = New-Object System.Windows.Forms.Label
$status.Text = "The mod must also be enabled in InfraSpace's Mods menu."
$status.Location = New-Object System.Drawing.Point(20, 244)
$status.Size = New-Object System.Drawing.Size(520, 22)
$form.Controls.Add($status)

$installButton = New-Object System.Windows.Forms.Button
$installButton.Text = 'Install'
$installButton.Location = New-Object System.Drawing.Point(350, 272)
$installButton.Size = New-Object System.Drawing.Size(90, 28)
$installButton.Add_Click({
    try {
        $installButton.Enabled = $false
        $status.Text = 'Installing...'
        $form.Refresh()

        New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        foreach ($fileName in @('InfraSpaceQoL.dll', '0Harmony.dll', 'config.json')) {
            Copy-Item -LiteralPath (Join-Path $packageRoot $fileName) -Destination $targetPath -Force
        }

        # Keep the player's choices when updating; seed settings only on first install.
        $settingsPath = Join-Path $targetPath 'settings.json'
        if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
            Copy-Item -LiteralPath (Join-Path $packageRoot 'settings.json') -Destination $settingsPath
        }

        $status.Text = 'Installed. Enable InfraSpace QoL in the Mods menu.'
        [System.Windows.Forms.MessageBox]::Show(
            "Installation complete.`r`n`r`nEnable InfraSpace QoL in the game's Mods menu.",
            'InfraSpace QoL Installer',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null

        if ($openFolder.Checked) {
            Start-Process -FilePath 'explorer.exe' -ArgumentList $targetPath
        }
        $form.Close()
    }
    catch {
        $status.Text = 'Installation failed.'
        $installButton.Enabled = $true
        [System.Windows.Forms.MessageBox]::Show(
            $_.Exception.Message,
            'InfraSpace QoL Installer',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    }
})
$form.Controls.Add($installButton)
$form.AcceptButton = $installButton

$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Text = 'Close'
$closeButton.Location = New-Object System.Drawing.Point(450, 272)
$closeButton.Size = New-Object System.Drawing.Size(90, 28)
$closeButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
$form.Controls.Add($closeButton)
$form.CancelButton = $closeButton

[void] $form.ShowDialog()
