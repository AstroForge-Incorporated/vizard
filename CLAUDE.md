# Vizard local macOS build

This repository contains Vizard, the Unity visualization application. Its local
folder is named `basilisk`; the Unity project is `VizardUnityProject/`.

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

Choose `Select`, select
`/Users/emerson/git/mono_1/_VizFiles/vizard_test_viz_run_0_UnityViz.bin`, then
`Start Visualization`. This file was verified with the pinned HD bundle and Unity
6000.0.68f1: Earth renders clouds and atmospheric shading.

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

Fresh installs default to spacecraft shadow brightness `0.10`, ambient brightness
`0.3`, and Sun intensity `1`, with distance attenuation off. These defaults live in
`PersistentUserSettings.cs`. Saved user preferences take precedence; use File →
Settings → Restore Default Settings to apply them to an existing installation.
A recording can also override lighting when it explicitly supplies those settings.

## Scenario display defaults

Floating scenario labels capitalize their first character for display; recording
names and lookup keys stay unchanged. The main scene's Vizard watermark is inactive.
Reaction-wheel panels use the title `Reaction Wheels`, letter labels A, B, C,
centered under each bar pair. Speeds use rad/s and torques use mN·m, for both
live values and scale limits (including the verbose display). Live readings always
include a sign and one decimal place; scale labels have wider margins and do not wrap. The torque scale is
capped at the 100 mN·m flight-software command limit in `ReactionWheelUtilities`;
the DS2 simulation permits 150 mN·m. Recorded torque values remain unchanged.
Single-device storage panels use a compact row with the quantity visible by default.
Mass readings in kg always show five decimal places; the quantity is left-aligned
next to the bar and its width is measured from the formatted capacity.
`CheckScenarioUI.Run` checks label formatting and wheel speed units in a fresh
batch Editor; use the same `unity run` command below with that execute method.

## Xenon thruster plumes

All thrusters use the cyan Hall-style plume, even when a recording requests a
different color. The plume-color chooser is disabled; status bars retain their
existing firing/recently-fired colors. No changes to `thrColors` or new recordings
are needed. The existing plume lifetime scalar controls plume length, while the
annular outlet stays the same size.

`XenonPlume.cs` creates shared geometry and uses the bundled `XenonPlume.shader`.
The translucent cyan glow narrows downstream, with only 0.3% brightness modulation.
Its visual origin is 0.022 m along the exhaust direction from the recorded thruster
marker, with a 0.044 m maximum radius and a 0.6 m mesh length. This places the base
about 5 cm inside the white outlet of `ds2_propulsion_updated.obj` after the
recording's model offset. The first 0.05014 m is a wide, constant cylinder filling
the channel; taper and fading start at the exit face. The length control keeps
that transition at the exit. The shader uses a taper coefficient of 2.94, a plume
brightness gain of 0.54, and an annulus gain of 0.99. A smooth central boost in
the cone gives 3.5 times the previous core brightness, including the 1.5 times
overall increase. This does not move the
simulated thrust point. The geometry constants live in `XenonPlume.cs` and
must be adjusted when fitting a different thruster model.
The glow follows current/max thrust and disappears at zero thrust. Animation uses
recorded time so pausing freezes it and seeking does not leave particle trails.
The outlet radius and plume dimensions are visual approximations, not measured
thruster or plasma properties. The effect casts no shadows and adds no lights.

The build runs `CheckXenonPlume.Run` before compiling the player; look for
`XENON_PLUME_CHECKS_PASSED` in the build log. To check the rendered profile, run
`unity run VizardUnityProject -- -executeMethod CheckXenonPlume.Run -logFile Logs/xenon-render-check.log`
(without `-nographics`). This verifies a bright core, translucent outer glow, and narrowing profile and
saves `Logs/xenon-profile.png`; the headless build skips this image check. Verify
thruster A with the recording above (it may not contain a thruster B burn), including firing/off intervals, pause/resume, seeking, close and distant
views, and the plume length control. Check for shader errors in the player log.

On failure, inspect the printed logs and fix the first relevant error. Preserve
the Editor and package versions unless a demonstrated incompatibility requires
a change. Do not clear caches routinely or treat an older `.app` as a new build.

Keep changes local unless explicitly asked to commit or push. This workflow does
not purchase services, install other optional bundles or VR support, or notarize an
application for distribution.

## Gimbal position panel

New mono recordings carry `astroforge.gimbal_positions.v1` event snapshots. The
Gimbal Limits panel opens below the wheel panel and can be reopened from Actuators.
Yellow is the final commanded target, not the intermediate slew reference. Cyan
is the current simulation-truth position. There are no sensor/source labels. Missing values show a dash. The compact numerical section uses one `(θ, φ)°` column per gimbal and
starts expanded; its bottom button collapses it. Theta error wraps at 360 degrees.
An exclamation mark marks an out-of-range number; only marker placement is clamped.
Coincident cyan markers sit within wider yellow markers at the same position.

