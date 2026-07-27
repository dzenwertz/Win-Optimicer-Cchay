# Script de Empaquetado de Instalación de Cchay Optimicer
param(
    [string]$InstallPath = "$env:LocalAppData\Programs\Cchay Optimicer"
)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  CCHAY OPTIMICER - INSTALADOR DE SYSTEM " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$publishExe = Join-Path $PSScriptRoot "..\publish\cchay-optimicer-cs.exe"

if (-not (Test-Path $publishExe)) {
    Write-Host "Error: No se encontró el ejecutable publicado en $publishExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

$destExe = Join-Path $InstallPath "CchayOptimicer.exe"
Copy-Item $publishExe $destExe -Force
Write-Host "[+] Ejecutable copiado a: $destExe" -ForegroundColor Green

# Create Desktop Shortcut
$wshShell = New-Object -ComObject WScript.Shell
$desktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopPath "Cchay Optimicer.lnk"
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $destExe
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "Cchay Optimicer - Optimizador de Rendimiento de PC"
$shortcut.Save()
Write-Host "[+] Acceso directo en Escritorio creado." -ForegroundColor Green

# Create Start Menu Shortcut
$startMenuPath = Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Programs)) "Cchay Optimicer"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}
$startShortcut = $wshShell.CreateShortcut((Join-Path $startMenuPath "Cchay Optimicer.lnk"))
$startShortcut.TargetPath = $destExe
$startShortcut.WorkingDirectory = $InstallPath
$startShortcut.Save()
Write-Host "[+] Acceso directo en Menú Inicio creado." -ForegroundColor Green

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " ¡Instalación de Cchay Optimicer Completa!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
