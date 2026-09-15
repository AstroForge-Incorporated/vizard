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
using UnityEngine;
using UnityEngine.Assertions;

public class TestTruePathTrajectory : MonoBehaviour
{
    // Start is called before the first frame update
    private TruePathOrbitLine testLine1;
    private TruePathOrbitLine testLine2;
    
    private GameObject spacecraftObject1;
    private GameObject spacecraftObject2;

    private GameObject earth;
    private GameObject moon;
    public string RunTruePathTrajectoryTestSuite()
    {
        MainCameraUtilities.CameraInUnitTestMode = true;
        earth = Instantiate(Resources.Load("Prefabs/CelestialBodyTemplate") as GameObject);
        earth.GetComponent<PlanetController>().InitializeCelestialBody(0,"earth", false, true);
        moon = Instantiate(Resources.Load("Prefabs/CelestialBodyTemplate") as GameObject);
        moon.GetComponent<PlanetController>().InitializeCelestialBody(1,"moon", false, true);
        spacecraftObject1 = Instantiate (Resources.Load ("Prefabs/basiliskSpacecraftTemplate") as GameObject) as GameObject;
        spacecraftObject1.GetComponent<SpacecraftController>().InitializeSpacecraft(0, true);
        spacecraftObject2 = Instantiate (Resources.Load ("Prefabs/basiliskSpacecraftTemplate") as GameObject) as GameObject;
        spacecraftObject2.GetComponent<SpacecraftController>().InitializeSpacecraft(1, true);
        SpacecraftStateUtilities.SpacecraftList = new List<GameObject>();
        SpacecraftStateUtilities.SpacecraftList.Add(spacecraftObject1);
        SpacecraftStateUtilities.SpacecraftList.Add(spacecraftObject2);
        CelestialBodyStateUtilities.IndexToBodyDictionary[0] = "Earth";
        CelestialBodyStateUtilities.IndexToBodyDictionary[1] = "Moon";
        
        testLine1 = this.gameObject.AddComponent<TruePathOrbitLine>();
        testLine1.truePathLinePlotter= testLine1.gameObject.AddComponent<TruePathLinePlotter>();
        testLine1.InitializeTruePathLine(spacecraftObject1, 0, true);
        
        testLine2 = this.gameObject.AddComponent<TruePathOrbitLine>();
        testLine2.truePathLinePlotter=testLine2.gameObject.AddComponent<TruePathLinePlotter>();
        testLine2.InitializeTruePathLine(spacecraftObject2, 1, true);
        
        // Hidden trajectories must not scan the recording when the camera changes chief.
        VizardGUISettings.TruePathLineMode = 2;
        VizardGUISettings.SpacecraftRelativeOrbitMode = 1;
        VizardGUISettings.TruePathLinesVisible = false;
        VizardGUISettings.RelativeTruePathChangeCount++;
        var previousPositions = SpacecraftStateUtilities.ChiefPositions;
        var previousRotations = SpacecraftStateUtilities.ChiefDCMs;
        SpacecraftStateUtilities.UpdateChiefSpacecraft(1);
        Assert.AreEqual(1, VizardGUISettings.ChiefSpacecraftIndex);
        Assert.AreEqual(previousPositions, SpacecraftStateUtilities.ChiefPositions);
        Assert.AreEqual(previousRotations, SpacecraftStateUtilities.ChiefDCMs);

        //True Path Mode 1 (inertial, camera-target relative only)
        Test_TruePathMode1();
        
        //True Path Mode 2 (spacecraft relative)
        Test_TruePathMode2();
        
        //True Path Mode 3 (celestial body relative)
        Test_TruePathMode3();
        
        //True Path Mode 4 (rotating frame)
        Test_TruePathMode4();
        
        //True Path Mode 5 (fixed frame)
        Test_TruePathMode5();
         Destroy(testLine1);
         Destroy(testLine2);
         Destroy(spacecraftObject1);
         Destroy(spacecraftObject2);
         Destroy(earth);
         Destroy(moon);
        MainCameraUtilities.CameraInUnitTestMode = false;
        return "\n\t All TruePathTrajectory.cs tests passed.";
    }

