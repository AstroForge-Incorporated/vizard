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
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Handles user input to the File>Settings panel
/// </summary>
public class SettingsPanelMethods : MonoBehaviour
{
	[Header ("General Settings Tab Components")]
	//Lighting
	public Slider ambientLightingSlider;
	public TextMeshProUGUI ambientText;
	public Slider emissiveSlider;
	public TextMeshProUGUI emissiveText;
	public TMP_InputField sunIntensity;
	public Toggle attenuateSunWithDistanceToggle;
	[Header (" ")]
	//GUI Sizing
	public TMP_InputField GUIReferenceResolutionHeight;
	public Button resetGUIReferenceResolution;
	private readonly int defaultGUIResolutionWidth = 1366;
	public GameObject mainCanvas;
	[Header (" ")]
	// Object Scaling
	public TMP_InputField spacecraftScale;
	public TMP_InputField spacecraftHelioScale;
	public TMP_InputField celestialBodyHelioScale;
	public TextMeshProUGUI currentSCPlanetScaleText;
	public TextMeshProUGUI currentSCHelioScaleText;
	public TextMeshProUGUI currentCBHelioScaleText;

	[Header (" ")]
	[Header ("Main Camera Tab GUI Components")]
	public TMP_InputField cameraAngularKeyboardRate;
	public TMP_InputField cameraZoomKeyboardRate;
	
	[Header ("Lines Tab GUI Components")]
	//Orbit Lines
	public TMP_InputField orbitLineSegments;
	public TMP_InputField osculatingOrbitRangeStart;
	public TMP_InputField osculatingOrbitRangeEnd;

	public Toggle useOpenGLOrbitLines;
	public Toggle useLineRendererLines;
	public TMP_InputField spacecraftOrbitLineWidth;
	public TMP_InputField celestialBodyOrbitLineWidth;
	[Header (" ")]
	//Ground Tracks
	public TMP_InputField osculatingGroundTrackRangeStart;
	public TMP_InputField osculatingGroundTrackRangeEnd;
	[Header (" ")]
	//Target Lines, Pointing Lines, Coordinate Frames
	public Toggle useLineRendererOnLinesAndFrames;
	public TMP_InputField linesAndFramesLineWidth;
	public AddPointingVectorPanelMethods pointingVectorPanel;
	
	[Header("Actuator Tab GUI Components")]
	//Actuators
	public Image defaultThrusterColor;
	public Button thrusterColorChooserButton;
	public TMP_InputField particleLifeScalar;
	public GameObject thrustersSettings;
	
	//Devices - none so far
	
	[Header("Broadcast Tab GUI Components")]
	//Broadcast
	public Toggle syncBroadcastViewersWithCommandVizardToggle;
	void Awake(){
		if (RenderSettings.ambientIntensity>ambientLightingSlider.maxValue){
			ambientLightingSlider.maxValue = RenderSettings.ambientIntensity;
		}
		ambientLightingSlider.value = RenderSettings.ambientIntensity;
	}

