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
using UnityEngine.Assertions;

public class TestOrbitVectorMath : MonoBehaviour
{
    double[] vec1 =  {10, 20, 30};
    double[] vec2 =  {45, 55, 65};
    private double[] testMatrix =  {1, 2, 3, -4, 5, -6, 7, 8, -9};
    double body1Mu = 398600.436;
    double body2Mu = 4902.799;
    
    public string RunOrbitVectorMathTestSuite()
    {
        Test_Cross();
        Test_Dot();
        Test_Add();
        Test_Subtract();
        Test_ScaleVector();
        Test_Magnitude();
        Test_Normalized();
        Test_ReturnVector3();
        Test_ReturnDoubleArray();
        Test_ReturnAngleBetweenVectorsInRadians();
        Test_ApplyTransformationMatrixToVector();
        Test_TransposeMatrix();
        Test_Dot3x3Matrix();
        Test_CalculateHillFrame();
        Test_CalculateHillFrameDegenerateStates();
        Test_CalculateVelocityFrame();
        Test_TransformFromUnityCStoBSK();
        Test_TransformFromBSKCStoUnity();
        Test_ConvertRightHandedMRPtoLeftHandedQuaternion();
        Test_ConvertRightHandedDCMtoLeftHandedQuaternion();
        Test_ConvertRightHandedMRPToRightHandedDCM();
        Test_CalculateStationOffsetJ2000_UnityCS();

        LoadOrbitVectorMathTestFile();
        Test_SphereOfInfluence();
        Test_FindPrimaryBody();
        Test_CalculateCenterOfMass();
        Test_CalculateRotatingFrame();

        Test_HSL2RGB();

        Test_CalculateRightHandedMRPFromEulerAngles();

        return "\t All orbitVectorMath.cs tests passed.";
    }

    private void LoadOrbitVectorMathTestFile()
    {
        string testFilePath = Application.dataPath + "/Tests/TestMessageFiles/orbitVectorMathTestFile.bin";
        
        int expectedTotalMessages = 951;
        
        //Change the buffer limit to large enough to load entire file
        MessageList.FirstMessageBuffersReadFromFile(testFilePath, 1000000);
        Assert.AreEqual(expectedTotalMessages, MessageList.TimestepsTotal); 
    }
    private void Test_Cross()
    {
        double[] result = OrbitVectorMath.Cross(vec1, vec2);
        Assert.AreEqual(-350, result[0]);
        Assert.AreEqual(700, result[1]);
        Assert.AreEqual(-350, result[2]);
    }

    private void Test_Dot()
    {
        double result = OrbitVectorMath.Dot(vec1, vec2);
        Assert.AreEqual(3500, result);
    }

    private void Test_Add()
    {
        double[] result = OrbitVectorMath.Add(vec1, vec2);
        Assert.AreEqual(55, result[0]);
        Assert.AreEqual(75, result[1]);
        Assert.AreEqual(95, result[2]);
    }
    
    private void Test_Subtract()
    {
        double[] result = OrbitVectorMath.Subtract(vec1, vec2);
        Assert.AreEqual(-35, result[0]);
        Assert.AreEqual(-35, result[1]);
        Assert.AreEqual(-35, result[2]);
    }

    private void Test_ScaleVector()
    {
        double[] result = OrbitVectorMath.ScaleVector(vec1, 1350);
        Assert.AreEqual(13500, result[0]);
        Assert.AreEqual(27000, result[1]);
        Assert.AreEqual(40500, result[2]);
    }

    private void Test_Magnitude()
    {
        double expectedResult = System.Math.Sqrt(vec1[0] * vec1[0] + vec1[1]*vec1[1] + vec1[2] * vec1[2]);
        double result = OrbitVectorMath.Magnitude(vec1);
        Assert.AreEqual(expectedResult, result);
    }

    private void Test_Normalized()
    {
        double magnitude = System.Math.Sqrt(vec1[0] * vec1[0] + vec1[1]*vec1[1] + vec1[2] * vec1[2]);
        double[] expectedResult =  {vec1[0] / magnitude, vec1[1] / magnitude, vec1[2] / magnitude};
        double[] result = OrbitVectorMath.Normalized(vec1);

        float compare0 = (float) (expectedResult[0] - result[0]);
        float compare1 = (float) (expectedResult[1] - result[1]);
        float compare2 = (float) (expectedResult[2] - result[2]);
        Assert.AreApproximatelyEqual(0, compare0);
        Assert.AreApproximatelyEqual(0, compare1);
        Assert.AreApproximatelyEqual(0, compare2);
    }

    private void Test_ReturnVector3()
    {
        Vector3 result = OrbitVectorMath.ReturnVector3(vec1);
        Assert.AreEqual(new Vector3((float)vec1[0], (float)vec1[1], (float)vec1[2]), result);
    }

