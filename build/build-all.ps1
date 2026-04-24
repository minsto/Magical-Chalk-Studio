[CmdletBinding()]
param(
    [string] $Version = "1.0.0",
    [string] $Configuration = "Release",
    [switch] $SingleFile,
    [switch] $SkipPublish,
    [switch] $SkipInstaller
)

$ErrorActionPreference = "Stop"
$buildDir = $PSScriptRoot
$root = Split-Path $buildDir -Parent
$proj = Join-Path (Join-Path $root "MagicalChalkStudio.Wpf") "MagicalChalkStudio.Wpf.csproj"

if (-not (Test-Path $proj)) {
    throw "Projet introuvable: $proj"
}

Write-Host "[1/3] Build projet ($Configuration)" -ForegroundColor Cyan
& dotnet build $proj -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipPublish) {
    Write-Host "[2/3] Publish runtimes Windows" -ForegroundColor Cyan
    $publishScript = Join-Path $buildDir "publish-windows.ps1"
    $args = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $publishScript, "-Version", $Version, "-Configuration", $Configuration)
    if ($SingleFile) { $args += "-SingleFile" }
    & powershell @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not $SkipInstaller) {
    Write-Host "[3/3] Compile installateur (Inno Setup)" -ForegroundColor Cyan
    $compileScript = Join-Path $buildDir "compile-installer.ps1"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $compileScript
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Terminé." -ForegroundColor Green
if (-not $SkipPublish) {
    Write-Host "ZIP: $buildDir\\dist" -ForegroundColor Green
}
if (-not $SkipInstaller) {
    Write-Host "Setup: $buildDir\\output" -ForegroundColor Green
}
