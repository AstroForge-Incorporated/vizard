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
/// Sets up and updates a Light HUD
/// </summary>
public class LightHUDMethods : MonoBehaviour
{
	public Light myLight;
	public GameObject mySphere;
	public LensFlare myFlare;
	public GameObject lightLabel;
	public Toggle inventoryButtonLightOnToggle;
	
	private Vector3 bskPosition;
	private Vector3 bskNormal;
	private float fovSet;
	private float rangeSet;
	private float intensitySet;
	private float lensDia;
	private string parentBodyName;
	private bool flareOn;
	private bool markerOn;
	private float emissionScalar=0.8f;
	private bool lightIsFromMessages;
	private int spacecraftIndex;
	private int lightMsgIndex;

	private bool inSpriteMode;
	
	public void InitializeLightFromPanel(string lightName, GameObject parentSC, Vector3 origin, Vector3 normal, float fov, float range, float intensity, float visibleLightDiameter, Color lightColor, bool flareEnabled, float flareSpeed, float flareBrightness, float emissionGamma, bool markerEnabled){
		gameObject.name = lightName;
		parentBodyName = parentSC.name;
		transform.SetParent(parentSC.transform);
		transform.localScale = Vector3.one;

		bskPosition = origin;
		bskNormal = normal;
		transform.localPosition = new Vector3 (origin.y, origin.z, -origin.x); //Converted to Unity CS from BSK CS
	
		Quaternion directionToPoint = Quaternion.LookRotation (new Vector3(normal.y, normal.z, -normal.x)); //Converted to Unity CS
		transform.localRotation = directionToPoint;

		if (fov is > 0 and < 180f){
			myLight.spotAngle = fov;
			fovSet = fov;
		}

		myLight.range = range;
		rangeSet = range;

		myLight.intensity = intensity;
		intensitySet = intensity;

		mySphere.transform.localScale = Vector3.one*visibleLightDiameter;
		lensDia = visibleLightDiameter;
		myFlare.fadeSpeed = flareSpeed>=0 ? flareSpeed : 4.0f;

		myFlare.brightness = flareBrightness>=0 ? flareBrightness : 0.3f;

		emissionScalar = emissionGamma;

		myLight.color = lightColor;
		myFlare.color = lightColor;

		myFlare.enabled= flareEnabled;
		flareOn = flareEnabled;

		mySphere.SetActive(markerEnabled);
		markerOn = markerEnabled;

		mySphere.GetComponent<MeshRenderer>().material.color = lightColor;
		Color emissionColor = lightColor*Mathf.LinearToGammaSpace(emissionScalar);
		mySphere.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", emissionColor);
		if (emissionScalar != 0){
			mySphere.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", emissionColor);
		}

		if (lightLabel!= null){
			lightLabel.GetComponent<TextMeshProUGUI>().text = LabelMaker.CapitalizeLabel(lightName);
			lightLabel.name = $"{parentBodyName} Light {lightName}";
		}

		if (parentSC.GetComponent<SpacecraftController>().spacecraftSprite.activeSelf)
		{
			inSpriteMode = true;
			ToggleHUDVisibility(false);
		}
	}