    private void Test_ReturnDoubleArray()
    {
        Vector3 testVector = new Vector3(1, 2, 3);
        double[] result = OrbitVectorMath.ReturnDoubleArray(testVector);
        Assert.AreEqual(1, result[0]);
        Assert.AreEqual(2, result[1]);
        Assert.AreEqual(3, result[2]);
    }

    private void Test_ReturnAngleBetweenVectorsInRadians() //This one might need more work
    {
        double[] testVec1 =  {1, 0, 0};
        double[] testVec2 =  {0, 1, 0};
        double result = OrbitVectorMath.ReturnAngleBetweenVectorsInRadians(testVec1, testVec2);
        float compare = (float) (System.Math.PI/2 - result);
        Assert.AreApproximatelyEqual(0, compare);
    }

    private void Test_ApplyTransformationMatrixToVector()
    {
        double[] result = OrbitVectorMath.ApplyTransformationMatrixToVector(testMatrix, vec1);
        
        Assert.AreEqual(140,result[0]);
        Assert.AreEqual(-120,result[1]);
        Assert.AreEqual(-40,result[2]);
    }

    private void Test_TransposeMatrix()
    {
        double[] result = OrbitVectorMath.TransposeMatrix(testMatrix);
        Assert.AreEqual(testMatrix[0],result[0]);
        Assert.AreEqual(testMatrix[3],result[1]);
        Assert.AreEqual(testMatrix[6],result[2]);
        Assert.AreEqual(testMatrix[1],result[3]);
        Assert.AreEqual(testMatrix[4],result[4]);
        Assert.AreEqual(testMatrix[7],result[5]);
        Assert.AreEqual(testMatrix[2],result[6]);
        Assert.AreEqual(testMatrix[5],result[7]);
        Assert.AreEqual(testMatrix[8],result[8]);
    }

    private void Test_Dot3x3Matrix()
    {
        double[] m1 = new double[] {0, 1, 2, 3, 4, 5, 6, 7, 8};
        double[] m2 = new double[] {-10, -20, -30, -45, -55, -65, -700, -800, -900};
        double[] result = OrbitVectorMath.Dot3x3Matrix(m1, m2);
        double[] expectedResult = new double[] {-1445, -1655, -1865, -3710, -4280, -4850, -5975, -6905, -7835};
        for (int i = 0; i < result.Length; i++)
        {
            Assert.AreEqual(expectedResult[i], result[i]);
        }

    }

    private void Test_CalculateHillFrame()
    {
        double[] result = OrbitVectorMath.CalculateHillFrame(vec1, vec2);
        double[] ri = OrbitVectorMath.Normalized(vec1);
        double[] hi = OrbitVectorMath.Normalized(OrbitVectorMath.Cross(ri, vec2));
        double[] ti = OrbitVectorMath.Normalized(OrbitVectorMath.Cross(hi, ri));
        Assert.AreEqual(ri[0],result[0]);
        Assert.AreEqual(ri[1],result[1]);
        Assert.AreEqual(ri[2],result[2]);
        Assert.AreEqual(ti[0],result[3]);
        Assert.AreEqual(ti[1],result[4]);
        Assert.AreEqual(ti[2],result[5]);
        Assert.AreEqual(hi[0],result[6]);
        Assert.AreEqual(hi[1],result[7]);
        Assert.AreEqual(hi[2],result[8]);
    }

    private void Test_CalculateHillFrameDegenerateStates()
    {
        double[] identity = {1, 0, 0, 0, 1, 0, 0, 0, 1};
        double[] previousFrame = {1, 0, 0, 0, 0, 1, 0, -1, 0};
        double[][] positions = {
            new double[] {10, 0, 0}, new double[] {10, 0, 0},
            new double[] {10, 0, 0}, new double[] {0, 0, 0}
        };
        double[][] velocities = {
            new double[] {0, 0, 0}, new double[] {2, 0, 0},
            new double[] {2, 1e-14, 0}, new double[] {0, 2, 0}
        };
        for (int state = 0; state < positions.Length; state++)
        {
            double[] initial = OrbitVectorMath.CalculateHillFrame(positions[state], velocities[state]);
            double[] retained = OrbitVectorMath.CalculateHillFrame(positions[state], velocities[state], previousFrame);
            for (int axis = 0; axis < 9; axis++)
            {
                Assert.AreEqual(identity[axis], initial[axis]);
                Assert.AreEqual(previousFrame[axis], retained[axis]);
            }
        }

        double[] resumed = OrbitVectorMath.CalculateHillFrame(new double[] {10, 0, 0},
            new double[] {0, 2, 0}, previousFrame);
        for (int axis = 0; axis < 9; axis++)
            Assert.AreEqual(identity[axis], resumed[axis]);
    }

