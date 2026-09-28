#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../VizardUnityProject" && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.0.68f1/Unity.app/Contents/MacOS/Unity}"
LOG_PATH="$PROJECT_DIR/Builds/SteamFrame/LocalValidation/editor.log"
mkdir -p "$(dirname "$LOG_PATH")"

# The Play Mode harness exits Unity with a nonzero status on failure.
# Do not add -quit: that would exit before the simulated interaction checks run.
"$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_DIR" -buildTarget Android \
    -executeMethod VrMenuLocalTools.RunChecks -logFile "$LOG_PATH"
cat "$PROJECT_DIR/Builds/SteamFrame/LocalValidation/results.txt"
echo "Screenshots and log: $(dirname "$LOG_PATH")"
