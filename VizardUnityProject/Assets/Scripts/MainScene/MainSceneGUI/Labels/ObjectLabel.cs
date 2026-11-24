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
using UnityEngine;
using TMPro;
/// <summary>
/// Forces text label to track onscreen object's location 
/// </summary>
public class ObjectLabel : MonoBehaviour
{
	public Transform targetTransform;
	public Vector2 screenOffset;
	public Camera cameraToUse;
	public bool updatePosition=true;
	private RectTransform mainCanvasRect;
	
    // Start is called before the first frame update
    void Start()
    {
		cameraToUse = Camera.main;
		mainCanvasRect =VizardGUISettings.GUICanvas.GetComponent<RectTransform>();
    }

	public void InitializeLabel(string displayText, Transform target, Vector2 offset, int alignment = 1){
		TextMeshProUGUI myText = GetComponent<TextMeshProUGUI>();
		myText.text = LabelMaker.CapitalizeLabel(displayText);
		myText.fontSize = LabelMaker.FontSize;
		targetTransform = target;
		screenOffset = offset;

		if (alignment  == 0){
			myText.alignment = TextAlignmentOptions.MidlineLeft;
		}else if (alignment  == 2){
			myText.alignment = TextAlignmentOptions.MidlineRight;
		}
	}

    // Update is called once per frame
    void Update()
    {
		if (updatePosition){
			#if VIZARD_OPENXR
			Vector2 localPoint;
			Vector2 screenPointOfObject =
				RectTransformUtility.WorldToScreenPoint(cameraToUse, targetTransform.position);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvasRect, screenPointOfObject, cameraToUse,
				out localPoint);
			Vector3 localPoint3D = new Vector3(localPoint.x, localPoint.y, 1f);
			GetComponent<RectTransform>().localPosition = localPoint3D;
#else
			Vector3 currentTargetTransformPosition = cameraToUse.WorldToScreenPoint((targetTransform.position));
			Vector2 maxDims = new Vector2( mainCanvasRect.rect.x, mainCanvasRect.rect.y);
			float factorScreenScale = 1 / mainCanvasRect.localScale.x;  //x and y scales should be the same value
			if (currentTargetTransformPosition.z>0){
				GetComponent<RectTransform>().anchoredPosition =
 factorScreenScale*new Vector2(currentTargetTransformPosition.x+screenOffset.x, currentTargetTransformPosition.y+screenOffset.y); 
			}else{
				//Keep the label off the screen if it's behind the main camera
				GetComponent<RectTransform>().anchoredPosition =
 factorScreenScale*new Vector2(maxDims.x+100,  maxDims.y+100);
			}
#endif
		}
    }
}
