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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VizProtobufferMessage;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using GameObject = UnityEngine.GameObject;
/// <summary>
/// Static class providing methods and object references for celestial bodies instantiated for the current scenario.
/// </summary>
public static class CelestialBodyStateUtilities{
	
    public static bool ViewIsLocal;
    public static bool ViewIsSpacecraftLocal;

    public static bool SunMsgAvailable;
    public static int SunIndex;
    public static Transform SunTransform;

    public static double HelioCenteredViewScale = 1E9; //viz scale at solar system wide view-> 1 scene unit: 1E9 meters
    public static double LocalPlanetViewScale = 1E5;//viz scale when a planet is targeted and zoomed to, --> 1 scene unit: 1E5 meters
    public static double SpacecraftLocalViewScale = 1.0f; //viz scale for a close-up spacecraft view , --> 1 scene unit: 1 meter
	
    public static float DefaultHelioPlanetScale = 10.0f; //Fake scale for the planets at solar system wide view
	
    private static float spacecraftLocalCelestialBodyOrbitLineWidth = 1f;
    private static float planetViewCelestialBodyOrbitLineWidth = 10f;
    private static float helioViewCelestialBodyOrbitLineWidth = 1f;
		
    public static List<GameObject> CelestialBodiesList = new List<GameObject>();
    public static Dictionary<string,DrawLocationMarker> LocationsDictionary = new Dictionary<string, DrawLocationMarker>();
    public static List<GameObject> MoonsList = new List<GameObject>();
    public static List<GameObject> CelestialBodyOrbitLines = new List<GameObject>();

    public static double[,] CenterOfMassPositions;
    public static double[,] CenterOfMassDCMs;

    //public static bool AllRemoteCatalogsLoaded = false;
	
    public static Material DefaultModelImportMtl;

    private static Dictionary<string, double[]> celestialBodyDictionary = new Dictionary<string, double[]>()
    {
        //for each celestial body name key, the array provides:
        // [0] body equatorial radius (km), 
        // [1]	mu (km^3/s^2), 
        // [2] average distance from sun (km), and [3] ellipticity (1-r_p/r_eq) (km)
        // [3] ellipticity (from each body's respective nssdc.gsfc.nasa.gov/planetary/factsheet/ )
        // Equatorial radius and mu  from Basilisk astroConstants.h on 10/6/2017
        // with exception of Phobos and Deimos (values from NASA Mars fact sheet)
        //								r_eq					mu				ave Dist				ellipticity		
        {"sun", new[] {695000.0, 132712440023.310, 0.0, 0.00005}},
        {"mercury", new[] {2439.7, 22032.080, 5.790918E7, 0.0000}},
        {"venus", new[] {6051.8, 324858.599, 1.082089E8, 0.0000}},
        {"earth", new[] {6378.1366, 398600.436, 1.49597870E8, 0.0033528}},
        {"moon", new[] {1737.4, 4902.799, 1.49597870E8, 0.0012}}, //https://www.nasa.gov/mission_pages/mer/images/pia09170.html  
        {"mars", new[] {3396.19, 42828.314, 2.279366E8, 0.005886}},
        {"jupiter", new[] {71492.0, 126712767.881, 7.78412E8, 0.06487}},
        {"saturn", new[] {60268.0, 37940626.068, 1.426726E9, 0.09796}},
        {"uranus", new[] {25559.0, 5794559.128, 2.870972E9, 0.02293}},
        {"neptune", new[] {24746.0, 6836534.065, 4.498253E9, 0.01708}},
        {"pluto", new[] {1137.0, 983.055, 5.906376E9, 0.0000}},
        //Note that we are using NASA 3D models for Phobos and Deimos that imported as meters instead of km. 
        //The models are not unit spheres. Use the x radius of the body to drive the scaling of the body. 
        //Phobos has dimensions of 27kmx22kmx18km
        //Deimos has dimensions of 15kmx12kmx11km
        {"phobos", new[] {13.51876, 0.0007073, 2.279366E8, 0.0000}},
        // True ellipticity of Phobos is 0.2017544, but we are using a Phobos-specific mesh that already reflects its odd shape. 
        //Note also that mu was calculated from Mars fact sheet available data for Phobos mass
        {"deimos", new[] {7.506228, 0.0001601, 2.279366E8, 0.0000}},
        //True ellipticity of Deimos is 0.15, but we are using a Deimos-specific mesh that already reflects its odd shape. 
        //Note also that mu was calculated from Mars fact sheet available data for Deimos mass
    };

