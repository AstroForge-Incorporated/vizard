Vizard Overview:
===================================
Vizard is the companion visualization application for the Basilisk spacecraft simulation framework. The purpose of Vizard is to provide an intuitive way to view the results of a simulation scenario. Vizard creates only the objects (spacecraft and celestial bodies), actuators, and instruments included in the scenario while providing some visual tools (orbit lines, pointing vectors, keep out/in cones, etc) to allow the user to evaluate the current results displayed. 


This repository contains the open source Unity Vizard project and documentation and supports building Vizard applications for MacOS, Windows, and Linux platforms. 

Custom direction vectors
-----------------------------------------------------------
Vizard renders a `GenericSensor` with exactly `fieldOfView = [0.0]` as a thin
arrow instead of a sensor cone. This uses the existing Basilisk wire format and
does not require rebuilding Basilisk. Positive-FOV sensors retain their usual
rendering.

Each vector belongs to a spacecraft. Provide these fields in **every frame**:

- `position`: three spacecraft body-frame coordinates, in meters.
- `normalVector`: three body-frame direction components; Vizard normalizes them.
- `size`: positive arrow length in meters (independent of vector magnitude).
- `color`: RGBA integers from 0 to 255.
- `label`: text at the arrow tip; an empty string hides the text.
- `isHidden`: hide/show the arrow. Zero or non-finite directions are also hidden.

Set `fieldOfView = [0.0]` when creating the vector. Include its entry
in the first message and keep the `genericSensors` order and count fixed during
playback or streaming. Position, direction, size, color, label text, and visibility can be
updated on subsequent frames, including when seeking backwards in a recording.
Arrows follow spacecraft attitude and display scale, and are hidden in sprite
mode. Use the spacecraft's **Custom Vectors / HUD** toggle to show/hide them;
their labels share the **Generic Sensor Labels** setting. Nonempty labels appear
by default, in the arrow color, unless the spacecraft instrument setting
`showGenericSensorLabels = -1` disables them. Length is supplied through `size`;
set it explicitly when using Basilisk directly.

For example, retain this object and pass it in `genericSensorList` to
`vizSupport.enableUnityVisualization`:

```python
from Basilisk.simulation import vizInterface

arrow = vizInterface.GenericSensor()
arrow.fieldOfView = vizInterface.DoubleVector([0.0])
arrow.r_SB_B = [0.0, 0.0, 0.0]
arrow.normalVector = [1.0, 0.0, 0.0]
arrow.size = 1.0  # meters
arrow.color = vizInterface.IntVector([0, 255, 255, 255])
arrow.label = "Custom direction"
arrow.genericSensorCmd = 1
# Before each visualization tick, update arrow.normalVector in body coordinates.
```

Build Vizard with these source changes to display the arrows. Previous builds
interpret the entries as zero-angle sensor cones. `TestCustomVectorHUD` is part
of the existing test-scene runner and checks coordinate conversion, dynamic
updates, hidden/zero/invalid vectors, and sprite visibility.

VizardUnityProject
-----------------------------------------------------------
Vizard's Unity project.

Cloning Vizard
---------------------------------
1. **Install the Unity Hub.** If not installed on your machine, in your web browser visit:
   
   https://cloud.unity.com/home/organizations/20066127084220/onboarding/post-download?locale=en&code=-FhAVEDtYJJ3vkdQlLptMQ012f&locale=en&session_state=37b2476116825f76831f9c6ebdad70ce7aad257405100dd0b5febc21cabe2f37.714TivAMP2dGdXHX_L5osA004f

   and follow their instructions to install the Unity Hub for your platform on your machine.
