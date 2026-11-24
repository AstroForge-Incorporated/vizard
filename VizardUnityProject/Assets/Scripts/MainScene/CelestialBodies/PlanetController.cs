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
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VizProtobufferMessage;

/// <summary>
/// Assembles celestial body with material and model for its assigned body
/// Updates the position, velocity, orientation and scaling its celestial body
/// </summary>
public class PlanetController : MonoBehaviour {
    public int planetIndex;
    public string parentBodyDictionaryKey;
    public int parentBodyIndex=-1;
    public string bodyDictionaryKey;
    public string modelKey;
    public float planetRadius; //meters
    public float ellipticity;// (1- r_p/r_eq)
    public float mu;
    public bool isMoon;
    public bool updateParentBody;
	
    public GameObject orbitLine;
    public GameObject planetModel;
    public GameObject clickableCollider;
    public GameObject coordinateAxes;
    public GameObject keepOutCollider;
    public GameObject planetSprite;
	
    public List<Vector3> hillFrameAxes = new List<Vector3>(); //Used by ellipsoids when present

    private GameObject nameLabel;
    private List<GameObject> allMyLabels = new List<GameObject>();
    private bool isVisible=true;
    private Camera cameraToUse;
#if !VIZARD_OPENXR
    private readonly int layerMask = ((1 << 7)| (1 << 9)|(1 << 11)); //7 = Unlit Spacecraft 9 = True Body Size Colliders, 11 = Spacecraft 
#endif

    public bool atmosphereMatAvailable;
    private bool usingCustomMaterial;

    private bool spriteWasOnLastFrame;

    private double ratioProjectionToTrueDistanceFromCam=1f;
    private float meshDimension;
	
    private VizProtobufferMessage.VizMessage.Types.CustomModel myModelSettings;

    private bool inNoDisplayMode;

    private AsyncOperationHandle<Material> matHandle;
    private AsyncOperationHandle<GameObject> modelHandle;
    private bool needRemoteModel;
    private bool inModelLoad;
    private string materialKey;
    private Material atmosphereMaterial;
    private bool usingAtmosphereMaterial;
    private bool waitingForAtmosphereMaterial;
	
    public void InitializeCelestialBody(int index, string bodyPropertiesKey, bool isCustom, bool inTestMode=false){
        planetIndex = index;
        VizMessage.Types.CelestialBody myMsg = MessageList.FirstMessage.CelestialBodies[planetIndex];

        inNoDisplayMode = DataManager.InNoDisplayMode;
        name = myMsg.BodyName;
		
        myModelSettings = new VizProtobufferMessage.VizMessage.Types.CustomModel{
            ModelPath = "HI_DEF_SPHERE", 
            SimBodiesToModify = {name}, 
            Offset = {0,0,0},
            Rotation = {0,0,0},
            Scale = {1,1,1},
        };
		
        bodyDictionaryKey = bodyPropertiesKey; //This dictionary at this point contains every single body included in the current run (whether built in or not)
        modelKey = myMsg.ModelDictionaryKey;

        if (!isCustom) //If someone accidentally populated the model key for an internally supported body, clear it
        {
            modelKey = "";
            UpdateBodyDictionaryValues(myMsg); 
            updateParentBody = false;
            parentBodyDictionaryKey = CelestialBodyStateUtilities.GetParentBodyDictionaryKey(bodyDictionaryKey);
            parentBodyIndex = CelestialBodyStateUtilities.FindSimulatedBodyWithCelestialBodyKey(parentBodyDictionaryKey);
        }

        planetRadius = CelestialBodyStateUtilities.GetCelestialBodyRadiusInMeters(name);
		
        ellipticity = CelestialBodyStateUtilities.GetCelestialBodyEllipticity(name);

        mu = (float) CelestialBodyStateUtilities.GetMu(name);

        if ((parentBodyIndex == -1)&&(CelestialBodyStateUtilities.SunMsgAvailable)){
            updateParentBody = true; //Couldn't find a match for the parent body string, so do the sphere of influence calculations
        }
		

        if ((parentBodyDictionaryKey!="sun")&&(!updateParentBody)){
            isMoon = true;
            CelestialBodyStateUtilities.MoonsList.Add(transform.gameObject);
        }else{
            transform.gameObject.tag = "Planet";
        }
		
        cameraToUse = Camera.main;
		
        // Rename the collider that allows the user to double-click and select planet
        clickableCollider.name = name + "ClickableCollider";
        clickableCollider.SetActive(true);
        // Rename the collider that keep out/in cones use to detect overlap
        keepOutCollider.name = name + "KeepOutCollider";

        if (!inTestMode)
        {
            CreateLabels();
        }

        CelestialBodyStateUtilities.CelestialBodiesList.Add(this.gameObject);

        if (modelKey == "")
        {
            ApplyDefaultMaterialAndModel();
        }
        else
        {
            ApplyUserSpecifiedModel(modelKey);
        }
    }

