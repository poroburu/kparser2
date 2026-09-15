param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$Capture)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1')
. (Join-Path $PSScriptRoot 'parity-evidence.ps1')
$validated = Read-UiRun -Path $Manifest -CapturePath $Capture
if ($null -eq $validated) { throw 'Missing UI evidence' }
Write-Output 'UI manifest and artifacts validated'
