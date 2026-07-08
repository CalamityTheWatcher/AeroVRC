<#
.SYNOPSIS
    Create (or refresh) a Desktop shortcut to the AeroVRC Publish & Release GUI.

.DESCRIPTION
    Run once per machine (e.g. after cloning the repo) to drop an
    "AeroVRC Publish" shortcut on your Desktop, using the AeroVRC icon. It points
    at "Publish AeroVRC.cmd" in this folder, so the GUI opens with no console.

.EXAMPLE
    .\create-shortcut.ps1
#>
$proj    = $PSScriptRoot
$desktop = [Environment]::GetFolderPath('Desktop')
$lnkPath = Join-Path $desktop 'AeroVRC Publish.lnk'
$target  = Join-Path $proj 'Publish AeroVRC.cmd'
$icon    = Join-Path $proj 'Logo.ico'

if (-not (Test-Path $target)) { throw "Launcher not found: $target" }

$ws  = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)
$lnk.TargetPath       = $target
$lnk.WorkingDirectory = $proj
if (Test-Path $icon) { $lnk.IconLocation = "$icon,0" }
$lnk.Description      = 'Open the AeroVRC Publish & Release tool'
$lnk.WindowStyle      = 7   # launch the cmd minimized so it doesn't flash a window
$lnk.Save()

Write-Host "Desktop shortcut created: $lnkPath"