    private void Test_TruePathMode1()
    {
        int startIndex = 0;
        int sampleSize = 10;
        MainCameraUtilities.CameraTarget = spacecraftObject1;
        MainCameraUtilities.CameraTargetIndex = 0;
        MainCameraUtilities.CameraTargetIsSpacecraftOrEffector = true;

        VizardGUISettings.TruePathLineMode = 1;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        testLine1.CallTruePathUpdate();
        
        List<double[]> resultPositionHistory = testLine1.Sample_mySpacecraftPositionHistoryBSK(startIndex, sampleSize);

        for (int i = 0; i < sampleSize; i++)
        {
            double[] expectedPosition = MessageList.GetMessageAtIndex(i+startIndex).Spacecraft[0].Position.ToArray();
            Assert.AreEqual(expectedPosition[0], resultPositionHistory[i][0]);
            Assert.AreEqual(expectedPosition[1], resultPositionHistory[i][1]);
            Assert.AreEqual(expectedPosition[2], resultPositionHistory[i][2]);
        }
        //Check spacecraftLocalView
        Vector3 expected = new Vector3(0.3193563f, 0, -0.2527806f);
        List<Vector3> result = testLine1.Sample_pointsToDrawOnscreen(99, 1);
        float compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check local view
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false;
        expected = new Vector3(0.3520018f, 0, 0.06392068f);
        result = testLine1.Sample_pointsToDrawOnscreen(167, 1);
        compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check solar system view
        CelestialBodyStateUtilities.ViewIsLocal = false;
        expected = new Vector3(-0.2661532f, 0, -0.2781071f);
        result = testLine1.Sample_pointsToDrawOnscreen(563, 1);
        compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
    }
    private void Test_TruePathMode2()
    {
        Test_TruePathMode2_SCRelMode1_Hill();
        Test_TruePathMode2_SCRelMode2_Velocity();
        Test_TruePathMode2_SCRelMode3_Inertial();
    }
    private void Test_TruePathMode2_SCRelMode1_Hill()
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false; //spacecraft local view requires a main camera position
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.TruePathLineMode = 2;
        VizardGUISettings.SpacecraftRelativeOrbitMode = 1;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        SpacecraftStateUtilities.UpdateChiefSpacecraft(1);
        Assert.AreEqual(1, VizardGUISettings.ChiefSpacecraftIndex);
        
        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();
        