	void Start()
	{
		ambientLightingSlider.onValueChanged.AddListener(UpdateBrightness);
		emissiveSlider.onValueChanged.AddListener(UpdateSpacecraftShaderEmissive);

		GUIReferenceResolutionHeight.onEndEdit.AddListener(ChangeGUIReferenceResolution);
		resetGUIReferenceResolution.onClick.AddListener(ResetGUIReferenceResolution);
		
		spacecraftScale.onEndEdit.AddListener(UpdateSpacecraftScaleSetting);
		spacecraftHelioScale.onEndEdit.AddListener(UpdateSpacecraftHelioScaleSetting);
		celestialBodyHelioScale.onEndEdit.AddListener(UpdateCelestialBodyHelioSetting);
		
		sunIntensity.onEndEdit.AddListener(UpdateSunOrMainLightIntensitySetting);
		attenuateSunWithDistanceToggle.onValueChanged.AddListener(UpdateLightAttenuation);

		cameraAngularKeyboardRate.onEndEdit.AddListener(SetKeyboardPanRate);
		cameraZoomKeyboardRate.onEndEdit.AddListener(SetKeyboardZoomMultiplier);
		
		orbitLineSegments.onEndEdit.AddListener(ChangeOrbitLineSegmentCount);
		osculatingOrbitRangeStart.onEndEdit.AddListener(ChangeOsculatingOrbitRangeSetting);
		osculatingOrbitRangeEnd.onEndEdit.AddListener(ChangeOsculatingOrbitRangeSetting);
		useLineRendererLines.onValueChanged.AddListener(UpdateOrbitLinePlotterSettings);
		useOpenGLOrbitLines.onValueChanged.AddListener(UpdateOrbitLinePlotterSettings);
		spacecraftOrbitLineWidth.onEndEdit.AddListener(ChangeSpacecraftOrbitLineWidth);
		celestialBodyOrbitLineWidth.onEndEdit.AddListener(ChangeCelestialBodyOrbitLineWidth);
		
		osculatingGroundTrackRangeStart.onEndEdit.AddListener(ChangeOsculatingGroundTrackRangeSetting);
		osculatingGroundTrackRangeEnd.onEndEdit.AddListener(ChangeOsculatingGroundTrackRangeSetting);
		
		useLineRendererOnLinesAndFrames.onValueChanged.AddListener(ToggleLineRendererUseOnLinesAndFrames);
		linesAndFramesLineWidth.onEndEdit.AddListener(ChangeLineWidthOnLinesAndFrames);
		
		syncBroadcastViewersWithCommandVizardToggle.onValueChanged.AddListener(ToggleForceBroadcastSyncSettings);

		particleLifeScalar.onEndEdit.AddListener(SetThrusterParticleLifeTimeFromSettingPanel);
		thrusterColorChooserButton.onClick.AddListener(EnableThrusterPlumeColorChooser);
	}

    // Start is called before the first frame update
    public void OnEnable()
    {
	    if (pointingVectorPanel == null)
	    {
		    pointingVectorPanel =
			    VizardGUISettings.GUICanvas.GetComponent<UserGUISettings>().addPointingVector;
	    }
	    ApplyUserSettingsToPanel();
    }
    public void ApplyUserSettingsToPanel(){

		transform.SetAsLastSibling();
		
		AdjustSettingsOptionsForScenarioObjects(); 
		
		//General
		ambientLightingSlider.value = RenderSettings.ambientIntensity;
		emissiveSlider.value = (float) PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftShadowBrightness;
		
		ShowCurrentGUIReferenceResolution();
		spacecraftScale.text = $"{SpacecraftStateUtilities.DefaultLocalViewSpacecraftScale}";
		spacecraftHelioScale.text = $"{SpacecraftStateUtilities.DefaultHelioViewSpacecraftScale}";
		celestialBodyHelioScale.text = $"{CelestialBodyStateUtilities.DefaultHelioPlanetScale}";
		UpdateScalesText();
		
		//Main Camera
		cameraAngularKeyboardRate.text = $"{MainCameraUtilities.KeyPanRate}";
		cameraZoomKeyboardRate.text = $"{MainCameraUtilities.KeyZoomMultiplier}";
		
		//Orbit Lines
		RestoreLastOrbitSegmentSetting();
		RestoreLastOrbitRangeSetting();
		spacecraftOrbitLineWidth.text = $"{PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftOrbitLineWidth}";
		celestialBodyOrbitLineWidth.text = $"{PersistentUserSettings.persistentSettingsFromLastSave.CelestialBodyOrbitLineWidth}";
		
		//Ground Tracks
		RestoreLastOsculatingGroundTrackSetting();
		
		//Target Lines, Pointing Vectors, Coordinate Axes
		useLineRendererOnLinesAndFrames.isOn =
			PersistentUserSettings.persistentSettingsFromLastSave.UseLineRenderersForTargetLinesAndFrames == 1;
		linesAndFramesLineWidth.text = $"{PersistentUserSettings.persistentSettingsFromLastSave.LinesAndFramesLineWidth}";
		
		//Actuators
		defaultThrusterColor.color = ThrusterUtilities.GetDefaultThrusterColor();
		particleLifeScalar.text = $"{ThrusterUtilities.GetParticleLifeUserSettingScalar()}";
		
		//Broadcast
		syncBroadcastViewersWithCommandVizardToggle.isOn = VizInputUtilities.ForceBroadcastSyncSettings;
		syncBroadcastViewersWithCommandVizardToggle.interactable = (DataManager.IsLiveSim && !DataManager.SocketIsReceiveOnly);
	}