Python configuration lives under `[vizard]` in mono's spacecraft configuration:
`gimbal_theta_range_deg = [0.0, 360.0]`, `gimbal_phi_range_deg = [0.0, 20.0]`,
These are display settings, not physical limits.
Snapshots read final targets by configured ID and current simulation truth from
`get_gimbal_direction_p()` in platform frame P. No sensor telemetry is read and
no source field is sent. Previously generated recordings keep their original
current values; regenerate them to guarantee simulation-truth-only readings.
The native event object must stay alive for the sender plugin's lifetime.
No schema generation or Basilisk rebuild is needed. Older recordings do not gain
this data retroactively and play without the new panel. Each frame is read directly,
so seeking cannot reuse a future snapshot. The build runs `CheckGimbalPanel.Run`.

## Truth angular velocity

The compact Angular Velocity panel opens automatically for new mono recordings
carrying `astroforge.angular_velocity.v1`. X/Y/Z are body-frame truth rates from
`get_omega_b_truth()`, converted from rad/s to deg/s in Python. Unity computes the
Euclidean norm from the same vector. No sensor telemetry or differencing of
recorded attitudes is used. Missing or invalid data displays a dash.
The three signed cyan bars use fixed bounds of -10 to +10 deg/s; the yellow
norm bar spans 0 to 15 deg/s. Each bar has explicit endpoint labels. Only bar lengths clamp; numerical readings remain
unchanged. The compact panel has no scale footer.
Close and reopen the panel through Actuators; drag its title to move it.
The existing snapshot emitter sends the vector on every visualization frame.
Older recordings need to be regenerated to include these values.

The reaction-wheel panel reads `astroforge.reaction_wheel_allocator.v1` frame
snapshots from Core-GNC `ReactionWheelAllocatorTelem.allocator_mode`. The sender
uses the existing event transport; older recordings show `Allocator: N/A`.
Regenerate the recording and restart the rebuilt Vizard app to see the mode.

## Determination panel

The Determination panel reads `astroforge.determination.v1` snapshots and opens
below the Angular Velocity panel. Three rows -- Attitude, Sun, Ephemeris -- each show
a colored dot and a short state name. Python sends the raw protobuf enum names from
Core-GNC `GncSummaryTelemetry`; all color and text mapping is display policy in
`DeterminationPanel.Status`, which is a single flat lookup because protobuf enum
value names are package-scoped and therefore unique across the three enums.

Red is `UndeterminedAttitude`, `EphemerisUndetermined`, `UndeterminedSunVector`.
Yellow is `DeterminedCoarse` and `PropagatedFromFineSunSensors`. Green is
`DeterminedFine`, `EphemerisDetermined`, `DeterminedFromInertialModel`, and
`DeterminedFromFineSunSensors`; the inertial model is a nominal source, only
propagated sensor data is treated as degraded. An unknown or missing name shows a
grey dot and a dash, so an enum variant added later is visible rather than silently
mapped. Close and reopen the panel through Actuators.

This panel clones `Prefabs/GUIGenerics/GenericSubpanel`, not the reaction-wheel
panel: it carries `ClosePanelButton` and `DragPanel` with no extra children to hide.
Older recordings do not carry the event and never build the panel.
`CheckGimbalPanel.Run` checks every state name and the dialog-suppression filter.

## Star tracker panel

`StarTrackerPanel` reads `astroforge.star_trackers.v1` snapshots. Each tracker has
Sun and Earth `[angle, limit]` pairs in degrees from the same simulation objects
used by the text diagnostics. Clearance is strictly `angle > limit`; missing pairs
show a dash. The 0–180 degree bars clamp visually without changing readings.
The panel can be closed, dragged, and reopened through Actuators.

## Guidance panel

`GuidancePanel` reads `astroforge.guidance.v1` snapshots from Core-GNC attitude
targeter and slew-generator telemetry. The angle is remaining planned slew path,
not shortest attitude separation; it can exceed 180 degrees. Target type and
accepted count and detailed target vectors appear here; the grey overlay has no
POINTING section. New recordings are required for this panel.

The single `Guidance` panel includes the accepted target description below the
four rows; the description wraps and determines the panel height. The additive
`description` field in the existing guidance snapshot needs a new recording.
Older snapshots show `Target details unavailable`.

## Propulsion and momentum panels

`PropulsionMomentumPanels` consumes `astroforge.propulsion_momentum.v1` for two
independent windows; allocator details start collapsed. Momentum is the flight
estimate, while firing is simulation truth and independent of the work request.
Fuel comes from the named spacecraft's native `Total Propellant` storage record.
Consumption subtracts the current mass from `MessageList.FirstMessage` mass,
never from capacity or a running accumulator. Storage refreshes even when the
flight snapshot is unchanged. Only new recordings with this event and a single
propellant storage device suppress that storage window's initial opening; its
menu toggle and other storage panels remain available. `CheckScenarioUI.Run`
checks field presence, fallback, fuel precision, consumption and routing.

The Momentum & Allocator panel shows X/Y/Z and norm bars from the recorded
flight estimate, with cyan signed axes and a yellow norm. Scales are ±10 and
0–15 N·m·s for display only; values are not clipped. Momentum correction is not
displayed. Propulsion retains precise fuel and used-mass readings without a bar.
The legacy `HUD` event is suppressed, including in older recordings; other
scenario dialogs still work. No comparison copies are created.