2. **Install the Unity6000.0.68f1 LTS editor on your machine.** Vizard is currently based on this Unity Editor version.
   1. In the Unity Hub, click "Installs" on the left side of the panel to see the Installs tab.
   2. Click the "Install Editor" button on the top right.
   3. Click the "Archive" tab at the top of the panel. Click the "download archive link" to go to the Unity Archive website.
   4. On the Unity download archive, make sure Unity 6 is selected and then select "LTS" from the row of options below.
   5. Scroll down to the 6000.0.68f1 (released Feb 18, 2026) and click Install to the right. 
   6. A dialog box will ask to "Open Unity Hub?", click the "Open Unity Hub" button.
   7. Select the desired Unity Editor for your platform. 
   8. A panel will open with "Install Unity 6.0 (6000.0.68f1)" at the top. There will be a list of additional installation options. Select any of the additional downloads shown that are desired. Items to consider:
      - Visual Studio Code
      - Linux Build Support (Mono)
      - Mac Build Support (IL2CPP)
      - Windows Build Support (Mono)
    9. Click Install to have Unity Hub install the Editor and all additional download options. 
    
3. **Check-out the master branch and clone the Vizard repository.** Note that Vizard uses git lfs for texture and model files (see the .gitattributes file for specifics) and cloning will pull down the necessary git lfs files. 
      
