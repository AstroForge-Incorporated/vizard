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
using UnityEngine;
/// <summary>
/// Math functions for Vizard including transformations between BSK right-handed z-up frame
/// to Unity left-handed y-up frame
/// </summary>
public static class OrbitVectorMath
{

	public static readonly double EPS = 1e-12; /* small numerical value parameter */
	public static double[] Cross(double[] v1, double[] v2){
		return new []{(v1[1]*v2[2]-v1[2]*v2[1]),-(v1[0]*v2[2]-v1[2]*v2[0]),(v1[0]*v2[1]-v1[1]*v2[0])};
	}

	public static double Dot(double[] v1, double[] v2){
		return (v1 [0] * v2 [0] + v1 [1] * v2 [1] + v1 [2] * v2 [2]);				
	}

	public static double[] Add(double[] v1, double[] v2){
		return new []{v1 [0] + v2 [0], v1 [1] +v2 [1], v1 [2] + v2 [2]};
	}

	public static double[] Subtract(double[] v1, double[] v2){
		return new []{v1 [0] - v2 [0], v1 [1] -v2 [1], v1 [2] - v2 [2]};
	}

	public static double[] ScaleVector(double[] v1, double scaleValue){
		return new []{v1[0]*scaleValue, v1[1]*scaleValue, v1[2]*scaleValue};
	}

	public static double Magnitude(double[] v1){
		return Math.Sqrt (Dot (v1, v1));
	}

	public static double[] Normalized(double[] v1){
		return ScaleVector (v1, (1/Magnitude (v1)));
	}

	public static Vector3 ReturnVector3(double[] v1){
		return new Vector3 ((float)v1 [0], (float)v1 [1], (float)v1 [2]);
	}
	
	public static double[] ReturnDoubleArray(Vector3 v1){
		return new double[] {v1 [0], v1 [1], v1 [2]};
	}

	public static double ReturnAngleBetweenVectorsInRadians(double[] v1, double[] v2){
		double numerator = Dot (v1, v2);
		double denominator = Magnitude (v1) * Magnitude (v2);
		double theta = Math.Acos (numerator / denominator);
		return theta; //Returns in radians
	}

	public static double[] ApplyTransformationMatrixToVector(double[] m, double[] v){
		return new []{
			m[0]*v[0]+m[1]*v[1]+m[2]*v[2],
			m[3]*v[0]+m[4]*v[1]+m[5]*v[2],
			m[6]*v[0]+m[7]*v[1]+m[8]*v[2]
		};
	}

	public static double[] TransposeMatrix(double[] m){
		return new []{
			m[0], m[3], m[6],
			m[1], m[4], m[7],
			m[2], m[5], m[8]
		};
	}

	public static double[] Dot3x3Matrix(double[] x, double[] y)
	{
		return new []
		{
			x[0] * y[0] + x[1] * y[3] + x[2] * y[6],
			x[0] * y[1] + x[1] * y[4] + x[2] * y[7],
			x[0] * y[2] + x[1] * y[5] + x[2] * y[8],

			x[3] * y[0] + x[4] * y[3] + x[5] * y[6],
			x[3] * y[1] + x[4] * y[4] + x[5] * y[7],
			x[3] * y[2] + x[4] * y[5] + x[5] * y[8],

			x[6] * y[0] + x[7] * y[3] + x[8] * y[6],
			x[6] * y[1] + x[7] * y[4] + x[8] * y[7],
			x[6] * y[2] + x[7] * y[5] + x[8] * y[8]
		};
	}

	public static double[] CalculateRotatingFrame(double[] rvec, double[] vvec)
	{
		double[] ri = Normalized(rvec);
		double[] hi = Normalized(Cross(ri, vvec));
		double[] ti = Normalized(Cross(hi,ri));
		double[] RFMatrix = {
			ri[0], ri[1], ri[2],
			ti[0], ti[1], ti[2],
			hi[0], hi[1], hi[2]
		};
		return RFMatrix;
	}
	