    private void UpdateBodyDictionaryValues(VizMessage.Types.CelestialBody cb)
    {
        if (cb.Mu <= 0)
        {
            return;
        }
        if (cb.RadiusEq <= 0)
        {
            return;
        }
        double e= 1 - cb.RadiusRatio;
        if (e >= 1)
        {
            return;
        }
			
        //Replace what's in the dictionary with the user provided values
        double[] newValues = new double[]
        {
            cb.RadiusEq, cb.Mu, CelestialBodyStateUtilities.GetAveDistanceToSun(bodyDictionaryKey), e
        };
			
        CelestialBodyStateUtilities.ReplaceValuesInCelestialBodyDictionary(bodyDictionaryKey, newValues);
    }

    void FixedUpdate(){
        if (GoodEnoughAddressables.AllRemoteCatalogsLoaded)
        {
            if ((needRemoteModel)&&(!inModelLoad))
            {
                ApplyUserSpecifiedModel(modelKey);
            }
        }

        if (!inNoDisplayMode){
            UpdateCelestialBody();
        }
    }

    public void UpdateCelestialBody()
    {
        double[] myPosition = {0, 0, 0};
        bool beyondSpriteThreshold = false;
        ratioProjectionToTrueDistanceFromCam = 1f;
        if ((MainCameraUtilities.CameraTargetName != name)||(!CelestialBodyStateUtilities.ViewIsSpacecraftLocal))
        {
            myPosition = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(planetIndex);
            if (CelestialBodyStateUtilities.ViewIsLocal)
            {
                double[] cameraTargetPosition = MainCameraUtilities.GetCameraTargetAbsolutePositionUnityCS();
                //Calculate relative position:
                myPosition = OrbitVectorMath.Subtract(myPosition, cameraTargetPosition); //meters, but in Unity frame

                if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    double[] mainCamPositionUnityUnits = OrbitVectorMath.ReturnDoubleArray(MainCameraUtilities.MainCamera.transform.position);
                    double[] camPositionMeters = MainCameraUtilities.GetAbsoluteMainCameraPositionInMeters();
                    double[] truePositionFromCameraMeters = OrbitVectorMath.Subtract(myPosition, camPositionMeters);
                    double trueDistanceFromCamMeters = OrbitVectorMath.Magnitude(truePositionFromCameraMeters);
                    double distanceToProjectionWallMeters =
                        MainCameraUtilities.DistanceToProjectionWallUnityUnits /
                        CelestialBodyStateUtilities.SpacecraftLocalViewScale;
                    double projectionWallStackingConstantMeters = MainCameraUtilities.ProjectionWallStackingConstantUnityUnits /
                                                                  CelestialBodyStateUtilities.SpacecraftLocalViewScale;
					
                    if (trueDistanceFromCamMeters > distanceToProjectionWallMeters)
                    {
                        if (trueDistanceFromCamMeters > MainCameraUtilities.TrueCameraDistanceToTargetMeters)
                        {
                            ratioProjectionToTrueDistanceFromCam =
                                (distanceToProjectionWallMeters + projectionWallStackingConstantMeters * System.Math.Log10(trueDistanceFromCamMeters-distanceToProjectionWallMeters)) / trueDistanceFromCamMeters;
                        }
                        else
                        {
                            ratioProjectionToTrueDistanceFromCam = distanceToProjectionWallMeters/ trueDistanceFromCamMeters;
                        }
                        myPosition = OrbitVectorMath.ScaleVector(truePositionFromCameraMeters,
                            ratioProjectionToTrueDistanceFromCam); //meters
                        myPosition = OrbitVectorMath.ScaleVector(myPosition,
                            CelestialBodyStateUtilities.SpacecraftLocalViewScale); //Unity Units
                        myPosition = OrbitVectorMath.Add(mainCamPositionUnityUnits,myPosition); //Unity units
                    }
                    else
                    {
                        myPosition = OrbitVectorMath.ScaleVector(myPosition,
                            (float) CelestialBodyStateUtilities.SpacecraftLocalViewScale); //Unity units
                    }
                }
                else
                {
                    myPosition = OrbitVectorMath.ScaleVector(myPosition,
                        1 / CelestialBodyStateUtilities.LocalPlanetViewScale);
                }
				
            }
            else
            {
                myPosition =
                    OrbitVectorMath.ScaleVector(myPosition, 1 / CelestialBodyStateUtilities.HelioCenteredViewScale);
                beyondSpriteThreshold = true;
            }
        }
        int bodyVisible = CheckForVisibleInCamera();
        if ((bodyVisible==1)|(bodyVisible==2)){
            if ((bodyVisible==1)&&(!isVisible)){
                isVisible = true;
                UpdateLabelVisibility();
            }else if((bodyVisible ==2)&&(isVisible)){
                isVisible = false;
                UpdateLabelVisibility();
            }
        }
        else
        {
            isVisible = false;
        }

