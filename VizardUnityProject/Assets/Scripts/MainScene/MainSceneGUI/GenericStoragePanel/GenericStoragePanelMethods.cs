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
/// Sets up the generic storage device display panel
/// for a single spacecraft and all of it's generic storage
/// sub-panels.
/// </summary>
public class GenericStoragePanelMethods : MonoBehaviour
{
	public int spacecraftIndex;
	public string spacecraftName;
	public TextMeshProUGUI panelLabel;
	public Button verboseButton;

	public List<GameObject> myBars;

	private float ypos = -25;
	private int deviceNameLength = 16;
	private int verboseLength;
	private int barWidth = 90;
	private int verboseWidth = 30;
	private int deviceCount;
	private bool verboseOn;
	private bool compact;
	private readonly float pixelsPerCharacter=7f;

	public void InitializePanel(int scIndex, string scName){
		verboseButton.onClick.AddListener(ToggleVerbose);
		spacecraftName = scName;
		spacecraftIndex = scIndex;
		string parentSCName = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].ParentSpacecraftName;
		
		string panelName = spacecraftName + " Storage";
		if (parentSCName != "")
		{
			panelName = parentSCName+"/"+panelName;
		}


		transform.gameObject.name = spacecraftName+"StoragePanel";
		deviceCount = MessageList.FirstMessage.Spacecraft[scIndex].StorageDevices.Count;
		compact = !DataManager.UseVR && deviceCount == 1
			&& MessageList.FirstMessage.Spacecraft.Count(sc => sc.StorageDevices.Count > 0) == 1;
		if (compact) {
			ypos = -11;
			verboseOn = true;
			panelLabel.gameObject.SetActive(false);
			transform.Find("CloseButton").GetComponent<RectTransform>().anchoredPosition = new Vector2(5, -11);
			verboseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, 0);
		} else if (deviceCount == 1) {
			ypos = -30;
		}

		for (int i = 0; i < deviceCount; i++)
		{
			VizProtobufferMessage.VizMessage.Types.GenericStorage sd = MessageList.FirstMessage.Spacecraft[scIndex]
				.StorageDevices[i];
			GameObject bar = DataManager.UseVR ? 
				Instantiate(Resources.Load("Prefabs/VR/VizardVR_GenericStoragePanelUnit") as GameObject) 
				: Instantiate(Resources.Load("Prefabs/SpacecraftPanels/GenericStoragePanelUnit") as GameObject);

			bar.transform.SetParent(transform);
			bar.GetComponent<RectTransform>().anchoredPosition = new Vector2(compact ? 25 : 0, ypos);
			ypos-=20;

			bar.GetComponent<GenericStorageUnitMethods>().InitializeStorageUnit(i, scIndex, sd);

			if (sd.Label.Length>deviceNameLength){
				deviceNameLength = sd.Label.Length;
			}
			
			if (sd.Units.Length+22>verboseLength){
				verboseLength = 22+sd.Units.Length;
			}
			myBars.Add(bar);
		}
			
		if (panelName.Length>16){
			barWidth = (int) (panelName.Length*pixelsPerCharacter);
		}

		verboseWidth = (verboseLength*5);
		if (compact) {
			barWidth = Mathf.Max(140, deviceNameLength * 7);
			var storage = MessageList.FirstMessage.Spacecraft[scIndex].StorageDevices[0];
			var text = myBars[0].GetComponent<GenericStorageUnitMethods>().verboseText;
			text.gameObject.SetActive(true);
			text.alignment = TextAlignmentOptions.MidlineLeft;
			verboseWidth = Mathf.CeilToInt(text.GetPreferredValues(GenericStorageUnitMethods.FormatReading(
				storage.MaxValue, storage.MaxValue, storage.Units)).x) + 10;
		}

		foreach(GameObject bar in myBars){
			bar.GetComponent<GenericStorageUnitMethods>().SetBarWidth(barWidth);
			bar.GetComponent<GenericStorageUnitMethods>().SetVerboseTextWidth(verboseWidth);
			bar.GetComponent<GenericStorageUnitMethods>().verboseText.gameObject.SetActive(verboseOn);
		}

		if (!DataManager.UseVR)
		{
			panelLabel.text = panelName;
		}

		SetPanelSize();
	}

	private void SetPanelSize(){
		int panelWidth = barWidth + (compact ? 60 : 30);
		if (verboseOn){
			panelWidth +=verboseWidth;
		}

		float panelHeight = -ypos+5;
		if (panelHeight < 60f){
			panelHeight = 60f;
		}
		GetComponent<RectTransform>().sizeDelta = new Vector2(panelWidth, compact ? 42 : panelHeight);
	}

	private void ToggleVerbose(){
		verboseOn = !verboseOn;
		SetPanelSize();
		foreach(GameObject bar in myBars){
			bar.GetComponent<GenericStorageUnitMethods>().verboseText.gameObject.SetActive(verboseOn);
		}
	}
}
