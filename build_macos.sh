#!/bin/bash
set -euo pipefail

case "${1:-}" in
    ""|--run) ;;
    --help|-h) echo "Usage: $0 [--run]"; exit 0 ;;
    *) echo "Usage: $0 [--run]" >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo "Too many arguments" >&2; exit 2; }
[[ $(uname -s) == Darwin && $(uname -m) == arm64 ]] || {
    echo "This build requires an Apple silicon Mac." >&2; exit 1;
}

repo_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
project_dir="$repo_dir/VizardUnityProject"
editor_version=$(awk '/^m_EditorVersion: / {print $2}' "$project_dir/ProjectSettings/ProjectVersion.txt")
[[ -n "$editor_version" ]] || { echo "Missing Unity Editor version" >&2; exit 1; }
mkdir -p "$project_dir/Logs" "$project_dir/Builds/macOS"
log_dir=$(mktemp -d "$project_dir/Logs/macos.XXXXXX")
on_exit() {
    rc=$?
    if (( rc != 0 )); then
        echo "Build failed. Logs: $log_dir" >&2
        if grep -Eiq 'No valid Unity Editor license|license.*(invalid|expired)|Failed to activate.*license' "$log_dir"/*.log 2>/dev/null; then
            echo "Open Unity Hub, sign in, and activate your Editor license, then retry." >&2
        fi
    fi
}
trap on_exit EXIT
export HOMEBREW_NO_AUTO_UPDATE=1 HOMEBREW_NO_INSTALL_CLEANUP=1

if ! command -v unity >/dev/null; then brew install --cask unity-cli; fi
if ! git lfs version >/dev/null 2>&1; then brew install git-lfs; fi
git -C "$repo_dir" lfs install --local
git -C "$repo_dir" lfs pull

# Official macOS HD Materials, catalog 2026.03.13.19.46.48.
# Revalidate with the project's Editor before changing these checksum pins.
hd_dir="$HOME/Library/Application Support/Vizard/Vizard/Resources/CustomModels"
hd_archive_sha=7355c12eef62ada0e76f0179ec67a97f5e8cc99d490e0004111be35ceddea118
hd_manifest="$log_dir/hd-materials.sha256"
cat > "$hd_manifest" <<'HD_CHECKSUMS'
bd70cd077b4cf8b2c314c6e2803bae60edf6a593794f60b1dad9124140edf3e9  Vizard_HD_Materials_unitybuiltinshaders_40a4f9a6693b81f075d49c3c05810af6.bundle
f98159407582496c95f2eaae55621697160386e2fe447f486ad46047a378af0e  vizard_hd_materials_assets_all_d11edd2a0945fceda08eb5ebe588a194.bundle
88ae3658e3366e98f1ceddea4a3fd1447edd0f4b9794c2b0edabf2d59bb5c8e5  Vizard_HD_Materials_catalog_2026.03.13.19.46.48.hash
9e28c26b1e612d01917d0e653f9f0fdf686621f8914c915c1f0bffa6f912e615  Vizard_HD_Materials_catalog_2026.03.13.19.46.48.json
HD_CHECKSUMS
if cmp -s "$hd_manifest" "$hd_dir/.vizard-hd-materials.sha256" && \
    (cd "$hd_dir" && shasum -a 256 -c "$hd_manifest") > "$log_dir/hd-materials.log" 2>&1; then
    echo "HD Materials already verified; skipping download."
else
    echo "Installing HD Materials... Logs: $log_dir/hd-materials.log"
    # Stage and validate everything before touching the installed assets.
    hd_stage="$log_dir/hd-materials"
    mkdir -p "$hd_stage"
    {
        curl --fail --location --retry 3 --connect-timeout 20 \
            --output "$hd_stage/hd.zip" \
            https://hanspeterschaub.info/bskFiles/Assets/Vizard_HD_Materials_macOS.zip
        echo "$hd_archive_sha  $hd_stage/hd.zip" | shasum -a 256 -c -
        unzip -q "$hd_stage/hd.zip" -d "$hd_stage"
        (cd "$hd_stage" && shasum -a 256 -c "$hd_manifest")
        mkdir -p "$hd_dir"
        rm -f "$hd_dir/.vizard-hd-materials.sha256"
        # Publish each complete file, with the catalog last. Leave other bundles alone.
        while read -r checksum filename; do
            cp "$hd_stage/$filename" "$hd_dir/.$filename.tmp"
            mv -f "$hd_dir/.$filename.tmp" "$hd_dir/$filename"
        done < "$hd_manifest"
        (cd "$hd_dir" && shasum -a 256 -c "$hd_manifest")
        cp "$hd_manifest" "$hd_dir/.vizard-hd-materials.sha256"
        rm -r "$hd_stage"
    } >> "$log_dir/hd-materials.log" 2>&1
    echo "HD Materials installed: $hd_dir"
fi

if ! unity editors path "$editor_version" --architecture arm64 --format json > "$log_dir/editor.json"; then
    unity install "$editor_version" --architecture arm64 --yes
    unity editors path "$editor_version" --architecture arm64 --format json > "$log_dir/editor.json"
fi
editor_dir=$(/usr/bin/plutil -extract data.path raw -o - "$log_dir/editor.json")
[[ -x "$editor_dir/Unity.app/Contents/MacOS/Unity" ]] || { echo "Editor installation is incomplete." >&2; exit 1; }
if [[ ! -d "$editor_dir/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport" ]]; then
    # macOS Mono support ships with the Mac Editor, not a separate module.
    unity install "$editor_version" --architecture arm64 --force --yes
fi

# Unity validates the local license on startup, including on repeat builds.
if [[ ! -f "$project_dir/Assets/TextMesh Pro/Resources/TMP Settings.asset" ]]; then
    echo "Preparing Unity packages and TMP resources..."
    unity run "$project_dir" --editor-version "$editor_version" --architecture arm64 \
        -- -buildTarget StandaloneOSX -executeMethod BuildMac.LocateTmpPackage -logFile "$log_dir/setup.log"
    tmp_package=$(cat "$project_dir/Library/VizardTmpPackage.txt")
    unity run "$project_dir" --editor-version "$editor_version" --architecture arm64 \
        -- -importPackage "$tmp_package" -logFile "$log_dir/import-tmp.log"
    [[ -f "$project_dir/Assets/TextMesh Pro/Resources/TMP Settings.asset" ]]
else
    echo "TMP resources already present; reusing Unity caches."
fi

app_path="$project_dir/Builds/macOS/Vizard.app"
echo "Building Vizard with Unity $editor_version..."
unity build "$project_dir" --editor-version "$editor_version" --architecture arm64 \
    --target StandaloneOSX --execute-method BuildMac.Build --output-path "$app_path" \
    --allow-dirty-build --log-file "$log_dir/build.log" --no-tail
grep -q 'VIZARD_BUILD_SUCCEEDED' "$log_dir/build.log"
executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app_path/Contents/Info.plist")
[[ -x "$app_path/Contents/MacOS/$executable" ]]
[[ $(/usr/bin/lipo -archs "$app_path/Contents/MacOS/$executable") == arm64 ]]
[[ -s "$app_path/Contents/Resources/Data/StreamingAssets/aa/settings.json" ]]
echo "Built: $app_path"
echo "Logs: $log_dir"
if [[ ${1:-} == --run ]]; then open "$app_path"; fi
