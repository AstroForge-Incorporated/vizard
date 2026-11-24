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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VizProtobufferMessage;

/// <summary>
/// Manages all GUI Panels added to the Vizard Main Scene, including
/// persistent panels (like the Color Chooser Panel) and scenario-specific
/// panels created at runtime (like secondary camera panels). 
/// </summary>
public class PanelViewManager : MonoBehaviour
{
    [Header("GUI Components")]
    public GameObject panelCanvas;
    public GUIPanelLayout guiPanelLayout;
    internal PanelDock TelemetryDock { get; private set; }
    
    [Header ("Main Menu GUI Components")]
    public GameObject loadNewFileButton;
    public GameObject compressMessagesButton;
    public Button addCameraButton;
    public GameObject cameraSubPanel; //This is the GUI panel that holds the Camera Panel toggles
    public GameObject camsBoresightToggle;
    public GameObject camsFrustumToggle;
    public GameObject camsPreviewToggle;
    public GameObject actuatorSubPanel; //This is the GUI panel that holds the Actuator Displays toggles
    public GameObject instrumentSubPanel; //This is the GUI panel that hold the Instruments Displays toggles
    public GameObject lightPanel;
    
    [Header("GUI Panels")]
    public GameObject colorWheelPanel;
    public GameObject fadingStatusText;
    
    private List<GameObject> labelsList = new List<GameObject>();
    private List<GameObject> stdCamToggles = new List<GameObject>();
    private List<GameObject> customCamToggles = new List<GameObject>();
    private List<GameObject> inventoryPanels = new List<GameObject>();

    private int xPosAct = 520;
    private int yPosAct = -15;
    private int xPosInst = 605;
    private int yPosInst = -15;

    private void Awake()
    {
        guiPanelLayout = transform.GetComponent<GUIPanelLayout>();
        if (!DataManager.UseVR && !DataManager.InNoDisplayMode)
        {
            TelemetryDock = new PanelDock(guiPanelLayout);
            gameObject.AddComponent<AngularVelocityPanel>();
            gameObject.AddComponent<DeterminationPanel>();
            gameObject.AddComponent<GuidancePanel>();
            gameObject.AddComponent<StarTrackerPanel>();
            gameObject.AddComponent<GimbalPanel>();
        }

        VizardGUISettings.ColorWheelPanel = colorWheelPanel;
        VizardGUISettings.FadingStatusTextBox = fadingStatusText;
        addCameraButton.onClick.AddListener(AddStandardCameraPanel);
        camsBoresightToggle.GetComponent<Toggle>().onValueChanged.AddListener(ShowCameraBoresightsHUD);
        camsFrustumToggle.GetComponent<Toggle>().onValueChanged.AddListener(ShowCameraFrustumsHUD);
        camsPreviewToggle.GetComponent<Toggle>().onValueChanged.AddListener(ShowCameraPreviewsHUD);
    }

    private void AddStandardCameraPanel()
    {
        int newCamID = stdCamToggles.Count + 1;
        GameObject newCameraPanel =
            Instantiate(Resources.Load("Prefabs/SpacecraftPanels/StandardCameraPanel") as GameObject, this.transform);
        string cameraName = "StandardCamera" + newCamID;
        StandardCameraPanelMethods panelMethods = newCameraPanel.GetComponent<StandardCameraPanelMethods>();
        newCameraPanel.name = cameraName + "Panel";
        panelMethods.panelName.text = "Standard Camera " + newCamID;
        GameObject newCamera = panelMethods.myCamera;
        newCamera.name = cameraName;

        string camLabelText = $"StdCam {newCamID}";
        GameObject camLabel = LabelMaker.CreateLabel(camLabelText, "Standard", newCamera, Vector2.zero, "Cameras");
        newCamera.GetComponent<SecondaryCameraHUDMethods>().cameraLabel = camLabel;
        camLabel.SetActive(false);

        GameObject toggle = CreateToggleForPanel("Standard Camera " + newCamID, newCameraPanel, cameraSubPanel);
        toggle.GetComponent<PanelToggle>().SetupCameraObjectToggle(panelMethods.myCamera);
        panelMethods.panelToggle = toggle.GetComponent<Toggle>();
        stdCamToggles.Add(toggle);

        AddPanelToPanelList(newCameraPanel);
        ResizePanel(cameraSubPanel);

        toggle.GetComponent<Toggle>().isOn = true;
        newCameraPanel.SetActive(true);
        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        newCameraPanel.GetComponent<RectTransform>().localScale = Vector3.one;
        newCameraPanel.SetActive(true);
    }

