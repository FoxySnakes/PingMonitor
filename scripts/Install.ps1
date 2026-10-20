[CmdletBinding()]
param(
    [switch]$StartWithWindows,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'PingMonitor nécessite Windows.' }
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Ce script installe la version Windows x64.' }

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'PingMonitor\PingMonitor.csproj'
$output = Join-Path $repoRoot 'artifacts\publish'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\PingMonitor'
$exePath = Join-Path $installDir 'PingMonitor.exe'

Get-Command dotnet -ErrorAction Stop | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'La compilation a échoué ; la version installée est inchangée.' }

Get-Process PingMonitor -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exePath } | Stop-Process
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $output '*') -Destination $installDir -Force
$iconPath = Join-Path $installDir 'PingMonitor.ico'
Copy-Item -LiteralPath (Join-Path $repoRoot 'PingMonitor\Assets\PingMonitor.ico') -Destination $iconPath -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'PingMonitor.lnk'))
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $iconPath + ',0'
$shortcut.Save()

if ($StartWithWindows) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty -Path $runKey -Name PingMonitor -Value ('"' + $exePath + '"') -PropertyType String -Force | Out-Null
}
if (-not $NoLaunch) {
    Start-Process -FilePath $exePath -WorkingDirectory $installDir -WindowStyle Hidden
}
Write-Output "PingMonitor installé : $exePath"
