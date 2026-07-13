# Build a portable, self-contained VisualEQ zip for Windows x64.
#
# Usage: ./publish-release.ps1 [-Version <string>] [-OutDir <path>]
#
# Output: release\VisualEQ-<Version>-win-x64.zip
#
# End users unzip this and run VisualEQ.exe. No .NET install needed.
# Assets (converted zone/chr zips) go to %APPDATA%\VisualEQ\zones\ at runtime.
#
# CI runs this same script — do NOT diverge the CI workflow from what this script does,
# so that "publishes locally" and "publishes on GitHub Actions" always match.

param(
    [string]$Version = "0.0.0-dev",
    [string]$OutDir  = "release"
)

$ErrorActionPreference = "Stop"

$repoRoot   = $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish/VisualEQ"
$outAbsDir  = Join-Path $repoRoot $OutDir

Write-Host "Publishing VisualEQ v$Version (win-x64, self-contained)..."

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (-not (Test-Path $outAbsDir)) { New-Item -ItemType Directory -Path $outAbsDir | Out-Null }

# Multi-file publish (not PublishSingleFile) — cimgui.dll is a native dep and cannot be
# bundled into a single-file exe anyway, so single-file gives no size win but hurts
# debuggability.
& dotnet publish (Join-Path $repoRoot "VisualEQ/VisualEQ.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Sanity check: cimgui.dll must sit next to VisualEQ.exe or the app will fail to start
# on a fresh machine. The CopyNativeLibsOnPublish MSBuild target should have handled this
# but a missing dll here means the layout is wrong and the zip would ship broken.
$cimgui = Join-Path $publishDir "cimgui.dll"
if (-not (Test-Path $cimgui)) {
    throw "cimgui.dll not found next to VisualEQ.exe at $publishDir. CopyNativeLibsOnPublish MSBuild target may have failed."
}

$zipName = "VisualEQ-$Version-win-x64.zip"
$zipPath = Join-Path $outAbsDir $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Wrote $zipPath ($sizeMB MB)"
