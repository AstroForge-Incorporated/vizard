#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../VizardUnityProject" && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.0.68f1/Unity.app/Contents/MacOS/Unity}"
LOG_PATH="$PROJECT_DIR/Builds/SteamFrame/ScenarioValidation/editor.log"
mkdir -p "$(dirname "$LOG_PATH")"
"$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_DIR" -buildTarget Android \
    -executeMethod VrMenuLocalTools.RunScenarioChecks -logFile "$LOG_PATH"
cat "$PROJECT_DIR/Builds/SteamFrame/ScenarioValidation/results.txt"