	// At rest or during radial motion, the orbital plane is undefined. Keep a
	// supplied display frame, or use inertial axes before a valid frame exists.
	public static double[] CalculateHillFrame(double[] rvec, double[] vvec, double[] previousFrame = null){
		double[] ri = Normalized(rvec);
		double[] normal = Cross(ri, vvec);
		if (!(Magnitude(normal) > EPS * Magnitude(vvec)))
			return previousFrame ?? new double[] {1, 0, 0, 0, 1, 0, 0, 0, 1};
		double[] hi = Normalized(normal);
		double[] ti = Normalized(Cross(hi,ri));
		double[] HFMatrix = {
			ri[0], ri[1], ri[2],
			ti[0], ti[1], ti[2],
			hi[0], hi[1], hi[2]
		};
		return HFMatrix;
	}

	public static double[] CalculateVelocityFrame(double[] rvec, double[] vvec){
		double[] vi = Normalized(vvec);
		double[] hi = Normalized(Cross(rvec, vvec));
		double[] ri = Normalized(Cross(vi, hi));
		double[] VFMatrix = {
			ri[0], ri[1], ri[2],
			vi[0], vi[1], vi[2],
			hi[0], hi[1], hi[2]
		};
		return VFMatrix;
	}

	public static double[] TransformFromUnityCStoBSK(double[] vec){
		return new [] {-vec[2], vec[0], vec[1]};
	}

	public static Vector3 TransformFromUnityCStoBSK(Vector3 vec)
	{
		return new Vector3(-vec.z, vec.x, vec.y);
	}

	public static double[] TransformFromBSKCStoUnity(double[] vec){
		// The Basilisk coordinate frame is right-handed with z up. Unity uses a left-handed coordinate frame with y up.
		// To change to right handed with y up, Basilisk position <p0,p1,p2> becomes the intermediate  right-handed frame <p1 ,p2, p0>
		// To change that intermediate frame to a left-handed frame with y up, x right,z into screen: 
		// the z component must be made negative leaving us with: <p1, p2, -p0>
		return new [] {vec[1], vec[2], -vec[0]};
	}
	
	public static Vector3 TransformFromBSKCStoUnity(Vector3 vec)
	{
		return new Vector3(vec.y, vec.z, -vec.x);
	}

	public static Quaternion ConvertRightHandedMRPtoLeftHandedQuaternion(double[] rightHandedMRP){
		//This method for converting MRP to quaternion was taken from the method MRP2EP of Basilisk/SimCode/Utilities/rigidBodyKinematics.c
		double ps = 1 + Dot(rightHandedMRP, rightHandedMRP);
		double[] sQtn = new double[4];

		// The Basilisk coordinate frame is right-handed with z up. Unity uses a left-handed coordinate frame with y up.
		// To change to right handed with y up, Basilisk quaternion <x,y,z,w> becomes the intermediate  right-handed frame <y,z,x,w>
		// To change that intermediate frame to a left-handed frame with y up, x right,z into screen: 
		// the z component must be made negative leaving us with: <y, z, -x, w >

		//Here the note above indicates the original ordering of the quaternion, but the final sQtn assignment is final left-handed Unity CS assignment
		//Calculating the scalar sQtn[3]
		sQtn [3] = (1 - Dot (rightHandedMRP, rightHandedMRP)) / ps;

		//Calculating the Unity x component (was Basilisk y)
		sQtn [0] = 2 * -rightHandedMRP[1] / ps;

		//Calculating the Unity y component (was Basilisk z)
		sQtn [1] = 2 * -rightHandedMRP[2] / ps;

		//Calculating the Unity z component (was Basilisk x, negated)
		sQtn [2] = 2 * rightHandedMRP[0] / ps;

		return new Quaternion((float)sQtn[0],(float)sQtn[1],(float) sQtn[2],(float) sQtn[3]);
	}

