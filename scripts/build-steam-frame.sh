#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../VizardUnityProject" && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-}"
APK_PATH="${VIZARD_ANDROID_APK_PATH:-$PROJECT_DIR/Builds/SteamFrame/Vizard.apk}"

if [[ -z "$UNITY_EDITOR" ]]; then
    for candidate in \
        "/Applications/Unity/Hub/Editor/6000.0.68f1/Unity.app/Contents/MacOS/Unity" \
        "$HOME/Applications/Unity/Hub/Editor/6000.0.68f1/Unity.app/Contents/MacOS/Unity"; do
        if [[ -x "$candidate" ]]; then
            UNITY_EDITOR="$candidate"
            break
        fi
    done
fi

if [[ -z "$UNITY_EDITOR" || ! -x "$UNITY_EDITOR" ]]; then
    echo "Unity 6000.0.68f1 was not found. Set UNITY_EDITOR to its executable path." >&2
    exit 2
fi

export VIZARD_ANDROID_APK_PATH="$APK_PATH"
mkdir -p "$(dirname "$APK_PATH")"
"$UNITY_EDITOR" -batchmode -quit \
    -projectPath "$PROJECT_DIR" \
    -buildTarget Android \
    -executeMethod SteamFrameBuild.Build \
    -logFile -

if [[ "${1:-}" == "--deploy" ]]; then
    ADB="${ADB:-adb}"
    FRAME_ADB="${STEAM_FRAME_ADB:-frame:5555}"
    "$ADB" connect "$FRAME_ADB"
    "$ADB" install -r "$APK_PATH"
    "$ADB" shell monkey -p com.Vizard.Vizard 1
fi

echo "APK: $APK_PATH"
