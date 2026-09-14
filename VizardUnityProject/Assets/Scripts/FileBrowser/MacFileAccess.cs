using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Native file selection and recording drops in the macOS player.</summary>
public class MacFileAccess : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("VizardFileAccess")]
    private static extern IntPtr VizardOpenFile(string directory, string extensions);
    [DllImport("VizardFileAccess")]
    private static extern void VizardEnableFileDrop();
    [DllImport("VizardFileAccess")]
    private static extern IntPtr VizardTakeFileDrop();
    [DllImport("VizardFileAccess")]
    private static extern void VizardDisableFileDrop();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        var receiver = new GameObject("MacFileAccess");
        receiver.AddComponent<MacFileAccess>();
        DontDestroyOnLoad(receiver);
    }

    public static string OpenFile(string lastPath, string extensions)
    {
        string directory = Directory.Exists(lastPath) ? lastPath : Path.GetDirectoryName(lastPath);
        if (!Directory.Exists(directory)) directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Marshal.PtrToStringUTF8(VizardOpenFile(directory, extensions.Replace("*.", "")));
    }

    private void Update()
    {
        VizardEnableFileDrop();
        string path = Marshal.PtrToStringUTF8(VizardTakeFileDrop());
        if (path == null) return;
        var startup = FindFirstObjectByType<StartUpScreenManager>();
        if (!CanOpenRecording(path, out string error))
        {
            if (startup != null)
            {
                startup.errorText.color = Color.red;
                startup.errorText.text = error;
            }
            else VizardGUISettings.UpdateErrorMessages(error);
            return;
        }
        if (startup != null)
        {
            startup.socketAddressInput.text = "";
            startup.filepathText.text = path;
            startup.VizardFileBrowser.fileBrowserPanel.SetActive(false);
            startup.StartVisualizationButtonClicked();
        }
        else if (!DataManager.IsLiveSim)
        {
            PersistentUserSettings.WritePersistentSettings();
            DataManager.FilePath = path;
            DataManager.LoadFile();
        }
        else
        {
            VizardGUISettings.UpdateErrorMessages("Open a separate Vizard instance to view a recording during a live connection.");
        }
    }

    private void OnApplicationQuit()
    {
        VizardDisableFileDrop();
    }
#endif

    // Check the first two frames before replacing a running scenario. The loader
    // needs both to establish its playback timestep and clears the old scene first.
    public static bool CanOpenRecording(string path, out string error)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            for (int i = 0; i < 2; i++)
            {
                var frame = VizProtobufferMessage.VizMessage.Parser.ParseDelimitedFrom(stream);
                if (frame?.CurrentTime == null)
                {
                    error = "Could not open recording: two timestamped frames are required.";
                    return false;
                }
            }
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            error = $"Could not open recording: {exception.Message}";
            return false;
        }
    }
}
