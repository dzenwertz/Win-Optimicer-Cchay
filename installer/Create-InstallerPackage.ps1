# Script de Empaquetado de Instalación de RETH Optimicer
param(
    [string]$InstallPath = "$env:LocalAppData\Programs\RETH Optimicer"
)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  RETH OPTIMICER - INSTALADOR" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$publishExe = Join-Path $PSScriptRoot "..\publish\RethOptimicer.exe"

if (-not (Test-Path $publishExe)) {
    Write-Host "Error: No se encontró el ejecutable publicado en $publishExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

$publishFolder = Join-Path $PSScriptRoot "..\publish"
Copy-Item "$publishFolder\*" $InstallPath -Recurse -Force
$destExe = Join-Path $InstallPath "RethOptimicer.exe"
$legacyExe = Join-Path $InstallPath "cchay-optimicer-cs.exe"
if ((Test-Path $legacyExe) -and -not (Test-Path $destExe)) {
    Move-Item -Path $legacyExe -Destination $destExe -Force
}
Write-Host "[+] Archivos de instalación copiados a: $InstallPath" -ForegroundColor Green

# Create Desktop Shortcut
$wshShell = New-Object -ComObject WScript.Shell
$desktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopPath "RETH Optimicer.lnk"
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $destExe
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "RETH Optimicer - Optimizador de Rendimiento de PC"
$shortcut.Save()
Write-Host "[+] Acceso directo en Escritorio creado." -ForegroundColor Green

# Create Start Menu Shortcut
$startMenuPath = Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Programs)) "RETH Optimicer"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}
$startShortcut = $wshShell.CreateShortcut((Join-Path $startMenuPath "RETH Optimicer.lnk"))
$startShortcut.TargetPath = $destExe
$startShortcut.WorkingDirectory = $InstallPath
$startShortcut.Save()
Write-Host "[+] Acceso directo en Menú Inicio creado." -ForegroundColor Green

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " ¡Instalación de RETH Optimicer Completa!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
