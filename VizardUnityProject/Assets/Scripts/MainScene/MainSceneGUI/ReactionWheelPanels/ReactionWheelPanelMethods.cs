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
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Sets up and updates a spacecraft's reaction wheel display panel
/// </summary>
public class ReactionWheelPanelMethods : MonoBehaviour {
	private TextMeshProUGUI allocatorLabel;
	private string spacecraftName;
	private string allocatorMode;
	private double speedScale;
	private double torqueScale;
	public int spacecraftIndex;
	private GameObject myPanel;
	private Button verboseButton;
	private GameObject verbosePanel;
	private bool verboseOn;
	private int wheelCount;

	private float ypos = -35;
	private float xpos = 50;


	private List<GameObject> speedBarsList = new List<GameObject> ();
	private List<GameObject> torqueBarsList = new List<GameObject> ();
	private List<TextMeshProUGUI> speedLabelsList = new List<TextMeshProUGUI>();
	private List<TextMeshProUGUI> torqueLabelsList = new List<TextMeshProUGUI>();
	

	public void InitializePanel (GameObject parentPanel, int spacecraftID, string spacecraftName){
		spacecraftIndex = spacecraftID;
		this.spacecraftName = spacecraftName;
		myPanel = parentPanel;
		string parentName = MessageList.FirstMessage.Spacecraft[spacecraftIndex].ParentSpacecraftName;
		string fullName = string.IsNullOrEmpty(parentName) ? spacecraftName : parentName + "/" + spacecraftName;
		myPanel.name = fullName + " Reaction Wheels Panel";
		bool multiplePanels = MessageList.FirstMessage.Spacecraft.Count(sc => sc.ReactionWheels.Count > 0) > 1;
		myPanel.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = multiplePanels ? fullName + " RW" : "Reaction Wheels";

		wheelCount = MessageList.FirstMessage.Spacecraft[spacecraftIndex].ReactionWheels.Count;

		Color blueColor = new Vector4 (0.32f, 0.73f, 0.93f, 1f);
		Color yellowColor = new Vector4(0.93f,0.84f,0.32f,1f);

		for (int i = 3; i <= 6; i++) {
			var label = myPanel.transform.GetChild(i).GetComponent<TextMeshProUGUI>();
			label.rectTransform.sizeDelta = new Vector2(55, 30);
			label.textWrappingMode = TextWrappingModes.NoWrap;
		}
		myPanel.transform.GetChild(7).GetComponent<RectTransform>().anchoredPosition += new Vector2(20, 0);
		UpdateScales ();
		myPanel.transform.GetChild (5).GetComponent<RectTransform> ().anchoredPosition = new Vector2 (42 * wheelCount + 50, -25);
		myPanel.transform.GetChild (6).GetComponent<RectTransform> ().anchoredPosition = new Vector2 (42 * wheelCount + 50, -115);
		myPanel.transform.GetChild (9).GetComponent<RectTransform> ().anchoredPosition = new Vector2 (42 * wheelCount + 47, -105);
		myPanel.transform.GetChild (7).GetComponent<RectTransform> ().sizeDelta = new Vector2 (42 * wheelCount+5, 1);

		verboseButton = myPanel.transform.GetChild (10).GetComponent<Button> ();
		verboseButton.onClick.AddListener (ToggleVerboseMode);
		verbosePanel = myPanel.transform.GetChild (11).gameObject;
		verbosePanel.GetComponent<RectTransform> ().sizeDelta = new Vector2 ((42*wheelCount+106), 40);

		for (int wheel=0; wheel<wheelCount ; wheel++){
			//Add the reaction wheel speed bar
			GameObject speedBar = Instantiate(Resources.Load("Prefabs/GUIGenerics/BarDisplay")as GameObject, myPanel.transform, true);
			speedBar.GetComponent<RectTransform> ().anchoredPosition = new Vector2 (xpos, ypos);
			speedBar.name = $"Speed RW{wheel}";
			speedBar.GetComponent<BarDisplayMethods> ().ChangeMaxValue (ReactionWheelUtilities.MaxSpeed[spacecraftIndex]);
			speedBar.transform.GetChild (1).transform.GetChild (0).GetComponent<Image> ().color = blueColor;

			//Add the label for the speed/torque bar pair
			GameObject rwLabel = Instantiate(Resources.Load("Prefabs/GUIGenerics/GUILabel") as GameObject, myPanel.transform, true);
			rwLabel.name = $"RW{wheel}";
			rwLabel.GetComponent<TextMeshProUGUI>().text = WheelLabel(wheel);
			rwLabel.GetComponent<TextMeshProUGUI> ().alignment = TextAlignmentOptions.Midline;
			rwLabel.GetComponent<RectTransform> ().anchoredPosition = new Vector2 ((xpos+5), -130);

			TextMeshProUGUI speedLabel = AddVerboseLabel ($"Speed Verbose RW{wheel}", xpos+7, 0);
			TextMeshProUGUI torqueLabel = AddVerboseLabel ($"Torque Verbose RW{wheel}", xpos+7, -15);
			
			xpos += 15;
			//Add the reaction wheel torque bar
			GameObject torqueBar = Instantiate(Resources.Load("Prefabs/GUIGenerics/BarDisplay") as GameObject, myPanel.transform, true);
			torqueBar.GetComponent<RectTransform> ().anchoredPosition = new Vector2 (xpos, ypos);
			torqueBar.name = $"Torque RW{wheel}";
			torqueBar.GetComponent<BarDisplayMethods> ().ChangeMaxValue (ReactionWheelUtilities.MaxTorque[spacecraftIndex]);
			torqueBar.transform.GetChild (1).transform.GetChild (0).GetComponent<Image> ().color = yellowColor;
			
			xpos += 27;
			speedBarsList.Add (speedBar);
			torqueBarsList.Add (torqueBar);
			torqueLabelsList.Add (torqueLabel);
			speedLabelsList.Add (speedLabel);
		}

		//Label the units once, in the column past the last wheel
		AddVerboseLabel ("Speed Units", xpos+7, 0).text = "rad/s";
		AddVerboseLabel ("Torque Units", xpos+7, -15).text = "mN·m";
			
		allocatorLabel = Instantiate(Resources.Load<GameObject>("Prefabs/GUIGenerics/GUILabel"), myPanel.transform, false).GetComponent<TextMeshProUGUI>();
		allocatorLabel.name = "AllocatorState";
		var rect = allocatorLabel.rectTransform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
		rect.anchoredPosition = new Vector2(8, -146);
		rect.sizeDelta = new Vector2(42 * wheelCount + 90, 20);
		allocatorLabel.fontSize = 11;
		allocatorLabel.alignment = TextAlignmentOptions.Midline;
		allocatorLabel.textWrappingMode = TextWrappingModes.NoWrap;
		allocatorLabel.gameObject.SetActive(false);
		UpdatePanelSize();
		verbosePanel.SetActive (false);

	}