	public bool InitializeLightFromMessage(string lightName, VizProtobufferMessage.VizMessage.Types.Light lightMsg, GameObject parentSC, int scIndex, int lightIndex){
		lightIsFromMessages = true;
		spacecraftIndex = scIndex;
		lightMsgIndex = lightIndex;
	
		gameObject.name = lightName;
		parentBodyName = parentSC.name;

		transform.SetParent(parentSC.transform);
		transform.localScale = Vector3.one;
		
		bskPosition =new Vector3 ((float) lightMsg.Position[0], (float) lightMsg.Position[1], (float) lightMsg.Position[2]);
		bskNormal = new Vector3 ((float) lightMsg.NormalVector[0], (float) lightMsg.NormalVector[1], (float) lightMsg.NormalVector[2]);

		transform.localPosition = new Vector3 (bskPosition.y, bskPosition.z, -bskPosition.x); //Converted to Unity CS from BSK CS

		Quaternion directionToPoint = Quaternion.LookRotation (new Vector3(bskNormal.y, bskNormal.z, -bskNormal.x)); //Converted to Unity CS
		transform.localRotation = directionToPoint;
		
		if (lightMsg.FieldOfView is > 0 and <= 180f){
			myLight.spotAngle = (float) lightMsg.FieldOfView;
			fovSet = (float) lightMsg.FieldOfView;
		}else{
			string errorString =
				$"Invalid FOV provided in Light message of spacecraft: {parentBodyName}, light: {lightName}. FOV must be greater than 0 and less than 180 degrees. No light was built for this Light message.";
			VizardGUISettings.UpdateErrorMessages(errorString);
			return false;
		}

		if (lightMsg.Range>0){
			myLight.range = (float) lightMsg.Range;
			rangeSet = (float) lightMsg.Range;
		}else{
			string errorString =
				$"Invalid Range provided in Light message of spacecraft: {parentBodyName}, light: {lightName}. Range must be greater than 0. No light was built for this Light message.";
			VizardGUISettings.UpdateErrorMessages(errorString);
			return false;
		}

		intensitySet = (float) lightMsg.Intensity;
		if (intensitySet <=0){
			intensitySet = 1.0f;
		}
		myLight.intensity = intensitySet;

		lensDia = (float) lightMsg.MarkerDiameter;
		if (lensDia <= 0){
			lensDia = 0.01f;
		}
		mySphere.transform.localScale = Vector3.one*lensDia;

		if (lightMsg.ShowLightMarker == -1){
			markerOn = false;
			mySphere.SetActive(false);
		}else{
			markerOn  = true;
			mySphere.SetActive(true);
		}

		float flareSpeed = (float) lightMsg.LensFlareFadeSpeed;
		if (flareSpeed<=0){
			flareSpeed =10.0f;
		}
		myFlare.fadeSpeed= flareSpeed;
	

		float flareBrightness = (float) lightMsg.LensFlareBrightness;
		if (flareBrightness<=0){
			flareBrightness =0.3f;
		}
		myFlare.brightness= flareBrightness;

		Color lightColor = Color.white;
		if (lightMsg.Color.Count>=4){
			lightColor = new Color(lightMsg.Color[0]/255f, lightMsg.Color[1]/255f, lightMsg.Color[2]/255f, lightMsg.Color[3]/255f);
		}else if (lightMsg.Color.Count==3){
			lightColor = new Color(lightMsg.Color[0]/255f, lightMsg.Color[1]/255f, lightMsg.Color[2]/255f, 1.0f);
		}

		emissionScalar = (float) lightMsg.GammaSaturation;
		if (emissionScalar<0){
			emissionScalar = 0.8f;
		}

		myLight.color = lightColor;
		myFlare.color = lightColor;

		if (lightMsg.ShowLensFlare >=0){
			flareOn = true;
			myFlare.enabled= true;
		}else{
			flareOn = false;
			myFlare.enabled= false;
		}


		mySphere.GetComponent<MeshRenderer>().material.color = lightColor;
		
				if (parentSC.GetComponent<SpacecraftController>().spacecraftSprite.activeSelf)
		{
			inSpriteMode = true;
			ToggleHUDVisibility(false);
		}

		Color emissionColor = lightColor*Mathf.LinearToGammaSpace (emissionScalar);
		mySphere.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", emissionColor);
		
		if (parentSC.GetComponent<SpacecraftController>().spacecraftSprite.activeSelf)
		{
			inSpriteMode = true;
			ToggleHUDVisibility(false);
		}
		if (emissionScalar != 0){
			mySphere.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", emissionColor);
		}
		if (lightMsg.LightOn ==-1){
			LightIsOn(false);
		}
		return true;
	}

	public void ToggleLensFlareFromPanel(bool isOn){
		flareOn = isOn;
		myFlare.enabled = isOn;
	}

	public void ToggleMarkerFromPanel(bool isOn){
		markerOn = isOn;
		mySphere.SetActive(isOn);
	}

	void Update(){
		if (!inSpriteMode)
		{
			if (lightIsFromMessages)
			{
				int currentState = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].Lights[lightMsgIndex].LightOn;
				if (currentState == -1)
				{
					LightIsOn(false);
				}
				else if (currentState == 1)
				{
					LightIsOn(true);
				}
			}
		}
	}
	public void ToggleGUIObjectFromPanel(bool isOn){
		LightIsOn(isOn);
	}

	private void LightIsOn(bool isOn){
		myLight.enabled = isOn;
		if (inventoryButtonLightOnToggle!=null){
			inventoryButtonLightOnToggle.isOn = isOn;
		}
		myFlare.enabled = isOn;
		mySphere.SetActive(isOn);
	}
	
	/// <summary>
	/// This receives a BroadcastMessage from the parent Spacecraft when
	/// going into sprite mode. Don't delete. 
	/// </summary>
	/// <param name="spriteOn">True if attached spacecraft is in sprite mode</param>
	public void ConfigureHUDForSpriteMode(bool spriteOn)
	{
		inSpriteMode = spriteOn;
		ToggleHUDVisibility(!inSpriteMode);
	}

	private void ToggleHUDVisibility(bool isOn)
	{
		myLight.transform.gameObject.SetActive(isOn);
		mySphere.SetActive(markerOn&&isOn);
	}

	public Vector3 GetBSKPosition()
	{
		return bskPosition;
	}
	
	public Vector3 GetBSKNormal()
	{
		return bskNormal;
	}

	public double GetFOV()
	{
		return fovSet;
	}

	public double GetRange()
	{
		return rangeSet;
	}

	public double GetIntensity()
	{
		return intensitySet;
	}

	public double GetMarkerDiameter()
	{
		return lensDia;
	}

	public double GetGammaSetting()
	{
		return emissionScalar;
	}

	public bool GetMarkerOn()
	{
		return markerOn;
	}

	public string GetParentBodyName()
	{
		return parentBodyName;
	}
}
