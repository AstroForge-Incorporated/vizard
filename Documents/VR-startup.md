# VR startup menu

Build the Android ARM64 APK locally with:

```sh
./scripts/build-steam-frame.sh
```

The output is `VizardUnityProject/Builds/SteamFrame/Vizard.apk`. This command does not install or launch anything on a headset. Unity 6000.0.68f1, its Android module, and an active Unity license are required. `UNITY_EDITOR` can override the Editor executable path. The build imports TMP Essentials when missing and regenerates `VizardVR_StartupScene` from the desktop startup scene using `VrStartupSceneBuilder`.

The Android build uses OpenXR, Vulkan, ARM64 IL2CPP, and the Oculus Touch interaction profile. Valve documents [Touch emulation on Steam Frame](https://github.com/ValveSoftware/Unity/blob/main/com.valvesoftware.openxr.utils/Documentation~/index.md) when the native Frame controller profile is not enabled. Trigger, grip and thumbstick bindings use the XRI Starter Assets action asset.

## Headset controls

- Point either controller at a control; pull and release its trigger to select it. The ray changes color over UI.
- Point at the file list and use the thumbstick to scroll.
- Select the connection address to open the VR keyboard. **Done** applies edits; **Cancel** preserves the previous address.
- Squeeze both grips to place the menu in front of your current view. The menu also recenters when tracking returns or the app regains focus.
- **Quit** opens a confirmation panel.

The file browser starts in Android's app data directory if no accessible previous directory is saved. Place scenario `.bin` files under the app's external files directory (`/sdcard/Android/data/com.Vizard.Vizard/files/`) when installing a build. No Android system keyboard or native file dialog is needed. A live simulation address normally has the form `tcp://HOST:PORT`; use the simulation machine's reachable address, not the headset's loopback address.

## Test locally without a headset

Run:

```sh
./scripts/check-vr-menu.sh
```

This launches Unity Play Mode, creates XRI simulated HMD/left/right controller devices, and sends pose, trigger, grip and thumbstick input through the production input actions. It checks targeting and clicking with both hands, the virtual keyboard, connection toggles, file browsing and scrolling, quit cancellation, tracking loss, recentering, and camera-rendered screenshots. Reports and images are written to `VizardUnityProject/Builds/SteamFrame/LocalValidation/`. Temporary file choices use that directory; user preferences are restored after the run. No ADB or SSH is used.

For an interactive desktop preview, open the Unity project and choose **Vizard → VR → Play menu with desktop simulator**. Focus the Game view. Move the mouse to point the right controller, click to pull the trigger, and scroll to operate the thumbstick. Hold left Shift to use the left controller. Left/right arrow keys turn the simulated head; R recenters the menu. Stop Play Mode to finish. All simulator and validation code is excluded from player builds.

Local simulation checks scene rendering and the application's controller interaction, but cannot validate Steam Frame's OpenXR compositor, stereo presentation, physical tracking, or comfort. Those still require an explicit headset run after deployment is authorized.