    private void Test_CalculateVelocityFrame()
    {
        double[] result = OrbitVectorMath.CalculateVelocityFrame(vec1, vec2);
        double[] vi = OrbitVectorMath.Normalized(vec2);
        double[] hi = OrbitVectorMath.Normalized(OrbitVectorMath.Cross(vec1, vec2));
        double[] ri = OrbitVectorMath.Normalized(OrbitVectorMath.Cross(vi, hi));
        Assert.AreEqual(ri[0],result[0]);
        Assert.AreEqual(ri[1],result[1]);
        Assert.AreEqual(ri[2],result[2]);
        Assert.AreEqual(vi[0],result[3]);
        Assert.AreEqual(vi[1],result[4]);
        Assert.AreEqual(vi[2],result[5]);
        Assert.AreEqual(hi[0],result[6]);
        Assert.AreEqual(hi[1],result[7]);
        Assert.AreEqual(hi[2],result[8]);
    }
    
    private void Test_TransformFromUnityCStoBSK()
    {
        double[] result = OrbitVectorMath.TransformFromUnityCStoBSK( vec2);
        Assert.AreEqual(-vec2[2],result[0]);
        Assert.AreEqual(vec2[0],result[1]);
        Assert.AreEqual(vec2[1],result[2]);

        Vector3 input = new Vector3(-1.5f, 6, 7);
        Vector3 resultVec = OrbitVectorMath.TransformFromUnityCStoBSK(input);
        Assert.AreEqual(-input.z,resultVec.x);
        Assert.AreEqual(input.x,resultVec.y);
        Assert.AreEqual(input.y,resultVec.z);
    }
    private void Test_TransformFromBSKCStoUnity()
    {
        double[] result = OrbitVectorMath.TransformFromBSKCStoUnity(vec2);
        Assert.AreEqual(vec2[1],result[0]);
        Assert.AreEqual(vec2[2],result[1]);
        Assert.AreEqual(-vec2[0],result[2]);
        
        Vector3 input = new Vector3(-1.5f, 6, 7);
        Vector3 resultVec = OrbitVectorMath.TransformFromBSKCStoUnity(input);
        Assert.AreEqual(input.y,resultVec.x);
        Assert.AreEqual(input.z,resultVec.y);
        Assert.AreEqual(-input.x,resultVec.z);
    }

    private void Test_ConvertRightHandedMRPtoLeftHandedQuaternion()
    {
        double[] testMRP = {-0.580354, -0.6371734, 0.0866201};
        double qm = 1 + OrbitVectorMath.Dot(testMRP, testMRP);
        //Below is the right handed quaternion
        double[] q = new[]
        {
             2 * testMRP[0] / qm, 2 * -testMRP[1] / qm,
            2 * -testMRP[2] / qm, (1 - OrbitVectorMath.Dot(testMRP, testMRP)) / qm
        };
        
        Quaternion result = OrbitVectorMath.ConvertRightHandedMRPtoLeftHandedQuaternion(testMRP);

        Assert.AreApproximatelyEqual((float)q[1], result.x);
        Assert.AreApproximatelyEqual((float)q[2], result.y);
        Assert.AreApproximatelyEqual((float)q[0], result.z);
        Assert.AreApproximatelyEqual((float)q[3], result.w);
    }

    private void Test_ConvertRightHandedDCMtoLeftHandedQuaternion()
    {
        double[,] testDCM = new double[,]
                {{1, 0, 0}, 
                {0, 0.998512978939763, 0.0545145016380063}, 
                {0, -0.0545145016380063, 0.998512978939763}};

        float[] expectedResult = {0, 0, 0.02726739f, 0.9996282f};

        Quaternion result = OrbitVectorMath.ConvertRightHandedDCMtoLeftHandedQuaternion(testDCM);

        Assert.AreApproximatelyEqual(expectedResult[0], result.x);
        Assert.AreApproximatelyEqual(expectedResult[1], result.y);
        Assert.AreApproximatelyEqual(expectedResult[2], result.z);
        Assert.AreApproximatelyEqual(expectedResult[3], result.w);
    }

