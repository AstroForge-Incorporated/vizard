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
using UnityEngine.EventSystems;

/// <summary>
/// Handles user input to pan, zoom or roll the main camera rig or to double-click a scenario object to change camera target
/// </summary>
public class MainCameraMovementController : MonoBehaviour
{
    [Header("CameraRig")] public Transform cameraRigTransform; //Transform of main camera rig
    public MainCameraViewManager MainCameraViewMgr; //Manages changing the current camera target or view scale

    //View Transition flag
    [HideInInspector]
    public bool waitUntilCamTransitionComplete; //True while main camera is transitioning between view scales

    //Device input variables
    protected float TimeDownMark = 300000000f; //System time of left mouse button being depressed
    protected bool LastFrameLeftMouseButtonDown; //True if the left mouse button was down in the last update frame
    private float lastClickTime; //System time of last mouse click (used to prevent unintended clicks)
    protected System.DateTime LastFrameTime; //System time at the end of last Update call

    private readonly float
        doubleClickCatchTime = 0.5f; //Time window for subsequent mouse click to be counted as a double click
    private float scrollUserInput; // Current mouse input "scrollwheel" value
    private Vector2 dragUserInput; // Current mouse input for drag

    //Raycast to check for camera change target
    private Ray ray; //Raycast ray used to check for possible camera target change

    private readonly int
        layerMask = (1 <<
                     1); // Layer to check for ray collision with collider: 1 is TransparentFX which is assigned to all ClickableColliders

    [Header("Flashlight")] [Tooltip("Light attached to main camera that increases illumination of camera target")]
    public GameObject
        flashlight; //Directional light attached to camera rig that is used as a flashlight and can be toggled by user to increase lighting on camera target

    /// <summary>
    /// Monodevelop method called before any Start calls
    /// </summary>
    void Awake()
    {
        MainCameraUtilities.MainCamera = GetComponent<Camera>();
        MainCameraViewMgr = GetComponent<MainCameraViewManager>();
    }

    /// <summary>
    /// Monodevelop method called after other Update methods in scene have finished at every frame
    /// <remarks>Camera movement controls work best when handled in LateUpdate</remarks>
    /// </summary>
    void LateUpdate()
    {
        System.DateTime currentFrameTime = System.DateTime.Now;
        //Prevent user input while transitioning to a different view scale
        if (!waitUntilCamTransitionComplete)
        {
            // Check for mouse (touchpad) down and cursor is NOT over GUI element
            if (Input.GetMouseButton(0) && (!EventSystem.current.IsPointerOverGameObject()))
            {
                // If mouse down last frame, enable Drag Panning if time out is met
                if (LastFrameLeftMouseButtonDown)
                {
                    if ((Time.time - TimeDownMark) > 0.2f)
                    {
                        // Pan about target after waiting long enough to make sure it's not a double click
                        DragToPanAboutTarget(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    }
                }
                else //If mouse (touchpad) not down last frame, check to see if double click
                {
                    TimeDownMark = Time.time; //record the time of mouse (touchpad) down
                    if (DoubleClick())
                    {
                        //See if the previous click was close enough to count as double click
                        CheckForChangeTarget();
                    }
                }

                LastFrameLeftMouseButtonDown = true;
            }
            else
            {
                LastFrameLeftMouseButtonDown = false;
            }

            // Apply any keyboard control of camera movement
            float elapsedSeconds = (float) (currentFrameTime - LastFrameTime).TotalSeconds;
            ApplyActiveCameraAutoMovement(elapsedSeconds);

            //Check for scroll after confirming cursor is not over GUI element
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                ScrollToZoomTarget(Input.GetAxis("Mouse ScrollWheel"));
            }
        }

        LastFrameTime = currentFrameTime;
    }


    /// <summary>
    /// Pan camera rig around camera target as directed by user mouse (touchpad) drag input
    /// </summary>
    protected void DragToPanAboutTarget(float inputXaxis, float inputYaxis)
    {
        float deltaYaw = inputXaxis* MainCameraUtilities.UserInputRotateSpeed;
        float deltaPitch = -inputYaxis * MainCameraUtilities.UserInputRotateSpeed;
        if ((deltaYaw != 0) || (deltaPitch != 0))
        {
            Vector3 pivot = MainCameraUtilities.CameraOrbitPivot;
            cameraRigTransform.RotateAround(pivot, cameraRigTransform.up, deltaYaw);
            cameraRigTransform.RotateAround(pivot, cameraRigTransform.right, deltaPitch);
            MainCameraViewMgr.SetOffset();
            MainCameraViewMgr.SetUp();
        }
    }

    /// <summary>
    /// Check for user double click event
    /// </summary>
    /// <returns></returns>
    protected bool DoubleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if ((Time.time - lastClickTime) < doubleClickCatchTime)
            {
                lastClickTime = Time.time;
                return true;
            }