	private void RestoreLastOrbitSegmentSetting(){
		orbitLineSegments.text = (PersistentUserSettings.persistentSettingsFromLastSave.OrbitLineSegments).ToString();
	}

	private void ChangeOrbitLineSegmentCount(string newValue){
		int newSegmentCount = int.Parse(newValue);
		if (newSegmentCount >= 4)
		{
			PersistentUserSettings.SetOrbitLineSegmentsPer360(newSegmentCount, true);
		}else{
			RestoreLastOrbitSegmentSetting();
		}
	}

	private void RestoreLastOrbitRangeSetting(){
		osculatingOrbitRangeStart.text = (PersistentUserSettings.persistentSettingsFromLastSave.OsculatingOrbitLineRange[0]).ToString();
		osculatingOrbitRangeEnd.text = (PersistentUserSettings.persistentSettingsFromLastSave.OsculatingOrbitLineRange[1]).ToString();
	}

	private void RestoreLastOsculatingGroundTrackSetting()
	{
		if (PersistentUserSettings.persistentSettingsFromLastSave.OsculatingGroundTrackRange.Count < 2)
		{
			PersistentUserSettings.SetOsculatingGroundTrackDegreeRange(-180,180, false);
		}
		osculatingGroundTrackRangeStart.text = (PersistentUserSettings.persistentSettingsFromLastSave.OsculatingGroundTrackRange[0]).ToString();
		osculatingGroundTrackRangeEnd.text = (PersistentUserSettings.persistentSettingsFromLastSave.OsculatingGroundTrackRange[1]).ToString();
	}

	private void ChangeOsculatingOrbitRangeSetting(string value)
	{
		int startRange = int.Parse(osculatingOrbitRangeStart.text);
		int endRange = int.Parse(osculatingOrbitRangeEnd.text);
		if (endRange > startRange)
		{
			PersistentUserSettings.SetOsculatingOrbitDegreeRange(startRange, endRange, true);
		}
	}

	private void ChangeOsculatingGroundTrackRangeSetting(string value)
	{
		int startRange = int.Parse(osculatingGroundTrackRangeStart.text);
		int endRange = int.Parse(osculatingGroundTrackRangeEnd.text);
		if (endRange > startRange)
		{
			Debug.Log($"Trying to set {startRange} and {endRange}");
			PersistentUserSettings.SetOsculatingGroundTrackDegreeRange(startRange, endRange, true);
		}
	}

	private void UpdateBrightness(float newValue){
		RenderSettings.ambientIntensity = newValue;
		PersistentUserSettings.persistentSettingsFromLastSave.Ambient=newValue;
		PersistentUserSettings.currentSessionUserAppliedSettings.Ambient = newValue;

		ambientText.text = newValue.ToString("#.00");
	}

	private void UpdateSpacecraftShaderEmissive(float newValue){
		
		emissiveText.text = newValue.ToString("#.00");
		PersistentUserSettings.SetSpacecraftShaderEmissive(newValue, true);
		if (PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftShadowBrightness <0.01){
			emissiveText.text = "0";
		}
	}

	private void AdjustSettingsOptionsForScenarioObjects(){
		//Is there a sun?
			if (CelestialBodyStateUtilities.SunMsgAvailable){
				sunIntensity.text = $"{PersistentUserSettings.persistentSettingsFromLastSave.SunIntensity}";
				attenuateSunWithDistanceToggle.gameObject.transform.parent.gameObject.SetActive(true);
				attenuateSunWithDistanceToggle.isOn = PersistentUserSettings.persistentSettingsFromLastSave.AttenuateSunLightWithDistance==1;
				sunIntensity.gameObject.transform.parent.gameObject.GetComponent<TextMeshProUGUI>().text = "Sun Intensity at 1 AU";
				sunIntensity.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2( 125,0);

			}else{
				sunIntensity.text = $"{PersistentUserSettings.persistentSettingsFromLastSave.SunIntensity}";
				attenuateSunWithDistanceToggle.gameObject.transform.parent.gameObject.SetActive(false);
				sunIntensity.gameObject.transform.parent.gameObject.GetComponent<TextMeshProUGUI>().text = "Main Light Intensity:";
				sunIntensity.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2( 115,0);
			}
			
		//Are there thrusters?
		if (!SpacecraftStateUtilities.ActuatorsList.Contains("Thruster")){
			foreach(Transform child in thrustersSettings.transform){
				foreach(TextMeshProUGUI childText in child.GetComponentsInChildren<TextMeshProUGUI>()){
					childText.color = new Color(.686f, .686f, .686f, 1f);
				}
				foreach(Button childButton in child.GetComponentsInChildren<Button>()){
					childButton.interactable = false;
				}
				foreach(TMP_InputField childInput in child.GetComponentsInChildren<TMP_InputField>()){
					childInput.interactable = false;
				}
			}
		}
	}

