#!/bin/bash
set -euo pipefail

repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
cp "$repo_dir/build_macos.sh" "$test_dir/"
project="$test_dir/VizardUnityProject"
mkdir -p "$test_dir/bin" "$project/ProjectSettings" \
    "$project/Assets/TextMesh Pro/Resources" "$project/Builds/macOS/Vizard.app" \
    "$test_dir/editor/Unity.app/Contents/MacOS" \
    "$test_dir/editor/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport"
echo 'm_EditorVersion: 6000.0.68f1' > "$project/ProjectSettings/ProjectVersion.txt"
touch "$project/Assets/TextMesh Pro/Resources/TMP Settings.asset"
touch "$project/Builds/macOS/Vizard.app/old-build"
touch "$test_dir/editor/Unity.app/Contents/MacOS/Unity"
chmod +x "$test_dir/editor/Unity.app/Contents/MacOS/Unity"
export TEST_BUILD_DIR="$test_dir"
# Use a tiny archive and an isolated install directory; exercise real ZIP and SHA tools.
mkdir -p "$test_dir/hd-fixture" "$test_dir/models"
sed -n '/^cat > "\$hd_manifest"/,/^HD_CHECKSUMS/p' "$repo_dir/build_macos.sh" | \
    sed '1d;$d' > "$test_dir/pinned.sha256"
while read -r checksum filename; do
    echo "test asset: $filename" > "$test_dir/hd-fixture/$filename"
done < "$test_dir/pinned.sha256"
(cd "$test_dir/hd-fixture" && shasum -a 256 * > "$test_dir/fixture.sha256" && \
    /usr/bin/zip -q "$test_dir/hd.zip" *)
fixture_sha=$(shasum -a 256 "$test_dir/hd.zip" | awk '{print $1}')
awk 'NR == FNR {hash[$2]=$1; next} $2 in hash {print hash[$2] "  " $2; next} {print}' \
    "$test_dir/fixture.sha256" "$repo_dir/build_macos.sh" | \
    sed -e "s/^hd_archive_sha=.*/hd_archive_sha=$fixture_sha/" \
        -e 's|^hd_dir=.*|hd_dir="$TEST_BUILD_DIR/models"|' > "$test_dir/build_macos.sh"
echo 'existing asteroid asset' > "$test_dir/models/asteroids.bundle"
cat > "$test_dir/bin/curl" <<'STUB'
#!/bin/bash
echo download >> "$TEST_BUILD_DIR/downloads"
[[ ${TEST_DOWNLOAD_RC:-0} == 0 ]] || exit "$TEST_DOWNLOAD_RC"
while [[ $# -gt 0 ]]; do
    if [[ $1 == --output ]]; then
        shift
        cp "$TEST_BUILD_DIR/hd.zip" "$1"
        if [[ ${TEST_CORRUPT_DOWNLOAD:-0} == 1 ]]; then echo corrupt >> "$1"; fi
    fi
    shift
done
STUB
cat > "$test_dir/bin/unity" <<'STUB'
#!/bin/bash
case "$1 $2" in
    'editors path') printf '{"data":{"path":"%s/editor"}}\n' "$TEST_BUILD_DIR" ;;
    'run '*) exit 23 ;;
    'build '*) exit "${TEST_UNITY_BUILD_RC:-42}" ;;
    *) exit 99 ;;
esac
STUB
cat > "$test_dir/bin/uname" <<'STUB'
#!/bin/bash
if [[ $1 == -s ]]; then echo "${TEST_BUILD_OS:-Darwin}"; else echo arm64; fi
STUB
printf '#!/bin/bash\nexit 0\n' > "$test_dir/bin/git"
chmod +x "$test_dir/bin/"*
export PATH="$test_dir/bin:$PATH"

expect_failure() {
    expected=$1
    shift
    rc=0
    "$test_dir/build_macos.sh" "$@" > "$test_dir/output" 2>&1 || rc=$?
    [[ $rc == "$expected" ]]
    ! grep -q '^Built:' "$test_dir/output"
    [[ -f "$project/Builds/macOS/Vizard.app/old-build" ]]
}

expect_failure 2 --unknown
echo 'PASS invalid argument'
TEST_BUILD_OS=Linux expect_failure 1
echo 'PASS unsupported platform'
expect_failure 42
echo 'PASS failed build does not report existing app as success'
[[ $(wc -l < "$test_dir/downloads") == 1 ]]
(cd "$test_dir/models" && shasum -a 256 -c .vizard-hd-materials.sha256 >/dev/null)
echo 'PASS first HD installation verifies all files'
expect_failure 42
[[ $(wc -l < "$test_dir/downloads") == 1 ]]
grep -q 'HD Materials already verified' "$test_dir/output"
echo 'PASS verified HD installation skips download'
hd_catalog=$(awk '/\.json$/ {print $2}' "$test_dir/fixture.sha256")
rm "$test_dir/models/$hd_catalog"
expect_failure 42
[[ $(wc -l < "$test_dir/downloads") == 2 ]]
echo 'PASS missing HD file is repaired'
echo damaged > "$test_dir/models/$hd_catalog"
TEST_DOWNLOAD_RC=22 expect_failure 22
[[ $(cat "$test_dir/models/$hd_catalog") == damaged ]]
! grep -q '^Building Vizard' "$test_dir/output"
echo 'PASS failed HD download preserves installation and prevents build'
TEST_CORRUPT_DOWNLOAD=1 expect_failure 1
[[ $(cat "$test_dir/models/$hd_catalog") == damaged ]]
! grep -q '^Building Vizard' "$test_dir/output"
echo 'PASS incorrect archive checksum prevents installation and build'
expect_failure 42
(cd "$test_dir/models" && shasum -a 256 -c .vizard-hd-materials.sha256 >/dev/null)
[[ $(cat "$test_dir/models/asteroids.bundle") == 'existing asteroid asset' ]]
echo 'PASS damaged HD file is repaired and other bundles are preserved'
TEST_UNITY_BUILD_RC=0 expect_failure 2
echo 'PASS zero exit without a fresh build marker is rejected'
rm "$project/Assets/TextMesh Pro/Resources/TMP Settings.asset"
expect_failure 23
echo 'PASS failed setup prevents build'