    public void AddActuatorPanels(List<string> actuatorList, int givenSpacecraftIndex, string spacecraftName)
    {
        if (actuatorList.Contains("ReactionWheel"))
        {
            ReactionWheelUtilities.InitializeMaxTorqueAndSpeedArrays();
            AddMenuDividerLabel("Reaction Wheels", actuatorSubPanel);
            if (MessageList.FirstMessage.Spacecraft[givenSpacecraftIndex].ReactionWheels.Count > 0)
            {
                if (MessageList.CurrentMessage.Spacecraft.Count > 1)
                {
                    CreateReactionWheelToggleAndPanel(spacecraftName, givenSpacecraftIndex, spacecraftName);
                }
                else
                {
                    CreateReactionWheelToggleAndPanel(spacecraftName + " Reaction Wheels Panel", givenSpacecraftIndex,
                        spacecraftName);
                    Vector2 oldSizeDelta = actuatorSubPanel.GetComponent<RectTransform>().sizeDelta;
                    actuatorSubPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(175, oldSizeDelta.y);
                }
            }
        }

        if (actuatorList.Contains("Thruster"))
        {
            AddMenuDividerLabel("Thrusters", actuatorSubPanel);
            if (MessageList.CurrentMessage.Spacecraft[givenSpacecraftIndex].Thrusters.Count > 0)
            {
                if (MessageList.CurrentMessage.Spacecraft.Count > 1)
                {
                    CreateThrusterToggleAndPanels(spacecraftName, givenSpacecraftIndex, spacecraftName);
                }
                else
                {
                    CreateThrusterToggleAndPanels(spacecraftName + " Thrusters Panel", givenSpacecraftIndex,
                        spacecraftName);
                    Vector2 oldSizeDelta = actuatorSubPanel.GetComponent<RectTransform>().sizeDelta;
                    actuatorSubPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(175, oldSizeDelta.y);
                }
            }
        }
    }

    private GameObject GetMenuDividerLabel(string labelName)
    {
        foreach (GameObject label in labelsList)
        {
            if (label.name == labelName)
            {
                return label;
            }
        }

        return null;
    }