	private void UpdateSpacecraftScaleSetting(string newValue){
		try{
			float newScale = float.Parse(newValue);
			if (newScale >0f){
					SpacecraftStateUtilities.DefaultLocalViewSpacecraftScale = newScale;
					PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftSizeMultiplier=newScale;
					PersistentUserSettings.currentSessionUserAppliedSettings.SpacecraftSizeMultiplier = newScale;
			}
		}
		catch{
			Debug.Log("Incorrect input string format for spacecraft planet local view scale value.");
		}
	}

	private void UpdateSpacecraftHelioScaleSetting(string newValue)
	{
		try{
			float newScale = float.Parse(newValue);
			if (newScale >0f)
			{
				SpacecraftStateUtilities.DefaultHelioViewSpacecraftScale = newScale;
				PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftHelioViewSizeMultiplier=newScale;
				PersistentUserSettings.currentSessionUserAppliedSettings.SpacecraftHelioViewSizeMultiplier = newScale;
			}
		}
		catch{
			Debug.Log("Incorrect input string format for spacecraft heliocentric scale value.");
		}
	}

	private void UpdateCelestialBodyHelioSetting(string newValue)
	{
		float newScale = float.Parse(newValue);
		if (newScale > 0f)
		{
			CelestialBodyStateUtilities.DefaultHelioPlanetScale = newScale;
			PersistentUserSettings.persistentSettingsFromLastSave.CelestialBodyHelioViewSizeMultiplier=newScale;
			PersistentUserSettings.currentSessionUserAppliedSettings.CelestialBodyHelioViewSizeMultiplier = newScale;
		}
	}

	public void UpdateScalesText(){
		currentSCPlanetScaleText.text = $"x {CelestialBodyStateUtilities.LocalPlanetViewScale:E2}m";
		currentSCHelioScaleText.text = $"x {CelestialBodyStateUtilities.HelioCenteredViewScale:E2}m";
		currentCBHelioScaleText.text = $"x {CelestialBodyStateUtilities.HelioCenteredViewScale:E2}m";
	}

	private void UpdateLightAttenuation(bool isOn){
		if (CelestialBodyStateUtilities.SunMsgAvailable){
			PersistentUserSettings.SetAttenuateSunLightWithDistance(isOn, true);
		}
	}

	private void UpdateSunOrMainLightIntensitySetting(string newValue){
		try{
			float newIntensity = float.Parse(newValue);
			if (newIntensity >0f)
			{
				PersistentUserSettings.SetSunOrMainLightIntensity(newIntensity, true);
			}
		}
		catch{
			Debug.Log("Incorrect input string format for main light intensity value.");
		}
	}

	private void ToggleForceBroadcastSyncSettings(bool isOn)
	{
		if (!DataManager.SocketIsReceiveOnly)
		{
			VizInputUtilities.ForceBroadcastSyncSettings = isOn;
		}
	}

	private void ShowCurrentGUIReferenceResolution()
	{
		Vector2 currentResolution = mainCanvas.GetComponent<CanvasScaler>().referenceResolution;
		GUIReferenceResolutionHeight.text = $"{currentResolution.y}";
	}

	private void ResetGUIReferenceResolution()
	{
		ChangeGUIReferenceResolution(PersistentUserSettings.DefaultGUIReferenceHeight.ToString());
	}

