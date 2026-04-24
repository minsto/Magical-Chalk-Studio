# Cherche ISCC (Inno Setup) et compile MagicalChalkStudio.iss
# Prérequis : lancer d'abord publish-windows.ps1

$ErrorActionPreference = "Stop"
$iss = Join-Path $PSScriptRoot "MagicalChalkStudio.iss"
$candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Error "ISCC introuvable. Installez Inno Setup 6 : https://jrsoftware.org/isdl.php"
}
& $iscc $iss
Write-Host "OK : installeur dans build\output\" -ForegroundColor Green