	public static Quaternion ConvertRightHandedDCMtoLeftHandedQuaternion(double[,] DCM)
	{
			//This method for converting Direction Cosine Matrices to quaternions was detailed in:
			// Farrell, Jay A. "Computation of the Quaternion from a Rotation Matrix," University of California, Riverside, November 30, 2015
			// Available at: http://www.ee.ucr.edu/~farrell/AidedNavigation/D_App_Quaternions/Rot2Quat.pdf
			double[] bQtn = new double[4];
			//Note that Unity's Mathf.Sqrt returns a float, so by including "using System" the .Net Math library is available
			//Calculate the scalar component of the quaternion, q3:
			bQtn[3] = 0.5*Math.Sqrt(1+DCM[0,0]+DCM[1,1]+DCM[2,2]);
			// The Basilisk coordinate frame is right-handed with z up. Unity uses a left-handed coordinate frame with y up.
			// To change to right handed with y up, Basilisk quaternion <q0,q1,q2,q3> becomes the intermediate  right-handed frame <q1,q2,q0,q3>
			// To change that intermediate frame to a left-handed frame with y up, x right,z into screen: 
			// the z component must be made negative leaving us with: <q1, q2, -q0, q3 >
		
			//Here the note above indicates the original ordering of the quaternion, but the final bQtn assignment is final left-handed Unity CS assignment
			//Calculate q0:
			bQtn[2] = -(DCM[2,1]-DCM[1,2])/(4.0*bQtn[3]);
			//Calculate q1:
			bQtn[0] = (DCM[0,2]-DCM[2,0])/(4.0*bQtn[3]);
			//Calculate q2:
			bQtn[1] = (DCM[1,0] - DCM[0,1])/(4.0*bQtn[3]);
		
			return new Quaternion((float) bQtn[0],(float) bQtn[1],(float) bQtn[2],(float) bQtn[3]);
	}
	
	public static double[] ConvertRightHandedMRPToRightHandedDCM(double[] MRP)
	{
			double[] DCM = {0,0,0, 0,0,0, 0,0,0};

			double q1 = MRP[0];
			double q2 = MRP[1];
			double q3 = MRP[2];

			double d1 = OrbitVectorMath.Dot(MRP,MRP);
			double S = 1 - d1;
			double d = (1+d1)*(1+d1);

			DCM[0]= 4 * (2 * q1 * q1 - d1) + S * S;
			DCM[1] = 8 * q1 * q2 + 4 * q3 * S;
			DCM[2] = 8 * q1 * q3 - 4 * q2 * S;
			DCM[3] = 8 * q2 * q1 - 4 * q3 * S;
			DCM[4] = 4 * (2 * q2 * q2 - d1) + S * S;
			DCM[5] = 8 * q2 * q3 + 4 * q1 * S;
			DCM[6] = 8 * q3 * q1 + 4 * q2 * S;
			DCM[7] = 8 * q3 * q2 - 4 * q1 * S;
			DCM[8] = 4 * (2 * q3 * q3 - d1) + S * S;

			for(int i = 0; i <9; i++){
				DCM[i] *= (1/d);
			}
			return DCM;
	}
	
	public static double[] CalculateStationOffsetJ2000_UnityCS(int bodyIndex, bool isSpacecraft, double[] stationOriginBSKCS)
	{
		double[] parentPositionUnity;
		double[] rightHandedDCM;
		if (isSpacecraft)
		{
			parentPositionUnity = SpacecraftStateUtilities.GetAbsSpacecraftPositionUnityCS(bodyIndex);
			double[] MRP = 
			{
				MessageList.CurrentMessage.Spacecraft[bodyIndex].Rotation[0],
				MessageList.CurrentMessage.Spacecraft[bodyIndex].Rotation[1],
				MessageList.CurrentMessage.Spacecraft[bodyIndex].Rotation[2]
			};

			rightHandedDCM = ConvertRightHandedMRPToRightHandedDCM(MRP); //BSK CS, Spacecraft Frame
		}
		else
		{
			parentPositionUnity = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(bodyIndex);
			double[,] spiceDCM = CelestialBodyStateUtilities.GetPlanetRotationDCM_BSK(bodyIndex); //BSK CS, Planet Frame
			rightHandedDCM = new []
			{
				spiceDCM[0, 0], spiceDCM[0, 1], spiceDCM[0, 2], 
				spiceDCM[1, 0], spiceDCM[1, 1], spiceDCM[1, 2],
				spiceDCM[2, 0], spiceDCM[2, 1], spiceDCM[2, 2]
			};
		}

		
		double[] rightHandedDCMTranspose = TransposeMatrix(rightHandedDCM); // BSK CS, J2000 Frame
		
		double[] stationOffsetJ2000_BSKCS = ApplyTransformationMatrixToVector(rightHandedDCMTranspose, stationOriginBSKCS);//BSK CS
		
		double[] stationOffsetJ2000_UnityCS = Add(parentPositionUnity, TransformFromBSKCStoUnity(stationOffsetJ2000_BSKCS)); //Unity CS J2000

		return stationOffsetJ2000_UnityCS;
	}
	