	private TextMeshProUGUI AddVerboseLabel (string name, float x, float y)
	{
		GameObject label = Instantiate(Resources.Load("Prefabs/GUIGenerics/GUILabel") as GameObject, verbosePanel.transform, true);
		label.name = name;
		label.GetComponent<RectTransform> ().sizeDelta = new Vector2 (42, 30);
		label.GetComponent<RectTransform> ().anchoredPosition = new Vector2 (x, y);
		TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI> ();
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.fontSize = 9;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		return text;
	}

	public static string FormatSigned(double value) => value.ToString("+0.0;-0.0;+0.0");

	public static string WheelLabel(int index)
	{
		string label = "";
		do { label = (char)('A' + index % 26) + label; index = index / 26 - 1; } while (index >= 0);
		return label;
	}

	private void UpdateScales(){
		speedScale = ReactionWheelUtilities.MaxSpeed[spacecraftIndex];
		torqueScale = ReactionWheelUtilities.MaxTorque[spacecraftIndex];
		myPanel.transform.GetChild(3).GetComponent<TextMeshProUGUI>().text = speedScale.ToString("F1");
		myPanel.transform.GetChild(4).GetComponent<TextMeshProUGUI>().text = (-speedScale).ToString("F1");
		myPanel.transform.GetChild(5).GetComponent<TextMeshProUGUI>().text = torqueScale.ToString("F1");
		myPanel.transform.GetChild(6).GetComponent<TextMeshProUGUI>().text = (-torqueScale).ToString("F1");
	}

	private void UpdateAllocatorMode(){
		var hud = StockHud.Current;
		string mode = hud != null && hud.Spacecraft == spacecraftName ? hud.Text("RW allocator mode") : null;
		if (allocatorMode == mode) return;
		allocatorMode = mode;
		bool show = !string.IsNullOrWhiteSpace(mode);
		allocatorLabel.gameObject.SetActive(show);
		if (show) {
			allocatorLabel.text = "RW Allocator Mode: " + System.Text.RegularExpressions.Regex.Replace(mode, "(?<=[a-z])(?=[A-Z])", " ");
		}
		UpdatePanelSize();
	}

	void Update () {
		UpdateAllocatorMode();
		double[] updatedSpeeds = ReactionWheelUtilities.GetReactionWheelSpeeds(spacecraftIndex);
		double[] updatedTorques = ReactionWheelUtilities.GetReactionWheelTorques (spacecraftIndex);

		if (speedScale != ReactionWheelUtilities.MaxSpeed[spacecraftIndex] || torqueScale != ReactionWheelUtilities.MaxTorque[spacecraftIndex]) {
			UpdateScales();
			for (int wheel = 0; wheel < wheelCount; wheel++) {
				speedBarsList[wheel].GetComponent<BarDisplayMethods>().ChangeMaxValue(speedScale);
				torqueBarsList[wheel].GetComponent<BarDisplayMethods>().ChangeMaxValue(torqueScale);
			}
		}

		for (int wheel = 0; wheel < wheelCount; wheel++) {
			speedBarsList [wheel].GetComponent<BarDisplayMethods> ().currentValue = (float)updatedSpeeds[wheel];
			torqueBarsList [wheel].GetComponent<BarDisplayMethods> ().currentValue = (float)updatedTorques [wheel];

			if (verboseOn) {
				speedLabelsList [wheel].text = FormatSigned(updatedSpeeds[wheel]);
				torqueLabelsList [wheel].text = FormatSigned(updatedTorques[wheel]);
			}
		}
	}

	private void UpdatePanelSize(){
		float height = allocatorLabel.gameObject.activeSelf ? 179 : 162;
		if (verboseOn) height += 41;
		myPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(42 * wheelCount + 106, height);
		VizardGUISettings.PanelViewMgr.TelemetryDock?.Reflow(PanelDock.Slot.ReactionWheels);
	}

	private void ToggleVerboseMode(){
		verboseOn = !verboseOn;
		verbosePanel.SetActive(verboseOn);
		UpdatePanelSize();
	}
}