    private void Test_ConvertRightHandedMRPToRightHandedDCM()
    {
        double[] testMRP =  {-0.5, -0.6, 0.01};
        double[] expectedResult = new double[]
        {
            -0.111238829514489, 0.931791193980798, 0.345530163123403,
            0.919759202322156, 0.228212076474385, -0.31931529542908,
            -0.376389336395119, 0.282284287503021, -0.88240956957467
        };
        double[] result = OrbitVectorMath.ConvertRightHandedMRPToRightHandedDCM(testMRP);

        for (int i = 0; i < expectedResult.Length; i++){
            float compare = (float) (expectedResult[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    }

    private void Test_CalculateStationOffsetJ2000_UnityCS()
    {
        // This test is using the test file at:
        // Application.dataPath + "/Tests/TestMessageFiles/bufferTestFileWithMissingFramesBufferLimit10k.bin";
        Assert.AreEqual(10, MessageList.CurrentIndex);
        
        //Test for spacecraft:
        double[] expectedResult =  {5506962.92494235, -1037506.55297854, -7042518.61666084}; 
        double[] result = OrbitVectorMath.CalculateStationOffsetJ2000_UnityCS(0, true,  new double[]{-1, 2, 3});
        for (int i = 0; i < expectedResult.Length; i++){
            float compare = (float) (expectedResult[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        
        //Test for planet:
        double[] expectedResult2 = {0, 0, -6378136.5};
        double[] result2 = OrbitVectorMath.CalculateStationOffsetJ2000_UnityCS(0, false, new double[] {CelestialBodyStateUtilities.GetCelestialBodyRadiusInMeters("earth"),0,0});
        for (int i = 0; i < expectedResult2.Length; i++){
            float compare = (float) (expectedResult2[i] - result2[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    }

    private void Test_SphereOfInfluence()
    {
        int body1Index = 0;
        int body2Index = 1;
        double[] scPosition = SpacecraftStateUtilities.GetAbsSpacecraftPositionUnityCS(0);

        System.Tuple<int, double> result =
            OrbitVectorMath.SphereOfInfluence(scPosition, body1Index, body1Mu, body2Index, body2Mu);
        Assert.AreEqual(body2Index, result.Item1);
        Assert.AreEqual(body2Mu, result.Item2);
    }
    
    private void Test_FindPrimaryBody()
    {
        int[] result = OrbitVectorMath.FindPrimaryBody(0, true, true);
        
        Assert.AreEqual(1, result[0]);
        //The parent body is the moon 
        Assert.AreEqual("Moon",MessageList.CurrentMessage.CelestialBodies[result[0]].BodyName);
    }

    private void Test_CalculateCenterOfMass()
    {
        double[] earthVector = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(0);
        double[] moonVector = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(1);
        
        double[] expectedResult = {0, 0, -226.667942722749};
        double[] result = OrbitVectorMath.CalculateCenterOfMass(earthVector, moonVector, body1Mu, body2Mu);
        for (int i = 0; i < expectedResult.Length; i++){
            float compare = (float) (expectedResult[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    }

    private void Test_CalculateRotatingFrame()
    {
        double[] earthVector = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(0);
        double[] moonVector = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(1);

        double[] earthVelocity = CelestialBodyStateUtilities.GetAbsPlanetVelocityUnityCS(0);
        double[] moonVelocity = CelestialBodyStateUtilities.GetAbsPlanetVelocityUnityCS(1);
        
        double[] expectedResultPosition = {0, 0, -226.667942722749};
        double[] resultPosition = OrbitVectorMath.CalculateCenterOfMass(earthVector, moonVector, body1Mu, body2Mu);
        for (int i = 0; i < expectedResultPosition.Length; i++){
            float compare = (float) (expectedResultPosition[i] - resultPosition[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
        double[] expectedResultVelocity = {0.000591851665544461, 0, 0};
        double[] resultVelocity = OrbitVectorMath.CalculateCenterOfMass(earthVelocity, moonVelocity, body1Mu, body2Mu);
        for (int i = 0; i < expectedResultVelocity.Length; i++){
            float compare = (float) (expectedResultVelocity[i] - resultVelocity[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }

        double[] expectedRotatingFrame = {0, 0, -1, 1, 0, 0, 0, -1, 0};
        double[] resultRotatingFrame = OrbitVectorMath.CalculateRotatingFrame(resultPosition, resultVelocity);
        for (int i = 0; i < resultRotatingFrame.Length; i++){
            float compare = (float) (expectedRotatingFrame[i] - resultRotatingFrame[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    }

    private void Test_HSL2RGB()
    {
        double h_input = 0;
        double sl_input = 1f;
        double l_input = 0.5f;

        Color expectedResult = Color.red;
        Color result = OrbitVectorMath.HSL2RGB(h_input, sl_input, l_input);
        Assert.AreEqual(expectedResult, result);
    }

    private void Test_CalculateRightHandedMRPFromEulerAngles()
    {
        double angle1 = System.Math.PI / 6;
        double angle2 = -System.Math.PI / 30;
        double angle3 = System.Math.PI - .1;

        double[] result = OrbitVectorMath.CalculatedRightHandedMRPFromEulerAngles(angle1, angle2, angle3);
        double[] expectedResult = new[] {0.931758854496826, 0.247046821352327, 0.0612819632376562};
        for (int i = 0; i < result.Length; i++)
        {
            float compare = (float) (expectedResult[i] - result[i]);
            Assert.AreApproximatelyEqual(0, compare);
        }
    }
    
    
}
