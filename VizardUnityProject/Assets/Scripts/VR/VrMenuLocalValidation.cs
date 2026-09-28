#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>Exercises the production scene via simulated XR poses, triggers and thumbsticks.</summary>
public sealed class VrMenuLocalValidation : VrMenuDesktopSimulator
{
    readonly List<string> results = new List<string>();
    readonly List<string> errors = new List<string>();
    string output;
    string fixtureDirectory;
    string savePath;
    byte[] savedPreferences;

    protected override void Awake()
    {
        base.Awake();
        output = Path.GetFullPath("Builds/SteamFrame/LocalValidation");
        Directory.CreateDirectory(output);
        savePath = Application.persistentDataPath + "/userData.save";
        savedPreferences = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
        Application.logMessageReceived += Log;
        StartCoroutine(Guard(Checks()));
    }

    protected override void Update() => Queue();

    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message);
    }

    IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object next = null;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception ex) { errors.Add(ex.ToString()); break; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        Application.logMessageReceived -= Log;
        if (savedPreferences != null) File.WriteAllBytes(savePath, savedPreferences);
        else if (File.Exists(savePath)) File.Delete(savePath);
        bool passed = errors.Count == 0;
        File.WriteAllText(Path.Combine(output, "results.txt"), (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) + "\n" + string.Join("\n", errors));
        Debug.Log("VR LOCAL VALIDATION " + (passed ? "PASS" : "FAIL") + ": " + output);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    IEnumerator Frames(int count = 6) { for (int i = 0; i < count; i++) yield return null; }

    void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add("PASS: " + description);
    }

    IEnumerator Click(GameObject target, bool useLeft = false)
    {
        var rect = target.GetComponent<RectTransform>();
        Aim(rect.TransformPoint(rect.rect.center), useLeft);
        yield return Frames();
        var pointer = useLeft ? menu.leftController.ray : menu.rightController.ray;
        if (!pointer.TryGetCurrentUIRaycastResult(out var diagnosticHit) ||
            !(diagnosticHit.gameObject == target || diagnosticHit.gameObject.transform.IsChildOf(target.transform)))
        {
            Capture("failed-interaction.png");
            Debug.Log("Ray diagnostic: target=" + target.name + " at " + rect.TransformPoint(rect.rect.center) +
                "; controller=" + pointer.transform.position + " forward=" + pointer.transform.forward +
                "; hit=" + (diagnosticHit.gameObject ? diagnosticHit.gameObject.name : "none"));
        }
        Check(pointer.TryGetCurrentUIRaycastResult(out var hit) &&
            (hit.gameObject == target || hit.gameObject.transform.IsChildOf(target.transform)),
            (useLeft ? "Left" : "Right") + " controller ray reaches " + target.name);
        if (useLeft) { leftState.trigger = 1; leftState.buttons = (ushort)(1 << (int)ControllerButton.TriggerButton); }
        else { rightState.trigger = 1; rightState.buttons = (ushort)(1 << (int)ControllerButton.TriggerButton); }
        yield return Frames(3);
        leftState.trigger = rightState.trigger = 0;
        leftState.buttons = rightState.buttons = 0;
        yield return Frames();
    }

    IEnumerator Checks()
    {
        yield return new WaitForSecondsRealtime(.7f);
        Check(menu.menu.renderMode == RenderMode.WorldSpace && menu.menu.transform.lossyScale.x > 0,
            "Menu has a nonzero world-space canvas");
        Check(!menu.head.orthographic && menu.head.enabled, "Perspective headset camera is active");
        headState.centerEyePosition = new Vector3(.1f, .05f, 0);
        yield return Frames();
        Check(Vector3.Distance(menu.head.transform.localPosition, headState.centerEyePosition) < .001f, "HMD pose actions move the real camera");
        menu.Recenter();
        menu.startup.socketAddressInput.text = "";
        menu.startup.filepathText.text = "";
        yield return Click(GameObject.Find("VisualizationStartButton"));
        Check(menu.startup.errorText.text.Contains("must specify"), "Start button reports missing scenario without leaving the menu");
        yield return Click(menu.startup.socketAddressInput.gameObject, true);
        Check(menu.KeyboardVisible, "Left trigger opens the VR keyboard");
        yield return Click(GameObject.Find("Key 1"));
        yield return Click(GameObject.Find("Key 2"), true);
        yield return Click(GameObject.Find("Key Delete"));
        yield return Click(GameObject.Find("Key Done"));
        Debug.Log("Keyboard validation: address='" + menu.startup.socketAddressInput.text + "', options=" + menu.startup.rxOnlyToggle.interactable);
        Capture("keyboard-done.png");
        Check(menu.startup.socketAddressInput.text == "1" && menu.startup.rxOnlyToggle.interactable, "Controller keyboard edits address and enables connection options");
        yield return Click(menu.startup.rxOnlyToggle.gameObject, true);
        Check(menu.startup.rxOnlyToggle.isOn, "Left trigger changes a connection toggle");
        yield return Click(menu.startup.rxTxToggle.gameObject);
        Check(menu.startup.rxTxToggle.isOn && !menu.startup.rxOnlyToggle.isOn, "Right trigger changes the mutually exclusive connection toggle");
        yield return Click(menu.startup.socketAddressInput.gameObject);
        yield return Click(GameObject.Find("Key 3"));
        yield return Click(GameObject.Find("Key Cancel"));
        Check(menu.startup.socketAddressInput.text == "1", "Keyboard cancel preserves the original address");
        menu.startup.socketAddressInput.text = "";

        fixtureDirectory = Path.Combine(output, "Files");
        Directory.CreateDirectory(fixtureDirectory);
        for (int i = 0; i < 60; i++) File.WriteAllBytes(Path.Combine(fixtureDirectory, "scenario-" + i.ToString("D2") + ".bin"), Array.Empty<byte>());
        DataManager.LastDirectory = fixtureDirectory;
        yield return Click(menu.startup.selectFileButton.gameObject);
        var browser = menu.startup.VizardFileBrowser;
        Check(browser.fileBrowserPanel.activeInHierarchy && browser.fileButtonInventory.childCount >= 60, "Controller opens the scenario browser in the world canvas");
        Capture("file-browser.png");
        var scroll = browser.fileBrowserPanel.GetComponentInChildren<ScrollRect>();
        Aim(scroll.viewport.TransformPoint(scroll.viewport.rect.center), false);
        yield return Frames();
        float before = scroll.verticalNormalizedPosition;
        rightState.primary2DAxis = new Vector2(0, -1);
        yield return Frames(25);
        rightState.primary2DAxis = Vector2.zero;
        yield return Frames();
        Check(Mathf.Abs(before - scroll.verticalNormalizedPosition) > .001f, "Controller thumbstick scrolls the scenario list");
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        yield return Frames();
        var fileButton = browser.fileButtonInventory.GetChild(0).gameObject;
        yield return Click(fileButton, true);
        Check(!browser.fileBrowserPanel.activeSelf && menu.startup.filepathText.text.EndsWith(".bin"), "Controller selects a scenario file and closes the browser");
        yield return Click(menu.startup.selectFileButton.gameObject);
        yield return Click(browser.directoryDropdown.gameObject);
        var dropdownList = GameObject.Find("Dropdown List");
        Check(dropdownList != null, "Directory dropdown opens in the world canvas");
        yield return Click(dropdownList.GetComponentsInChildren<Toggle>()[1].gameObject, true);
        // TMP closes its dropdown with an unscaled-time fade, not a fixed frame count.
        yield return new WaitForSecondsRealtime(.25f);
        Check(browser.directoryDropdown.options[0].text == output, "Controller navigates to the parent directory");
        yield return Click(browser.fileButtonInventory.GetChild(0).gameObject);
        Check(browser.directoryDropdown.options[0].text == fixtureDirectory, "Controller navigates into a scenario directory");
        yield return Click(GameObject.Find("CancelBrowserButton"));
        Check(!browser.fileBrowserPanel.activeSelf, "Controller closes the browser without selecting a file");
        menu.startup.filepathText.text = "";

        var quit = menu.menu.GetComponent<FileMenuMethods>();
        yield return Click(quit.quitVizardButton.gameObject);
        Check(quit.quitConfirmation.activeInHierarchy, "Quit opens confirmation");
        yield return Click(quit.quitConfirmation.GetComponentsInChildren<Button>().First(b => b.name == "CancelButton").gameObject, true);
        Check(!quit.quitConfirmation.activeSelf, "Controller cancels quit and returns to the menu");

        leftState.isTracked = rightState.isTracked = false;
        yield return Frames();
        Check(!menu.leftController.ray.enabled && !menu.rightController.ray.enabled, "Lost controllers cannot leave active phantom pointers");
        leftState.isTracked = rightState.isTracked = true;
        headState.centerEyeRotation = Quaternion.Euler(0, 25, 0);
        yield return Frames();
        leftState.buttons = rightState.buttons = (ushort)(1 << (int)ControllerButton.GripButton);
        yield return Frames();
        Check(Vector3.Dot(menu.menu.transform.forward, menu.head.transform.forward) > .999f, "Both grips recenter the menu after a head turn");
        leftState.buttons = rightState.buttons = 0;
        headState.centerEyeRotation = Quaternion.identity;
        yield return Frames();
        menu.Recenter();
        menu.startup.errorText.text = "";
        Aim(menu.startup.selectFileButton.transform.position, false);
        yield return Frames();
        BeforeRender();
        var lineRenderer = menu.rightController.GetComponent<LineRenderer>();
        Check(menu.rightController.line.enabled && lineRenderer.enabled && lineRenderer.positionCount >= 2,
            "Tracked controller has an enabled visible ray renderer");
        Capture("main-menu.png");
        yield return Click(menu.startup.socketAddressInput.gameObject);
        Capture("vr-keyboard.png");
    }

    void Capture(string filename)
    {
        BeforeRender();
        var target = new RenderTexture(1600, 1000, 24);
        var previousTarget = menu.head.targetTexture;
        var previousActive = RenderTexture.active;
        menu.head.targetTexture = target;
        menu.head.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
        var pixels = image.GetPixels();
        Check(pixels.Count(p => p.maxColorComponent > .1f) > pixels.Length * .1f &&
              pixels.Count(p => p.maxColorComponent > .7f) > pixels.Length * .0005f,
              filename + " contains a visible panel and legible bright text");
        menu.head.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        Destroy(image);
        target.Release();
        Destroy(target);
    }

    static void BeforeRender()
    {
        // Batch Mode has no Game view render loop. Camera.Render alone does not invoke this
        // engine callback, which XRI uses to update ray visuals and late HMD/controller poses.
        // Invoke the same callback before our offscreen camera render; don't substitute visuals.
        var callback = typeof(Application).GetMethod("InvokeOnBeforeRender", BindingFlags.Static | BindingFlags.NonPublic);
        if (callback == null) throw new MissingMethodException("Unity Application.InvokeOnBeforeRender");
        callback.Invoke(null, null);
    }
}
#endif
