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
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Handles user input to VR controllers to pan or zoom the camera rig or
/// select a new main camera target with raycast
/// </summary>
public class VizardVR_MainCameraMovementController : MainCameraMovementController
{
    [Header("VR Rig Movement")] public float vrRotateSpeed = 0.25f; //Speed the camera rotates about target in VR
    public float vrZoomFactor = 0.025f; //Speed the camera zooms in/out from target in VR
    public GameObject temporaryStatusPanel; //Fading status panel for user alerts

    // Orthographic viewpoints
    private readonly List<Vector3[]> viewpoints = new()
    {
        new[] {new Vector3(0, 0, -1), Vector3.up}, //Front
        new[] {new Vector3(1, 0, 0), Vector3.up}, //Right
        new[] {new Vector3(0, 1, 0), Vector3.forward}, //Top
        new[] {new Vector3(0, 0, 1), Vector3.up}, //Rear
        new[] {new Vector3(-1, 0, 0), Vector3.up}, //Left
        new[] {new Vector3(0, -1, 0), Vector3.forward} //Bottom
    };

    [Header("VR Input")] [Tooltip("Input action map to use")]
    public InputActionAsset inputActionAsset;

    [Tooltip("Left controller laser pointer")]
    public GameObject leftRaycast;

    [Tooltip("Right controller laser pointer")]
    public GameObject rightRaycast;

    [Tooltip("Left controller forward marker")]
    public Transform leftEndMarker;

    [Tooltip("Right controller forward marker")]
    public Transform rightEndMarker;
#if VIZARD_OPENXR
    // Input actions required for input to main camera movement and target selection
    private InputAction leftThumbstick;
    private InputAction rightThumbstick;
    private InputAction rightTrigger;
    private InputAction rightPrimaryButton;
    private InputAction leftTrigger;
    private InputAction leftPrimaryButton;
    private InputAction leftSecondaryButton;

    // Layer mask set to allow user interaction with Clickable Colliders (TransparentFX) layer,
    // VR UI layer, and regular UI layer
    private const int LayerMask = (1 << 1) | (1 << 5) | (1 << 26);

    // GameObject that laser pointer is currently aimed at that could be
    // selected by user to be new camera target
    private GameObject potentialCameraTarget;

    /// <summary>
    /// Monodevelop method called before any Update calls
    /// </summary>
    void Start()
    {
        leftThumbstick = inputActionAsset.FindAction("LeftThumbstick");
        rightThumbstick = inputActionAsset.FindAction("RightThumbstick");
        rightTrigger = inputActionAsset.FindAction("RightTriggerPress");
        leftTrigger = inputActionAsset.FindAction("LeftTriggerPress");
        leftSecondaryButton = inputActionAsset.FindAction("LeftSecondaryButton");
        rightPrimaryButton = inputActionAsset.FindAction("RightPrimaryButton");
        leftPrimaryButton = inputActionAsset.FindAction("LeftPrimaryButton");

        //Set user input pan and zoom factors to the preferred values for VR
        MainCameraUtilities.UserInputZoomFactor = vrZoomFactor;
        MainCameraUtilities.UserInputRotateSpeed = vrRotateSpeed;

        //Register listeners for controller action inputs
        rightPrimaryButton.performed += SelectNewCameraTarget;
        leftPrimaryButton.performed += SelectNewCameraTarget;
        leftSecondaryButton.performed += ToggleFlashlightVR;

        //Prevent input main camera movement until startup is complete
        waitUntilCamTransitionComplete = true;
    }

    /// <summary>
    /// Monodevelop method called when this instance is destroyed
    /// </summary>
    void OnDestroy()
    {
        //Deregister listeners for controller action inputs
        rightPrimaryButton.performed -= SelectNewCameraTarget;
        leftPrimaryButton.performed -= SelectNewCameraTarget;
        leftSecondaryButton.performed -= ToggleFlashlightVR;
    }

    /// <summary>
    /// Monodevelop method called after other Update methods in scene have finished at every frame
    /// <remarks>Camera movement controls work best when handled in LateUpdate</remarks>
    /// <remarks>Overrides the MainCameraMovementController LateUpdate</remarks>
    /// </summary>
    void LateUpdate() //User input handled after all updates are complete
    {
        System.DateTime currentFrameTime = System.DateTime.Now;

        if (!waitUntilCamTransitionComplete)
        {
            //Enable the right or left raycast laser pointers if triggers are pressed
            rightRaycast.SetActive(rightTrigger.inProgress);
            leftRaycast.SetActive(leftTrigger.inProgress);

            // User input to camera movement or target selection
            // only enabled if radial menu is closed
            if (!VizardGUISettings.GetVRMenuActive())
            {
                //check if there is a possible camera target along laser pointer
                if (rightTrigger.inProgress || leftTrigger.inProgress)
                {
                    CheckForRaycastTarget();
                }

                //Pan about camera target using left thumbstick
                DragToPanAboutTarget(leftThumbstick.ReadValue<Vector2>().x, -leftThumbstick.ReadValue<Vector2>().y);
                //Zoom in or out from camera target using right thumbstick
                ScrollToZoomTarget(rightThumbstick.ReadValue<Vector2>().y);
            }

            // Apply any keyboard control of camera movement
            float elapsedSeconds = (float) (currentFrameTime - LastFrameTime).TotalSeconds;
            ApplyActiveCameraAutoMovement(elapsedSeconds);
        }
        else
        {
            Debug.Log("waitUntilCamTransitionComplete is not complte");
        }

        LastFrameTime = currentFrameTime;
    }
    