        if (name == MainCameraUtilities.CameraTargetName){
            UpdateHillFrame();
        }

        //Update the position of the planet
        transform.position = OrbitVectorMath.ReturnVector3(myPosition);

        //Update the planet rotation
        transform.rotation = CelestialBodyStateUtilities.GetPlanetRotationUnityCS(planetIndex);

        float scaleToUse = GetDesiredScale(CelestialBodyStateUtilities.ViewIsLocal, CelestialBodyStateUtilities.ViewIsSpacecraftLocal, MainCameraUtilities.CameraTarget);

        bool turnSpriteOnThisFrame = ((VizardGUISettings.ShowSpritesForPlanets) && (beyondSpriteThreshold));

        if (turnSpriteOnThisFrame)
        {
            float spriteScale = VizardGUISettings.CalculateScaleForPlanetSprite(transform);
            planetSprite.transform.localScale = Vector3.one*(spriteScale/scaleToUse);
        }
        if (turnSpriteOnThisFrame != spriteWasOnLastFrame){
            BroadcastMessage("ConfigureHUDForSpriteMode", turnSpriteOnThisFrame, SendMessageOptions.DontRequireReceiver);
            planetSprite.SetActive(turnSpriteOnThisFrame);
            planetModel.SetActive(!turnSpriteOnThisFrame);
        }