        Assert.AreEqual(VizardGUISettings.TruePathLineMode,testLine1.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine1.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine1.GetIndexOfRelativeSpacecraft());
        // Test line should set relative body index to the parent body,
        // VizardGUISettings should be -1 to indicate each line should use its spacecraft's parent body
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine1.GetIndexOfRelativeBody());
        Assert.AreEqual(-1, VizardGUISettings.RelativeBodyIndex);
        Assert.AreEqual(1, testLine1.GetIndexOfRelativeBody()); 

        //Sample rotation frame DCM
        double[] expected = new double[]
        {
            -0.839172915364253, 0.543864705711867, 7.46880440030615E-08, 
            0.543864705711868, 0.839172915364256, -9.11783349279405E-09, 
            -6.76350514581911E-08, 3.29687521579662E-08, -0.999999999999997
        };
        double[] result = testLine1.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result);
        double[] result2 = testLine2.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result2);
        //string outString="{";
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            //outString+=$"{result[i]},";
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare); 
        }
        
        //Sample rotating frame positions
        expected = new double[] {328364972.723979,235619259.822744,3};
        result = testLine1.Sample_rotatingFramePositions(700);
        result2 = testLine2.Sample_rotatingFramePositions(700);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample relativeToRotatingFrameBodyPositions_BSK
        expected = new double[] {-0.108003984384132,-11.1798182128502,2.99999997783004};
        double[] expected2 = new double[3]; //all-zero because it is the chief sc
        result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result2);

        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected2[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample points to plot onscreen
        Vector3 expected_v = new Vector3(-1.774578E-05f,-2.340374E-13f,-1.105896E-05f);
        List<Vector3> result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1);
        //PrintVector3(result_v[0]);
        float compare_v = (expected_v - result_v[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare_v); 
        
        //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
        Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
        Assert.AreEqual(0,testLine2.GetCount_pointsToDrawOnscreen());
        
        //Test spacecraft local///////////////////////////////////////
        MainCameraUtilities.UnitTestCameraPosition = new double[] {350, 0, 350};
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true;
        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();

        //Sample rotation frame DCM (should be unchanged from local view)
        expected = new double[]
        {-0.839172915364253,0.543864705711867,7.46880440030615E-08,
            0.543864705711868,0.839172915364256,-9.11783349279405E-09,
            -6.76350514581911E-08,3.29687521579662E-08,-0.999999999999997
        };
        result = testLine1.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result2);
        //string outString="{";
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            //outString+=$"{result[i]},";
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare); 
        }
        
        //Sample rotating frame positions (should be unchanged from local view)
        expected = new double[] {328364972.723979,235619259.822744,3};
        result = testLine1.Sample_rotatingFramePositions(700);
        result2 = testLine2.Sample_rotatingFramePositions(700);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample relativeToRotatingFrameBodyPositions_BSK (should be unchanged from local view)
        expected = new double[] {-0.108003984384132,-11.1798182128502,2.99999997783004};
        expected2 = new double[3]; //all-zero because it is the chief sc
        result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result2);

        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected2[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample points to plot onscreen 
        expected_v = new Vector3(-1.774578f,-2.340374E-08f,-1.105896f);
        result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1);
        //PrintVector3(result_v[0]);
        compare_v = (expected_v - result_v[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare_v); 
        
        //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
        Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
        Assert.AreEqual(0,testLine2.GetCount_pointsToDrawOnscreen());
    }
    private void Test_TruePathMode2_SCRelMode2_Velocity()
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false; //spacecraft local view requires a main camera position
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.TruePathLineMode = 2;
        VizardGUISettings.SpacecraftRelativeOrbitMode = 2;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        SpacecraftStateUtilities.UpdateChiefSpacecraft(1);
        Assert.AreEqual(1, VizardGUISettings.ChiefSpacecraftIndex);

        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();

        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine1.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine1.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine1.GetIndexOfRelativeSpacecraft());
        // Test line should set relative body index to the parent body,
        // VizardGUISettings should be -1 to indicate each line should use its spacecraft's parent body
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine1.GetIndexOfRelativeBody());
        Assert.AreEqual(-1, VizardGUISettings.RelativeBodyIndex);
        Assert.AreEqual(1, testLine1.GetIndexOfRelativeBody()); 

        //Sample rotation frame DCM
        double[] expected = new double[]
        {-0.898893864524172,0.438166429933644,7.5242533214968E-08,
            0.438166429933645,0.898893864524174,0,
            -6.76350514581911E-08,3.29687521579662E-08,-0.999999999999997};
        double[] result = testLine1.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result);
        double[] result2 = testLine2.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result2);
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare); 
        }

        //Sample rotating frame positions
        expected = new double[] {328364972.723979, 235619259.822744, 3};
        result = testLine1.Sample_rotatingFramePositions(700);
        result2 = testLine2.Sample_rotatingFramePositions(700);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }

        //Sample relativeToRotatingFrameBodyPositions_BSK
        expected = new double[] {-0.291508294896689,-11.1765389565387,2.99999997783004};
        double[] expected2 = new double[3]; //all-zero because it is the chief sc
        result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected2[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample points to plot onscreen
        Vector3 expected_v = new Vector3(-1.312929E-05f,2.532594E-14f,-7.724908E-06f);
        List<Vector3> result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1);
        //PrintVector3(result_v[0]);
        float compare_v = (expected_v - result_v[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare_v); 
        
        //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
        Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
        Assert.AreEqual(0,testLine2.GetCount_pointsToDrawOnscreen());
        
        //Test spacecraft local///////////////////////////////////////
        MainCameraUtilities.UnitTestCameraPosition = new double[] {350, 0, 350};
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true;
        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();

        //Sample rotation frame DCM (should be unchanged from local view)
        expected = new double[]
        {
            -0.898893864524172, 0.438166429933644, 7.5242533214968E-08,
            0.438166429933645, 0.898893864524174, 0,
            -6.76350514581911E-08, 3.29687521579662E-08, -0.999999999999997
        };
        result = testLine1.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_rotatingFrameDCMs(16);
        //PrintDoubleArray(result2);
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }


        //Sample rotating frame positions
        expected = new double[] {328364972.723979, 235619259.822744, 3};
        result = testLine1.Sample_rotatingFramePositions(700);
        result2 = testLine2.Sample_rotatingFramePositions(700);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }

        //Sample relativeToRotatingFrameBodyPositions_BSK
        expected = new double[] {-0.291508294896689, -11.1765389565387, 2.99999997783004};
        expected2 = new double[3]; //all-zero because it is the chief sc
        result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        //PrintDoubleArray(result);
        result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare);  
            compare = (float) (expected2[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Sample points to plot onscreen
        expected_v = new Vector3(-1.312929f,2.532594E-09f,-0.7724907f);
        result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1);
        //PrintVector3(result_v[0]);
        compare_v = (expected_v - result_v[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare_v); 
        
        //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
        Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
        Assert.AreEqual(0,testLine2.GetCount_pointsToDrawOnscreen());
    }
    private void Test_TruePathMode2_SCRelMode3_Inertial()
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true; 
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.TruePathLineMode = 2;
        VizardGUISettings.SpacecraftRelativeOrbitMode = 3;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        SpacecraftStateUtilities.UpdateChiefSpacecraft(0);
        VizardGUISettings.RelativeTruePathChangeCount++;
        Assert.AreEqual(0, VizardGUISettings.ChiefSpacecraftIndex);

        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();

        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine1.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine1.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine1.GetIndexOfRelativeSpacecraft());
        
        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine2.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine2.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine2.GetIndexOfRelativeSpacecraft());
        
        // Test line should set relative body index to the parent body,
        // VizardGUISettings should be -1 to indicate each line should use its spacecraft's parent body
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine1.GetIndexOfRelativeBody());
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine2.GetIndexOfRelativeBody());
        Assert.AreEqual(-1, VizardGUISettings.RelativeBodyIndex);
        Assert.AreEqual(1, testLine1.GetIndexOfRelativeBody()); 
        
        //Test BuildRelativeBodyData()
        List<double[]> resultPositionHistory = testLine2.Sample_mySpacecraftPositionHistoryBSK(150, 5);
        List<double[]> resultRelativeToBodyPositions_Unity = testLine2.Sample_relativeToBodyPositions_Unity(150, 5);

        for (int i = 0; i < 5; i++)
        {
            double[] expectedPosition = MessageList.GetMessageAtIndex(150+i).Spacecraft[1].Position.ToArray();
            double[] expectedRelativeBodyPosition = MessageList.GetMessageAtIndex(150+i)
                .Spacecraft[testLine2.GetIndexOfRelativeSpacecraft()].Position.ToArray();
            Assert.AreEqual(expectedPosition[0], resultPositionHistory[i][0]);
            Assert.AreEqual(expectedPosition[1], resultPositionHistory[i][1]);
            Assert.AreEqual(expectedPosition[2], resultPositionHistory[i][2]);
            
            double[] expectedRelativeToBodyPosition_Unity = OrbitVectorMath.TransformFromBSKCStoUnity(
                OrbitVectorMath.Subtract(expectedPosition, expectedRelativeBodyPosition));
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[0], resultRelativeToBodyPositions_Unity[i][0]);
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[1], resultRelativeToBodyPositions_Unity[i][1]);
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[2], resultRelativeToBodyPositions_Unity[i][2]);
        }
        
        //Test CalculatePointsToPlot_BodyRelative();
        
        //Check spacecraftLocalView
        Vector3 expected = new Vector3(5f, 3f, -10f);
        List<Vector3> result = testLine2.Sample_pointsToDrawOnscreen(99, 1);
        float compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check local view
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false;
        expected = new Vector3(5E-05f, 3E-05f, -0.0001f);
        result = testLine2.Sample_pointsToDrawOnscreen(167, 1);
        compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check solar system view
        CelestialBodyStateUtilities.ViewIsLocal = false;
        expected = new Vector3(5E-09f, 3E-09f, -1E-08f);
        result = testLine2.Sample_pointsToDrawOnscreen(563, 1);
        compare = (expected - result[0]).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
    }
    private void Test_TruePathMode3()
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true; 
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.TruePathLineMode = 3;
        VizardGUISettings.SpacecraftRelativeOrbitMode = 3;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        testLine1.CallTruePathUpdate(); 
        testLine2.CallTruePathUpdate();

        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine1.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine1.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine1.GetIndexOfRelativeSpacecraft());
        
        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine2.GetLastFrameTruePathLineMode());
        Assert.AreEqual(VizardGUISettings.SpacecraftRelativeOrbitMode, testLine2.GetLastFrameSpacecraftRelMode());
        Assert.AreEqual(VizardGUISettings.ChiefSpacecraftIndex, testLine2.GetIndexOfRelativeSpacecraft());
        
        // Test line should set relative body index to the parent body,
        // VizardGUISettings should be -1 to indicate each line should use its spacecraft's parent body
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine1.GetIndexOfRelativeBody());
        Assert.AreNotEqual(VizardGUISettings.RelativeBodyIndex, testLine2.GetIndexOfRelativeBody());
        Assert.AreEqual(-1, VizardGUISettings.RelativeBodyIndex);
        Assert.AreEqual(1, testLine1.GetIndexOfRelativeBody());
        
        //Test BuildRelativeBodyData()
        int startingIndex = 226;
        int numberOfFrames = 5;
        List<double[]> resultPositionHistory = testLine1.Sample_mySpacecraftPositionHistoryBSK(startingIndex, numberOfFrames);
        List<double[]> resultRelativeToBodyPositions_Unity = testLine1.Sample_relativeToBodyPositions_Unity(startingIndex, numberOfFrames);

        for (int i = 0; i < 5; i++)
        {
            double[] expectedPosition = MessageList.GetMessageAtIndex(startingIndex+i).Spacecraft[0].Position.ToArray();
            double[] expectedRelativeBodyPosition = MessageList.GetMessageAtIndex(startingIndex+i)
                .CelestialBodies[testLine1.GetIndexOfRelativeBody()].Position.ToArray();
            Assert.AreEqual(expectedPosition[0], resultPositionHistory[i][0]);
            Assert.AreEqual(expectedPosition[1], resultPositionHistory[i][1]);
            Assert.AreEqual(expectedPosition[2], resultPositionHistory[i][2]);
            
            double[] expectedRelativeToBodyPosition_Unity = OrbitVectorMath.TransformFromBSKCStoUnity(
                OrbitVectorMath.Subtract(expectedPosition, expectedRelativeBodyPosition));
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[0], resultRelativeToBodyPositions_Unity[i][0]);
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[1], resultRelativeToBodyPositions_Unity[i][1]);
            Assert.AreEqual(expectedRelativeToBodyPosition_Unity[2], resultRelativeToBodyPositions_Unity[i][2]);
        }
        
        //Test CalculatePointsToPlot_BodyRelative();
        //Check spacecraftLocalView
        double[] expectedRelPos = testLine1.Sample_relativeToBodyPositions_Unity(83, 1)[0];
        Vector3 extRelPos_Unity = OrbitVectorMath.ReturnVector3(expectedRelPos);
        Vector3 expectedBodyPosition =
            CelestialBodyStateUtilities.GetCelestialBodyObject(testLine1.GetIndexOfRelativeBody()).transform.position;
        Vector3 expected = expectedBodyPosition+1f*extRelPos_Unity;
        Vector3 result = testLine1.Sample_pointToDrawOnscreen(83);
        float compare = (expected-result).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check local view
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false;
        testLine1.CallTruePathUpdate();
        expectedRelPos = testLine1.Sample_relativeToBodyPositions_Unity(167, 1)[0];
        extRelPos_Unity = OrbitVectorMath.ReturnVector3(expectedRelPos)/(float)CelestialBodyStateUtilities.GetCurrentScale();
        expectedBodyPosition =
            OrbitVectorMath.ReturnVector3(MainCameraUtilities.GetScaledObjectPositionRelToCamTgt(testLine1.GetIndexOfRelativeBody(), false));
        expected = expectedBodyPosition+extRelPos_Unity;
        result = testLine1.Sample_pointToDrawOnscreen(167);
        compare = (expected - result).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
        
        //Check solar system view
        CelestialBodyStateUtilities.ViewIsLocal = false;
        testLine1.CallTruePathUpdate();
        expected = new Vector3(-0.03220068f, 0.00f, 0.02761615f);
        result = testLine1.Sample_pointToDrawOnscreen(563);
        compare = (expected - result).magnitude;
        Assert.AreApproximatelyEqual(0, compare);
    }
    private void Test_TruePathMode4() //Two body Rotating frame
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true; 
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.RotatingFrameBody1Index = 0;
        VizardGUISettings.RotatingFrameBody2Index = 1;
        CelestialBodyStateUtilities.CalculateRotatingFramePositionAndVelocityHistories();
        VizardGUISettings.TruePathLineMode = 4;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();
    
        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine1.GetLastFrameTruePathLineMode());
        
        //Sample rotation frame DCM
        double[] expected = new double[]
        {
            0.987227283374182,0.159318206623198,0,
            -0.159318206623198,0.987227283374182, 0,
            0,0,1
        };
        double[] result = testLine1.Sample_rotatingFrameDCMs(16);
        double[] result2 = testLine2.Sample_rotatingFrameDCMs(16);
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    
        //Sample rotating frame positions
        expected = new double[] {170.748577724364,148.798503612214,0};
        result = testLine1.Sample_rotatingFramePositions(700);
        result2 = testLine2.Sample_rotatingFramePositions(700);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    
        //Sample relativeToRotatingFrameBodyPositions_BSK
        expected = new double[] {424230390.632162,3858871.2752552,0};
    double[] expected2 = new double[]{424230391.833172,3858860.15960968,3}; 
        result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i]-result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected2[i]-result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }

    //Sample points to plot onscreen
    Vector3 expected_v = new Vector3(9109377f,0,-899100.8f);
    List<Vector3> result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1);
    float compare_v = (expected_v - result_v[0]).magnitude;
    Assert.AreApproximatelyEqual(0, compare_v);
    
    expected_v = new Vector3(9109382f,3f,-899111.1f);
    result_v = testLine2.Sample_pointsToDrawOnscreen(5, 1);
    compare_v = (expected_v.x-result_v[0].x);
    Assert.AreApproximatelyEqual(0, compare_v);
    compare_v = (expected_v.y-result_v[0].y);
    Assert.AreApproximatelyEqual(0, compare_v);
    compare_v = (expected_v.z-result_v[0].z);
    Assert.IsTrue(Mathf.Abs(compare_v)<0.1);
    
    //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
    Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
    Assert.AreEqual(951,testLine2.GetCount_pointsToDrawOnscreen());
    
    //Test planet local///////////////////////////////////////
    MainCameraUtilities.UnitTestCameraPosition = new double[] {88, 12, 65};
    CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false;
    testLine1.CallTruePathUpdate();
    testLine2.CallTruePathUpdate();
    
    //Sample rotation frame DCM (should be unchanged from local view)
           //Sample rotation frame DCM
    expected = new double[]
    {
        0.987227283374182,0.159318206623198,0,
        -0.159318206623198,0.987227283374182, 0,
        0,0,1
    };
    result = testLine1.Sample_rotatingFrameDCMs(16);
    result2 = testLine2.Sample_rotatingFrameDCMs(16);
    for (int i = 0; i < 9; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }
    
    //Sample rotating frame positions
    expected = new double[] {170.748577724364,148.798503612214,0};
    result = testLine1.Sample_rotatingFramePositions(700);
    result2 = testLine2.Sample_rotatingFramePositions(700);
    for (int i = 0; i < 3; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }

    //Sample relativeToRotatingFrameBodyPositions_BSK
    expected = new double[] {424230390.632162, 3858871.2752552, 0,};
    expected2 = new double[] {424230391.833172, 3858860.15960968, 3}; //all-zero because it is the chief sc
    result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
    result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
    for (int i = 0; i < 3; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected2[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }
        
    //Sample points to plot onscreen
    expected_v = new Vector3(-224.6852f,0,-719.921f);
    Vector3 expected_v2 = new Vector3(-224.6852f, 3E-05f, -719.921f);
    Vector3 result_v1 = testLine1.Sample_pointsToDrawOnscreen(88, 1)[0];
    Vector3 result_v2 = testLine2.Sample_pointsToDrawOnscreen(88, 1)[0];
    compare_v = (expected_v - result_v1).magnitude;
    Assert.IsTrue(compare_v<.0001);
    compare_v = (expected_v2 - result_v2).magnitude;
    Assert.IsTrue(compare_v<.0001);
    
    //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
    Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
    Assert.AreEqual(951,testLine2.GetCount_pointsToDrawOnscreen());
    
    //Test solar system view///////////////////////////////////////
    MainCameraUtilities.UnitTestCameraPosition = new double[] {88, 12, 65};
    CelestialBodyStateUtilities.ViewIsLocal = false;
    testLine1.CallTruePathUpdate();
    testLine2.CallTruePathUpdate();
    
    //Sample rotation frame DCM (should be unchanged from local view)
           //Sample rotation frame DCM
    expected = new double[]
    {
        0.987227283374182,0.159318206623198,0,
        -0.159318206623198,0.987227283374182, 0,
        0,0,1
    };
    result = testLine1.Sample_rotatingFrameDCMs(16);
    result2 = testLine2.Sample_rotatingFrameDCMs(16);
    for (int i = 0; i < 9; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }
    
    //Sample rotating frame positions
    expected = new double[] {170.748577724364,148.798503612214,0};
    result = testLine1.Sample_rotatingFramePositions(700);
    result2 = testLine2.Sample_rotatingFramePositions(700);
    for (int i = 0; i < 3; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }

    //Sample relativeToRotatingFrameBodyPositions_BSK
    expected = new double[] {424230390.632162, 3858871.2752552, 0,};
    expected2 = new double[] {424230391.833172, 3858860.15960968, 3}; //all-zero because it is the chief sc
    result = testLine1.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
    result2 = testLine2.Sample_relativeToRotatingFrameBodyPositions_BSK(821);
    for (int i = 0; i < 3; i++)
    {
        float compare = (float) (expected[i] - result[i]);
        Assert.AreApproximatelyEqual(0, compare);
        compare = (float) (expected2[i] - result2[i]);
        Assert.AreApproximatelyEqual(0, compare);
    }
        
    //Sample points to plot onscreen
    expected_v = new Vector3(-0.02246852f,0,-0.418396f);
    expected_v2 = new Vector3(-0.02246852f,3E-09f,-0.418396f);
    result_v1 = testLine1.Sample_pointsToDrawOnscreen(88, 1)[0];
    result_v2 = testLine2.Sample_pointsToDrawOnscreen(88, 1)[0];
    compare_v = (expected_v - result_v1).magnitude;
    Assert.IsTrue(compare_v<.0001);
    compare_v = (expected_v2 - result_v2).magnitude;
    Assert.IsTrue(compare_v<.0001);
    
    //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
    Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
    Assert.AreEqual(951,testLine2.GetCount_pointsToDrawOnscreen());
    }
    private void Test_TruePathMode5() //fixed frame 
    {
        CelestialBodyStateUtilities.ViewIsSpacecraftLocal = true; 
        CelestialBodyStateUtilities.ViewIsLocal = true;
        VizardGUISettings.TruePathLineMode = 5;
        VizardGUISettings.FixedBodyIsSpacecraft = true;
        VizardGUISettings.FixedBodyIndex = 1;
        VizardGUISettings.TruePathLinesVisible = true;
        VizardGUISettings.RelativeTruePathChangeCount++;
        testLine1.CallTruePathUpdate();
        testLine2.CallTruePathUpdate();
    
        Assert.AreEqual(VizardGUISettings.TruePathLineMode, testLine1.GetLastFrameTruePathLineMode());

         //Sample rotation frame DCM
        double[] expected = new double[]
        {
            0.483630006189873,0.428516155878856,0.763201101455961,
            -0.814815398085568,-0.0980213144111087,0.571373510904886,
            0.319652755588687,-0.898201383917842,0.301755513243143
        };
        double[] result = testLine1.Sample_fixedFrameDCMs(256);
        //PrintDoubleArray(result);
        double[] result2 = testLine2.Sample_fixedFrameDCMs(256);
        //PrintDoubleArray(result2);
        for (int i = 0; i < 9; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare); 
            compare = (float) (expected[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare); //WHAT
        }
    
        //Sample fixed frame positions
        expected = new double[] {-6.08555527679512,8.25750719412046,5.36466139771373};
        double[] expected2 = new double[3];
        result = testLine1.Sample_scRelativeToFixedBodyPositions_fixedBSK(444);
        result2 = testLine2.Sample_scRelativeToFixedBodyPositions_fixedBSK(444);
        for (int i = 0; i < 3; i++)
        {
            float compare = (float) (expected[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
            compare = (float) (expected2[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    
    //Sample points to plot onscreen - spacecraft local
    Vector3 expected_v = new Vector3(-0.0221054f,0.1655897f,0.03715974f);
    Vector3 result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1)[0];
    float compare_v = (expected_v - result_v).magnitude;
    Assert.AreApproximatelyEqual(0, compare_v);

    //Test planet local///////////////////////////////////////
    CelestialBodyStateUtilities.ViewIsSpacecraftLocal = false;
    testLine1.CallTruePathUpdate();
    testLine2.CallTruePathUpdate();
    expected_v = new Vector3(-2.21054E-07f,1.655897E-06f,3.715974E-07f);
    result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1)[0];
    compare_v = (expected_v - result_v).magnitude;
    Assert.AreApproximatelyEqual(0, compare_v);
    
    //Test solar system///////////////////////////////////////
    CelestialBodyStateUtilities.ViewIsLocal = false;
    testLine1.CallTruePathUpdate();
    testLine2.CallTruePathUpdate();
    expected_v = new Vector3(-2.21054E-11f,1.655897E-10f,3.715974E-11f);
    result_v = testLine1.Sample_pointsToDrawOnscreen(5, 1)[0];
    compare_v = (expected_v - result_v).magnitude;
    Assert.AreApproximatelyEqual(0, compare_v);
    
    //Confirm that spacecraft 2's points to plot is empty (because it is the chief)
    Assert.AreEqual(951,testLine1.GetCount_pointsToDrawOnscreen());
    Assert.AreEqual(0,testLine2.GetCount_pointsToDrawOnscreen());
    }
    
    private void PrintVector3(Vector3 result_v)
    {
        Debug.Log($"{result_v.x}f,{result_v.y}f,{result_v.z}f");
    }

    private void PrintDoubleArray(double[] array)
    {
        string outString = "{";
        for (int i = 0; i < array.Length; i++)
        {
            outString += $"{array[i]},";
        }

        outString += "}";
        Debug.Log(outString);
    }
}
