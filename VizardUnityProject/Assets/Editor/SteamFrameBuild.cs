using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

/// <summary>Builds the Vizard OpenXR Android APK for Valve Steam Frame.</summary>
public static class SteamFrameBuild
{
    private const string OpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string StartupScene = VrStartupSceneBuilder.ScenePath;
    private const string VrScene = "Assets/Scenes/VizardVR_MainScene.unity";

    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Unity Android Build Support (including IL2CPP) is required.");

        EnableAndroidOpenXr();
        EnableVizardVrCode();
        VrStartupSceneBuilder.CreateScene();

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.buildAppBundle = false;

        string output = Environment.GetEnvironmentVariable("VIZARD_ANDROID_APK_PATH");
        if (string.IsNullOrWhiteSpace(output))
            output = Path.GetFullPath("Builds/SteamFrame/Vizard.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        var options = new BuildPlayerOptions
        {
            scenes = new[] { StartupScene, VrScene },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Steam Frame APK build failed: {report.summary.result}");

        Debug.Log($"Steam Frame APK built: {output} ({report.summary.totalSize} bytes)");
    }

    private static void EnableVizardVrCode()
    {
        var target = NamedBuildTarget.Android;
        string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
        var list = symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!list.Contains("VIZARD_OPENXR"))
            list.Add("VIZARD_OPENXR");
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
    }

    private static void EnableAndroidOpenXr()
    {
        const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
        if (perTarget == null)
            throw new InvalidOperationException($"Missing XR settings asset: {settingsPath}");

        var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (android == null)
        {
            android = ScriptableObject.CreateInstance<XRGeneralSettings>();
            android.name = "Android XR Settings";
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            manager.name = "Android XR Manager Settings";
            AssetDatabase.CreateAsset(manager, "Assets/XR/Android XR Manager Settings.asset");
            android.Manager = manager;
            perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, android);
            AssetDatabase.AddObjectToAsset(android, perTarget);
            EditorUtility.SetDirty(perTarget);
        }

        var generalSettingsObject = new SerializedObject(android);
        generalSettingsObject.FindProperty("m_InitManagerOnStart").boolValue = true;
        generalSettingsObject.ApplyModifiedPropertiesWithoutUndo();
        android.Manager.automaticLoading = true;
        android.Manager.automaticRunning = true;

        if (!XRPackageMetadataStore.AssignLoader(android.Manager, OpenXrLoaderType, BuildTargetGroup.Android))
            throw new InvalidOperationException("Could not assign Unity OpenXR Loader to the Android build target.");

        // Valve documents Touch emulation for Frame when the native Frame profile is not used.
        // Without an enabled Android interaction profile OpenXR supplies no controller bindings.
        var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var touch = openXr.GetFeature<OculusTouchControllerProfile>();
        if (touch == null) throw new InvalidOperationException("Android Oculus Touch interaction profile is missing.");
        touch.enabled = true;
        EditorUtility.SetDirty(touch);
        EditorUtility.SetDirty(openXr);

        EditorUtility.SetDirty(android.Manager);
        EditorUtility.SetDirty(android);
        EditorUtility.SetDirty(perTarget);
        AssetDatabase.SaveAssets();
    }
}
