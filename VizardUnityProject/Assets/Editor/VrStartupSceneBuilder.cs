using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>Reproducibly derives the VR startup scene from the existing desktop menu.</summary>
public static class VrStartupSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/VizardVR_StartupScene.unity";
    public const string ActionsPath = "Assets/Samples/XR Interaction Toolkit/3.0.10/Starter Assets/XRI Default Input Actions.inputactions";
    static InputActionReference[] references;

    [MenuItem("Vizard/VR/Rebuild startup scene")]
    public static void CreateScene()
    {
        EnsureTextResources();
        references = AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>().ToArray();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (actions == null) throw new InvalidOperationException("XRI Starter Assets input actions are missing.");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/VizardStartupScene.unity");
        var startup = Object.FindFirstObjectByType<StartUpScreenManager>();
        var browserPrefab = PrefabUtility.GetOutermostPrefabInstanceRoot(startup.VizardFileBrowser.gameObject);
        if (browserPrefab != null) PrefabUtility.UnpackPrefabInstance(browserPrefab, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var canvas = GameObject.Find("StartUpCanvas").GetComponent<Canvas>();
        var oldCamera = Camera.main;
        if (oldCamera != null) Object.DestroyImmediate(oldCamera.gameObject);

        var rig = new GameObject("Startup XR Origin");
        rig.SetActive(false);
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(rig.transform, false);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(offset.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .04f, .065f, 1);
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 50;
        camera.fieldOfView = 65;
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        Pose(cameraObject, "XRI Head");

        var origin = rig.AddComponent<XROrigin>();
        origin.Origin = rig;
        origin.Camera = camera;
        origin.CameraFloorOffsetObject = offset;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        origin.CameraYOffset = 1.6f;
        offset.transform.localPosition = new Vector3(0, 1.6f, 0);
        var manager = rig.AddComponent<XRInteractionManager>();
        rig.AddComponent<InputActionManager>().actionAssets = new List<InputActionAsset> { actions };

        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        foreach (var module in eventSystem.GetComponents<BaseInputModule>()) Object.DestroyImmediate(module);
        var ui = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        ui.enableXRInput = true;
        ui.enableMouseInput = false;
        ui.enableTouchInput = false;
        ui.enableGamepadInput = false;
        ui.enableJoystickInput = false;

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.pixelPerfect = false;
        var rect = (RectTransform)canvas.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(1366, 768);
        rect.localScale = Vector3.one * .0013f;
        rect.SetPositionAndRotation(new Vector3(0, 1.6f, 1.7f), Quaternion.identity);
        canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
        foreach (var childCanvas in canvas.GetComponentsInChildren<Canvas>(true))
        {
            childCanvas.worldCamera = camera;
            foreach (var raycaster in childCanvas.GetComponents<GraphicRaycaster>()) Object.DestroyImmediate(raycaster);
            if (!childCanvas.GetComponent<TrackedDeviceGraphicRaycaster>()) childCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        var panel = RebuildPanel(canvas, startup);
        var presentation = rig.AddComponent<VrStartupMenu>();
        presentation.head = camera;
        presentation.menu = canvas;
        presentation.mainPanel = panel;
        presentation.startup = startup;
        presentation.headTracked = Action("XRI Head/Is Tracked");
        presentation.leftGrip = Action("XRI Left Locomotion/Teleport Mode Cancel");
        presentation.rightGrip = Action("XRI Right Locomotion/Teleport Mode Cancel");
        presentation.leftController = Controller(offset.transform, manager, "Left");
        presentation.rightController = Controller(offset.transform, manager, "Right");
        var hint = new GameObject("VR Controller Help", typeof(RectTransform), typeof(TextMeshProUGUI));
        hint.transform.SetParent(canvas.transform, false);
        presentation.help = hint.GetComponent<TextMeshProUGUI>();
        presentation.help.rectTransform.anchoredPosition = new Vector2(0, -430);
        presentation.help.rectTransform.sizeDelta = new Vector2(1400, 65);
        presentation.help.font = TMP_Settings.defaultFontAsset;
        presentation.help.fontSize = 23;
        presentation.help.alignment = TextAlignmentOptions.Center;
        presentation.help.color = Color.white;
        presentation.help.raycastTarget = false;
        presentation.help.text = "Point and pull trigger to select | Thumbstick to scroll | Squeeze both grips to recenter";

        // Headless streaming is inappropriate for an in-headset startup menu.
        startup.noDisplayToggle.gameObject.SetActive(false);
        startup.liveStreamingToggle.isOn = true;
        RebuildBrowser(canvas, startup);
        rig.SetActive(true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created VR startup scene: " + ScenePath);
    }

    static void RebuildBrowser(Canvas canvas, StartUpScreenManager startup)
    {
        var old = startup.VizardFileBrowser;
        var controller = new GameObject("VR File Browser").AddComponent<VizardFileBrowser>();
        controller.transform.SetParent(canvas.transform, false);
        var panel = Rect("Scenario Browser", canvas.transform, Vector2.zero, new Vector2(1200, 768));
        panel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .14f, 1);
        Text("SCENARIO FILES", panel, new Vector2(0, 323), new Vector2(1100, 60), 38);
        var dropdown = Object.Instantiate(old.directoryDropdown.gameObject, panel).GetComponent<TMP_Dropdown>();
        dropdown.name = "DirectoryDropdown";
        var dropdownRect = (RectTransform)dropdown.transform;
        dropdownRect.anchorMin = dropdownRect.anchorMax = dropdownRect.pivot = new Vector2(.5f, .5f);
        dropdownRect.anchoredPosition3D = new Vector3(0, 254, 0);
        dropdownRect.sizeDelta = new Vector2(1100, 58);
        dropdownRect.localScale = Vector3.one;
        foreach (var text in dropdown.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.fontSize = 25;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 300);
        var item = dropdown.template.GetComponentInChildren<Toggle>(true).GetComponent<RectTransform>();
        item.sizeDelta = new Vector2(item.sizeDelta.x, 52);
        var scrollRect = Rect("Scenario Scroll", panel, new Vector2(0, -10), new Vector2(1100, 435));
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        var viewport = Rect("Viewport", scrollRect, Vector2.zero, scrollRect.sizeDelta);
        viewport.gameObject.AddComponent<Image>().color = new Color(.1f, .14f, .2f, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("File Inventory", viewport, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        controller.fileBrowserPanel = panel.gameObject;
        controller.fileButtonInventory = content;
        controller.directoryDropdown = dropdown;
        controller.errorMsgText = Text("", panel, new Vector2(0, -263), new Vector2(1100, 60), 24);
        controller.largeTargets = true;
        var cancel = Button("CancelBrowserButton", "Back to menu", panel, new Vector2(0, -333), new Vector2(420, 60));
        UnityEventTools.AddBoolPersistentListener(cancel.onClick, panel.gameObject.SetActive, false);
        startup.VizardFileBrowser = controller;
        Object.DestroyImmediate(old.gameObject);
        panel.gameObject.SetActive(false);
    }

    static VrMenuController Controller(Transform parent, XRInteractionManager manager, string hand)
    {
        var go = new GameObject(hand + " Menu Controller");
        go.transform.SetParent(parent, false);
        Pose(go, "XRI " + hand);
        var ray = go.AddComponent<XRRayInteractor>();
        ray.interactionManager = manager;
        ray.enableUIInteraction = true;
        ray.maxRaycastDistance = 10;
        ray.lineType = XRRayInteractor.LineType.StraightLine;
        // XRI also applies this mask to UI graphics, including runtime dropdowns/keyboards.
        ray.raycastMask = ~0;
        ray.uiPressInput = new XRInputButtonReader
        {
            inputActionReferencePerformed = Action("XRI " + hand + " Interaction/UI Press"),
            inputActionReferenceValue = Action("XRI " + hand + " Interaction/UI Press Value")
        };
        ray.uiScrollInput = new XRInputValueReader<Vector2>
        {
            inputActionReference = Action("XRI " + hand + " Interaction/UI Scroll")
        };
        var renderer = go.AddComponent<LineRenderer>();
        renderer.sharedMaterial = PointerMaterial();
        renderer.widthMultiplier = .004f;
        var line = go.AddComponent<XRInteractorLineVisual>();
        line.lineWidth = .004f;
        line.overrideInteractorLineLength = true;
        line.lineLength = 3;
        line.stopLineAtFirstRaycastHit = true;
        line.validColorGradient = Gradient(new Color(.2f, 1, .8f));
        line.invalidColorGradient = Gradient(new Color(.4f, .7f, 1, .7f));
        var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        model.name = hand + " Controller Marker";
        Object.DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(go.transform, false);
        model.transform.localScale = new Vector3(.025f, .05f, .025f);
        model.GetComponent<Renderer>().sharedMaterial = PointerMaterial();
        var tracking = go.AddComponent<VrMenuController>();
        tracking.isTracked = Action("XRI " + hand + "/Is Tracked");
        tracking.ray = ray;
        tracking.line = line;
        tracking.model = model;
        return tracking;
    }

    static GameObject RebuildPanel(Canvas canvas, StartUpScreenManager startup)
    {
        startup.darkBackground = true;
        var oldPanel = canvas.transform.Find("Panel").gameObject;
        var oldInput = startup.socketAddressInput;
        var panel = Rect("VR Main Menu", canvas.transform, Vector2.zero, new Vector2(1200, 768));
        panel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .14f, 1);
        Text("VIZARD", panel, new Vector2(0, 303), new Vector2(1100, 65), 52);
        startup.interactionGroup = panel.gameObject.AddComponent<CanvasGroup>();
        Text("Choose a scenario file or connect to a simulation", panel, new Vector2(0, 242), new Vector2(1100, 45), 26);
        Text("SCENARIO FILE", panel, new Vector2(-400, 178), new Vector2(300, 40), 23);
        var fileBox = Rect("File selection", panel, new Vector2(-110, 126), new Vector2(880, 65));
        fileBox.gameObject.AddComponent<Image>().color = new Color(.12f, .18f, .24f, 1);
        startup.filepathText = Text("", fileBox, Vector2.zero, new Vector2(850, 60), 24);
        startup.filepathText.alignment = TextAlignmentOptions.MidlineLeft;
        startup.filepathText.overflowMode = TextOverflowModes.Ellipsis;
        startup.selectFileButton = Button("SelectFileButton", "Browse", panel, new Vector2(450, 126), new Vector2(190, 65));
        Text("CONNECTION ADDRESS", panel, new Vector2(-345, 58), new Vector2(410, 40), 23);
        var input = Object.Instantiate(oldInput.gameObject, panel).GetComponent<TMP_InputField>();
        input.name = "SocketAddressInputField";
        var inputRect = (RectTransform)input.transform;
        inputRect.anchorMin = inputRect.anchorMax = inputRect.pivot = new Vector2(.5f, .5f);
        inputRect.anchoredPosition3D = new Vector3(0, 6, 0);
        inputRect.localScale = Vector3.one;
        inputRect.sizeDelta = new Vector2(1100, 65);
        input.pointSize = 28;
        input.textComponent.fontSize = 28;
        input.textComponent.color = new Color(.05f, .08f, .12f);
        if (input.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.text = "tcp://192.168.1.10:5556";
            placeholder.fontSize = 28;
        }
        startup.socketAddressInput = input;
        var group = panel.gameObject.AddComponent<ToggleGroup>();
        startup.connectionText = Text("CONNECTION TYPE", panel, new Vector2(-365, -77), new Vector2(370, 45), 23);
        startup.rxOnlyToggle = Toggle("ReceiveOnlyToggle", "Receive only", panel, new Vector2(35, -77), group);
        startup.rxTxToggle = Toggle("ReceiveAndReplyToggle", "Receive & reply", panel, new Vector2(375, -77), group);
        startup.rxTxToggle.isOn = true;
        startup.modeText = Text("Immersive VR display", panel, new Vector2(0, -137), new Vector2(1000, 40), 23);
        // Retain the existing manager's display fields while enforcing VR rendering.
        startup.liveStreamingToggle = Toggle("LiveStreamingToggle", "VR", panel, Vector2.zero, null);
        startup.noDisplayToggle = Toggle("NoDisplayToggle", "", panel, Vector2.zero, null);
        startup.liveStreamingToggle.isOn = true;
        startup.noDisplayToggle.isOn = false;
        startup.liveStreamingToggle.gameObject.SetActive(false);
        startup.noDisplayToggle.gameObject.SetActive(false);
        startup.errorText = Text("", panel, new Vector2(0, -210), new Vector2(1100, 75), 25);
        var start = Button("VisualizationStartButton", "Start visualization", panel, new Vector2(-100, -305), new Vector2(650, 75));
        UnityEventTools.AddPersistentListener(start.onClick, startup.StartVisualizationButtonClicked);
        var fileMenu = canvas.GetComponent<FileMenuMethods>();
        Object.DestroyImmediate(fileMenu.quitConfirmation);
        fileMenu.quitVizardButton = Button("QuitButton", "Quit", panel, new Vector2(415, -305), new Vector2(260, 75));
        var confirmation = Rect("QuitVizardConfirmationPanel", canvas.transform, Vector2.zero, new Vector2(1200, 768));
        confirmation.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .14f, 1);
        Text("Quit Vizard?", confirmation, new Vector2(0, 80), new Vector2(1050, 80), 45);
        var cancel = Button("CancelButton", "Keep exploring", confirmation, new Vector2(-245, -80), new Vector2(410, 85));
        var confirm = Button("ConfirmQuitButton", "Quit", confirmation, new Vector2(245, -80), new Vector2(410, 85));
        UnityEventTools.AddPersistentListener(cancel.onClick, fileMenu.CancelQuit);
        UnityEventTools.AddPersistentListener(confirm.onClick, fileMenu.ConfirmQuit);
        fileMenu.quitConfirmation = confirmation.gameObject;
        confirmation.gameObject.SetActive(false);
        Object.DestroyImmediate(oldPanel);
        return panel.gameObject;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static TextMeshProUGUI Text(string text, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(text, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    static Button Button(string name, string title, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size);
        rect.gameObject.AddComponent<Image>().color = new Color(.18f, .34f, .48f, 1);
        var button = rect.gameObject.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(.45f, 1, 1);
        colors.pressedColor = new Color(.3f, .7f, 1);
        button.colors = colors;
        Text(title, rect, Vector2.zero, size - new Vector2(10, 10), 29);
        return button;
    }

    static Toggle Toggle(string name, string title, Transform parent, Vector2 position, ToggleGroup group)
    {
        var rect = Rect(name, parent, position, new Vector2(310, 55));
        var background = Rect("Toggle Background", rect, new Vector2(-131, 0), new Vector2(42, 42)).gameObject.AddComponent<Image>();
        background.color = new Color(.2f, .32f, .43f);
        var check = Rect("Checkmark", background.transform, Vector2.zero, new Vector2(26, 26)).gameObject.AddComponent<Image>();
        check.color = new Color(.2f, .9f, .7f);
        var toggle = rect.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.isOn = false;
        toggle.group = group;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        Text(title, rect, new Vector2(23, 0), new Vector2(260, 55), 25);
        // The entire label row is a target, so pointing need not hit the small checkbox.
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        return toggle;
    }

    static Gradient Gradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
            new[] { new GradientAlphaKey(color.a, 0), new GradientAlphaKey(color.a, 1) });
        return gradient;
    }

    static Material PointerMaterial()
    {
        const string path = "Assets/Resources/Materials/VR Startup Pointer.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    static void Pose(GameObject go, string map)
    {
        var pose = go.AddComponent<TrackedPoseDriver>();
        pose.positionInput = new InputActionProperty(Action(map + "/Position"));
        pose.rotationInput = new InputActionProperty(Action(map + "/Rotation"));
        pose.trackingStateInput = new InputActionProperty(Action(map + "/Tracking State"));
        pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
    }

    // The Input System importer retains legacy subasset IDs for renamed actions.
    static InputActionReference Action(string path) => references.First(r => r.action.actionMap.name + "/" + r.action.name == path);

    public static void EnsureTextResources()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return;
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
        string essentials = Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
        if (!File.Exists(essentials)) throw new FileNotFoundException("TMP Essential Resources package is missing", essentials);
        AssetDatabase.ImportPackage(essentials, false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
