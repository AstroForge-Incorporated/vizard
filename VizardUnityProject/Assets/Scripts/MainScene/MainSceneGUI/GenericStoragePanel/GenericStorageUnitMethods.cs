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
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sets up and updates one storage device's sub-panel display
/// </summary>
public class GenericStorageUnitMethods : MonoBehaviour
{
    public RectTransform measurementRect;
    public RectTransform backgroundRect;
    public TextMeshProUGUI verboseText;
    public TextMeshProUGUI deviceName;
    public TextMeshProUGUI hoverText;

    private int deviceIndex;
    private int spacecraftIndex;
    private string units;

    private List<Color> barColors = new List<Color>();
    private readonly Color CUgold = new((207f / 255f), (184f / 255f), (124f / 255f), 1f);
    private readonly Color HUDGreen = new(0f, 1f, 26f/255f, 1f);
    private int colorCount;
    private List<int> colorThresholds = new List<int>();
    private bool checkThresholds;
    private int currentColor;
    
    private int barWidth = 90;
    private int barHeight = 15; 
    
    public void InitializeStorageUnit(int sdIndex, int scIndex,
        VizProtobufferMessage.VizMessage.Types.GenericStorage myMsg)
    {
        deviceIndex = sdIndex;
        spacecraftIndex = scIndex;
        
        units = myMsg.Units;

        deviceName.text = myMsg.Label;
        if(DataManager.UseVR){
            barHeight = 13; 
        }

        SetBarColorsList(myMsg);
        if (colorCount > 1)
        {
            SetColorThresholds(myMsg);
        }
        else
        {
            measurementRect.GetComponent<Image>().color = barColors[0];
            currentColor = 0;
        }

        UpdateCurrentValue();
    }

	void FixedUpdate()
	{
		UpdateCurrentValue();
	}

    private void UpdateCurrentValue()
    {
        
        VizProtobufferMessage.VizMessage.Types.GenericStorage myMsg = MessageList.CurrentMessage
            .Spacecraft[spacecraftIndex].StorageDevices[deviceIndex];

        float value = (float) myMsg.CurrentValue;
        float maxValue = (float) myMsg.MaxValue;

        
        float valueRatio = 0;
        if (maxValue > 0)
        {
            valueRatio = value / maxValue;
        }

        //Debug.Log("I am trying to update my value:"+valueRatio);
        
        if (value >= 0)
        {
            hoverText.text = $"{value}";
            if (verboseText.gameObject.activeSelf)
            {
                if (!DataManager.UseVR)
                {
                    verboseText.text = FormatReading(myMsg.CurrentValue, myMsg.MaxValue, units);

                }
                else
                {
                    float percentage = value / maxValue * 100f;
                    verboseText.text = percentage.ToString("F0") + "%";
                    verboseText.color = HUDGreen;

                }
            } 

            measurementRect.sizeDelta = new Vector2(valueRatio * barWidth, barHeight);
            
            if (checkThresholds)
            {
                float valuePercent = valueRatio * 100f;
                if ((valuePercent < colorThresholds[currentColor]) || (valuePercent >= colorThresholds[currentColor + 1]))
                {
                    for (int i = 0; i < colorCount; i++)
                    {
                        if ((valuePercent >= colorThresholds[i]) && (valuePercent < colorThresholds[i + 1]))
                        {

                            currentColor = i;
                            break;
                        }
                    }
                }
                measurementRect.GetComponent<Image>().color = barColors[currentColor];
            }
        }
        else
        {
            hoverText.text = "Unavailable";
            if (!DataManager.UseVR)
            {
                verboseText.text =  "Unavailable";
                verboseText.color = Color.gray;
                
            }
            else
            {
                verboseText.text =  "Stale";
                verboseText.color = Color.gray;
            }
            
            
            measurementRect.GetComponent<Image>().color = Color.gray;
            measurementRect.sizeDelta = new Vector2(0, barHeight);
        }


    }

    public static string FormatReading(double value, double maximum, string units) => units == "kg"
        ? $"{value:F5} / {maximum} {units}" : $"{(float)value} / {(float)maximum} {units}";

    private void SetBarColorsList(VizProtobufferMessage.VizMessage.Types.GenericStorage myMsg)
    {
        int colorSize = myMsg.Color.Count;
        if (colorSize >= 4)
        {
            int i = 3;
            if (colorSize % 4 != 0)
            {
                string errorString =
                    $"Generic Storage Color message requires 4 values per color (R, G, B, and A). {colorSize} were provided for spacecraft: {spacecraftIndex}, device index: {deviceIndex}";
                VizardGUISettings.UpdateErrorMessages(errorString);
            }

            while (i < colorSize)
            {
                barColors.Add(new Color(myMsg.Color[i - 3] / 255f, myMsg.Color[i - 2] / 255f, myMsg.Color[i - 1] / 255f,
                    myMsg.Color[i] / 255f));
                i += 4;
            }
        }
        else
        {
            if (colorSize > 0)
            {
                string errorString =
                    $"Generic Storage Color message requires 4 values per color (R, G, B, and A). Only {colorSize} were provided for spacecraft index: {spacecraftIndex}, device index: {deviceIndex}";
                VizardGUISettings.UpdateErrorMessages(errorString);
            }

            barColors.Add(CUgold); //Use this as our default color
        }

        colorCount = barColors.Count;
    }

    private void SetColorThresholds(VizProtobufferMessage.VizMessage.Types.GenericStorage myMsg)
    {
        int thresholdCount = myMsg.Thresholds.Count;
        if ((thresholdCount > 0) && (thresholdCount == (colorCount - 1)))
        {
            checkThresholds = true;
            colorThresholds.Add(0);
            foreach (int threshold in myMsg.Thresholds)
            {
                colorThresholds.Add(threshold);
            }

            colorThresholds.Add(101); //changed to 101 to avoid issues at 100
        }
        else
        {
            checkThresholds = false;
        }
    }

    public void SetBarWidth(int pixelCount)
    {
        barWidth = pixelCount;
        backgroundRect.sizeDelta = new Vector2(pixelCount, barHeight);
    }

    public void SetVerboseTextWidth(int pixelCount)
    {
        verboseText.GetComponent<RectTransform>().sizeDelta = new Vector2(pixelCount, barHeight);
    }
}