    public static Dictionary<int, string> IndexToBodyDictionary = new Dictionary<int, string>();

    public static Dictionary<string, string> bodySpecificModels = new Dictionary<string,string>(){
        //Provide paths instantiate body-specific models (like those with tuned atmospheres or non-spherical bodies)
        //REMEMBER TO SET THE UNIT MODEL EXTENTS IN MODEL BOUNDS SCRIPT ON ANY NEW MODEL IMPORTS TO ENSURE CORRECT SCALING!
        {"phobos", "Phobos"},
        {"deimos", "Deimos"},	
    };

    public static readonly Dictionary<string, float[]> BodySpecificAtmosphereSettings = new Dictionary<string, float[]>(){
        //for each body with an atmosphere shader material, the following custom settings must be applied 
//										[0] HDR					 [1-4] Atmosphere					 [5] Atmosphere 		     [6] Outer 		[7] Cloud 
//											Exposure					Base Color							   Strength					Scale Ratio		Speed
        //										----------			--------------------			---------				---------	--------  		// [7] average speed of clouds[km/hr] (used in atmosphere shader calculations)
        {"venus", new[] 		{1f, 					189f, 176f, 152f, 255f, 				15f,								1.015f,			240f}}, //https://www.discovermagazine.com/the-sciences/why-are-venus-clouds-so-weird "Vega I and II revealed just how fast Venus’ clouds travel, measuring speeds in excess of 150 mph"
        {"earth", new[] 		{1f, 					77f, 153f, 184f, 255f,					15f,								1.015f, 			120f}}, //https://eartheclipse.com/geography/how-fast-do-clouds-move.html   "Typically, clouds can move 30-120 miles per hour. It depends on the situation and the type of cloud that determines the speed. For instance, high cirrus clouds can travel at a speed of more than 100 mph during the jet stream. Clouds during the thunderstorm can travel at speed up to 30 to 40 mph."
        {"mars", new[] 		{1f, 					113f, 94f, 27f, 255f,					10f,								1.015f, 			10f}}, //http://cab.inta-csic.es/rems/en/mars-atmosphere/  "The Viking and Pathfinder observations showed that the mean wind speed on Mars is fairly weak: 1 – 4 m/s (about 4 – 15 km/h). However, under some extraordinary conditions – such as in the event of global or local dust storms – winds are expected to blow at speeds over 30 m/s or even more (> 110 km/h )."
        {"jupiter", new[] 	{1f, 					189f, 176f, 152f, 255f, 				15f,								1.015f,			240f}},
        {"saturn",new[]		{1f, 					189f, 176f, 152f, 255f,				15f,								1.015f,			240f}},
        {"uranus", new[]	{1f, 					189f, 176f, 152f, 255f, 				15f,								1.015f,			240f}},
        {"neptune", new[]	{1f, 					189f, 176f, 152f, 255f, 				15f,								1.015f,			240f}},
    };

    public static string GetParentBodyDictionaryKey(string dictionaryKey)
    {
        string parentBodyDictionaryKey = "sun";
        if (dictionaryKey == "phobos")
        {
            parentBodyDictionaryKey = "mars";
        }
        else if (dictionaryKey == "deimos")
        {
            parentBodyDictionaryKey = "mars";
        }
        else if (dictionaryKey == "moon")
        {
            parentBodyDictionaryKey = "earth";
        }

        return parentBodyDictionaryKey;
    }
	
    public static string FindCelestialBodyInDictionary(string nameToMatch)
    {
        string lowerCaseNameToMatch = nameToMatch.ToLower();
        foreach (string keyValue in celestialBodyDictionary.Keys){
            if (lowerCaseNameToMatch.Contains(keyValue)){
                return keyValue;
            }
        }
        return "";
    }
    
