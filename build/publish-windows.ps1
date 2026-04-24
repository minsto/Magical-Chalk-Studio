# Publie Magical Chalk Studio pour les runtimes Windows (x64, x86, arm64) en autonome.
# Usage: .\publish-windows.ps1 [-Version "1.0.0"] [-Configuration Release] [-SingleFile]

[CmdletBinding()]
param(
    [string] $Version = "1.0.0",
    [string] $Configuration = "Release",
    [switch] $SingleFile
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path (Join-Path $root "MagicalChalkStudio.Wpf") "MagicalChalkStudio.Wpf.csproj"
if (-not (Test-Path $proj)) {
    Write-Error "Projet introuvable: $proj"
}
$distRoot = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

$rids = @("win-x64", "win-x86", "win-arm64")
$sf = $SingleFile.IsPresent

foreach ($rid in $rids) {
    $out = Join-Path $distRoot $rid
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    $ver4 = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }
    $args = @(
        "publish", $proj,
        "-c", $Configuration,
        "-r", $rid,
        "--self-contained", "true",
        "-o", $out,
        "/p:Version=$Version",
        "/p:FileVersion=$ver4",
        "/p:AssemblyVersion=$ver4"
    )
    if ($sf) { $args += @("/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true") }
    Write-Host "dotnet $($args -join ' ')" -ForegroundColor Cyan
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# Archives ZIP pour partage simple
$zipName = "MagicalChalkStudio-$Version"
foreach ($rid in $rids) {
    $src = Join-Path $distRoot $rid
    $dest = Join-Path $distRoot "$zipName-$rid.zip"
    if (Test-Path $dest) { Remove-Item -Force $dest }
    Get-ChildItem -Path $src | Compress-Archive -DestinationPath $dest -Force
    Write-Host "OK: $dest" -ForegroundColor Green
}

Write-Host "Terminé. Dossiers: $distRoot" -ForegroundColor Green
