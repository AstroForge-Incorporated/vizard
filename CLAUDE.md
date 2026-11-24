# Vizard local macOS build

This repository contains Vizard, the Unity visualization application.
The Unity project is `VizardUnityProject/`.

## Build first

Run the existing script instead of reconstructing Unity commands:

```bash
./build_macos.sh        # Set up missing dependencies and build
./build_macos.sh --run  # Build, then open the application
```

A running Vizard keeps the build it was launched from. Rebuild and relaunch
before you measure a UI change, or you will read the old layout.

The script also works by absolute path from another directory. It requires an
Apple silicon Mac and Homebrew. It installs missing Unity CLI and Git LFS tools,
reads the exact Editor version from `ProjectSettings/ProjectVersion.txt`, and
installs that Editor if missing. Existing tools are not upgraded. macOS Mono
build support is part of the Mac Editor installation.

The Unity Editor checks the local license at startup. If activation fails, sign
in and activate the Editor license in Unity Hub, then rerun the script. Hub can
stay open. Close any Editor using this same project before a batch build; do not
kill another Editor or delete its lock file.

## What the script owns

- Retrieve missing Git LFS assets for the current checkout.
- Install the official macOS HD Materials bundle by default. Verify the pinned
  file checksums on every run; skip downloading when complete, or reinstall
  missing or damaged files. Download and validate in staging before installation.
- Let Unity resolve the existing package manifest and lockfile.
- Import TMP Essential Resources from the resolved UI package when missing.
  Package resolution, native `-importPackage`, and building use separate Editor
  invocations. Do not replace native import with `AssetDatabase.ImportPackage`
  inside a batch execute method: it queues work that `-quit` can cancel.
- Build Apple silicon ARM64 with the existing Mono backend, startup scene first,
  followed by the normal main scene. The optional VR scene is excluded.
- Generate the base Addressables catalog before the player build. Even without
  optional HD bundles, runtime initialization needs `StreamingAssets/aa/settings.json`.
- Reuse Unity caches. Preserve logs and stop on failed commands. Validate the
  current build's success marker, ARM64 executable, and Addressables runtime
  settings before reporting success.

`Assets/Editor/BuildMac.cs` supplies the Unity-only setup and build operations.
Do not add another build wrapper or install a Python environment for this flow.

## Outputs and checks

Application: `VizardUnityProject/Builds/macOS/Vizard.app`.
HD Materials: `~/Library/Application Support/Vizard/Vizard/Resources/CustomModels`.
These files are shared by this user's Vizard installations, outside Git and Git
LFS. Keep the catalog, hash, and bundles directly in that directory, without a
subfolder. The installer preserves other bundles. Restart Vizard after installing.

The pinned download is
`https://hanspeterschaub.info/bskFiles/Assets/Vizard_HD_Materials_macOS.zip`,
catalog `2026.03.13.19.46.48` (about 124 MB downloaded, 137 MB installed).
The script pins both the archive and extracted file SHA-256 checksums. The
installed `.vizard-hd-materials.sha256` records the file identity and checksums.
If the upstream archive changes, the script stops rather than accepting new
content. Review and test the replacement with the project's Unity version before
updating the pins; do not bypass checksum verification.

Each invocation prints its own log directory under `VizardUnityProject/Logs/`.
`hd-materials.log` records installation or checksum verification. Failed staging
files stay beside that log for inspection; successful staging files are removed.
The setup log is present only when TMP setup runs. Unity CLI also writes build
provenance beside the application. Generated resources, caches, logs, and builds
are covered by the project's existing `.gitignore`.

For build changes, run `bash -n build_macos.sh` and
`bash tests/test_build_macos.sh`, build once, then build again to
verify repeatability. Confirm the second build skips TMP setup and the HD download.
Inspect the app and play a known Basilisk recording; a successful compiler exit
alone does not verify rendering. Start through the welcome screen:

```bash
./build_macos.sh --run
```

Choose `Select`, open a Basilisk scenario `.bin` recording, then choose
`Start Visualization`. Supply your own recording; none is bundled with this
build setup.

Known upstream startup limitation: `-loadFile` can create planets before the
asynchronous HD materials finish loading. The logs can show successful HD loads
after playback starts while Earth still uses its basic material. Use the welcome
screen for HD playback. The installer does not change runtime loading code.

Verify readable startup text, scene rendering, and play/pause controls. Runtime
logs normally appear at `~/Library/Logs/Vizard/Vizard/Player.log`; pass Unity's
`-logFile` argument to use a separate log for a verification run.

Confirm runtime logs show Earth, Venus, and Mars HD materials loading. Missing
`Mesh_earthMaterial_HD`, `Mesh_venusMaterial_HD`, or `Mesh_marsMaterial_HD`, shader
errors, a missing base Addressables `settings.json`, or runtime exceptions are
not expected. Treat bundle compatibility failures as unresolved even if the build
passed. Check Earth's atmosphere in playback. HD Materials do not change the
spacecraft shadow-brightness or ambient-light settings.

On failure, inspect the printed logs and fix the first relevant error. Preserve
the Editor and package versions unless a demonstrated incompatibility requires
a change. Do not clear caches routinely or treat an older `.app` as a new build.

Keep changes local unless explicitly asked to commit or push. This workflow does
not purchase services, install other optional bundles or VR support, or notarize an
application for distribution.