    private static string[] planetsUsingAtmosphereShader = {"earth", "venus", "mars"};

    public static void LoadAllAtmosphereMaterials()
    {

        if (GoodEnoughAddressables.AllRemoteCatalogsLoaded)
        {
            foreach (string bodyKey in planetsUsingAtmosphereShader)
            {
                string materialKey = "Mesh_" + bodyKey + "Material_HD";
                if (RemoteModelKeyValid(materialKey))
                {
                    AsyncOperationHandle<Material> matHandle = Addressables.LoadAssetAsync<Material>(materialKey);
                    matHandle.Completed += AtmosphereMatHandleLoaded;
                }
                else
                {
                    string errMsg = $"Celestial body material key: {materialKey} not found in Addressables bundles.";
                    VizardGUISettings.UpdateErrorMessages(errMsg);
                }
            }
        }
    }

    private static void AtmosphereMatHandleLoaded(AsyncOperationHandle<Material> operation)
    {
        if (operation.Status == AsyncOperationStatus.Succeeded)
        {
            Material newMaterial = GameObject.Instantiate(operation.Result);
            foreach (string planetName in planetsUsingAtmosphereShader)
            {
                if (newMaterial.name.Contains(planetName))
                {
                    AtmosphereMaterials[planetName] = newMaterial;
                    Debug.Log(newMaterial.name);
                    foreach (GameObject body in CelestialBodiesList)
                    {
                        if (body != null && body.TryGetComponent<PlanetController>(out var planet) &&
                            planet.bodyDictionaryKey == planetName)
                        {
                            planet.ApplyLateAtmosphereMaterial(newMaterial);
                        }
                    }
                    return;
                }
            }
        }
        else
        {
            Debug.Log("Loading material failed.");
        }
    }
    public static Material AtmosphereMaterialAvailable(string bodyKey)
    {
        if (AtmosphereMaterials.ContainsKey(bodyKey))
        {
            return AtmosphereMaterials[bodyKey];
        }
        return null;
    }


