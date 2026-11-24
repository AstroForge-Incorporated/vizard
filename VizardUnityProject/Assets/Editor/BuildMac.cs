using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.OSXStandalone;
using UnityEngine;

public static class BuildMac
{
    public const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    public static void LocateTmpPackage()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
        var resources = Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(resources)) throw new FileNotFoundException("TMP Essential Resources not found", resources);
        File.WriteAllText("Library/VizardTmpPackage.txt", resources);
    }

    public static void Build()
    {
        if (!File.Exists(TmpSettings)) throw new InvalidOperationException("Run build_macos.sh to import TMP resources before building.");
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-buildOutput");
        if (index < 0 || index + 1 >= args.Length || !args[index + 1].EndsWith(".app"))
            throw new ArgumentException("Pass --output-path ending in .app to unity build.");

        UserBuildSettings.architecture = OSArchitecture.ARM64;
        UserBuildSettings.createXcodeProject = false;
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var content);
        if (!string.IsNullOrEmpty(content.Error))
            throw new InvalidOperationException($"Addressables build failed: {content.Error}");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/VizardStartupScene.unity", "Assets/Scenes/VizardMainScene.unity" },
            target = BuildTarget.StandaloneOSX,
            locationPathName = args[index + 1],
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"macOS build failed: {report.summary.result}");
        Debug.Log("VIZARD_BUILD_SUCCEEDED");
    }
}