	public static int[] FindPrimaryBody(int thisIndex, bool isSpacecraft, bool test=false)
	{
		int nearbySmallBodyIndex = -1;

		int bigIndex = -1;
		int medIndex = -1;
		int smallIndex= -1;
		double bigF = 0;
		double medF = 0;
		double smallF = 0;
		double bigMu = 0;
		double medMu = 0;
		double smallMu = 0;

		double minDistanceSqd=1E6;

		double[] thisBodyPosition;
		if (isSpacecraft)
		{
			thisBodyPosition= SpacecraftStateUtilities.GetAbsSpacecraftPositionUnityCS(thisIndex);
		}
		else
		{
			thisBodyPosition = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(thisIndex);
		}

		for (int bodyIndex = 0; bodyIndex < MessageList.CurrentMessage.CelestialBodies.Count; bodyIndex++)
		{
			if ((isSpacecraft) || (thisIndex != bodyIndex))
			{
				double[] r_sc_to_p = Subtract(thisBodyPosition,
					CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(bodyIndex));
				double r_sc_to_p_sqd = Dot(r_sc_to_p, r_sc_to_p) / (1000 * 1000); //squared distance in km^2
				double bodyMu;
				if (test)
				{
					string bodyDictionaryName =
						CelestialBodyStateUtilities.FindCelestialBodyInDictionary(
							MessageList.CurrentMessage.CelestialBodies[bodyIndex].BodyName);
					bodyMu = CelestialBodyStateUtilities.GetMu(bodyDictionaryName);
				}
				else
				{
					GameObject body = CelestialBodyStateUtilities.CelestialBodiesList[bodyIndex];
					if (body.CompareTag("Sun"))
					{
						bodyMu = (float) CelestialBodyStateUtilities.GetMu("sun");
					}
					else
					{
						bodyMu = (body.GetComponent<PlanetController>().mu);
					}
				}

				double forceByBody = bodyMu / r_sc_to_p_sqd;
				if (forceByBody >= bigF)
				{
					smallF = medF;
					smallIndex = medIndex;
					smallMu = medMu;
					medF = bigF;
					medIndex = bigIndex;
					medMu = bigMu;
					bigF = forceByBody;
					bigIndex = bodyIndex;
					bigMu = bodyMu;
				}
				else if (forceByBody >= medF)
				{
					smallF = medF;
					smallIndex = medIndex;
					smallMu = medMu;
					medF = forceByBody;
					medIndex = bodyIndex;
					medMu = bodyMu;
				}
				else if (forceByBody >= smallF)
				{
					smallF = forceByBody;
					smallIndex = bodyIndex;
					smallMu = bodyMu;
				}

				if (r_sc_to_p_sqd <= minDistanceSqd)
				{
					minDistanceSqd = r_sc_to_p_sqd;
					nearbySmallBodyIndex = bodyIndex;
				}
			}
		}

		Tuple<int, double> firstCompareResultBody = SphereOfInfluence(thisBodyPosition, medIndex, medMu,  bigIndex, bigMu);
		Tuple<int, double> finalResultBody = SphereOfInfluence(thisBodyPosition, smallIndex, smallMu, firstCompareResultBody.Item1, firstCompareResultBody.Item2);
		
		int parentBodyIndex = finalResultBody.Item1;
		
		return new []{parentBodyIndex, nearbySmallBodyIndex};
	}