    private static Dictionary<string, Material> AtmosphereMaterials = new Dictionary<string, Material>();
    public static int FindSimulatedBodyWithCelestialBodyKey(string key)
    {
        int index = 0;
        foreach (VizMessage.Types.CelestialBody cb in MessageList.FirstMessage.CelestialBodies)
        {
            string lowerCaseNameToMatch = (cb.BodyName).ToLower();
            if (lowerCaseNameToMatch.Contains(key))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
	
    public static double[] GetAbsolutePlanetPositionUnityCS(int planetIndex)
    {
        VizMessage.Types.CelestialBody planet = MessageList.CurrentMessage.CelestialBodies[planetIndex];
        if (planet == null)
        {
            return new double[]{0,0,0};
        }

        return OrbitVectorMath.TransformFromBSKCStoUnity(new[]{planet.Position[0], planet.Position[1], planet.Position[2]});
    }
	
    public static double[] GetAbsPlanetVelocityUnityCS(int planetIndex)
    {
        VizMessage.Types.CelestialBody planet = MessageList.CurrentMessage.CelestialBodies[planetIndex];
        if (planet == null)
        {
            return new double[]{0,0,0};
        }

        // The Basilisk coordinate frame is right-handed with z up. Unity uses a left-handed coordinate frame with y up.
        // To change to right handed with y up, Basilisk velocity <v0,v1,v2> becomes the intermediate  right-handed frame <v1 ,v2, v0>
        // To change that intermediate frame to a left-handed frame with y up, x right,z into screen: 
        // the z component must be made negative leaving us with: <v1, v2, -v0>
        return new [] {planet.Velocity[1], planet.Velocity[2], -planet.Velocity[0]};
    }

    public static double[,] GetPlanetRotationDCM_BSK(int planetIndex)
    {
        VizMessage.Types.CelestialBody planet = MessageList.CurrentMessage.CelestialBodies[planetIndex];
        double[,] spiceDCM = new double[3,3];
        if (planet == null)
        {
            return spiceDCM;
        }

        for(int i=0; i<3; i++)
        {
            for(int j=0; j<3; j++)
            {
                spiceDCM[i,j] = planet.Rotation[i*3+j];
            }
        }

        return spiceDCM;
    }
	
    public static Quaternion GetPlanetRotationUnityCS(int planetIndex)
    {
        double[,] spiceDCM = GetPlanetRotationDCM_BSK(planetIndex);
        //Send it off to be converted to a left-handed quaternion
        return OrbitVectorMath.ConvertRightHandedDCMtoLeftHandedQuaternion(spiceDCM);
    }

    public static double GetCurrentScale(){
        if (ViewIsLocal) {
            if (ViewIsSpacecraftLocal) {
                return SpacecraftLocalViewScale;
            }
            return LocalPlanetViewScale;
        } 
        return HelioCenteredViewScale;
    }


    public static float GetCelestialBodyRadiusInMeters(string bodyName){
        if (celestialBodyDictionary.TryGetValue(bodyName, out var value))
        {
            return (float) value[0]*1000;
        }
        //check for lowercase
        if (celestialBodyDictionary.ContainsKey(bodyName.ToLower()))
        {
            return (float) celestialBodyDictionary[bodyName.ToLower()][0]*1000;
        }
        // look for a planet in the dictionary that is a substring of the bodyName 
        foreach (string keyValue in celestialBodyDictionary.Keys)
        {
            string searchString = bodyName.ToLower();
            if (searchString.Contains(keyValue))
            {
                if (bodyName.IndexOf(keyValue) >= 0)
                {
                    return 1000.0f * (float) celestialBodyDictionary[keyValue][0]; //Return the requested diameter in meters from the dictionary
                }
            }
        }

        Debug.LogFormat($"{bodyName} was not found in CelestialBodyDictionary and default radius value of 6000 km was returned.");
        return 6000000f;
    }

    public static double GetMu(string bodyName){
        if (celestialBodyDictionary.TryGetValue(bodyName, out var value))
        {
            return (float) value[1];
        }
        if (celestialBodyDictionary.ContainsKey(bodyName.ToLower()))
        {
            return (float) celestialBodyDictionary[bodyName.ToLower()][1];
        }
        foreach (string keyValue in celestialBodyDictionary.Keys)
        {
            string searchString = bodyName.ToLower();
            if (searchString.Contains(keyValue))
            {
                if (bodyName.IndexOf(keyValue) >= 0)
                {
                    return celestialBodyDictionary[keyValue][1];
                }
            }
        }
        return 0;
    }

    public static double GetAveDistanceToSun(string bodyName){
        if (celestialBodyDictionary.TryGetValue(bodyName, out var value))
        {
            return (float) value[2];
        }
        if (celestialBodyDictionary.ContainsKey(bodyName.ToLower()))
        {
            return (float) celestialBodyDictionary[bodyName.ToLower()][2];
        }

        foreach (string keyValue in celestialBodyDictionary.Keys)
        {
            string searchString = bodyName.ToLower();
            if (searchString.Contains(keyValue))
            {
                if (bodyName.IndexOf(keyValue) >= 0)
                {
                    return celestialBodyDictionary[keyValue][2];
                }
            }
        }

        Debug.LogFormat($"{bodyName} was not found in CelestialBodyDictionary and average distance to Sun of 0.0 km was returned.");
        return 0.0;
    }

    public static float GetCelestialBodyEllipticity(string bodyName){
        if (celestialBodyDictionary.TryGetValue(bodyName, out var value))
        {
            return (float) value[3];
        }
        if (celestialBodyDictionary.ContainsKey(bodyName.ToLower()))
        {
            return (float) celestialBodyDictionary[bodyName.ToLower()][3];
        }
        foreach (string keyValue in celestialBodyDictionary.Keys)
        {
            string searchString = bodyName.ToLower();
            if (searchString.Contains(keyValue))
            {
                if (bodyName.IndexOf(keyValue) >= 0)
                {
                    return (float) celestialBodyDictionary[keyValue][3];
                }
            }
        }
        
        Debug.LogFormat($"{bodyName} was not found in CelestialBodyDictionary and default ellipticity of 1.0 was returned.");
        return 1.0f;
    }

    public static void AddToCelestialBodyDictionary(string name, double[] constants){
        celestialBodyDictionary[name] =constants;
    }

    public static void ReplaceValuesInCelestialBodyDictionary(string key, double[] constants){
        celestialBodyDictionary[key] = constants;
    }

    public static int GetCelestialBodyIndex(string bodyName)
    {
        for(int i=0; i< CelestialBodiesList.Count;i++)
        {
            if (CelestialBodiesList[i].name == bodyName) {
                return i;
            }
        }
        Debug.LogFormat ("Celestial body gameObject of name {0} was not found", bodyName);
        return -1;
    }
	
    public static GameObject GetCelestialBodyObject(string bodyName){
        foreach (GameObject body in CelestialBodiesList) {
            if (body.name == bodyName) {
                return body;
            }
        }
        Debug.LogFormat ("Celestial body gameObject of name {0} was not found", bodyName);
        return null;
    }

    public static GameObject GetCelestialBodyObject(int index)
    {
        return CelestialBodiesList[index];
    }

    public static void CalculateSpacecraftLocalViewScale()
    {
        float minDim = 1f;
        foreach (GameObject sc in SpacecraftStateUtilities.SpacecraftList)
        {
            float minDimToCompare = sc.GetComponent<SpacecraftController>().minDimension;
            if (minDimToCompare < minDim)
            {
                minDim = minDimToCompare;
            }
        }
        SpacecraftLocalViewScale = 1f;
        if (minDim < 1f)
        {
            SpacecraftLocalViewScale /= (minDim/2f);
            foreach (GameObject sc in SpacecraftStateUtilities.SpacecraftList)
            {
                sc.transform.localScale = Vector3.one * (float) SpacecraftLocalViewScale;
            }
            MainCameraUtilities.SpacecraftLocalViewScaleChanged = true;
            MainCameraUtilities.DistanceToProjectionWallUnityUnits = 1000 * SpacecraftLocalViewScale;
        }
    }
	
    public static void CalculateLocalPlanetViewScale(GameObject targetObject){
        float largestDimInUnits=1;
        GameObject bodyToCheck = targetObject;
        if (targetObject.CompareTag("Spacecraft")){
            bodyToCheck = GetCelestialBodyObject(targetObject.GetComponent<SpacecraftController>().spacecraftParentBodyIndex);
        }
        if (bodyToCheck.CompareTag("Sun")){
            largestDimInUnits = 1E5f;
            //BUT, what about if we are very close to a small body and we don't want it to disappear by zooming out so far
            if (targetObject.CompareTag("Spacecraft")&& (targetObject.GetComponent<SpacecraftController>().spacecraftNearbySmallBodyIndex!=-1)){
                bodyToCheck = GetCelestialBodyObject(targetObject.GetComponent<SpacecraftController>().spacecraftNearbySmallBodyIndex);
            }
        }
        if (bodyToCheck.CompareTag("Planet")){
            float bodyRadius = bodyToCheck.GetComponent<PlanetController>().planetRadius;
            largestDimInUnits=bodyRadius;
            if (bodyRadius >=100){
                largestDimInUnits/=100;
            }
        }
        LocalPlanetViewScale = largestDimInUnits;
    }
    public static GameObject GetGameObjectWithBodyName(string nameToMatch, string parentName=""){
        foreach (GameObject sc in SpacecraftStateUtilities.SpacecraftList) {
            if (sc.name == nameToMatch) {
                if (parentName == "")
                {
                    return sc;
                }

                if (sc.GetComponent<SpacecraftController>().parentSpacecraftName == parentName) //Check to see if this is the correct effector
                {
                    return sc;
                }
            }
        }
        foreach (GameObject body in CelestialBodiesList) {
            if (body.name == nameToMatch) {
                return body;
            }
        }
        return null;
    }

    private static GameObject GetLocationObjectWithName(string nameToMatch)
    {
        if (LocationsDictionary.TryGetValue(nameToMatch, out var value))
        {
            return value.gameObject;
        }

        return null;
    }

    public static GameObject GetLineTargetGameObjectWithName(string nameToMatch, string effectorParentName=""){
        GameObject target = GetGameObjectWithBodyName(nameToMatch, effectorParentName);
        if (target ==null){
            target = GetLocationObjectWithName(nameToMatch);
        }
        return target;
    }

    public static bool RemoteModelKeyValid(string modelKey)
    {
        foreach (var l in Addressables.ResourceLocators) {
            IList<IResourceLocation> locs;
            if (l.Locate(modelKey, null, out locs))
                return true;
        }
        return false;
    }
	
    public static void UpdatePlanetCSVisibility (){
        foreach (GameObject p in CelestialBodiesList) {
            if (p.CompareTag("Sun"))
            {
                p.GetComponent<SunBuilder>().sunCoordinateAxes.SetActive(p.name == MainCameraUtilities.CameraTargetName
                    ? VizardGUISettings.CameraTargetCSOn
                    : VizardGUISettings.AllPlanetCSOn);
            }else if (p.CompareTag("OriginTarget"))
            {
                p.transform.GetChild(2).gameObject.SetActive(MainCameraUtilities.CameraTarget.CompareTag("OriginTarget")
                    ? VizardGUISettings.CameraTargetCSOn
                    : VizardGUISettings.AllPlanetCSOn);
            }
            else
            {
                p.GetComponent<PlanetController>().coordinateAxes.SetActive(
                    p.name == MainCameraUtilities.CameraTargetName
                        ? VizardGUISettings.CameraTargetCSOn
                        : VizardGUISettings.AllPlanetCSOn);
            }
        }
    }
	
    /// <summary>
    /// Enable HD atmosphere on planet that is the camera target
    /// -OR- is the parent body of the camera target
    /// </summary>
    /// <param name="newTarget">Scenario object that is being set as the new main camera target</param>
    /// <param name="oldTarget">Scenario object that was the previous main camera target</param>
    /// <param name="newTargetIsSC">True if the new main camera target is a spacecraft or effector</param>
    public static void AdjustAtmosphereSettingsForNewCameraTarget(GameObject newTarget, GameObject oldTarget, bool newTargetIsSC)
    {
        GameObject planetCameraTarget = newTarget;
        if (newTarget.CompareTag("Spacecraft"))
        {
            planetCameraTarget= CelestialBodiesList[
                newTarget.GetComponent<SpacecraftController>().spacecraftParentBodyIndex];
        }
       
        foreach (GameObject planet in CelestialBodiesList)
        {
            if (!planet.CompareTag("Sun")){
                planet.GetComponent<PlanetController>().EnableAtmosphereCalculations(String.Equals(planetCameraTarget.name,planet.name), true);
            }
        }
    }

    public static void CalculateRotatingFramePositionAndVelocityHistories()
    {
        double[,] body1Positions = MessageList.GetPositionHistoryBSK(false, VizardGUISettings.RotatingFrameBody1Index);
        double[,] body2Positions = MessageList.GetPositionHistoryBSK(false, VizardGUISettings.RotatingFrameBody2Index);
        double[,] body1Velocities = MessageList.GetVelocityHistoryBSK(false, VizardGUISettings.RotatingFrameBody1Index);
        double[,] body2Velocities = MessageList.GetVelocityHistoryBSK(false, VizardGUISettings.RotatingFrameBody2Index);
		
        CenterOfMassPositions = new double[body1Positions.GetLength(0),3];
        CenterOfMassDCMs = new double[body1Positions.GetLength(0),9];
		
        double body1Mu = GetMu(IndexToBodyDictionary[VizardGUISettings.RotatingFrameBody1Index]);
        double body2Mu = GetMu(IndexToBodyDictionary[VizardGUISettings.RotatingFrameBody2Index]);

        double[] body1Pos = {0 ,0, 0};
        double[] body2Pos = {0, 0, 0};
        double[] body1Vel = {0, 0, 0};
        double[] body2Vel = {0, 0, 0};

        double[] COMposition;
        double[] COMvelocity;
        double[] COM_DCM;
        for (int i = 0; i < body1Positions.GetLength(0); i++)
        {
            for (int j = 0; j < 3; j++)
            {
                body1Pos[j] = body1Positions[i, j];
                body2Pos[j] = body2Positions[i, j];
                body1Vel[j] = body1Velocities[i, j];
                body2Vel[j] = body2Velocities[i, j];
            }

            COMposition = OrbitVectorMath.CalculateCenterOfMass(body1Pos, body2Pos, body1Mu, body2Mu);
            COMvelocity = OrbitVectorMath.CalculateCenterOfMass(body1Vel, body2Vel, body1Mu, body2Mu);

            COM_DCM = OrbitVectorMath.CalculateRotatingFrame(COMposition, COMvelocity);
            for (int j = 0; j < 3; j++)
            {
                CenterOfMassPositions[i, j] = COMposition[j];
            }
            for (int j = 0; j < 9; j++)
            {
                CenterOfMassDCMs[i, j] = COM_DCM[j];
            }
        }
    }
	
    public static void AppendCOMData(int currentMsgCount) //This only works because we don't buffer when running live
    {
        int oldArrayLength = CenterOfMassDCMs.GetLength(0);
        if (oldArrayLength < currentMsgCount)
        {
            double[,] newCenterOfMassPos = new double[currentMsgCount, 3];
            double[,] newCenterOfMassDCMs = new double[currentMsgCount, 9];

            for (int i = 0; i < oldArrayLength; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    newCenterOfMassPos[i, j] = CenterOfMassPositions[i, j];
                }

                for (int j = 0; j < 9; j++)
                {
                    newCenterOfMassDCMs[i, j] = CenterOfMassDCMs[i, j];
                }
            }

            double body1Mu = GetMu(IndexToBodyDictionary[VizardGUISettings.RotatingFrameBody1Index]);
            double body2Mu = GetMu(IndexToBodyDictionary[VizardGUISettings.RotatingFrameBody2Index]);
            for (int k = oldArrayLength; k < currentMsgCount; k++)
            {
                VizMessage message = MessageList.GetMessageAtIndex(k);
                double[] body1Position = message.CelestialBodies[VizardGUISettings.RotatingFrameBody1Index].Position
                    .ToArray();

                double[] body2Position =
                    message.CelestialBodies[VizardGUISettings.RotatingFrameBody2Index].Position.ToArray();

                double[] body1Velocity = message.CelestialBodies[VizardGUISettings.RotatingFrameBody1Index].Velocity
                    .ToArray();
                double[] body2Velocity = message.CelestialBodies[VizardGUISettings.RotatingFrameBody2Index].Velocity
                    .ToArray();
				
                double[] COMposition =
                    OrbitVectorMath.CalculateCenterOfMass(body1Position, body2Position, body1Mu, body2Mu);
                double[] COMvelocity =
                    OrbitVectorMath.CalculateCenterOfMass(body1Velocity, body2Velocity, body1Mu, body2Mu);
                double[] DCM = OrbitVectorMath.CalculateRotatingFrame(COMposition, COMvelocity);

                for (int j = 0; j < 3; j++)
                {
                    newCenterOfMassPos[k, j] = COMposition[j];
                }

                for (int j = 0; j < 9; j++)
                {
                    newCenterOfMassDCMs[k, j] = DCM[j];
                }
            }

            CenterOfMassPositions = newCenterOfMassPos;
            CenterOfMassDCMs = newCenterOfMassDCMs;
        }
    }
	
    public static void UpdateCelestialBodyOrbitLineWidth()
    {
        float newWidth = GetCurrentCelestialBodyOrbitLineWidth();

        foreach (GameObject line in CelestialBodyOrbitLines)
        {
            line.GetComponent<OsculatingOrbitLinePlotter>().UpdateLineRendererLineThickness(newWidth);
        }
    }

    public static float GetCurrentCelestialBodyOrbitLineWidth()
    {
        float newWidth = GetCurrentCelestialBodyOrbitLineConstant();
        newWidth *= (float) PersistentUserSettings.persistentSettingsFromLastSave.CelestialBodyOrbitLineWidth;
        return newWidth;
    }

    private static float GetCurrentCelestialBodyOrbitLineConstant()
    {
        float constant;
        if (ViewIsLocal)
        {
            constant = ViewIsSpacecraftLocal? spacecraftLocalCelestialBodyOrbitLineWidth: planetViewCelestialBodyOrbitLineWidth;
        }
        else
        {
            constant = helioViewCelestialBodyOrbitLineWidth;
        }

        return constant;
    }


    public static void ResetCelestialBodyStateUtilities()
    {
        ViewIsLocal = false;
        ViewIsSpacecraftLocal = false;
        SunMsgAvailable = false;
        SunIndex = -1;
        HelioCenteredViewScale = 1E9; 
        LocalPlanetViewScale = 1E5; 
        SpacecraftLocalViewScale = 1.0f; 
        DefaultHelioPlanetScale = 10.0f; 
        CelestialBodiesList = new List<GameObject>();
        CelestialBodyOrbitLines = new List<GameObject>();
        LocationsDictionary = new Dictionary<string, DrawLocationMarker>();
        MoonsList = new List<GameObject>();
        CenterOfMassPositions = new double[,]{};
        CenterOfMassDCMs = new double[,]{};
        IndexToBodyDictionary = new Dictionary<int, string>();
        celestialBodyDictionary = new Dictionary<string, double[]>()
        {
            //for each celestial body name key, the array provides:
            // [0] body equatorial radius (km), 
            // [1]	mu (km^3/s^2), 
            // [2] average distance from sun (km), and [3] ellipticity (1-r_p/r_eq) (km)
            // [3] ellipticity (from each body's respective nssdc.gsfc.nasa.gov/planetary/factsheet/ )
            // Equatorial radius and mu  from Basilisk astroConstants.h on 10/6/2017
            // with exception of Phobos and Deimos (values from NASA Mars fact sheet)
            //								r_eq					mu				ave Dist				ellipticity		
            {"sun", new[] {695000.0, 132712440023.310, 0.0, 0.00005}},
            {"mercury", new[] {2439.7, 22032.080, 5.790918E7, 0.0000}},
            {"venus", new[] {6051.8, 324858.599, 1.082089E8, 0.0000}},
            {"earth", new[] {6378.1366, 398600.436, 1.49597870E8, 0.0033528}},
            {"moon", new[] {1737.4, 4902.799, 1.49597870E8, 0.0012}}, //https://www.nasa.gov/mission_pages/mer/images/pia09170.html  
            {"mars", new[] {3396.19, 42828.314, 2.279366E8, 0.005886}},
            {"jupiter", new[] {71492.0, 126712767.881, 7.78412E8, 0.06487}},
            {"saturn", new[] {60268.0, 37940626.068, 1.426726E9, 0.09796}},
            {"uranus", new[] {25559.0, 5794559.128, 2.870972E9, 0.02293}},
            {"neptune", new[] {24746.0, 6836534.065, 4.498253E9, 0.01708}},
            {"pluto", new[] {1137.0, 983.055, 5.906376E9, 0.0000}},
            //Note that we are using NASA 3D models for Phobos and Deimos that imported as meters instead of km. 
            //The models are not unit spheres. Use the x radius of the body to drive the scaling of the body. 
            //Phobos has dimensions of 27kmx22kmx18km
            //Deimos has dimensions of 15kmx12kmx11km
            {"phobos", new[] {13.51876, 0.0007073, 2.279366E8, 0.0000}},
            // True ellipticity of Phobos is 0.2017544, but we are using a Phobos-specific mesh that already reflects its odd shape. 
            //Note also that mu was calculated from Mars fact sheet available data for Phobos mass
            {"deimos", new[] {7.506228, 0.0001601, 2.279366E8, 0.0000}},
            //True ellipticity of Deimos is 0.15, but we are using a Deimos-specific mesh that already reflects its odd shape. 
            //Note also that mu was calculated from Mars fact sheet available data for Deimos mass
        };
    }
}