    /// <summary>
    /// Raycast from camera along laser pointer and check for a possible camera target object
    /// <remarks>Each spacecraft and celestial body object has a "clickable" collider in the
    /// TransparentFX layer, if the ray intersects one of these colliders, its game
    /// object is set as the _potentialCameraTarget.</remarks>
    /// </summary>
    private void CheckForRaycastTarget()
    {
        RaycastHit hit;

        Vector3 startPoint = transform.position; //position of camera

        // vector from camera through laser pointer marker
        Vector3 direction = (rightTrigger.inProgress ? rightEndMarker.position : leftEndMarker.position) - startPoint;

        //If the ray hits a clickable collider of the TransparentFX layer (the only layer of
        //objects included in the layerMask), set its parent object to _potentialCameraTarget
        if (Physics.Raycast(startPoint, direction, out hit, MainCameraUtilities.MainCamera.farClipPlane, LayerMask))
        {
            potentialCameraTarget = hit.collider.gameObject.transform.parent.gameObject;
            //Ignore celestial body colliders in spacecraft local view in VR
            //as it is too easy select by mistake
            if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
            {
                if (!potentialCameraTarget.CompareTag("Spacecraft")) 
                {
                    potentialCameraTarget = null;
                }
            }
        }
        else
        {
            potentialCameraTarget = null;
        }
    }

    /// <summary>
    /// User input action initiates making potentialCameraTarget the new camera target
    /// </summary>
    /// <param name="value">InputAction value</param>
    private void SelectNewCameraTarget(InputAction.CallbackContext value)
    {
        if ((!waitUntilCamTransitionComplete) && (!VizardGUISettings.GetVRMenuActive()))
        {
            if (potentialCameraTarget != null)
            {
                if (potentialCameraTarget.CompareTag("Spacecraft") || potentialCameraTarget.CompareTag("Planet") ||
                    potentialCameraTarget.CompareTag("Sun"))
                {
                    MainCameraViewMgr.SetupChangeOfMainCameraTarget(potentialCameraTarget);
                }

                potentialCameraTarget = null;
            }
        }
    }
#endif
    /// <summary>
    /// Move the camera rig to user selected orthographic view of target object
    /// </summary>
    /// <param name="desiredViewpoint">User selected viewpoint option</param>
    public void SetVRviewpoint(int desiredViewpoint)
    {
        if ((desiredViewpoint >= 0) && (desiredViewpoint < viewpoints.Count))
        {
            //Preset up and loot-at vectors for selected viewpoint
            Vector3 viewPointVector = viewpoints[desiredViewpoint][0];
            Vector3 viewPointUp = viewpoints[desiredViewpoint][1];

            //Calculate current distance to target
            Vector3 targetPosition = MainCameraUtilities.CameraFocusPosition;
            float distToTarget = (targetPosition - cameraRigTransform.position).magnitude;

            //Move camera rig to new viewpoint as same distance from target
            cameraRigTransform.transform.position = targetPosition + distToTarget * viewPointVector;
            cameraRigTransform.LookAt(targetPosition, viewPointUp);
            MainCameraViewMgr.SetOffset();

            //Display alert to user of new viewpoint
            SetViewpointModeText(desiredViewpoint);
        }
    }

    /// <summary>
    /// Set the status alert text box to show the newly selected viewpoint of main camera
    /// </summary>
    /// <param name="currentViewpoint">user selected viewpoint option</param>
    private void SetViewpointModeText(int currentViewpoint)
    {
        string displayText = "Unsupported View Index";
        switch (currentViewpoint)
        {
            case 0:
                displayText = "Front";
                break;
            case 1:
                displayText = "Right";
                break;
            case 2:
                displayText = "Top";
                break;
            case 3:
                displayText = "Rear";
                break;
            case 4:
                displayText = "Left";
                break;
            case 5:
                displayText = "Bottom";
                break;
            default:
                Debug.Log($"Request viewpoint index of {currentViewpoint} is not supported.");
                break;
        }

        //Display alert to user of new viewpoint
        SetStatusPanelText(displayText);
    }

    /// <summary>
    /// Setup status panel text for temporary display
    /// </summary>
    /// <param name="displayText"></param>
    public void SetStatusPanelText(string displayText)
    {
        StopCoroutine("ShowStatusPanel");
        temporaryStatusPanel.SetActive(true);
        temporaryStatusPanel.GetComponentInChildren<TextMeshProUGUI>().text = displayText;
        StartCoroutine("ShowStatusPanel");
    }

    /// <summary>
    /// Deactivate status panel after 2 second wait
    /// </summary>
    /// <returns></returns>
    private IEnumerator ShowStatusPanel()
    {
        yield return new WaitForSeconds(2.0f);
        temporaryStatusPanel.SetActive(false);
    }

    /// <summary>
    /// User input action toggles directional light flashlight and alerts user with status text
    /// </summary>
    /// <param name="value">InputAction value</param>
    private void ToggleFlashlightVR(InputAction.CallbackContext value)
    {
        if (!VizardGUISettings.GetVRMenuActive())
        {
            MainCameraUtilities.ToggleFlashlight();
            if (flashlight.activeInHierarchy)
            {
                SetStatusPanelText("Light On");
            }
            else
            {
                SetStatusPanelText("Light Off");
            }
        }
    }
}