    private void AddMenuDividerLabel(string labelName, GameObject associatedPanel)
    {
        if (GetMenuDividerLabel(labelName) == null)
        {
            GameObject menuLabel =
                Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericLabel") as GameObject,
                    associatedPanel.transform);
            menuLabel.name = labelName;
            TextMeshProUGUI menuLabelText = menuLabel.GetComponent<TextMeshProUGUI>();
            menuLabelText.text = labelName;
            menuLabelText.alignment = TextAlignmentOptions.MidlineLeft;
            menuLabelText.fontStyle = FontStyles.Bold;
            menuLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(5, 0);
            menuLabel.GetComponent<RectTransform>().sizeDelta =
                new Vector2(associatedPanel.GetComponent<RectTransform>().sizeDelta.x, 25);
            //menuLabel.transform.SetParent(associatedPanel.transform);
            menuLabel.GetComponent<RectTransform>().localScale = Vector3.one;
            labelsList.Add(menuLabel);
        }
    }

    private void OrderButtonInMenu(string groupName, GameObject button)
    {
        int menuLabelIndex = GameObject.Find(groupName).transform.GetSiblingIndex();
        button.transform.SetSiblingIndex(menuLabelIndex + 1);
    }

    private GameObject CreateToggleForPanel(string toggleName, GameObject panelToBeToggled,
        GameObject panelToHoldToggle, bool isInventoryPanel = false)
    {
        GameObject toggle = CreateToggle(toggleName, panelToHoldToggle, isInventoryPanel);
        toggle.GetComponent<PanelToggle>().enabled = true;
        toggle.GetComponent<PanelToggle>().panelToToggle = panelToBeToggled;
        return toggle;
    }

    private GameObject CreateToggle(string toggleName, GameObject panelToHoldToggle, bool isInventoryPanel = false)
    {
        GameObject toggle = Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericTogglePanel") as GameObject);
        toggle.name = toggleName + panelToHoldToggle.name + "Toggle";
        toggle.GetComponentInChildren<TextMeshProUGUI>().text = toggleName;
        toggle.GetComponent<PanelToggle>().enabled = false;
        toggle.SetActive(true);
        if (isInventoryPanel)
        {
            Transform contentTransform = panelToHoldToggle.transform.GetChild(4).transform.GetChild(0).transform
                .GetChild(0).transform;
            toggle.transform.SetParent(contentTransform);
            int count = 0;
            foreach (Transform child in contentTransform)
            {
                if (child.gameObject.activeSelf)
                {
                    toggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(5, -20 * count);
                    count += 1;
                }
            }

            contentTransform.GetComponent<RectTransform>().sizeDelta = new Vector2(130, count * 20);
            panelToHoldToggle.GetComponent<RectTransform>().sizeDelta =
                (count <= 4) ? new Vector2(130, 25 + count * 20) : new Vector2(130, 110);
        }
        else
        {
            toggle.transform.SetParent(panelToHoldToggle.transform);
        }

        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        return toggle;
    }

    private void FinalizePanel(GameObject subPanel, GameObject toggle, bool keepPanelOn = false)
    {
        subPanel.GetComponentInChildren<ClosePanelButton>().SetMyToggle(toggle);
        subPanel.SetActive(keepPanelOn);
        AddPanelToPanelList(subPanel);
    }

    public void CreateInstrumentCameraToggleAndPanel(GameObject spacecraft, GameObject instCamera)
    {
        int camID = instCamera.GetComponent<InstrumentCameraMethods>().cameraID;
        string spacecraftName = spacecraft.name;
        string parentSpacecraftName = spacecraft.GetComponent<SpacecraftController>().parentSpacecraftName;
        string instCameraName = spacecraftName + " Instrument Camera " + camID;
        if (parentSpacecraftName != "")
        {
            instCameraName = parentSpacecraftName + "/" + instCameraName;
        }


        GameObject panelToHoldToggles = instrumentSubPanel;
        panelToHoldToggles.gameObject.SetActive(true);
        AddMenuDividerLabel("Instrument Cameras", panelToHoldToggles);
        bool isMultiCam = false;

        GameObject panel =
            Instantiate(Resources.Load("Prefabs/SpacecraftPanels/InstrumentCameraPanel") as GameObject, this.transform);
        // Change the transparency of the image to prevent seeing through camera image to the main view
        panel.GetComponent<Image>().color = new Color(1, 1, 1, 1);

        if (MessageList.FirstMessage.Cameras.Count > 1)
        {
            panelToHoldToggles = GetPanelToHoldToggles("Instrument Cameras", "Panels", instrumentSubPanel);
            isMultiCam = true;
        }

        GameObject toggle = CreateToggleForPanel(instCameraName, panel, panelToHoldToggles, isMultiCam);


        toggle.GetComponent<PanelToggle>().SetupCameraComponentToggle(instCamera);
        toggle.GetComponent<Toggle>().isOn = true;
        customCamToggles.Add(toggle);

        //Camera View panel specific setup (adding camera panel script)
        panel.GetComponent<InstrumentCameraPanelMethods>().SetupPanel(instCameraName, instCamera, toggle);
        AddPanelToPanelList(panel);

        instrumentSubPanel.gameObject.SetActive(false);

        if (isMultiCam)
        {
            if (parentSpacecraftName != "")
            {
                string parentString = parentSpacecraftName + "Instrument CamerasPanels";
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
            }
            else
            {
                foreach (Transform child in panelToHoldToggles.transform.GetChild(4).GetChild(0).GetChild(0))
                {
                    if (child.name == spacecraftName + "Instrument CamerasPanelsMasterToggle")
                    {
                        child.gameObject.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
                        break;
                    }
                }
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }

        ResizePanel(instrumentSubPanel);
        panel.SetActive(true);
        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        panel.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    internal void RegisterTelemetryPanel(GameObject panel, string title)
    {
        panel.transform.SetParent(guiPanelLayout.transform, false);
        var toggle = CreateToggleForPanel(title, panel, actuatorSubPanel);
        panel.GetComponentInChildren<ClosePanelButton>().SetMyToggle(toggle);
        toggle.GetComponent<Toggle>().isOn = true;
        ResizePanel(actuatorSubPanel);
    }

    private void CreateReactionWheelToggleAndPanel(string toggleName, int scIndex, string spacecraftName)
    {
        GameObject panelToHoldToggles = actuatorSubPanel;
        panelToHoldToggles.gameObject.SetActive(true);
        bool isMultiSCSim = false;

        GameObject panel = Instantiate(Resources.Load("Prefabs/SpacecraftPanels/ReactionWheelsPanel") as GameObject);

        if (MessageList.FirstMessage.Spacecraft.Count > 1)
        {
            panelToHoldToggles = GetPanelToHoldToggles("Reaction Wheels", "Panels", actuatorSubPanel);
            isMultiSCSim = true;
        }

        GameObject toggle = CreateToggleForPanel(toggleName, panel, panelToHoldToggles, isMultiSCSim);

        ReactionWheelPanelMethods reactionWheelPanelMethods = panel.AddComponent<ReactionWheelPanelMethods>();
        reactionWheelPanelMethods.InitializePanel(panel, scIndex, spacecraftName);
        FinalizePanel(panel, toggle);
        if (MessageList.FirstMessage.Spacecraft.Count == 1)
        {
            toggle.transform.SetSiblingIndex(GetMenuDividerLabel("Reaction Wheels").transform.GetSiblingIndex() + 1);
        }

        ResizePanel(actuatorSubPanel);
        actuatorSubPanel.gameObject.SetActive(false);

        VizMessage.Types.ActuatorSettings myGUIActuatorSettings = VizardGUISettings.GetActuatorSettings(spacecraftName);
        if (myGUIActuatorSettings != null)
        {
            if (myGUIActuatorSettings.ViewRWPanel == 1)
            {
                panel.SetActive(true);
                toggle.GetComponent<Toggle>().isOn = true;
            }
            else if ((myGUIActuatorSettings.ViewRWPanel == 0) || (myGUIActuatorSettings.ViewRWPanel == -1))
            {
                panel.SetActive(false);
                toggle.GetComponent<Toggle>().isOn = false;
            }
        }

        if (isMultiSCSim)
        {
            string parentSpacecraftName = MessageList.CurrentMessage.Spacecraft[scIndex].ParentSpacecraftName;
            if (parentSpacecraftName != "")
            {
                string parentString = parentSpacecraftName + "Reaction WheelsPanels";
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }

        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        panel.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    private void CreateThrusterToggleAndPanels(string toggleName, int scIndex, string spacecraftName)
    {
        GameObject panelToHoldToggles = actuatorSubPanel;
        panelToHoldToggles.gameObject.SetActive(true);
        bool isMultiSCSim = false;

        GameObject panel = Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericSubpanel") as GameObject);

        if (MessageList.FirstMessage.Spacecraft.Count > 1)
        {
            panelToHoldToggles = GetPanelToHoldToggles("Thrusters", "Panels", actuatorSubPanel);
            isMultiSCSim = true;
        }

        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 135);
        panel.transform.GetChild(3).gameObject.SetActive(false);
        GameObject toggle = CreateToggleForPanel(toggleName, panel, panelToHoldToggles, isMultiSCSim);

        panel.AddComponent<ThrusterPanelMethods>();
        panel.GetComponent<ThrusterPanelMethods>().InitializePanel(panel, scIndex, toggle);

        FinalizePanel(panel, toggle);
        ResizePanel(actuatorSubPanel);
        actuatorSubPanel.gameObject.SetActive(false);

        VizMessage.Types.ActuatorSettings myGUIActuatorSettings = VizardGUISettings.GetActuatorSettings(spacecraftName);
        if (myGUIActuatorSettings != null)
        {
            if (myGUIActuatorSettings.ViewThrusterPanel == 1)
            {
                panel.SetActive(true);
                toggle.GetComponent<Toggle>().isOn = true;
            }
            else if ((myGUIActuatorSettings.ViewThrusterPanel == 0) || (myGUIActuatorSettings.ViewThrusterPanel == -1))
            {
                panel.SetActive(false);
                toggle.GetComponent<Toggle>().isOn = false;
            }
        }

        if (isMultiSCSim)
        {
            string parentSpacecraftName = MessageList.CurrentMessage.Spacecraft[scIndex].ParentSpacecraftName;
            if (parentSpacecraftName != "")
            {
                string parentString = parentSpacecraftName + "ThrustersPanels";
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }

        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        panel.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    public void AddInstrumentPanels(List<string> sensorList, int scIndex, string spacecraftName)
    {
        if (sensorList.Contains("CSS"))
        {
            AddMenuDividerLabel("Coarse Sun Sensors", instrumentSubPanel);
            if (MessageList.FirstMessage.Spacecraft[scIndex].CSS.Count > 0)
            {
                if (MessageList.FirstMessage.Spacecraft.Count > 1)
                {
                    CreateCSSToggleAndPanel(spacecraftName, scIndex, spacecraftName);
                }
                else
                {
                    CreateCSSToggleAndPanel(spacecraftName + " CSS Panel", scIndex, spacecraftName);
                    Vector2 oldSizeDelta = instrumentSubPanel.GetComponent<RectTransform>().sizeDelta;
                    instrumentSubPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(170, oldSizeDelta.y);
                }
            }
        }

        if (sensorList.Contains("GenericStorage"))
        {
            AddMenuDividerLabel("Storage Devices", instrumentSubPanel);
            if (MessageList.FirstMessage.Spacecraft[scIndex].StorageDevices.Count > 0)
            {
                CreateGenericStorageToggleAndPanel(scIndex, spacecraftName);
            }
        }
    }

    private GameObject FindParentSpacecraftToggleGroup(string parentSpacecraftName, string parentToggleString,
        GameObject panelToHoldToggles)
    {
        foreach (Transform child in panelToHoldToggles.transform.GetChild(4).GetChild(0).GetChild(0))
        {
            if (child.gameObject.name == parentToggleString + "MasterToggle")
            {
                return child.gameObject;
            }
        }

        return CreateParentSpacecraftToggleGroup(parentSpacecraftName, parentToggleString, panelToHoldToggles);
    }

    private GameObject CreateParentSpacecraftToggleGroup(string parentSpacecraftName, string parentToggleString,
        GameObject panelToHoldToggles)
    {
        Transform panelInventoryContentTransform = panelToHoldToggles.transform.GetChild(4).GetChild(0).GetChild(0);
        GameObject masterToggle =
            Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericSmallToggleWithButton") as GameObject,
                panelInventoryContentTransform);
        masterToggle.GetComponentInChildren<Toggle>().isOn = false;
        ShowHideSubToggles masterToggleMethods = masterToggle.GetComponentInChildren<Button>().transform.gameObject
            .AddComponent<ShowHideSubToggles>();
        masterToggleMethods.SetMasterToggle(masterToggle);
        masterToggle.name = parentToggleString + "MasterToggle";
        masterToggle.GetComponentInChildren<TextMeshProUGUI>().text = "All " + parentSpacecraftName;
        masterToggle.SetActive(true);
        masterToggle.GetComponent<RectTransform>().SetLeft(5);

        foreach (Transform child in panelInventoryContentTransform)
        {
            if (child.gameObject.name == parentToggleString + "PanelToggle")
            {
                GameObject parentSpacecraftToggle = child.gameObject;
                Vector2 childPosition = parentSpacecraftToggle.GetComponent<RectTransform>().anchoredPosition;
                int childSpot = parentSpacecraftToggle.transform.GetSiblingIndex();
                masterToggle.transform.SetSiblingIndex(childSpot);
                masterToggle.GetComponent<RectTransform>().anchoredPosition = childPosition;
                parentSpacecraftToggle.GetComponent<RectTransform>().SetLeft(20);
                parentSpacecraftToggle.GetComponent<RectTransform>().SetRight(0);
                //parentSpacecraftToggle.GetComponent<RectTransform>().anchoredPosition =new Vector2(20, childPosition.y-20);
                masterToggleMethods.AddSubToggle(parentSpacecraftToggle);
                break;
            }

            if (parentToggleString.Contains(
                    "Instrument Cameras")) //Could be multiple instrument camera toggles at the base level and above string match doesn't work because of camera ID
            {
                if (child.gameObject.name.Contains(parentSpacecraftName + " Instrument Camera"))
                {
                    Debug.Log(" I am in " + parentSpacecraftName + "Instrument Camera search the child name is: " +
                              child.gameObject.name);
                    GameObject parentSpacecraftToggle = child.gameObject;
                    Vector2 childPosition = parentSpacecraftToggle.GetComponent<RectTransform>().anchoredPosition;
                    int childSpot = parentSpacecraftToggle.transform.GetSiblingIndex();
                    masterToggle.transform.SetSiblingIndex(childSpot);
                    masterToggle.GetComponent<RectTransform>().anchoredPosition = childPosition;
                    parentSpacecraftToggle.GetComponent<RectTransform>().SetLeft(20);
                    parentSpacecraftToggle.GetComponent<RectTransform>().SetRight(0);
                    //parentSpacecraftToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, childPosition.y-20);
                    masterToggleMethods.AddSubToggle(parentSpacecraftToggle);
                    // Vector2 masterTogglePos = masterToggle.GetComponent<RectTransform>().anchoredPosition;
                    // masterToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(5, masterTogglePos.y);
                    masterToggle.GetComponent<RectTransform>().SetLeft(5);
                    masterToggle.GetComponent<RectTransform>().SetRight(0);
                }
            }
        }

        return masterToggle;
    }

    public void AddHUDToggle(string spacecraftName, string groupName, string HUDType, GameObject HUDToToggle,
        bool isActuatorHUD, bool HUDon, string parentSpacecraftName)
    {
        GameObject mainMenuPanel = instrumentSubPanel;
        bool isMultiSCSim = MessageList.CurrentMessage.Spacecraft.Count > 1;

        if (isActuatorHUD)
        {
            mainMenuPanel = actuatorSubPanel;
        }

        mainMenuPanel.gameObject.SetActive(true);
        AddMenuDividerLabel(groupName, mainMenuPanel);

        string nameToUseForToggle = spacecraftName;

        GameObject panelToHoldHUDToggles = mainMenuPanel;
        string toggleName = $"{nameToUseForToggle} {groupName} {HUDType}";
        if (groupName == "Coarse Sun Sensors")
        {
            toggleName = $"{nameToUseForToggle} CSS {HUDType}";
        }

        if (isMultiSCSim)
        {
            panelToHoldHUDToggles = GetPanelToHoldToggles(groupName, HUDType, mainMenuPanel);
            toggleName = nameToUseForToggle;
        }

        //Now add the toggle into the inventory for the specific spacecraft's HUD			
        GameObject myToggle = CreateToggleForPanel(toggleName, HUDToToggle, panelToHoldHUDToggles, isMultiSCSim);
        myToggle.GetComponent<Toggle>().isOn = HUDon;
        AddMasterToggleIfNeeded(myToggle, panelToHoldHUDToggles, isMultiSCSim, parentSpacecraftName,
            parentSpacecraftName + groupName + HUDType);


        if (groupName == "Thrusters")
        {
            string groupToggleName = $"{spacecraftName} {groupName} Geometry";
            if (isMultiSCSim)
            {
                panelToHoldHUDToggles = GetPanelToHoldToggles("Thrusters", "Geometry", mainMenuPanel);
                groupToggleName = spacecraftName;
            }

            GameObject toggleGeometry = CreateToggle(groupToggleName, panelToHoldHUDToggles, isMultiSCSim);
            AddMasterToggleIfNeeded(toggleGeometry, panelToHoldHUDToggles, isMultiSCSim, parentSpacecraftName,
                parentSpacecraftName + groupName + "Geometry");


            groupToggleName = $"{spacecraftName} {groupName} Normals";
            if (isMultiSCSim)
            {
                panelToHoldHUDToggles = GetPanelToHoldToggles("Thrusters", "Normals", mainMenuPanel);
                groupToggleName = spacecraftName;
            }

            GameObject toggleNormals = CreateToggle(groupToggleName, panelToHoldHUDToggles, isMultiSCSim);
            AddMasterToggleIfNeeded(toggleNormals, panelToHoldHUDToggles, isMultiSCSim, parentSpacecraftName,
                parentSpacecraftName + groupName + "Normals");

            foreach (Transform child in HUDToToggle.transform)
            {
                foreach (Transform grandchild in child)
                {
                    toggleGeometry.GetComponent<Toggle>().onValueChanged.AddListener(grandchild.gameObject
                        .GetComponent<ThrusterHUDMethods>().ToggleThrusterGeometry);
                    toggleNormals.GetComponent<Toggle>().onValueChanged.AddListener(grandchild.gameObject
                        .GetComponent<ThrusterHUDMethods>().ToggleThrusterNormals);
                }
            }
        }

        if (groupName == "Coarse Sun Sensors")
        {
            if (HUDType == "Coverage")
            {
                myToggle.GetComponent<PanelToggle>().enabled = false;
                foreach (Transform child in HUDToToggle.transform)
                {
                    myToggle.GetComponent<Toggle>().onValueChanged
                        .AddListener(child.GetComponent<CSSHUDMethods>().ToggleCSSCoverageHUD);
                }
            }

            if (HUDType == "Boresight")
            {
                myToggle.GetComponent<PanelToggle>().enabled = false;
                foreach (Transform child in HUDToToggle.transform)
                {
                    myToggle.GetComponent<Toggle>().onValueChanged
                        .AddListener(child.gameObject.GetComponent<CSSHUDMethods>().ToggleCSSNormalHUD);
                }
            }
        }

        if (groupName == "Transceivers")
        {
            if (HUDType == "Comm")
            {
                myToggle.GetComponent<PanelToggle>().enabled = false;
                foreach (Transform child in HUDToToggle.transform)
                {
                    myToggle.GetComponent<Toggle>().onValueChanged
                        .AddListener(child.GetComponent<TransceiverHUDMethods>().ToggleTransceiverCommHUD);
                }
            }

            if (HUDType == "Frustum")
            {
                myToggle.GetComponent<PanelToggle>().enabled = false;
                foreach (Transform child in HUDToToggle.transform)
                {
                    myToggle.GetComponent<Toggle>().onValueChanged.AddListener(child.gameObject
                        .GetComponent<TransceiverHUDMethods>().ToggleTransceiverFrustumHUD);
                }
            }
        }


        ResizePanel(mainMenuPanel);
        mainMenuPanel.gameObject.SetActive(false);
    }

    private void AddMasterToggleIfNeeded(GameObject toggleToPlace, GameObject panelToHoldToggles, bool isMultiSCSim,
        string parentSpacecraftName, string parentString)
    {
        if (isMultiSCSim)
        {
            if (parentSpacecraftName != "")
            {
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggleToPlace);
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }
    }

    private GameObject GetPanelToHoldToggles(string groupName, string HUDtype, GameObject mainPanelToHoldButton)
    {
        GameObject panelToHoldHUDToggles = null;
        string subSubpanelName = $"{groupName}{HUDtype}Panel";

        foreach (GameObject panel in inventoryPanels)
        {
            if (panel.name == subSubpanelName)
            {
                panelToHoldHUDToggles = panel;
            }
        }

        if (panelToHoldHUDToggles == null)
        {
            panelToHoldHUDToggles =
                Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericInventoryPanel") as GameObject,
                    panelCanvas.transform, true);
            panelToHoldHUDToggles.name = subSubpanelName;
            if (groupName == "Coarse Sun Sensors")
            {
                panelToHoldHUDToggles.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"CSS {HUDtype}";
            }
            else if (groupName == "Reaction Wheels")
            {
                panelToHoldHUDToggles.GetComponentInChildren<TextMeshProUGUI>().text = $"RW {HUDtype}";
            }
            else if (groupName == "Storage Devices")
            {
                panelToHoldHUDToggles.GetComponentInChildren<TextMeshProUGUI>().text = $"Storage Devices {HUDtype}";
            }
            else
            {
                panelToHoldHUDToggles.GetComponentInChildren<TextMeshProUGUI>().text = $"{groupName} {HUDtype}";
            }

            int xPos;
            int yPos;
            if ((groupName == "Reaction Wheels") || (groupName == "Thrusters"))
            {
                xPos = xPosAct;
                yPos = yPosAct;
                yPosAct -= (30 + (MessageList.CurrentMessage.Spacecraft.Count));
                if (yPosAct < -400)
                {
                    yPosAct = -15;
                    xPosAct += 130;
                }
            }
            else
            {
                xPos = xPosInst;
                yPos = yPosInst;
                yPosInst -= (30 * (MessageList.CurrentMessage.Spacecraft.Count));
                if (yPosInst < -400)
                {
                    yPosInst = -15;
                    xPosInst += 130;
                }
            }

            panelToHoldHUDToggles.GetComponent<RectTransform>().anchoredPosition = new Vector2(xPos, yPos);
            inventoryPanels.Add(panelToHoldHUDToggles);
            panelToHoldHUDToggles.SetActive(false);

            //Add the "all spacecraft" toggle now
            GameObject allSCToggle = CreateToggle("All spacecraft", panelToHoldHUDToggles, true);
            allSCToggle.AddComponent<InventoryToggleAllSpacecraft>();

            GameObject rootButton =
                Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericLabeledButton") as GameObject,
                    mainPanelToHoldButton.transform, true);
            rootButton.name = $"{groupName}{HUDtype}Button";
            OrderButtonInMenu(groupName, rootButton);

            rootButton.GetComponent<Image>().color = new Color(1, 1, 1, 0.196f);
            TextMeshProUGUI buttonText = rootButton.GetComponentInChildren<TextMeshProUGUI>();

            buttonText.text = $"{groupName} {HUDtype}";

            buttonText.fontSize = 11;
            buttonText.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, 0);
            rootButton.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 25);
            rootButton.GetComponent<Button>().onClick.AddListener(delegate
            {
                VizardGUISettings.GUICanvas.GetComponent<TogglePanelButton>().TogglePanel(panelToHoldHUDToggles);
            });

            allSCToggle.GetComponent<Toggle>().onValueChanged.AddListener(delegate { ToggleAllSCToggle(allSCToggle); });

            panelToHoldHUDToggles.GetComponent<RectTransform>().localScale = Vector3.one;

            rootButton.GetComponent<RectTransform>().localScale = Vector3.one;
        }

        return panelToHoldHUDToggles;
    }

    private void ResizePanel(GameObject panelToFix)
    {
        int count = 0;
        foreach (Transform child in panelToFix.transform)
        {
            if (child.gameObject.activeSelf)
            {
                Vector2 oldPosition = child.GetComponent<RectTransform>().anchoredPosition;
                child.GetComponent<RectTransform>().anchoredPosition = new Vector2(oldPosition.x, -25 * count);
                count += 1;
            }
        }

        Vector2 oldSizeDelta = panelToFix.GetComponent<RectTransform>().sizeDelta;
        panelToFix.GetComponent<RectTransform>().sizeDelta = new Vector2(oldSizeDelta.x, 25 * count);
    }

    public void ConfigureStandardCameraPanelsToUserSettings(VizMessage.Types.VizSettingsPb newSettings)
    {
        camsBoresightToggle.GetComponent<Toggle>().isOn = (newSettings.ViewCameraBoresightHUD == 1);
        camsFrustumToggle.GetComponent<Toggle>().isOn =
            (newSettings.ViewCameraFrustumHUD == 1) || (newSettings.ViewCameraViewHUD == 1);
        camsPreviewToggle.GetComponent<Toggle>().isOn = (newSettings.ViewCameraViewHUD == 1);

        int cameraToSet = 0;
        foreach (VizMessage.Types.StandardCameraSettings mySettings in newSettings.StandardCameraSettings)
        {
            AddStandardCameraPanelWithSettingsMessage(mySettings, cameraToSet);
            cameraToSet++;
        }
    }

    private void AddStandardCameraPanelWithSettingsMessage(VizMessage.Types.StandardCameraSettings mySettings,
        int cameraToSet)
    {
        AddStandardCameraPanel();
        GameObject myToggle = stdCamToggles[cameraToSet];
        myToggle.GetComponent<Toggle>().isOn = true;
        GameObject cameraPanel = myToggle.GetComponent<PanelToggle>().panelToToggle;
        cameraPanel.SetActive(true);
        cameraPanel.GetComponent<StandardCameraPanelMethods>()
            .SetupCameraPanelWithUserSettings(mySettings, cameraToSet);

        if (VizardGUISettings.ShowCameraLabels)
        {
            cameraPanel.GetComponent<StandardCameraPanelMethods>().myCamera.GetComponent<SecondaryCameraHUDMethods>()
                .cameraLabel.SetActive(true);
        }
    }

    public void VizardVR_AddStandardCameraPanelFromRadialMenu(VizMessage.Types.StandardCameraSettings mySettings)
    {
        AddStandardCameraPanelWithSettingsMessage(mySettings, stdCamToggles.Count);
    }

    private void ShowCameraBoresightsHUD(bool showHUD)
    {
        VizardGUISettings.ShowCamBoresights = showHUD;
        foreach (GameObject tog in stdCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraBoresightHUD(showHUD);
        }

        foreach (GameObject tog in customCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraBoresightHUD(showHUD);
        }
    }

    private void ToggleAllSCToggle(GameObject triggerToggle)
    {
        bool triggerIsOn = triggerToggle.GetComponent<Toggle>().isOn;
        Toggle[] allToggles = triggerToggle.transform.parent.GetComponentsInChildren<Toggle>();
        foreach (Toggle toggle in allToggles)
        {
            toggle.isOn = triggerIsOn;
        }
    }

    private void ShowCameraFrustumsHUD(bool showHUD)
    {
        VizardGUISettings.ShowCamFrustums = showHUD;
        foreach (GameObject tog in stdCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraFrustumHUD(showHUD);
        }

        foreach (GameObject tog in customCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraFrustumHUD(showHUD);
        }
    }

    private void ShowCameraPreviewsHUD(bool showHUD)
    {
        VizardGUISettings.ShowCamPreviews = showHUD;
        foreach (GameObject tog in stdCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraPreviewHUD(showHUD);
        }

        foreach (GameObject tog in customCamToggles)
        {
            tog.GetComponent<PanelToggle>().cameraToToggle.GetComponent<SecondaryCameraHUDMethods>()
                .ToggleCameraPreviewHUD(showHUD);
        }
    }

    private void CreateCSSToggleAndPanel(string toggleName, int scIndex, string spacecraftName)
    {
        GameObject panelToHoldToggles = instrumentSubPanel;
        panelToHoldToggles.gameObject.SetActive(true);
        bool isMultiSCSim = false;

        GameObject panel = Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericSubpanel") as GameObject);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 135);
        panel.transform.GetChild(3).gameObject.SetActive(false);

        if (MessageList.CurrentMessage.Spacecraft.Count > 1)
        {
            panelToHoldToggles = GetPanelToHoldToggles("Coarse Sun Sensors", "Panels", instrumentSubPanel);
            isMultiSCSim = true;
        }

        GameObject toggle = CreateToggleForPanel(toggleName, panel, panelToHoldToggles, isMultiSCSim);

        panel.AddComponent<CSSPanelMethods>();
        panel.GetComponent<CSSPanelMethods>().InitializePanel(panel, scIndex, toggle);
        FinalizePanel(panel, toggle);
        if (MessageList.FirstMessage.Spacecraft.Count == 1)
        {
            toggle.transform.SetSiblingIndex(GetMenuDividerLabel("Coarse Sun Sensors").transform.GetSiblingIndex() + 1);
        }

        ResizePanel(instrumentSubPanel);
        instrumentSubPanel.gameObject.SetActive(false);

        VizMessage.Types.InstrumentSettings myGUIInstSettings = VizardGUISettings.GetInstrumentSettings(spacecraftName);
        if (myGUIInstSettings != null)
        {
            if (myGUIInstSettings.ViewCSSPanel == 1)
            {
                panel.SetActive(true);
                toggle.GetComponent<Toggle>().isOn = true;
            }
            else if ((myGUIInstSettings.ViewCSSPanel == 0) || (myGUIInstSettings.ViewCSSPanel == -1))
            {
                panel.SetActive(false);
                toggle.GetComponent<Toggle>().isOn = false;
            }
        }

        if (isMultiSCSim)
        {
            string parentSpacecraftName = MessageList.CurrentMessage.Spacecraft[scIndex].ParentSpacecraftName;
            if (parentSpacecraftName != "")
            {
                string parentString = parentSpacecraftName + "Coarse Sun SensorsPanels";
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }

        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        panel.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    private void CreateGenericStorageToggleAndPanel(int scIndex, string spacecraftName)
    {
        string toggleName = spacecraftName + " Storage";
        GameObject panelToHoldToggles = instrumentSubPanel;
        panelToHoldToggles.gameObject.SetActive(true);
        bool isMultiSCSim = false;
        
        string resourcePath = DataManager.UseVR
            ? "Prefabs/VR/VizardVR_GenericStoragePanel"
            : "Prefabs/SpacecraftPanels/GenericStoragePanel";
        GameObject panel = Instantiate(Resources.Load(resourcePath) as GameObject);


        panel.GetComponent<GenericStoragePanelMethods>().InitializePanel(scIndex, spacecraftName);
        panel.transform.GetChild(3).gameObject.SetActive(false);

        if (MessageList.FirstMessage.Spacecraft.Count > 1)
        {
            panelToHoldToggles = GetPanelToHoldToggles("Storage Devices", "Panels", instrumentSubPanel);
            isMultiSCSim = true;
        }

        GameObject toggle = CreateToggleForPanel(toggleName, panel, panelToHoldToggles, isMultiSCSim);

        FinalizePanel(panel, toggle);
        ResizePanel(instrumentSubPanel);
        instrumentSubPanel.gameObject.SetActive(false);

        VizMessage.Types.InstrumentSettings myGUIInstSettings = VizardGUISettings.GetInstrumentSettings(spacecraftName);
        if (myGUIInstSettings != null)
        {
            if (myGUIInstSettings.ShowGenericStoragePanel == 1)
            {
                panel.SetActive(true);
                toggle.GetComponent<Toggle>().isOn = true;
            }
            else if ((myGUIInstSettings.ShowGenericStoragePanel == 0) ||
                     (myGUIInstSettings.ShowGenericStoragePanel == -1))
            {
                panel.SetActive(false);
                toggle.GetComponent<Toggle>().isOn = false;
            }
        }
#if VIZARD_MRTK_VR
		if (DataManager.useVR)
		{
			SatStateLabel myLabel = panel.AddComponent<SatStateLabel>();
			myLabel.targetTransform = SpacecraftStateUtilities.GetSpacecraftObject(scIndex).transform;
			panel.SetActive(true);
			panel.transform.GetChild(2).gameObject.SetActive(false);

			GameObject labelHolder = GameObject.Find("StateLabels");
			//gameObject.transform.parent =  labelHolder.transform; 
			panel.transform.SetParent(labelHolder.transform); //savannah

		}
#endif

        if (isMultiSCSim)
        {
            string parentSpacecraftName = MessageList.CurrentMessage.Spacecraft[scIndex].ParentSpacecraftName;
            if (parentSpacecraftName != "")
            {
                string parentString = parentSpacecraftName + " StorageStorage DevicesPanels";
                GameObject masterToggle =
                    FindParentSpacecraftToggleGroup(parentSpacecraftName, parentString, panelToHoldToggles);
                masterToggle.GetComponentInChildren<ShowHideSubToggles>().AddSubToggle(toggle);
            }

            ResizeInventoryPanel(panelToHoldToggles);
        }

        toggle.GetComponent<RectTransform>().localScale = Vector3.one;
        panel.GetComponent<RectTransform>().localScale = Vector3.one;
    }

    private void ResizeInventoryPanel(GameObject panel)
    {
        int contentToggleCount = 0;
        int charCount = 20;
        foreach (Transform child in panel.transform.GetChild(4).GetChild(0).GetChild(0).transform)
        {
            int newTextWidth = child.gameObject.GetComponentInChildren<TextMeshProUGUI>().text.Length;
            if (newTextWidth > charCount)
            {
                charCount = newTextWidth;
            }

            if (child.gameObject.activeSelf)
            {
                contentToggleCount += 1;
            }

            child.gameObject.GetComponent<RectTransform>().SetRight(0);
        }

        if (contentToggleCount > 4)
        {
            contentToggleCount = 4; //Will use the scroll bar if there are more than 4 options.
        }

        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(charCount * 7 + 20, 35 + contentToggleCount * 20);
        panel.transform.GetChild(4).GetChild(0).GetChild(0).gameObject.GetComponent<RectTransform>().SetRight(0);
    }

    public void SetPlayModeDependentOptions()
    {
        loadNewFileButton.SetActive(!DataManager.IsLiveSim);
        compressMessagesButton.SetActive(DataManager.IsLiveSim);
    }

    private void AddPanelToPanelList(GameObject panel)
    {
        guiPanelLayout.AddPanelToPanelList(panel);
    }
}