            lastClickTime = Time.time;
            return false;
        }

        return false;
    }

    /// <summary>
    /// Raycast from camera through user selected point and check for a possible camera target object
    /// <remarks>Each spacecraft and celestial body object has a "clickable" collider in the
    /// TransparentFX layer, if the ray intersects one of these collider, the main camera target is changed.</remarks>
    /// </summary>
    protected void CheckForChangeTarget()
    {
        //Cast a ray into the screen at the current cursor position 
        ray = MainCameraUtilities.MainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        //If the ray hits a clickable collider of the TransparentFX layer (the only layer of
        //objects included in the layerMask), check if change of main camera target
        //should be initiated
        if ((Physics.Raycast(ray, out hit, MainCameraUtilities.MainCamera.farClipPlane, layerMask)))
        {
            //Set the target object to the top level parent instead of clickable collider child
            GameObject newTarget = hit.collider.gameObject.transform.parent.gameObject;

            // If the raycast hit a target that is already set as the main camera target
            // halve the distance between the main camera and the target
            if (newTarget == MainCameraUtilities.CameraTarget)
            {
                if (MainCameraUtilities.CameraTargetIsSpacecraftOrEffector &&
                    CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    HalveMainCameraDistanceToTarget(true);
                    return;
                }

                if (!MainCameraUtilities.CameraTargetIsSpacecraftOrEffector &&
                    !CelestialBodyStateUtilities.ViewIsSpacecraftLocal && CelestialBodyStateUtilities.ViewIsLocal)
                {
                    HalveMainCameraDistanceToTarget(false);
                    return;
                }
            }

            //If selected object is different from current target or the view could be changed to a closer view scale
            //trigger change camera target
            MainCameraViewMgr.SetupChangeOfMainCameraTarget(newTarget);
        }
    }

    /// <summary>
    /// Halves the distance of the main camera from the target if the distance is greater than a minimum offset distance
    /// </summary>
    /// <param name="targetIsSpacecraft">True if the camera target is a spacecraft or effector</param>
    private void HalveMainCameraDistanceToTarget(bool targetIsSpacecraft)
    {
        float minDistance; //Minimum offset distance of camera from target
        if (targetIsSpacecraft)
        {
            float spacecraftBounds =
                MainCameraUtilities.CameraTarget.GetComponent<SpacecraftController>().meshDimension;
            minDistance = 2f * spacecraftBounds * (float) CelestialBodyStateUtilities.SpacecraftLocalViewScale;
        }
        else
        {
            minDistance = 2f * MainCameraUtilities.CameraTarget.transform.localScale.magnitude;
        }

        Vector3 toFocus = MainCameraUtilities.CameraOrbitPivot - cameraRigTransform.position;
        if (toFocus.magnitude > minDistance)
        {
            if (targetIsSpacecraft)
            {
                CalculateCameraPositionForSpacecraftLocal(
                    (float)(-0.5 * MainCameraUtilities.TrueCameraDistanceToTargetMeters), toFocus);
            }
            else
            {
                cameraRigTransform.position /= 2f;
            }
            MainCameraViewMgr.SetOffset();
        }
    }

    /// <summary>
    /// Zoom camera rig in or out as directed by user mouse (touchpad) scroll event
    /// </summary>
    protected void ScrollToZoomTarget(float scrollUserInput)
    {
        //Main camera is zooming in on camera target
        float zoomMode = 0f; //Zoom is off
        if ((scrollUserInput > 0) || (MainCameraUtilities.KeyZoom > 0))
        {
            zoomMode = -1f; //Zoom in
        }
        //Main camera is zooming out from camera target
        else if ((scrollUserInput < 0) || (MainCameraUtilities.KeyZoom < 0))
        {
            zoomMode = 1f; //Zoom out
        }

        if (zoomMode != 0) //zooming 
        {
            Vector3 camVectorToTargetUnityUnits =
                MainCameraUtilities.CameraFocusPosition - cameraRigTransform.position;

            //Check for camera position being within the current view's boundary, otherwise trigger transition to new view
            if (MainCameraViewMgr.CameraWithinCurrentViewBoundary(camVectorToTargetUnityUnits
                    .magnitude)) //If true, no transition required, zoom allowed
            {
                float desiredZoomDistanceChange = CelestialBodyStateUtilities.ViewIsSpacecraftLocal
                    ? (float) MainCameraUtilities.TrueCameraDistanceToTargetMeters
                    : 0.7f * camVectorToTargetUnityUnits.magnitude;

                float zoomSourceMultiplier = (scrollUserInput != 0)
                    ? MainCameraUtilities.UserInputZoomFactor
                    : Mathf.Abs(MainCameraUtilities.KeyZoom * MainCameraUtilities.KeyZoomRate *
                                MainCameraUtilities.KeyZoomMultiplier);

                desiredZoomDistanceChange *= zoomSourceMultiplier;
                
                //If the view is spacecraft local, the distance between the camera
                //and the target will not be a linearly scaled number of Unity Units/metere
                //if the camera is positioned outside of the local area of the target
                if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    CalculateCameraPositionForSpacecraftLocal(zoomMode * desiredZoomDistanceChange,
                        camVectorToTargetUnityUnits);
                }
                else //Planet Local and Helio Scales - no projection wall needed to stay within floating point distances
                {
                    cameraRigTransform.position -= zoomMode * desiredZoomDistanceChange *
                                                   camVectorToTargetUnityUnits.normalized;
                    SetTrueCameraDistanceForPlanetOrHelioViews();
                }

                //Adjust clipping planes to keep scenario objects in camera frustum
                MainCameraViewMgr.AdjustClippingPlanes(true);
            }

            //Update the offset to maintain camera following
            MainCameraViewMgr.SetOffset();
        }
    }

    /// <summary>
    /// Update the range distance from the main camera to the camera target for planet or helio views
    /// </summary>
    protected void SetTrueCameraDistanceForPlanetOrHelioViews()
    {
        double currentScale = CelestialBodyStateUtilities.GetCurrentScale();
        float cameraDistance = (MainCameraUtilities.CameraTarget.transform.position - cameraRigTransform.position)
            .magnitude;
        MainCameraUtilities.TrueCameraDistanceToTargetMeters = currentScale * cameraDistance;
    }

    /// <summary>
    /// Calculate the camera position in Unity Units from the target for spacecraft local view
    /// <remarks>In spacecraft local view, scenario objects distant from the camera are projected onto
    /// a projection wall. If the distance to the camera target is greater than or equal to the
    /// projection wall distance, the camera is positioned at the wall,  and the true meters distance to
    /// between target is calculated and used to reduce the scale of the camera target</remarks>
    /// </summary>
    /// <param name="desiredZoomDistanceChange">Distance to move the camera in this frame update</param>
    /// <param name="camVectorToTargetUnityUnits">Unity CS vector of camera to camera target in Unity Units</param>
    protected void CalculateCameraPositionForSpacecraftLocal(float desiredZoomDistanceChange
        , Vector3 camVectorToTargetUnityUnits)
    {
        //Calculate the true distance of the camera to the target in meters
        double newDistanceToCameraMeters =
            MainCameraUtilities.TrueCameraDistanceToTargetMeters + desiredZoomDistanceChange;

        //Set the distance in meters of the camera from the camera target
        MainCameraUtilities.TrueCameraDistanceToTargetMeters=newDistanceToCameraMeters;

        //Calculate the distance in Unity Units at which to place the camera for the true distance
        double distanceToPlaceCameraUnityUnits = newDistanceToCameraMeters *
                                                 CelestialBodyStateUtilities.SpacecraftLocalViewScale;

        // Beyond the projection wall, Vizard scales the scene instead of moving the camera farther.
        float distance = (float)System.Math.Min(distanceToPlaceCameraUnityUnits,
            MainCameraUtilities.DistanceToProjectionWallUnityUnits);
        cameraRigTransform.position = MainCameraUtilities.CameraFocusPosition - camVectorToTargetUnityUnits.normalized * distance;
    }

    /// <summary>
    /// Move the camera rig per the current keyboard controlled camera movement settings
    /// </summary>
    /// <param name="elapsedSeconds">Seconds elapsed since last Update completed</param>
    protected void ApplyActiveCameraAutoMovement(float elapsedSeconds)
    {
        bool changeOffset = false;
        //Apply horizontal panning
        if (MainCameraUtilities.KeyHorizPan != 0)
        {
            transform.RotateAround(MainCameraUtilities.CameraOrbitPivot, transform.up,
                MainCameraUtilities.KeyHorizPan * MainCameraUtilities.KeyPanRate * elapsedSeconds);
            changeOffset = true;
        }

        //Apply vertical panning
        if (MainCameraUtilities.KeyVertPan != 0)
        {
            transform.RotateAround(MainCameraUtilities.CameraOrbitPivot, transform.right,
                MainCameraUtilities.KeyVertPan * MainCameraUtilities.KeyPanRate * elapsedSeconds);
            changeOffset = true;
        }

        //Apply camera rig roll
        if (MainCameraUtilities.KeyRoll != 0)
        {
            transform.RotateAround(MainCameraUtilities.CameraOrbitPivot, transform.forward,
                MainCameraUtilities.KeyRoll * MainCameraUtilities.KeyPanRate * elapsedSeconds);
            MainCameraViewMgr.SetUp();
            changeOffset = true;
        }

        //If auto movement of camera occurred, update the camera offset setting
        if (changeOffset)
        {
            MainCameraViewMgr.SetOffset();
        }
    }
}