        spriteWasOnLastFrame = turnSpriteOnThisFrame;
        SetScale (scaleToUse);
    }

    private float GetDesiredScale(bool viewIsLocal, bool viewIsSpacecraftLocal, GameObject cameraTarget){
        double desiredScale = planetRadius;
        if (!viewIsLocal) {
            desiredScale = CelestialBodyStateUtilities.DefaultHelioPlanetScale; 
        } else {
            if (viewIsSpacecraftLocal) {
                desiredScale *= CelestialBodyStateUtilities.SpacecraftLocalViewScale;
                desiredScale *= ratioProjectionToTrueDistanceFromCam;
            } else {
                desiredScale /= (float)CelestialBodyStateUtilities.LocalPlanetViewScale;
                desiredScale *= ratioProjectionToTrueDistanceFromCam;
                if (cameraTarget.name == name)
                {
                    desiredScale = planetRadius <100 ? planetRadius : 100f;
                }
            }
        }
        return (float) desiredScale;			
    }

    private void SetScale(float newRadius){
        transform.localScale = new Vector3 (newRadius, newRadius, newRadius);
        if ((atmosphereMatAvailable)&&(usingAtmosphereMaterial)){
            try
            {
                planetModel.GetComponent<MeshRenderer>().material.SetFloat("fInnerRadius", newRadius);
            }
            catch
            {
                Debug.Log(name + "'s material doesn't have a _fInnerRadius property");
                atmosphereMatAvailable = false;
            }
        }
    }

    private void CreateLabels(){
        //Body Name
        Vector2 screenOffset = new Vector2(5,15);
        if (isMoon){
            screenOffset = new Vector2(10,0);
        }
        nameLabel = LabelMaker.CreateLabel(name, "Label", transform.gameObject, screenOffset, "CelestialBodies");
        allMyLabels.Add(nameLabel);	
        nameLabel.SetActive(VizardGUISettings.ShowCelestialBodyLabels);
        
        //Coordinate System
        char prefix = '\u0070';
        string xLabel = $"{prefix.ToString()}{LabelMaker.Circumflex.ToString()}<sub>1</sub>";
        string yLabel = $"{prefix.ToString()}{LabelMaker.Circumflex.ToString()}<sub>2</sub>";
        string zLabel = $"{prefix.ToString()}{LabelMaker.Circumflex.ToString()}<sub>3</sub>";
        GameObject x = LabelMaker.CreateLabel(xLabel, name, coordinateAxes.transform.GetChild(0).gameObject, Vector2.zero, "CoordinateSystems");
        GameObject y = LabelMaker.CreateLabel(yLabel, name, coordinateAxes.transform.GetChild(1).gameObject, Vector2.zero, "CoordinateSystems");
        GameObject z = LabelMaker.CreateLabel(zLabel, name, coordinateAxes.transform.GetChild(2).gameObject, Vector2.zero, "CoordinateSystems");
        coordinateAxes.GetComponent<DrawAxes>().AttachCSLabels(x,y,z);
        allMyLabels.Add(x);
        allMyLabels.Add(y);
        allMyLabels.Add(z);
    }

    private void UpdateLabelVisibility(){
        foreach(GameObject label in allMyLabels){
            label.GetComponent<TextMeshProUGUI>().enabled = isVisible;
        }
    }

    private int CheckForVisibleInCamera(){
#if VIZARD_OPENXR
		Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cameraToUse);
		if (GeometryUtility.TestPlanesAABB(planes, clickableCollider.GetComponent<Collider>().bounds))
			return 1;
		else
		{
			return 2;
		}
#else

        Vector3 origin = cameraToUse.transform.position;
        Vector3 direction = transform.position - origin;
        int maxDistance = (int) direction.magnitude;

        if (Physics.Raycast(origin, direction, out var hit, maxDistance, layerMask)){
            if (transform.gameObject == hit.collider.gameObject.transform.parent.gameObject){
                //	Debug.LogFormat("I hit my gameObject {0}.", hit.collider.gameObject.transform.parent.gameObject.name);
                return 1;
            } else{
                //	Debug.LogFormat("I hit {0}", hit.collider.gameObject.transform.parent.gameObject.name);
                return 2;
            }
        }
        //Debug.Log("I didn't hit anything.");
        return 0;
