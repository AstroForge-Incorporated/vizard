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
/// <summary>
/// Label Factory for creating floating labels for scenario objects at runtime
/// </summary>
public class LabelMaker : MonoBehaviour
{
	[Header("Label Holders")]
	[Tooltip("Holds all the label holders and their labels")]
	public static GameObject allLabelsHolder; //Don't make private
	[Tooltip("List of all label holders")]
	public static List<GameObject> labelHolders=new List<GameObject> (); //Don't make private
	
	public static readonly char Circumflex = '\u0302';
	public static int FontSize = 14;

	public static string CapitalizeLabel(string text) => string.IsNullOrEmpty(text)
		? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

	public static GameObject CreateLabel(string displayText, string parentObjectName, GameObject targetObject, Vector3 localOffset, string labelType, int alignment = 1){
		GameObject parentHolder = null;

		if (labelHolders.Count>0){
			foreach(GameObject holder in labelHolders){
				if (holder.name == labelType){
					parentHolder = holder;
					break;
				}
			}
		}
		if (parentHolder == null){
			parentHolder = AddLabelGroup(labelType);
		}
		// Create a new label
		GameObject newLabel = Instantiate (Resources.Load ("Prefabs/GUIGenerics/FloatingLabel") as GameObject, parentHolder.transform, true);
		newLabel.name = $"{parentObjectName} {labelType} {displayText}";
		newLabel.GetComponent<RectTransform>().localScale = Vector3.one;
		newLabel.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0,0,0);
		newLabel.GetComponent<ObjectLabel>().InitializeLabel(displayText, targetObject.transform, localOffset, alignment);
		return newLabel;
	}

	private static GameObject AddLabelGroup(string labelGroup){
		if (allLabelsHolder == null){
			allLabelsHolder=GameObject.Find("ScenarioObjectLabels");
		}
		GameObject newLabelHolder = new GameObject(labelGroup,typeof(RectTransform));
		newLabelHolder.transform.SetParent(allLabelsHolder.transform);
		newLabelHolder.GetComponent<RectTransform>().localScale = Vector3.one;
		newLabelHolder.GetComponent<RectTransform>().anchorMin = Vector2.zero;
		newLabelHolder.GetComponent<RectTransform>().anchorMax = Vector2.zero;
		newLabelHolder.GetComponent<RectTransform>().pivot =Vector2.zero;
		newLabelHolder.GetComponent<RectTransform>().anchoredPosition3D=Vector3.zero;
		labelHolders.Add(newLabelHolder);
		return newLabelHolder;
	}

	public static void ChangeFontSize(float scale, int newFontSize = 0){
		FontSize = (int) Mathf.Clamp(13f*scale,13f,30f);
		if (newFontSize != 0){
			FontSize = newFontSize;
		}
		foreach(GameObject holder in labelHolders){
			foreach(Transform child in holder.transform){
				child.GetComponent<TextMeshProUGUI>().fontSize = FontSize;	
			}
		}
	}

	public static void ResetLabelMaker()
	{
		allLabelsHolder = null;
		labelHolders = new List<GameObject> ();
		FontSize = 14;
	}
}
