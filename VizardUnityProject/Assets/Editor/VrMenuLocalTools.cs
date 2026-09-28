using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Local Play Mode checks use XRI simulated devices and never connect to a headset.</summary>
[InitializeOnLoad]
public static class VrMenuLocalTools
{
    const string CheckKey = "Vizard.RunVrMenuChecks";
    const string SimulatorKey = "Vizard.RunVrMenuSimulator";
    const string ScenarioKey = "Vizard.RunVrScenarioChecks";

    static VrMenuLocalTools()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (SessionState.GetBool(ScenarioKey, false))
            {
                SessionState.SetBool(ScenarioKey, false);
                new GameObject("Local VR scenario checks").AddComponent<VrScenarioLocalValidation>();
            }
            else if (SessionState.GetBool(CheckKey, false))
            {
                SessionState.SetBool(CheckKey, false);
                new GameObject("Local VR menu checks").AddComponent<VrMenuLocalValidation>();
            }
            else if (SessionState.GetBool(SimulatorKey, false))
            {
                SessionState.SetBool(SimulatorKey, false);
                new GameObject("Local VR simulator").AddComponent<VrMenuDesktopSimulator>();
            }
        };
    }

    [MenuItem("Vizard/VR/Run local controller checks")]
    public static void RunChecks()
    {
        VrStartupSceneBuilder.CreateScene();
        SessionState.SetBool(CheckKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Vizard/VR/Run local scenario comfort checks")]
    public static void RunScenarioChecks()
    {
        VrStartupSceneBuilder.CreateScene();
        SessionState.SetBool(ScenarioKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Vizard/VR/Play menu with desktop simulator")]
    public static void Simulate()
    {
        if (!System.IO.File.Exists(VrStartupSceneBuilder.ScenePath)) VrStartupSceneBuilder.CreateScene();
        EditorSceneManager.OpenScene(VrStartupSceneBuilder.ScenePath);
        SessionState.SetBool(SimulatorKey, true);
        EditorApplication.EnterPlaymode();
    }
}