	private void ChangeGUIReferenceResolution(string newValue)
	{
		try
		{
			int newHeight = int.Parse(newValue);
			if (newHeight < 300)
			{
				newHeight = 300;
			}

			mainCanvas.GetComponent<CanvasScaler>().referenceResolution =
				new Vector2(defaultGUIResolutionWidth, newHeight);
			PersistentUserSettings.persistentSettingsFromLastSave.CustomGUIReferenceHeight = newHeight;
			PersistentUserSettings.currentSessionUserAppliedSettings.CustomGUIReferenceHeight = newHeight;
			ShowCurrentGUIReferenceResolution();
		}
		catch
		{
			Debug.Log("Invalid reference resolution height entered.");
		}
	}

	private void SetKeyboardZoomMultiplier(string value){
		try
		{
			float newValue = Mathf.Abs(float.Parse(value));
			MainCameraUtilities.KeyZoomMultiplier=newValue;
			PersistentUserSettings.persistentSettingsFromLastSave.KeyboardZoomRate = newValue;
			PersistentUserSettings.currentSessionUserAppliedSettings.KeyboardZoomRate = newValue;
		}catch{
			Debug.Log("Incorrect input string format for zoom rate.");
		}
	}

	private void SetKeyboardPanRate(string value){
		try{
			float newValue = Mathf.Abs(float.Parse(value));
			MainCameraUtilities.KeyPanRate = newValue;
			PersistentUserSettings.persistentSettingsFromLastSave.KeyboardAngularRate = newValue;
			PersistentUserSettings.currentSessionUserAppliedSettings.KeyboardAngularRate = newValue;
		}catch{
			Debug.Log("Incorrect input string format for pan rate.");
		}
	}

	private void ChangeSpacecraftOrbitLineWidth(string value)
	{
		try
		{
			float newMultiplier = float.Parse(value);
			if (newMultiplier > 0f)
			{
				PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftOrbitLineWidth = newMultiplier;
				PersistentUserSettings.currentSessionUserAppliedSettings.SpacecraftOrbitLineWidth = newMultiplier;
				SpacecraftStateUtilities.UpdateSpacecraftOrbitLineWidth();
			}
			else
			{
				spacecraftOrbitLineWidth.text =
					$"{PersistentUserSettings.persistentSettingsFromLastSave.SpacecraftOrbitLineWidth}";
			}
		}
		catch
		{
			Debug.Log("Incorrect input string format for spacecraft orbit line scale value.");
		}
	}

	private void ChangeCelestialBodyOrbitLineWidth(string value)
	{
		try
		{
			float newMultiplier = float.Parse(value);
			if (newMultiplier > 0f)
			{
				PersistentUserSettings.persistentSettingsFromLastSave.CelestialBodyOrbitLineWidth = newMultiplier;
				PersistentUserSettings.currentSessionUserAppliedSettings.CelestialBodyOrbitLineWidth = newMultiplier;
				CelestialBodyStateUtilities.UpdateCelestialBodyOrbitLineWidth();
			}
			else
			{
				celestialBodyOrbitLineWidth.text =
					$"{PersistentUserSettings.persistentSettingsFromLastSave.CelestialBodyOrbitLineWidth}";
			}
		}
		catch
		{
			Debug.Log("Incorrect input string format for celestial body orbit line scale value.");
		}
	}

	private void UpdateOrbitLinePlotterSettings(bool isOn)
	{
		bool lineRenderersOn = useLineRendererLines.isOn;
		bool openGLLinesOn = useOpenGLOrbitLines.isOn; 
		foreach (GameObject line in SpacecraftStateUtilities.SpacecraftOrbitLinesList)
		{
			line.GetComponent<OsculatingOrbitLinePlotter>().UpdateLinePlotters(openGLLinesOn, lineRenderersOn, true);
		}
		foreach (GameObject line in CelestialBodyStateUtilities.CelestialBodyOrbitLines)
		{
			line.GetComponent<OsculatingOrbitLinePlotter>().UpdateLinePlotters(openGLLinesOn, lineRenderersOn);
		}

		spacecraftOrbitLineWidth.interactable = lineRenderersOn;
		celestialBodyOrbitLineWidth.interactable = lineRenderersOn;
		Color textColor = lineRenderersOn ? Color.white : Color.gray;
		
		Transform spacecraftOrbitLineWidthParent = spacecraftOrbitLineWidth.transform.parent;
		spacecraftOrbitLineWidthParent.GetComponent<TextMeshProUGUI>().color = textColor;
		spacecraftOrbitLineWidthParent.GetChild(1).GetComponent<TextMeshProUGUI>().color = textColor;
		
		Transform celestialBodyOrbitLineWidthParent = celestialBodyOrbitLineWidth.transform.parent;
		celestialBodyOrbitLineWidthParent.GetComponent<TextMeshProUGUI>().color = textColor;
		celestialBodyOrbitLineWidthParent.GetChild(1).GetComponent<TextMeshProUGUI>().color = textColor;
	}
	
