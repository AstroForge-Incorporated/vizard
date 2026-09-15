/*
 ISC License

 Copyright (c) 2025, Autonomous Vehicle Systems Lab, University of Colorado at Boulder

 Permission to use, copy, modify, and/or distribute this software for any
 purpose with or without fee is hereby granted, provided that the above
 copyright notice and this permission notice appear in all copies.

 THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if USE_NATIVE_FILE_BROWSER
using Crosstales.FB;
#endif

/// <summary>
/// This class handles inputs to the VizardStartupScene UI, sets up the main scene
/// for streaming, file playback, and/or VR. 
/// </summary>
public class StartUpScreenManager : MonoBehaviour
{
    [Header("Panel GUI - File Playback")]
    public TextMeshProUGUI filepathText; // Displays user selected file for playback

    public Button selectFileButton; // Enables the file browser to select file

    [Header("Panel GUI - Streaming Options")]
    public TMP_InputField socketAddressInput; // Input field for tcp address for live connection

    public TextMeshProUGUI connectionText; // Connection toggle text
    public Toggle rxOnlyToggle; // Receive Only (Broadcast) when toggle enabled
    public Toggle rxTxToggle; // Receive and Reply (Two-Way) when toggle enabled
    public TextMeshProUGUI modeText; // Display mode toggle text
    public Toggle liveStreamingToggle; // Render Vizard Main Scene to screen
    public Toggle noDisplayToggle; // Do not render Vizard Main Scene to screen (reduces rendering cost/time)
    public TextMeshProUGUI errorText; // Display any connection or file errors

    [Header("File Browser")] [Tooltip("The file browser used for Linux builds or when NativeFileBrowser asset is not installed.")]
    public VizardFileBrowser VizardFileBrowser; // Third Party File Browser that works well with Linux platforms

    [Header("Streaming")] [Tooltip("Provides the direct comm streaming connection and message handling.")]
    public DirectCommunicationController directCommController;

    public static bool LoadingRecording { get; private set; }

    private Save lastSave; // Vizard Configuration data from last use (used to set up Startup Screen GUI)

    private readonly Color
        inactiveTextColor = new Color(0.3962f, 0.3868f, 0.3868f, 1f); //Text color for inactive GUI components

    private readonly Color
        activeTextColor = new Color(0.1960784f, 0.1960784f, 0.1960784f, 1f); //Text color for active GUI components


#if VIZARD_OPENXR
    // Sets up use of controllers for input to VizardVR_StartupScene
    // [Header("VR Input")]
    // public InputActionAsset inputActionAsset;
    // public GameObject rightRaycast;
    // public GameObject leftRaycast;
    // public Transform leftEndMarker;
    // public Transform rightEndMarker;
    //
    //
    // private InputAction _rightTrigger;
    // private InputAction _leftTrigger;
#endif
    /// <summary>
    ///  Handle any command line arguments and save data from last use
    ///  </summary>
    void Start()
    {
#if USE_NATIVE_FILE_BROWSER
        // Note: The crosstales FileBrowser prefab must have been moved into Resources>Prefabs for this instantiation to work in Vizard player
        GameObject fileBrowser = Instantiate (Resources.Load ("Prefabs/FileBrowser") as GameObject);
        fileBrowser.GetComponent<FileBrowser>().AllowSyncCalls = true;
#endif
        DataManager.FirstMessageDisplayed = false;
        Debug.Log("Resetting from Startup Scene Manager.");
        DataManager.ResetAllUtilities();
        if (DataManager.RecordingToLoadOnStartup == null)
            GoodEnoughAddressables.InitializeAddressables();


        socketAddressInput.onValueChanged.AddListener(SocketAddressFieldChange);
        selectFileButton.onClick.AddListener(SelectFileButtonClicked);

        if (DataManager.RecordingToLoadOnStartup != null)
        {
            filepathText.text = DataManager.RecordingToLoadOnStartup;
            DataManager.RecordingToLoadOnStartup = null;
            socketAddressInput.text = "";
            StartVisualizationButtonClicked();
            return;
        }

        Debug.Log("My platform is: " + Application.platform);
        string[] args = Environment.GetCommandLineArgs();

        //First check for any command line settings that are NOT the run mode
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-saveMsgFile")
            {
                DataManager.SaveMsgFileOnQuit = true;
                if (i + 1 < args.Length)
                {
                    string possibleFileName = args[i + 1];
                    if (possibleFileName.Substring(0, 1) != "-")
                    {
                        DataManager.SaveMsgFileName=possibleFileName;
                    }
                }
            }

            if (args[i] == "-saveMetrics")
            {
                DataManager.SaveFPSMetricsToFile = true;
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-opNavMode" || args[i] == "-opNav" || args[i] == "-noDisplay")
            {
                string socketAddress = args[i + 1];
                socketAddressInput.text = socketAddress;
                DataManager.SocketAddress = socketAddress;
                Debug.Log("Connecting to socketAddress: " + DataManager.SocketAddress);
                SetDataManagerSettingsAndUpdateToggles(true, false, true);
                StartVisualizationButtonClicked();
                return;
            }

            if (args[i] == "-directComm")
            {
                string socketAddress = args[i + 1];
                socketAddressInput.text = socketAddress;
                DataManager.SocketAddress = socketAddress;
                Debug.Log("Connecting to SocketAddress: " + DataManager.SocketAddress);
                SetDataManagerSettingsAndUpdateToggles(true, false, false);
                StartVisualizationButtonClicked();
                return;
            }

            if (args[i] == "-loadFile")
            {
                if (i + 1 >= args.Length)
                {
                    errorText.color = Color.red;
                    errorText.text = "Usage: -loadFile <path to recording.bin>";
                    break;
                }
                string filepathArg = args[i + 1];
                filepathText.text = filepathArg;
                SetDataManagerSettingsAndUpdateToggles(false, false, false);
                DataManager.FilePath = filepathArg;
                StartVisualizationButtonClicked();
                return;
            }
        }

        lastSave = DataManager.LoadUserData();
        SetLastCommMethod();
        LiveConnectionTogglesInteractable(false);

    }
#if VIZARD_OPENXR
///<summary>
/// Checks for trigger in progress in each frame to enable/disable raycast
/// </summary>
    // void Update()
    // {
    //     rightRaycast.SetActive(_rightTrigger.inProgress);
    //     leftRaycast.SetActive(_leftTrigger.inProgress);
    // }
#endif
    /// <summary>
    /// Called when socket address input field text is changed.
    /// If this field is not empty or whitespace, the Connection Type
    /// and Display mode toggles are enabled.
    /// </summary>
    /// <param name="newText">New string in input field</param>
    public void SocketAddressFieldChange(string newText)
    {
        DataManager.SocketAddress = newText;
        bool isLive = !String.IsNullOrWhiteSpace(socketAddressInput.transform.GetComponent<TMP_InputField>().text);
        DataManager.IsLiveSim = isLive;
        LiveConnectionTogglesInteractable(isLive);
    }

    /// <summary>
    /// Applies command line arguments or last save data to set StartupScene GUI components
    /// </summary>
    /// <param name="useLiveSim">True if streaming is enabled</param>
    /// <param name="rxOnly">True if broadcast streaming is enabled</param>
    /// <param name="useNoDisplay">True if Vizard main scene will not render to display</param>
    private void SetDataManagerSettingsAndUpdateToggles(bool useLiveSim, bool rxOnly, bool useNoDisplay)
    {
        DataManager.IsLiveSim = useLiveSim;
        DataManager.SocketIsReceiveOnly = rxOnly;
        DataManager.InNoDisplayMode = useNoDisplay;

        rxOnlyToggle.isOn = DataManager.SocketIsReceiveOnly;
        rxTxToggle.isOn = !DataManager.SocketIsReceiveOnly;
        liveStreamingToggle.isOn = !DataManager.IsLiveSim;
        noDisplayToggle.isOn = DataManager.InNoDisplayMode;
    }

    /// <summary>
    /// Enable file browser window
    /// If Linux, use the FileChooser asset
    /// If MacOS or Windows, use the FileBrowser asset
    /// </summary>
    public void SelectFileButtonClicked()
    {
        errorText.color = Color.blue;
        errorText.text = "";
        #if USE_NATIVE_FILE_BROWSER
        if (Application.platform == RuntimePlatform.LinuxPlayer)
        {
            VizardFileBrowser.OpenFileBrowser(filepathText,"*.bin");
        }
        else
        {
            OpenSingleFileWithFileBrowser();
        }
#else
        VizardFileBrowser.OpenFileBrowser(filepathText,"*.bin");
        #endif
    }
#if USE_NATIVE_FILE_BROWSER
    /// <summary>
    /// Callback method for third party Crosstales Native File Browser
    /// </summary>
    public void OpenSingleFileWithFileBrowser()
    {
        string singleFile = FileBrowser.Instance.OpenSingleFile("Choose playback file", DataManager.LastDirectory,
            string.Empty, "bin");
        filepathText.text = singleFile;
        DataManager.FilePath = singleFile;
    }
#endif

    /// <summary>
    /// Update DataManager for user playback/streaming selections
    /// </summary>
    private void UpdateDataManagerSettings()
    {
        MessageList.ResetFirstMessage();
        if (socketAddressInput.text != "")
        {
            DataManager.IsLiveSim = true;
            LiveConnectionTogglesInteractable(true);
            DataManager.SocketIsReceiveOnly = rxOnlyToggle.isOn;
            DataManager.InNoDisplayMode = noDisplayToggle.isOn;
        }
        else
        {
            DataManager.IsLiveSim = false;
            DataManager.InNoDisplayMode = false;
        }
    }

    /// <summary>
    /// Make live connection toggles interactable when streaming address provided, inactive if empty
    /// </summary>
    /// <param name="togglesInteractive">True if streaming address provided</param>
    private void LiveConnectionTogglesInteractable(bool togglesInteractive)
    {
        Color textColor = togglesInteractive ? activeTextColor : inactiveTextColor;

        connectionText.color = textColor;
        rxOnlyToggle.interactable = togglesInteractive;
        rxOnlyToggle.transform.GetComponentInChildren<TextMeshProUGUI>().color = textColor;
        rxTxToggle.interactable = togglesInteractive;
        rxTxToggle.transform.GetComponentInChildren<TextMeshProUGUI>().color = textColor;

        modeText.color = textColor;
        liveStreamingToggle.interactable = togglesInteractive;
        liveStreamingToggle.transform.GetComponentInChildren<TextMeshProUGUI>().color = textColor;
        noDisplayToggle.interactable = togglesInteractive;
        noDisplayToggle.transform.GetComponentInChildren<TextMeshProUGUI>().color = textColor;
    }

    /// <summary>
    /// Configure DirectCommController and DataManager for user settings and trigger transition to VizardMainScene
    /// </summary>
    public void StartVisualizationButtonClicked()
    {
        if (LoadingRecording) return;
        UpdateDataManagerSettings();
        SaveUserData();
        errorText.color = Color.blue;

        directCommController.GetComponent<VizInputAccumulator>().enabled =
            DataManager.IsLiveSim && !DataManager.SocketIsReceiveOnly;
        if (DataManager.IsLiveSim)
        {
            errorText.text = "Please stand by. Establishing communication...";

            if (!directCommController.StartCommunication(DataManager.SocketAddress))
            {
                errorText.color = Color.red;
                errorText.text = "Socket failed. Please check connection address.";
                return;
            }
        }
        else if (!string.IsNullOrEmpty(filepathText.text))
        {
            DataManager.FilePath = filepathText.text;
            StartCoroutine(LoadRecording());
            return;
        }
        else
        {
            errorText.color = Color.red;
            errorText.text = "You must specify a message file or socket address.";
            return;
        }

        DateTime startTime = DateTime.Now;
        while (MessageList.TimestepsTotal < 1)
        {
            Debug.Log("Waiting for messages to load.");
            TimeSpan interval = DateTime.Now - startTime;
            if (interval.TotalSeconds > 0.5)
            {
                Debug.Log("Timed out waiting for messages to load.");
                if (DataManager.IsLiveSim)
                {
                    errorText.color = Color.red;
                    errorText.text = "Timed out waiting for connection. Please check address and try again.";
                    directCommController.StopSocket();
                }

                return;
            }

            System.Threading.Thread.Sleep(50);
        }

        if (DataManager.IsLiveSim && DataManager.SocketIsReceiveOnly)
        {
            while (!MessageList.SettingsMessageReceived)
            {
                Debug.Log("Waiting on settings message.");
                TimeSpan interval = DateTime.Now - startTime;
                if (interval.TotalSeconds > 0.5)
                {
                    MessageList.SettingsMessageReceived = true;
                    VizardGUISettings.UpdateErrorMessages(
                        "Setting message was not received within the first four seconds of Receive Only live streaming and could not be applied.",
                        true);
                }

                System.Threading.Thread.Sleep(50);
            }
        }

        SceneManager.LoadScene(DataManager.MainSceneToLoad);
    }

    private IEnumerator LoadRecording()
    {
        LoadingRecording = true;
        var canvas = errorText.canvas.transform;
        var inputBlock = canvas.gameObject.AddComponent<CanvasGroup>();
        inputBlock.interactable = false;
        var panel = new GameObject("Recording load progress", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 0.98f);
        var label = Instantiate(errorText, panel.transform);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(0, 35);
        label.rectTransform.sizeDelta = new Vector2(500, 80);
        label.fontSize = 18;
        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.color = activeTextColor;
        label.richText = false;

        var track = new GameObject("Progress bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(panel.transform, false);
        var trackRect = (RectTransform)track.transform;
        trackRect.sizeDelta = new Vector2(440, 12);
        track.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);
        var fill = new GameObject("Progress", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        fill.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.8f);
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        string error = null;
        try
        {
            using (var reader = MessageList.ReadFileWithProgress(DataManager.FilePath).GetEnumerator())
            {
                while (true)
                {
                    bool more;
                    try { more = reader.MoveNext(); }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                        break;
                    }
                    if (!more) break;
                    float progress = reader.Current;
                    fillRect.anchorMax = new Vector2(progress, 1);
                    label.text = $"Loading recording — {progress:P0}\n{MessageList.TimestepsTotal:N0} frames indexed";
                    yield return null;
                }
            }
            if (error == null)
            {
                label.text = "Preparing visualization…";
                var startupScene = gameObject.scene;
                var loadingCanvas = errorText.canvas;
                loadingCanvas.sortingOrder = short.MaxValue;
                // Keep the overlay alive while the main scene initializes. Loading
                // additively also avoids Unity scanning the entire recording heap
                // for unused assets during a single-scene replacement.
                var startupRoots = startupScene.GetRootGameObjects();
                foreach (var root in startupRoots)
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>())
                        camera.gameObject.SetActive(false);
                    foreach (var events in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>())
                        events.gameObject.SetActive(false);
                }
                yield return null;
                var sceneLoad = SceneManager.LoadSceneAsync(DataManager.MainSceneToLoad, LoadSceneMode.Additive);
                while (!sceneLoad.isDone) yield return null;
                var mainScene = SceneManager.GetSceneByName(DataManager.MainSceneToLoad);
                SceneManager.SetActiveScene(mainScene);
                // Awake can create root objects before the loaded scene can become active.
                foreach (var root in startupScene.GetRootGameObjects())
                    if (Array.IndexOf(startupRoots, root) < 0)
                        SceneManager.MoveGameObjectToScene(root, mainScene);
                while (!DataManager.FirstMessageDisplayed || !VizardGUISettings.AssetLoadingComplete)
                {
                    // This phase has no frame count; animate the bar as an activity indicator.
                    fillRect.anchorMax = new Vector2(Mathf.PingPong(Time.unscaledTime, 1f), 1);
                    yield return null;
                }
                yield return null;
            }
        }
        finally
        {
            LoadingRecording = false;
            Destroy(panel);
            Destroy(inputBlock);
        }
        if (error != null)
        {
            errorText.color = Color.red;
            errorText.text = $"Could not load recording: {error}";
            yield break;
        }
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    /// <summary>
    /// Save the current user configuration for next use
    /// </summary>
    public void SaveUserData()
    {
        string savePath = filepathText.text;
        if (savePath == "")
        {
            savePath = DataManager.LastDirectory;
        }

        DataManager.CreateUserSaveData(savePath);
    }

    /// <summary>
    /// Configure DataManager and StartupScene toggles for communication method used last by user
    /// </summary>
    public void SetLastCommMethod()
    {
        if (lastSave != null)
        {
            SetDataManagerSettingsAndUpdateToggles(false, (lastSave.lastCommMode == "RxOnly"),
                (lastSave.lastDisplayMode == "NoDisplay"));
            DataManager.LastDirectory = lastSave.lastFilePath;
        }
    }
}