#endif
    }
	

    public void SetDefaultModel(VizMessage.Types.CustomModel newSettings){
        myModelSettings = newSettings;
    }

    public VizProtobufferMessage.VizMessage.Types.CustomModel GetDefaultModel(){
        return myModelSettings;
    }

    private void UpdateHillFrame(){
        if (parentBodyIndex != -1)
        {
            double[] camTgtBodyPosition = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(planetIndex);
            double[] camTgtBodyVelocity = CelestialBodyStateUtilities.GetAbsPlanetVelocityUnityCS(planetIndex);

            double[] camTgtParentPosition =
                CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(parentBodyIndex);
            double[] camTgtParentVelocity = CelestialBodyStateUtilities.GetAbsPlanetVelocityUnityCS(parentBodyIndex);

            //Note that I am also putting these vectors back into BSK CS 
            double[] rvec = 
            {
                (-camTgtBodyPosition[2] + camTgtParentPosition[2]),
                (camTgtBodyPosition[0] - camTgtParentPosition[0]),
                (camTgtBodyPosition[1] - camTgtParentPosition[1])
            };

            double[] vvec = 
            {
                (-camTgtBodyVelocity[2] + camTgtParentVelocity[2]),
                (camTgtBodyVelocity[0] - camTgtParentVelocity[0]),
                (camTgtBodyVelocity[1] - camTgtParentVelocity[1])
            };

            double[] hillFrameTranspose =
                OrbitVectorMath.TransposeMatrix(OrbitVectorMath.CalculateHillFrame(rvec, vvec));

            Vector3 v1 = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.ApplyTransformationMatrixToVector(hillFrameTranspose, new double[] {1, 0, 0}));
            Vector3 v2 = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.ApplyTransformationMatrixToVector(hillFrameTranspose, new double[] {0, 1, 0}));
            Vector3 v3 = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.ApplyTransformationMatrixToVector(hillFrameTranspose, new double[] {0, 0, 1}));

            //Convert back to Unity CS from inertial
            v1 = new Vector3(v1.y, v1.z, -v1.x);
            v2 = new Vector3(v2.y, v2.z, -v2.x);
            v3 = new Vector3(v3.y, v3.z, -v3.x);

            hillFrameAxes = new List<Vector3> {v1, v2, v3};
        }
    }

    public void CalculateMeshDimension(){
        Vector3 size = (SpacecraftStateUtilities.CalculateModelBounds(planetModel)).size;
        meshDimension = Mathf.Max(new float[]{size.x,size.y,size.z});
        BroadcastMessage("ApplyMeshDimUpdate", meshDimension, SendMessageOptions.DontRequireReceiver);
    }

    public void EnableAtmosphereCalculations(bool isOn, bool calledExternally)
    {

            if (bodyDictionaryKey == "saturn")
            {
                planetModel.transform.GetComponentInChildren<RingsHelper>().UseHDRings(isOn);
            }

            if (VizardGUISettings.UseAtmosphereShaderIfAvailable)
            {
                if (calledExternally)
                {
                    usingAtmosphereMaterial = isOn;
                }

                if ((atmosphereMatAvailable) && (!usingCustomMaterial))
                {
                    if (isOn)
                    {
                        ApplyLoadedAtmosphereMaterial();
                    }
                    else
                    {
                        //Instantiate the default shader for the planet (used when not in local view or when in solar system view)
                        planetModel.GetComponent<MeshRenderer>().material =
                            ((Material) Resources.Load("Materials/CelestialBodies/Mesh_" + bodyDictionaryKey +
                                                       "Material"));
                        planetModel.GetComponent<AtmosphereShaderHelper>().ToggleHDAtmosphere(false);
                        planetModel.GetComponent<AtmosphereShaderHelper>().PlanetMaterial =
                            planetModel.GetComponent<Renderer>().material;
                    }
                }
            }
    }

    private void ApplyLoadedAtmosphereMaterial()
    {
        planetModel.GetComponent<MeshRenderer>().material = atmosphereMaterial;
        planetModel.GetComponent<AtmosphereShaderHelper>().InitHDMaterial(planetModel.GetComponent<Renderer>().material);
    }
    private void ApplyUserSpecifiedModel(string modelKeyToApply)
    {
        needRemoteModel = true;
        //Try to load from remote addressables
        if (GoodEnoughAddressables.AllRemoteCatalogsLoaded)
        {
            inModelLoad = true;
            if (CelestialBodyStateUtilities.RemoteModelKeyValid(modelKeyToApply))
            {
                VizardGUISettings.AddRemoteAssetLoadToList(modelKeyToApply);
                modelHandle = Addressables.LoadAssetAsync<GameObject>(modelKeyToApply);
                modelHandle.Completed += ModelHandleLoaded;
            }
            else
            {
                string errMsg = $"Celestial body model key: {modelKeyToApply} not found in Addressables bundles.";
                VizardGUISettings.UpdateErrorMessages(errMsg);
            }
        }
    }

    private void ChangeLayerOfAllChildren(Transform child, int layerNumber)
    {
        child.gameObject.layer = layerNumber;
        foreach (Transform grandchild in child)
        {
            ChangeLayerOfAllChildren(grandchild, layerNumber);
        }
    }
    private void ModelHandleLoaded(AsyncOperationHandle<GameObject> operation)
    {
        if (operation.Status == AsyncOperationStatus.Succeeded)
        {
            needRemoteModel = false;
            VizardGUISettings.PopRemoteAssetLoadFromList(modelKey, true);
            GameObject newModel = Instantiate(operation.Result);
            ChangeLayerOfAllChildren(newModel.transform, 8);
            if (!newModel.GetComponent<ModelBounds>())
            {
                ModelBounds newModelBounds = newModel.AddComponent<ModelBounds>();
                newModelBounds.SetupUnitBoundsForModel(newModel);
                newModelBounds.SetupModelBoundsWithModel(false, newModel);
            }

            FinalizeAppliedModel(newModel, false);
        }
        else
        {
            needRemoteModel = false;
            VizardGUISettings.PopRemoteAssetLoadFromList(bodyDictionaryKey, false);
            Debug.Log($"Model for {name} failed to load.");
            GameObject newModel = Instantiate(Resources.Load("Models/BasicPlanetModel")as GameObject);
            FinalizeAppliedModel(newModel, true);
        }

        inModelLoad = false;
    }
    public void FinalizeAppliedModel(GameObject newModel, bool isSphere)
    {
        waitingForAtmosphereMaterial = false;
        atmosphereMatAvailable = atmosphereMatAvailable && newModel.GetComponent<AtmosphereShaderHelper>() != null;
        GameObject oldModel = planetModel;
        Quaternion newModelRotation = newModel.transform.localRotation;
        Vector3 newModelLocalPosition = newModel.transform.localPosition;
        if (!newModel.GetComponent<ModelBounds>())
        {
            Debug.Log("I am in the FinalizeAppliedModel adding ModelBounds.");
            //Set up ModelBounds
            newModel.AddComponent<ModelBounds>();
            newModel.GetComponent<ModelBounds>().SetupUnitBoundsForModel(newModel);
        }

        Vector3 localScaleToSave = newModel.transform.localScale;
        newModel.GetComponent<ModelBounds>().useBoxCollider = !isSphere;
        planetRadius = CelestialBodyStateUtilities.GetCelestialBodyRadiusInMeters(bodyDictionaryKey);
        if (planetRadius == 0)
        {
            if (isSphere)
            {
                planetRadius = 1; //Set radius to 1m so that something will be visible.
            }
            else
            {
                planetRadius =
                    newModel.GetComponent<ModelBounds>().unitModelMaxExtent * 1000f; //*newModel.transform.localScale.x; //Assumes the model is in km
                //If the model was imported with non-identity scaling, now that the desired max dimension has been attained,
                // need to calculate the bounds when scale is the identity matrix
                newModel.transform.localRotation = Quaternion.Euler(0,0,0);
                newModel.transform.localPosition = Vector3.zero;
                newModel.transform.localScale = Vector3.one;
                newModel.GetComponent<ModelBounds>().SetupUnitBoundsForModel(newModel); 
            }

            localScaleToSave /= (planetRadius / 1000);
        }
        else //Use the set req value to control the size. Make the model a unit model.
        {
            localScaleToSave = Vector3.one;
			
            //If the model was imported with non-identity scaling, now that the desired max dimension has been attained,
            // need to calculate the bounds when scale is the identity matrix
            newModel.transform.localRotation = Quaternion.Euler(0,0,0);
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localScale = Vector3.one;
            newModel.GetComponent<ModelBounds>().SetupUnitBoundsForModel(newModel);
            localScaleToSave /= newModel.GetComponent<ModelBounds>().unitModelMaxExtent;
        }
        newModel.transform.SetParent(transform);
        newModel.transform.SetSiblingIndex(0);
        planetModel = newModel;
        planetModel.transform.localPosition = newModelLocalPosition;
        if (isSphere)
        {
            planetModel.transform.localScale = new Vector3(1f, 1f - ellipticity, 1f);
            planetModel.transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
        else
        {

            planetModel.transform.localScale = localScaleToSave;
            planetModel.transform.localRotation = newModelRotation;
        }

        Destroy(oldModel);
        planetModel.name = name + "Mesh";
        //Add the rings to Saturn
        if (bodyDictionaryKey == "saturn"){
            GameObject rings = Instantiate (Resources.Load ("Prefabs/Rings") as GameObject, planetModel.transform, true);
            rings.transform.localScale = Vector3.one*0.017f;
            rings.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
        }
    }

    public void ApplyDefaultMaterialAndModel()
    {
        if (updateParentBody)
        {
            if (modelKey != "")
            {
                ApplyUserSpecifiedModel(modelKey);
            }
            else
            {
                //Apply the default planet sphere for now (may be replaced by an obj model after custom settings are applied)
                needRemoteModel = false;
                GameObject newModel =
                    Instantiate(Resources.Load("Models/BasicPlanetModel") as GameObject);
                FinalizeAppliedModel(newModel, true);
            }
        }
        else
        {
            if (bodyDictionaryKey == "phobos")
            {
                modelKey = "Phobos";
            }
            else if (bodyDictionaryKey == "deimos")
            {
                modelKey = "Deimos";
            }

            if (modelKey != "")
            {
                needRemoteModel = true;
                ApplyUserSpecifiedModel(modelKey);
            }
            else
            {
                needRemoteModel = false;
                GameObject newModel =
                    Instantiate(Resources.Load("Models/BasicPlanetModel") as GameObject);
                FinalizeAppliedModel(newModel, true);

                string matToLoad = "Mesh_" + bodyDictionaryKey + "Material";

                Material myMaterial = CelestialBodyStateUtilities.AtmosphereMaterialAvailable(bodyDictionaryKey);
                if (!TryApplyAtmosphereMaterial(myMaterial))
                {
                    waitingForAtmosphereMaterial = myMaterial == null;
                    planetModel.GetComponent<Renderer>().material = ((Material)
                        Resources.Load("Materials/CelestialBodies/" + matToLoad));
                }
            }
        }
    }

    public void ApplyLateAtmosphereMaterial(Material material)
    {
        if (!waitingForAtmosphereMaterial)
        {
            return;
        }
        waitingForAtmosphereMaterial = false;
        if (TryApplyAtmosphereMaterial(material) && planetModel.GetComponent<AtmosphereShaderHelper>().HDatmosphere)
        {
            Debug.Log($"Applied {material.name} to {name} in local view.");
        }
    }

    private bool TryApplyAtmosphereMaterial(Material newMaterial)
    {
        if (newMaterial == null || !VizardGUISettings.UseAtmosphereShaderIfAvailable)
        {
            return false;
        }
        if (ellipticity > 0.01f)
        {
            VizardGUISettings.UpdateErrorMessages($"Ellipticity of {name} set to >0.01. Atmosphere shader disabled.", true);
            return false;
        }
        atmosphereMatAvailable = true;
        VizardGUISettings.PopRemoteAssetLoadFromList(materialKey, true);
        atmosphereMaterial = newMaterial;
        planetModel.GetComponent<MeshRenderer>().material = atmosphereMaterial;
        AddAtmosphereShaderHelper();
        return true;
    }

    public void ApplyDefaultMaterial()
    {
		
    }

	
    private void AddAtmosphereShaderHelper()
    {
        if (!planetModel.GetComponent<AtmosphereShaderHelper>())
        {
            AtmosphereShaderHelper atmosphereHelper = planetModel.AddComponent<AtmosphereShaderHelper>();
            atmosphereHelper.SetAtmosphereSettings(
                CelestialBodyStateUtilities.BodySpecificAtmosphereSettings[bodyDictionaryKey]);
            atmosphereHelper.PlanetMaterial = planetModel.GetComponent<Renderer>().material;
            atmosphereHelper.SetPlanetValues(bodyDictionaryKey);
        }
        bool enableAtmosphere = CelestialBodyStateUtilities.ViewIsLocal && GetAtmosphereEnabled();
        EnableAtmosphereCalculations(enableAtmosphere,false);
    }

    private bool GetAtmosphereEnabled()
    {
        GameObject cameraTarget = MainCameraUtilities.CameraTarget;
        if (cameraTarget != null)
        {

            if (cameraTarget.name == name)
            {
                return true;
            }

            if (cameraTarget.CompareTag("Spacecraft") &&
                (cameraTarget.GetComponent<SpacecraftController>().spacecraftParentBodyIndex ==
                 planetIndex))
            {
                return true;
            }

            if (cameraTarget.CompareTag("Planet") &&
                (cameraTarget.GetComponent<PlanetController>().parentBodyIndex == planetIndex))
            {
                return true;
            }

            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (matHandle.IsValid())
        {
            Addressables.Release(matHandle);
        }
        if (modelHandle.IsValid())
        {
            Addressables.Release(modelHandle);
        }
    }

    public double GetRatioProjectionToTrueDistanceFromCam()
    {
        return ratioProjectionToTrueDistanceFromCam;
    }

    public void ChangeEllipticityForTesting(double newEllipticity)
    {
        ellipticity = (float) newEllipticity;
        planetModel.transform.localScale = new Vector3(1f, 1f - ellipticity, 1f);
        Debug.Log($"I updated the ellipticity to {ellipticity}");
    }
}