	private void ToggleLineRendererUseOnLinesAndFrames(bool isOn)
	{
		int value = isOn ? 1 : -1;
		PersistentUserSettings.persistentSettingsFromLastSave.UseLineRenderersForTargetLinesAndFrames = value;
		PersistentUserSettings.currentSessionUserAppliedSettings.UseLineRenderersForTargetLinesAndFrames = value;
		UpdateTargetLinesPointingLinesAndCoordinateFrames();
	}

	private void ChangeLineWidthOnLinesAndFrames(string newValue)
	{
		try
		{
			float newMultiplier = float.Parse(newValue);
			if (newMultiplier > 0f)
			{
				PersistentUserSettings.persistentSettingsFromLastSave.LinesAndFramesLineWidth = newMultiplier;
				PersistentUserSettings.currentSessionUserAppliedSettings.LinesAndFramesLineWidth = newMultiplier;
				UpdateTargetLinesPointingLinesAndCoordinateFrames();
			}
			else
			{
				linesAndFramesLineWidth.text=$"{PersistentUserSettings.persistentSettingsFromLastSave.LinesAndFramesLineWidth}";
			}
		}
		catch
		{
			Debug.Log("Incorrect input string format for lines and coordinate frames line scalar.");
		}
	}

	public void UpdateTargetLinesPointingLinesAndCoordinateFrames()
	{
		bool linesOn = PersistentUserSettings.persistentSettingsFromLastSave.UseLineRenderersForTargetLinesAndFrames == 1;
		//Talk to DrawTargetLines
		VizardGUISettings.PlaybackManager.transform.GetComponent<DrawTargetLines>().UpdateLineRendererSettings(linesOn);
		
		//Talk to PointingVectorPanel to change all Pointing Vectors width
		pointingVectorPanel.UpdateAllPointingLineLineRenderers(linesOn);
		
		//Talk to all full Locations about the communication lines' width
		if (!VizardGUISettings.UseSimpleMarkersForLocations)
		{
			foreach (DrawLocationMarker location in CelestialBodyStateUtilities.LocationsDictionary.Values)
			{
				location.UpdateLineRendererSettings(PersistentUserSettings.persistentSettingsFromLastSave.UseLineRenderersForTargetLinesAndFrames==1);
			}
		}
		
		//Update any active coordinate frames using their "DrawAxes" script
		foreach (GameObject sc in SpacecraftStateUtilities.SpacecraftList)
		{
			DrawAxes[] allCS = sc.GetComponentsInChildren<DrawAxes>();
			foreach (DrawAxes da in allCS)
			{
				da.UpdateLineRendererSettings(linesOn);
			}
		}
		foreach (GameObject cb in CelestialBodyStateUtilities.CelestialBodiesList)
		{
			DrawAxes[] allCS = cb.GetComponentsInChildren<DrawAxes>();
			foreach (DrawAxes da in allCS)
			{
				da.UpdateLineRendererSettings(linesOn);
			}
		}
	}

	private void EnableThrusterPlumeColorChooser () 
	{
		VizardGUISettings.ColorWheelPanel.SetActive (true);
		VizardGUISettings.ColorWheelPanel.GetComponent<ColorWheelMethods> ().SetCallerName("thrusterSettingsPanel");
	}

	private void SetThrusterParticleLifeTimeFromSettingPanel(string newValue){ 
		try{
			ThrusterUtilities.SetParticleLifeUserSettingScalar(Mathf.Abs( float.Parse(newValue)));
			float valueToSet = ThrusterUtilities.GetParticleLifeUserSettingScalar();
			PersistentUserSettings.persistentSettingsFromLastSave.DefaultThrusterPlumeLifeScalar = valueToSet;
			PersistentUserSettings.currentSessionUserAppliedSettings.DefaultThrusterPlumeLifeScalar = valueToSet;
		}catch{
			Debug.Log("Incorrect input string format for particle life setting.");
		}
	}
}
