#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Loads a real playback file with a stationary simulated headset and measures camera drift.</summary>
public sealed class VrScenarioLocalValidation : VrMenuDesktopSimulator
{
    readonly List<string> results = new();
    readonly List<string> errors = new();
    string output;
    string savePath;
    byte[] savedPreferences;

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
        output = Path.GetFullPath("Builds/SteamFrame/ScenarioValidation");
        Directory.CreateDirectory(output);
        savePath = Application.persistentDataPath + "/userData.save";
        savedPreferences = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
        Application.logMessageReceived += Log;
        StartCoroutine(Guard(Checks()));
    }

    protected override void Update() => Queue();

    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
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
        File.WriteAllText(Path.Combine(output, "results.txt"), (passed ? "PASS" : "FAIL") + "\n" +
            string.Join("\n", results) + "\n" + string.Join("\n", errors));
        Debug.Log("VR SCENARIO VALIDATION " + (passed ? "PASS" : "FAIL"));
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }

    IEnumerator Frames(int count = 6) { for (int i = 0; i < count; i++) yield return null; }
    void Check(bool condition, string description)
    {
        results.Add((condition ? "PASS: " : "FAIL: ") + description);
        if (!condition) errors.Add(description);
    }

    IEnumerator Checks()
    {
        yield return Frames(15);
        var startup = FindFirstObjectByType<StartUpScreenManager>();
        startup.filepathText.text = Environment.GetEnvironmentVariable("VIZARD_VR_SCENARIO") ??
            Path.Combine(output, "BasicOrbitLEO.bin");
        startup.StartVisualizationButtonClicked();
        float deadline = Time.realtimeSinceStartup + 120;
        while ((!MainCameraUtilities.MainCamera || !DataManager.FirstMessageDisplayed ||
                !VizardGUISettings.AssetLoadingComplete) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(DataManager.FirstMessageDisplayed && VizardGUISettings.AssetLoadingComplete, "Scenario loaded");
        MessageList.PlaybackPaused = true;
        yield return new WaitForSecondsRealtime(1);
        var movement = FindFirstObjectByType<VizardVR_MainCameraMovementController>();
        var rig = movement.cameraRigTransform;
        var camera = MainCameraUtilities.MainCamera.transform;
        var rotation = rig.rotation;
        var headRotation = camera.rotation;
        var offset = rig.position - MainCameraUtilities.CameraTarget.transform.position;
        float maxRotation = 0, maxHeadRotation = 0, maxOffset = 0;
        int sampleCount = Math.Min(60, MessageList.TimestepsTotal);
        for (int i = 0; i <= sampleCount; i++)
        {
            MessageList.SetNextIndex(i == sampleCount ? 0 : i * (MessageList.TimestepsTotal - 1) / sampleCount, true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return Frames(2);
            maxRotation = Mathf.Max(maxRotation, Quaternion.Angle(rotation, rig.rotation));
            maxHeadRotation = Mathf.Max(maxHeadRotation, Quaternion.Angle(headRotation, camera.rotation));
            maxOffset = Mathf.Max(maxOffset, Vector3.Distance(offset, rig.position - MainCameraUtilities.CameraTarget.transform.position));
        }
        results.Add($"Sampled {sampleCount} positions from {MessageList.TimestepsTotal} demo timesteps, including playback wrap.");
        Check(maxRotation < .05f, $"Rig orientation stays inertial: max drift {maxRotation:F6} degrees");
        Check(maxHeadRotation < .05f, $"Stationary headset view stays inertial: max drift {maxHeadRotation:F6} degrees");
        Check(maxOffset < .001f, $"Target offset stays inertial: max drift {maxOffset:F6} units");
    }
}
#endif