6. **Open the Vizard Unity Project.**
   1. Start the Unity Hub.
   2. In the Unity Hub panel, click the "Add" dropdown (top right) and select "Add Project From Disk".
   3. Navigate to inside the Vizard>VizardUnityProject directory and click the "Open" button on the file browser.
   4. In the Unity Hub project list, click on the newly added project to open it.
      (Note: If your installation Unity6000.0 does not match the repository's last used version, the Unity Editor will ask you to confirm opening the project in a non-matching editor, click Yes)
7. **Load the VizardStartupScene in the Unity Editor.** Once the VizardUnityProject has finished importing, type "VizardStartupScene" in the Editor Project search bar (bottom center). Double-click on the VizardStartupScene Unity asset shown in the results to open the scene in the Hierarchy (top left).
8. **Install TMP Essentials Unity Package.** After completing starting the project, click inside the hierarchy and open the StartUpCanvas>Panel>VersionText. This will help force the Unity Editor to pop up the "TMP Importer" panel. Click the "Import TMP Essentials" button to add TextMeshPro UI asset support to the project. After the TMP Essentials have installed, you can close the panel (TMP Examples & Extras and not necessary and can be skipped).
9. **Test local installation.** Press the Play button at the top center of the Unity Editor and in the Game screen, use the "Select" button to navigate to a Basilisk scenario .bin file to confirm successful installation.
10. **Optional: Install C# IDE.** A C# IDE is recommended for script editing. Both Visual Studio and JetBrains Rider have optional packages to support Unity Development. 

Building a Vizard Application with the Unity Editor
---------------------------------
1. In the Unity Editor, open the Build panel (File>Build Profiles)
2. Select the desired target platform from the left-hand list. Note: If the platform you wish to build for is not listed, return to the Unity Hub and install support for the desired platform.
3. Confirm that the VizardStartupScene and the VizardMainScene are checked in the Scene List (right panel). If not:
   1. Select "Scene List" from the left-hand panel.
   2. In the Scene tab, enable both scenes by checking their boxes.  
5. Click "Build" or "Build And Run" to build the app at the location you select. (Note that the first build for each target platform will take a long time as Unity compiles shaders for the platform. Subsequent builds will be much faster. Rebasing the project on a newer release of the Unity Editor will incur the long shader compiles as well.)
6. Note: Users of the Vizard Application will have to install any desired optional Vizard Addressables bundles locally on their machine. They will not be included in the built application.

Optional: Install Crosstales File Browser PRO
---------------------------------
1. If platform-native file browser support is desired, purchase the crosstales File Browser PRO asset from the Unity Asset store:
https://assetstore.unity.com/packages/tools/utilities/file-browser-pro-98713?_ga=2.91810076.784070175.1623179805-396823333.1600266143&_gl=1*1dgp1sa*_ga*Mzk2ODIzMzMzLjE2MDAyNjYxNDM.*_ga_1S78EFL1W5*MTYyMzI2NzQ0Ny44LjAuMTYyMzI2NzQ0Ny42MA..&utm_source=partnerize&clickref=1110l35YyIPA&utm_medium=affiliate&utm_campaign=unity_affiliate
2. In the Unity Editor, open the Package Manager (Window>PackageManager). Make sure the Editor is not in Play-mode.
3. In the PackageManager panel, click on "My Assets" in the list on the left side.
4. Click on "File Browser PRO"
5. In the right panel, click "Import 2024.1.1 to project" (or newer version, if available and desired)
6. In the "Import Unity Package" panel that opens, click the "Import" button (bottom right). Note: It is recommended you import the entire File Browser PRO package unless you are familiar with its contents.
7. The "FB PRO" panel will pop up. Close the FB PRO panel.
8. Open the Unity Player Settings by navigating to Edit>Project Settings. Click on "Player" in the left-hand list.
9. Scroll down to the Scripting Define Symbols in the Player Settings. Click the + button and type "USE_NATIVE_FILE_BROWSER" into the newly added space. Click "Apply".
10. Press Play in the UnityEditor and then "Select" to test the newly added platform native file browser.

Optional: Install HD Materials and other optional Vizard Addressables bundles assets
---------------------------------
The atmosphere shader materials available for Earth, Mars, and Venus are available as part of the Vizard_HD_Materials bundle at:

   https://avslab.github.io/basilisk/Vizard/VizardDownload.html
   
1. Download any optional Vizard Addressables asset bundles from the above webpage. Important: They are platform-specific, so take care to download the correct version for your machine.
2. Unzip the downloaded archive and install the four files contained in the bundle into the  ~/Vizard/Resources/Custom Models/ directory on your machine. Note that the files must be installed at the root level of that directory to be found by Vizard.
   
   **Windows path:**
   
   `C:/Users/user_name/AppData/LocalLow/Vizard/Vizard/Resources/CustomModels`

   **Linux path:**
   
   `/home/user_name/.config/Unity3d/Vizard/Vizard/Resources/CustomModels`

   **MacOS path:**
   
   `/Users/user_name/Library/Application Support/Vizard/Vizard/Resources/CustomModels`

4. Repeat for any additional Vizard Addressable bundles desired.

Optional: Configure Vizard for Virtual Reality (Windows Only)
---------------------------------
Vizard has been configured to run with the Unity Open XR packages and is currently in use with Meta Quest 2 and Quest 3 headsets. The Meta Horizon Link application must be installed on your Windows machine and your Quest headset must have developer mode enabled. 

To enable the Virtual Reality implementation of Vizard in the Vizard Unity Project:
1. Open VizardUnityProject in the Unity Editor.
2. **Install XR Packages** 
    1. Open the Unity Package Manager panel (Window>Package Manager).
    2. Select "Unity Registry" from left-hand list.
    3. In the list of available packages, select "XR Interaction Toolkit".
    4. In the information panel (right) showing the XR Interaction Toolkit package details, click "Install". 
    4. Next, in the list of available Unity Registry packages, select "XR Plug-In Management".
    5. In the information panel (right) showing the XR Plug-In Management package details, click "Install". 
    6. Close the Unity Package Manager.
3. **Enable OpenXR in XR Management Plugin**
    1. Open the Project Settings panel (Edit>Project Settings)
    2. Select "XR Plug-In Management" in the left-hand list.
    3. Check the "OpenXR" option in the list of Plug-in providers.
    4. Confirm that "Initialize XR on Startup" is enabled above the list of Plug-in providers.
4. **Add the VIZARD_OPENXR compile argument**
    1. Select the "Player" settings in the left-hand list of the Project Settings panel.
    2. Scroll down to the Scripting Define Symbols in the Player Settings. Click the + button and type "VIZARD_OPENXR" into the newly added space. Click "Apply".
5. **Add VizardVR_MainScene to Scene List**
    1. Open the Build Profiles panel (File>Build Profiles).
    2. Select "Scene List" from the left-hand list.
    3. In the Scene List tab, check the "Scenes/VizardVR_MainScene" checkbox. Optional: Disable the 2D main scene by unchecking "Scenes/VizardMainScene".
6. **Connect Quest headset**
    1. Open the Meta Horizon Link app.
    2. Connect your Quest headset to your machine via physical cable (preferred) or Bluetooth and enable the Link. The headset should display a white waiting room.
    3. Press Play on the Unity Editor on your desktop. You will need to navigate selecting a playback file or connecting a live Basilisk simulation from the Editor window on your desktop. File selection is not currently available inside the Quest headset in Vizard. 