	public static Tuple<int, double> SphereOfInfluence(double[] spacecraftPosition, int body1Index, double body1Mu, int body2Index, double body2Mu){
		if ((body1Index != -1) && (body2Index != -1))
		{
			double[] body1Pos = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(body1Index);
			double[] body2Pos = CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(body2Index);
			double R_btwn_bodies = Magnitude(Subtract(body1Pos, body2Pos));

			if (body1Mu < body2Mu)
			{
				double r_small = Magnitude(Subtract(spacecraftPosition, body1Pos));
				double rSOI = R_btwn_bodies * Math.Pow(body1Mu / body2Mu, 0.4);
				if (r_small < rSOI)
				{
					return Tuple.Create(body1Index, body1Mu);
				}
				return Tuple.Create(body2Index, body2Mu);
			}
			else
			{
				double r_small = Magnitude(Subtract(spacecraftPosition, body2Pos));
				double rSOI = R_btwn_bodies * Math.Pow(body2Mu / body1Mu, 0.4);
				if (r_small < rSOI)
				{
					return Tuple.Create(body2Index, body2Mu);
				}
					
				return Tuple.Create(body1Index, body1Mu);
			}
		}
		return Tuple.Create(body2Index, body2Mu);
	}
	 public static Color HSL2RGB(double h, double sl, double l)
      {
	      //Courtesy of GeekyMonkey: https://geekymonkey.com/Programming/CSharp/RGB2HSL_HSL2RGB.htm
	      /* To select a rainbow of colors:
	       https://stackoverflow.com/questions/2288498/how-do-i-get-a-rainbow-color-gradient-in-c
	       Then you simply iterate over all of the possible values of the hue h while keeping the saturation s and luminosity l constant.
	       If you want 100 colors of the rainbow spaced out equally:
				for(double i = 0; i < 1; i+=0.01)
				{
					ColorRGB c = HSL2RGB(i, 0.5, 0.5);
				}
	       */
            double v;
            double r,g,b;
            
            r = l;   // default to gray
            g = l;
            b = l;
            v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);

            if (v > 0)
            {
                  double m;
                  double sv;
                  int sextant;

                  double fract, vsf, mid1, mid2;

                  m = l + l - v;
                  sv = (v - m ) / v;
                  h *= 6.0;
                  sextant = (int)h;
                  fract = h - sextant;
                  vsf = v * sv * fract;
                  mid1 = m + vsf;
                  mid2 = v - vsf;
                  switch (sextant)
                  {
                        case 0:
                              r = v;
                              g = mid1;
                              b = m;
                              break;

                        case 1:
                              r = mid2;
                              g = v;
                              b = m;
                              break;

                        case 2:
                              r = m;
                              g = v;
                              b = mid1;

                              break;

                        case 3:
                              r = m;
                              g = mid2;
                              b = v;
                              break;

                        case 4:
                              r = mid1;
                              g = m;
                              b = v;

                              break;

                        case 5:
                              r = v;
                              g = m;
                              b = mid2;

                              break;

                  }
            }
            return new Color((float)r, (float) g, (float) b, 1.0f);
      }

      public static double[] CalculateCenterOfMass(double[] vec1, double[] vec2, double mu1, double mu2)
      {
	      return ScaleVector(Add(ScaleVector(vec1, mu1), ScaleVector(vec2, mu2)), (1 / (mu1 + mu2)));
      }

      public static double[] CalculatedRightHandedMRPFromEulerAngles(double q1, double q2, double q3)
      {
	      //Euler angles must be in radians
	      //First calculate 3-2-1 euler parameter vector Q
	      double b0 = Math.Cos(q1 / 2) * Math.Cos(q2 / 2) * Math.Cos(q3 / 2) +
	                  Math.Sin(q1 / 2) * Math.Sin(q2 / 2) * Math.Sin(q3 / 2);
	      
	      double b1 = Math.Cos(q1 / 2) * Math.Cos(q2 / 2) * Math.Sin(q3 / 2) -
	                  Math.Sin(q1 / 2) * Math.Sin(q2 / 2) * Math.Cos(q3 / 2);
	      
	      double b2 = Math.Cos(q1 / 2) * Math.Sin(q2 / 2) * Math.Cos(q3 / 2) + 
	                  Math.Sin(q1 / 2) * Math.Cos(q2 / 2) * Math.Sin(q3 / 2);
	      
	      double b3 = Math.Sin(q1 / 2) * Math.Cos(q2 / 2) * Math.Cos(q3 / 2) -
	                  Math.Cos(q1 / 2) * Math.Sin(q2 / 2) * Math.Sin(q3 / 2);
	      
	      //Now convert to Right-handed MRP
	      if (b0 < 0)
	      {
		      b0 *= -1;
		      b1 *= -1;
		      b2 *= -1;
		      b3 *= -1;
	      }

	      double MRP1 = b1 / (1 + b0);
	      double MRP2 = b2 / (1 + b0);
	      double MRP3 = b3 / (1 + b0);

	      return new [] {MRP1, MRP2, MRP3};
      }
}
