# Build Vizard (Windows x64, Mono) from this checkout. Mirrors build_macos.sh without the macOS-only steps.
#   powershell -ExecutionPolicy Bypass -File C:\dev\vizard\build_windows.ps1 [-Run]
# Prereq: Unity Hub signed in (Personal license activated) + the Editor version in ProjectVersion.txt.
# Close any Editor that has this project open first: batch mode needs the project lock.
param([switch]$Run)
$ErrorActionPreference = "Stop"

$repo = $PSScriptRoot
$project = Join-Path $repo "VizardUnityProject"
$version = (Select-String -Path (Join-Path $project "ProjectSettings\ProjectVersion.txt") -Pattern "^m_EditorVersion: (.+)$").Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity. Install it from Unity Hub (Installs > Install Editor > Archive)." }

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logs = Join-Path $project "Logs\windows-build-$stamp"
New-Item -ItemType Directory -Force $logs | Out-Null
$output = Join-Path $project "Builds\Windows\Vizard.exe"
Write-Host "Unity $version · logs: $logs"

function Invoke-Unity([string]$name, [string[]]$unityArgs) {
    $log = Join-Path $logs "$name.log"
    $p = Start-Process -FilePath $unity -ArgumentList (@("-batchmode", "-nographics", "-quit", "-projectPath", "`"$project`"", "-logFile", "`"$log`"") + $unityArgs) -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) { throw "Unity step '$name' failed (exit $($p.ExitCode)). See $log" }
    Write-Host "  ok: $name"
}

git -C $repo lfs pull
if ($LASTEXITCODE -ne 0) { throw "git lfs pull failed" }

# 1) Resolve packages, compile, and locate TMP Essential Resources (separate invocation from the import).
if (-not (Test-Path (Join-Path $project "Assets\TextMesh Pro\Resources\TMP Settings.asset"))) {
    Invoke-Unity "setup-locate-tmp" @("-executeMethod", "BuildWindows.LocateTmpPackage")
    $tmp = (Get-Content (Join-Path $project "Library\VizardTmpPackage.txt") -Raw).Trim()
    # 2) Native package import (not AssetDatabase.ImportPackage, which -quit can cancel).
    Invoke-Unity "setup-import-tmp" @("-importPackage", "`"$tmp`"")
} else { Write-Host "  skip: TMP Essentials already imported" }

# 3) Addressables content + player build.
Invoke-Unity "build" @("-buildTarget", "Win64", "-executeMethod", "BuildWindows.Build", "-buildOutput", "`"$output`"")
if (-not (Select-String -Path (Join-Path $logs "build.log") -Pattern "VIZARD_BUILD_SUCCEEDED" -Quiet)) { throw "No success marker in build.log" }
if (-not (Test-Path $output)) { throw "Build reported success but $output is missing" }
$aa = Join-Path $project "Builds\Windows\Vizard_Data\StreamingAssets\aa\settings.json"
if (-not (Test-Path $aa)) { throw "Addressables runtime settings missing: $aa" }
Write-Host "Built $output"
if ($Run) { Start-Process $output }
