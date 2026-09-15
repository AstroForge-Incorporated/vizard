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
using VizProtobufferMessage;

/// <summary>
///  Assembles and maintains position history for scenario object
///  and transforms those positions into the current frame
///  specified by user
/// </summary>
public class TruePathOrbitLine : MonoBehaviour
{
    [HideInInspector] public TruePathLinePlotter truePathLinePlotter;
    protected int SpacecraftIndex;
    protected GameObject SpacecraftObject;
    private int indexOfRelativeSpacecraft = -1;
    private int indexOfRelativeBody = -1;

    private int lastFrameTruePathLineMode;
    private int lastFrameSpacecraftRelMode;
    private int visibleHistoryUpdateCount = -1; //Tracks message buffer updates
    private int relativeTruePathChangeCount = -1; //Tracks changes in true path trajectory settings

    private double[,] mySpacecraftPositionHistoryBSK;
    private double[,] relativeToBodyPositionsUnity;

    private double[,] relativeToRotatingFrameBodyPositionsBSK= {};
    private double[,] rotatingFramePositions= {};
    private double[,] rotatingFrameDCMs= {};

    private double[,] scRelativeToFixedBodyPositionsFixedBSK= {};
    private double[,] fixedFrameDCMs= {};
    
    private static bool alreadyInAppend;

    void Awake()
    {
        truePathLinePlotter = GetComponent<TruePathLinePlotter>();
        if (truePathLinePlotter != null)
        {
            truePathLinePlotter.isOrbitLine = true;
        }
    }

    public void InitializeTruePathLine(GameObject scObject, int scIndex, bool isUnitTest = false)
    {
        SpacecraftObject = scObject;
        SpacecraftIndex = scIndex;
        indexOfRelativeBody = SpacecraftObject.GetComponent<SpacecraftController>().spacecraftParentBodyIndex;
        indexOfRelativeSpacecraft = VizardGUISettings.ChiefSpacecraftIndex;
        truePathLinePlotter.InitializeDrawTruePathLine(isUnitTest);
    }

    private void UpdateAllCounters()
    {
        visibleHistoryUpdateCount = MessageList.VisibleHistoryUpdateCount;
        relativeTruePathChangeCount = VizardGUISettings.RelativeTruePathChangeCount;
        lastFrameTruePathLineMode = VizardGUISettings.TruePathLineMode;
        lastFrameSpacecraftRelMode = VizardGUISettings.SpacecraftRelativeOrbitMode;
    }

    private void BuildPositionHistoryBSK()
    {
        mySpacecraftPositionHistoryBSK = MessageList.GetPositionHistoryBSK(true, SpacecraftIndex);
    }
    
    private void BuildRelativePositionHistory()
    {
        switch (VizardGUISettings.TruePathLineMode)
        {
            case 1:
                if (!CelestialBodyStateUtilities.ViewIsLocal)
                {
                    indexOfRelativeBody = CelestialBodyStateUtilities.SunIndex;
                    BuildRelativeBodyData();
                }

                break;
            case 2: //spacecraft relative
                indexOfRelativeSpacecraft = VizardGUISettings.ChiefSpacecraftIndex;
                indexOfRelativeBody = OrbitVectorMath.FindPrimaryBody(indexOfRelativeSpacecraft, true)[0];
                
                if (VizardGUISettings.SpacecraftRelativeOrbitMode == 3) //inertial relative --> Build Relative Points
                {
                    BuildRelativeBodyData();
                }
                else //Hill Frame or Velocity Frame
                {
                    BuildRotationFrameData();
                }

                break;
            case 3: //celestial body relative
                indexOfRelativeBody = VizardGUISettings.RelativeBodyIndex;
                if (VizardGUISettings.UseSpacecraftParentBodyForRelativeTraj)
                {
                    indexOfRelativeBody = SpacecraftObject.GetComponent<SpacecraftController>()
                        .spacecraftParentBodyIndex;
                }

                BuildRelativeBodyData();
                break;
            case 4: //rotating frame
                BuildRotationFrameData();
                break;
            case 5: //fixed frame
                indexOfRelativeBody = VizardGUISettings.FixedBodyIndex;
                if (VizardGUISettings.UseSpacecraftParentBodyForFixedFrameTraj &&
                    !VizardGUISettings.FixedBodyIsSpacecraft)
                {
                    indexOfRelativeBody = SpacecraftObject.GetComponent<SpacecraftController>()
                        .spacecraftParentBodyIndex;
                }

                BuildFixedFrameData();
                break;
            default:
                Debug.Log($"The true path line mode of {lastFrameTruePathLineMode} is not implemented.");
                break;
        }
    }

    void FixedUpdate()
    {
        UpdateHistoriesAndPointsToDraw();
    }

    private void UpdateHistoriesAndPointsToDraw()
    {
        if ((VizardGUISettings.TruePathLinesVisible) && (!MessageList.InBufferLoad))
        {
            if (visibleHistoryUpdateCount != MessageList.VisibleHistoryUpdateCount) //This happens when buffer changes
            {
                
                SpacecraftStateUtilities.UpdateChiefSpacecraft(VizardGUISettings.ChiefSpacecraftIndex);
                BuildPositionHistoryBSK();
                truePathLinePlotter.BuildTrajectoryColorHistory(SpacecraftIndex);
                BuildRelativePositionHistory();
            } // User has changed relative true path trajectory settings
            else if ((lastFrameTruePathLineMode != VizardGUISettings.TruePathLineMode) ||
                     (VizardGUISettings.RelativeTruePathChangeCount != relativeTruePathChangeCount) ||
                     (lastFrameSpacecraftRelMode !=
                      VizardGUISettings.SpacecraftRelativeOrbitMode)) //captures rotating frame 
            {
                BuildRelativePositionHistory();
            } // chief spacecraft parent body has changed and chief spacecraft relative 
            else if ((lastFrameTruePathLineMode==2)&&(indexOfRelativeBody!=SpacecraftStateUtilities.SpacecraftList[indexOfRelativeSpacecraft].GetComponent<SpacecraftController>()
                         .spacecraftParentBodyIndex))
            {
                SpacecraftStateUtilities.UpdateChiefSpacecraft(indexOfRelativeSpacecraft, true);
                BuildRelativePositionHistory();
            }
            //Spacecraft parent body has changed and user desires trajectory relative to parent body
            else if ((lastFrameTruePathLineMode == 3) && (VizardGUISettings.UseSpacecraftParentBodyForRelativeTraj) &&
                     (indexOfRelativeBody != SpacecraftObject.GetComponent<SpacecraftController>()
                         .spacecraftParentBodyIndex))
            {
                indexOfRelativeBody = SpacecraftObject.GetComponent<SpacecraftController>()
                    .spacecraftParentBodyIndex;
                BuildRelativePositionHistory();
            }
            else if ((lastFrameTruePathLineMode == 5) && (VizardGUISettings.UseSpacecraftParentBodyForFixedFrameTraj) &&
                     (indexOfRelativeBody != SpacecraftObject.GetComponent<SpacecraftController>()
                         .spacecraftParentBodyIndex))
            {
                BuildRelativePositionHistory();
            }
            else if ((DataManager.IsLiveSim) &&
                     (mySpacecraftPositionHistoryBSK.GetLength(0) != MessageList.TimestepsTotal))
            {
                if (!alreadyInAppend)
                {
                    AppendNewMessagePositionToLists();
                }
            }

            UpdateAllCounters();
            UpdatePointsToDraw();
        }
    }

private void BuildRelativeBodyData() //The relative body can be a spacecraft (true line path mode = 2) or a celestial body (true line path mode = 3)
    {
        if (VizardGUISettings.TruePathLineMode == 2)
        {
            relativeToBodyPositionsUnity = MessageList.GetPositionHistoryBSK(true, indexOfRelativeSpacecraft);
        }
        else
        {
            relativeToBodyPositionsUnity = MessageList.GetPositionHistoryBSK(false, indexOfRelativeBody);
        }

        double[] bodyPosition = {0, 0, 0};
        for (int i = 0; i < Mathf.Min(mySpacecraftPositionHistoryBSK.GetLength(0),relativeToBodyPositionsUnity.GetLength(0)); i++)
        {
            for (int j = 0; j < 3; j++)
            {
                bodyPosition[j] = mySpacecraftPositionHistoryBSK[i, j] - relativeToBodyPositionsUnity[i, j];
            }

            relativeToBodyPositionsUnity[i, 0] = bodyPosition[1];
            relativeToBodyPositionsUnity[i, 1] = bodyPosition[2];
            relativeToBodyPositionsUnity[i, 2] = -bodyPosition[0];
        }
    }
    
    private void BuildRotationFrameData()
    {
        if(VizardGUISettings.TruePathLineMode==2) //spacecraft relative (Hill or velocity)
        {
            SpacecraftStateUtilities.UpdateChiefSpacecraft(indexOfRelativeSpacecraft);
            rotatingFramePositions = SpacecraftStateUtilities.ChiefPositions;
            rotatingFrameDCMs = SpacecraftStateUtilities.ChiefDCMs;
        }
        else //celestial body rotating frame
        {
            rotatingFramePositions = CelestialBodyStateUtilities.CenterOfMassPositions;
            rotatingFrameDCMs = CelestialBodyStateUtilities.CenterOfMassDCMs;
        }

        if (relativeToRotatingFrameBodyPositionsBSK.GetLength(0) != rotatingFramePositions.GetLength(0))
        {
            relativeToRotatingFrameBodyPositionsBSK = new double[rotatingFramePositions.GetLength(0), 3];
        }

        double[] rrel = {0, 0, 0};
        double[] DCM = new double[9];
        for (int i = 0; i < Mathf.Min(mySpacecraftPositionHistoryBSK.GetLength(0),rotatingFramePositions.GetLength(0)); i++)
        {
            for (int j = 0; j < 3; j++)
            {
                rrel[j] = mySpacecraftPositionHistoryBSK[i, j] - rotatingFramePositions[i, j];
            }

            for (int j = 0; j < 9; j++)
            {
                DCM[j] = rotatingFrameDCMs[i, j];
            }
            rrel = OrbitVectorMath.ApplyTransformationMatrixToVector(DCM, rrel);
            
            for (int j = 0; j < 3; j++)
            {
                relativeToRotatingFrameBodyPositionsBSK[i, j] = rrel[j];
            }
        }
    }

    private void BuildFixedFrameData()
    {
        scRelativeToFixedBodyPositionsFixedBSK = MessageList.GetPositionHistoryBSK(VizardGUISettings.FixedBodyIsSpacecraft, indexOfRelativeBody);
        fixedFrameDCMs = MessageList.GetRotationHistoryDCM_BSK(VizardGUISettings.FixedBodyIsSpacecraft, indexOfRelativeBody);

        double[] relativePositionBSK = {0, 0, 0};
        double[] fixedFrameDCM = new double[9];
        for (int i = 0; i < Mathf.Min(scRelativeToFixedBodyPositionsFixedBSK.GetLength(0), mySpacecraftPositionHistoryBSK.GetLength(0)); i++)
        {
            for (int j = 0; j < 9; j++)
            {
                fixedFrameDCM[j] = fixedFrameDCMs[i, j];
            }
            for (int j = 0; j < 3; j++)
            {
                relativePositionBSK[j] =
                    mySpacecraftPositionHistoryBSK[i, j] - scRelativeToFixedBodyPositionsFixedBSK[i, j];
            }
            
            relativePositionBSK = OrbitVectorMath.ApplyTransformationMatrixToVector(fixedFrameDCM, relativePositionBSK);

            for (int j = 0; j < 3; j++)
            {
                scRelativeToFixedBodyPositionsFixedBSK[i, j] = relativePositionBSK[j];
            }
        }
    }
    
    

    private void UpdatePointsToDraw()
    {
        if (truePathLinePlotter.pointsToDrawOnscreen.Length != mySpacecraftPositionHistoryBSK.GetLength(0))
        {
            truePathLinePlotter.pointsToDrawOnscreen = new Vector3[mySpacecraftPositionHistoryBSK.GetLength(0)];
        }
        switch (lastFrameTruePathLineMode)
        {
            case 1:
                CalculatePointsToPlot_CameraTargetCorrectionOnly();
                break;
            case 2:
                if (lastFrameSpacecraftRelMode == 3) //Spacecraft relative inertial
                {
                    CalculatePointsToPlot_BodyRelative();
                }else{ //Hill Frame or Velocity Frame, Spacecraft Relative
                    CalculatePointsToPlot_BodyFrame();
                }
                break;
            case 3: //Celestial body relative
                CalculatePointsToPlot_BodyRelative();
                break;
            case 4: //Rotating frame
                CalculatePointsToPlot_BodyFrame();
                break;
            case 5: //Fixed Frame
                CalculatePointsToPlot_FixedFrame();
                break;
        }
        truePathLinePlotter.PlotPointsTruePathLineRenderers();
    }

    private void CalculatePointsToPlot_CameraTargetCorrectionOnly()
    {
        double currentScale = CelestialBodyStateUtilities.GetCurrentScale();
        if (!CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
        {
            currentScale = 1 / currentScale;
        }

        if (CelestialBodyStateUtilities.ViewIsLocal)
        {
            double[] currentCameraTargetPositionUnityMeters = MainCameraUtilities.GetCameraTargetAbsolutePositionUnityCS();

            double[] scaledRelPosition = {0, 0, 0};
            for (int i = 0; i < mySpacecraftPositionHistoryBSK.GetLength(0); i++)
            {
                //Transform to Unity CS and subtract off currentCameraTargetPosition which is already in Unity CS
                //And scale by currentScale
                scaledRelPosition[0] = currentScale*(mySpacecraftPositionHistoryBSK[i, 1] - currentCameraTargetPositionUnityMeters[0]);
                scaledRelPosition[1] = currentScale*(mySpacecraftPositionHistoryBSK[i, 2] - currentCameraTargetPositionUnityMeters[1]);
                scaledRelPosition[2] = currentScale*(-mySpacecraftPositionHistoryBSK[i, 0] - currentCameraTargetPositionUnityMeters[2]);
                truePathLinePlotter.pointsToDrawOnscreen[i]=OrbitVectorMath.ReturnVector3(scaledRelPosition);
            }
        }
        else
        {
            double[] scaledRelPosition = {0, 0, 0};
            for (int i = 0; i < Mathf.Min(mySpacecraftPositionHistoryBSK.GetLength(0),relativeToBodyPositionsUnity.GetLength(0)); i++)
            {
                // Scale by current scale and transform to Unity CS from BSK
                scaledRelPosition[0] = currentScale * (mySpacecraftPositionHistoryBSK[i, 1]);
                scaledRelPosition[1] = currentScale * (mySpacecraftPositionHistoryBSK[i, 2]);
                scaledRelPosition[2] = currentScale * (-mySpacecraftPositionHistoryBSK[i, 0]);
                truePathLinePlotter.pointsToDrawOnscreen[i] = OrbitVectorMath.ReturnVector3(scaledRelPosition); 
            }
        }
        if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
        {
            int parentBodyIndex = SpacecraftObject.GetComponent<SpacecraftController>()
                .spacecraftParentBodyIndex;
            GameObject parentBody = CelestialBodyStateUtilities.GetCelestialBodyObject(parentBodyIndex);
            Vector3 planetCenterCoordAbsUnityUnits = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.Subtract(
                    CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(parentBodyIndex),
                    MainCameraUtilities.GetCameraTargetAbsolutePositionUnityCS()));
            if (planetCenterCoordAbsUnityUnits.magnitude < 100000f)
            {
                float ratioWallDistToTrueDist = 1f;
                if (parentBodyIndex != CelestialBodyStateUtilities.SunIndex)
                {
                    ratioWallDistToTrueDist = (float) parentBody.GetComponent<PlanetController>()
                        .GetRatioProjectionToTrueDistanceFromCam();
                }
                else
                {
                    ratioWallDistToTrueDist = (float) parentBody.GetComponent<SunBuilder>()
                        .GetRatioProjectionToTrueDistanceFromCam();
                }

                Vector3 asDrawnPlanetCenterUnityUnits = parentBody.transform.position;
                if ((MainCameraUtilities.TrueCameraDistanceToTargetMeters >
                     10f))
                {
                    for (int i = 0; i < (mySpacecraftPositionHistoryBSK.GetLength(0)); i++)
                    {
                        Vector3 vectorFromPlanetCenterToPoint =
                            truePathLinePlotter.pointsToDrawOnscreen[i] - planetCenterCoordAbsUnityUnits * (float) currentScale;
                        truePathLinePlotter.pointsToDrawOnscreen[i] = asDrawnPlanetCenterUnityUnits +
                                                                   vectorFromPlanetCenterToPoint * ratioWallDistToTrueDist;
                    }
                }
            }
        }
    }
    
       private void CalculatePointsToPlot_BodyFrame()
    {
        if ((lastFrameTruePathLineMode == 2)&&(indexOfRelativeSpacecraft==SpacecraftIndex))
        {
            truePathLinePlotter.pointsToDrawOnscreen = new Vector3[]{};
        }
        else
        {
            int currentIndex = MessageList.CurrentIndex - MessageList.FirstMessageIndexOfPlottedMessages;
            if ((currentIndex >= 0) && (currentIndex < mySpacecraftPositionHistoryBSK.GetLength(0)))
            {
                double currentScale = CelestialBodyStateUtilities.GetCurrentScale();
                if (!CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    currentScale = 1f / currentScale;
                }
                double[] currentCamTargetPosition = {0, 0, 0};
                if (CelestialBodyStateUtilities.ViewIsLocal)
                {
                    currentCamTargetPosition = MainCameraUtilities.GetCameraTargetAbsolutePositionBSK();
                }
                double[] body0pos = new double[3];
                double[] DCM_T0 = {1,0,0,0,1,0,0,0,1};
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        body0pos[i] = rotatingFramePositions[currentIndex,i];
                    }

                    for (int i = 0; i < 9; i++)
                    {
                        DCM_T0[i] = rotatingFrameDCMs[currentIndex,i];
                    }
                }
                catch
                {
                    Debug.Log(
                        $"Broken for current Index of {currentIndex} given MessageList.CurrentIndex: {MessageList.CurrentIndex} and FirstIndex of Plotted messages ({MessageList.FirstMessageIndexOfPlottedMessages}");
                }
                double[] DCMTranspose_T0 = OrbitVectorMath.TransposeMatrix(DCM_T0);
                
                double[] relativeBodyPosToCamTgt = OrbitVectorMath.Subtract(body0pos, currentCamTargetPosition);

                double[] posRelativeToCamTgt = {0, 0, 0};
                for (int i = 0; i < relativeToRotatingFrameBodyPositionsBSK.GetLength(0); i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        posRelativeToCamTgt[j] = relativeToRotatingFrameBodyPositionsBSK[i, j];
                    }

                    //Calculate relative position of the deputy to the chief
                    posRelativeToCamTgt =
                        OrbitVectorMath.ApplyTransformationMatrixToVector(DCMTranspose_T0, posRelativeToCamTgt);

                    //add the offset from the camera target to chief and scale it by current scale
                    for (int j = 0; j < 3; j++)
                    {
                        posRelativeToCamTgt[j] += relativeBodyPosToCamTgt[j];
                        posRelativeToCamTgt[j] *= currentScale;
                    }

                    //rotate into Unity frame, and convert to Vector3
                    truePathLinePlotter.pointsToDrawOnscreen[i] = new Vector3(
                        (float) posRelativeToCamTgt[1],
                        (float) posRelativeToCamTgt[2],
                        -(float) posRelativeToCamTgt[0]
                    );
                }

                if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    int parentBodyIndex = SpacecraftObject.GetComponent<SpacecraftController>()
                        .spacecraftParentBodyIndex;
                    GameObject parentBody = CelestialBodyStateUtilities.GetCelestialBodyObject(parentBodyIndex);
                    float ratioWallDistToTrueDist = 1f;
                    if (parentBodyIndex != CelestialBodyStateUtilities.SunIndex)
                    {
                        ratioWallDistToTrueDist = (float) parentBody.GetComponent<PlanetController>()
                            .GetRatioProjectionToTrueDistanceFromCam();
                    }
                    else
                    {
                        ratioWallDistToTrueDist = (float) parentBody.GetComponent<SunBuilder>()
                            .GetRatioProjectionToTrueDistanceFromCam();
                    }
                    Vector3 planetCenterCoordAbsUnityUnits = OrbitVectorMath.ReturnVector3(
                        OrbitVectorMath.Subtract(
                            CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(parentBodyIndex),
                            MainCameraUtilities.GetCameraTargetAbsolutePositionUnityCS()));
                    Vector3 asDrawnPlanetCenterUnityUnits = parentBody.transform.position;
                    if ((MainCameraUtilities.TrueCameraDistanceToTargetMeters > MainCameraUtilities.LineAndSpriteProjectionCorrectionThreshold))
                    {
                        for (int i = 0; i < (mySpacecraftPositionHistoryBSK.GetLength(0)); i++)
                        {
                            Vector3 vectorFromPlanetCenterToPoint = truePathLinePlotter.pointsToDrawOnscreen[i] - planetCenterCoordAbsUnityUnits;
                            truePathLinePlotter.pointsToDrawOnscreen[i]=asDrawnPlanetCenterUnityUnits +
                                                                     vectorFromPlanetCenterToPoint * ratioWallDistToTrueDist;
                        }
                    }
                }
            }
        }
    }

    private void CalculatePointsToPlot_BodyRelative()
    {
        bool relativeBodyIsSpacecraft = VizardGUISettings.TruePathLineMode == 2;
        
        if (relativeBodyIsSpacecraft && (indexOfRelativeSpacecraft == SpacecraftIndex))
        {
            truePathLinePlotter.pointsToDrawOnscreen = new Vector3[] {};
        }
        else
        {
            int currentIndex = MessageList.CurrentIndex - MessageList.FirstMessageIndexOfPlottedMessages;
            if ((currentIndex >= 0) && (currentIndex < mySpacecraftPositionHistoryBSK.GetLength(0)))
            {
                if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                {
                    GameObject bodyObject = relativeBodyIsSpacecraft
                        ? SpacecraftStateUtilities.GetSpacecraftObject(indexOfRelativeSpacecraft)
                        : CelestialBodyStateUtilities.GetCelestialBodyObject(indexOfRelativeBody);

                    float ratioWallDistToTrueDist = 1000f;
                    if (relativeBodyIsSpacecraft)
                    {
                        ratioWallDistToTrueDist = (float)bodyObject.GetComponent<SpacecraftController>()
                            .GetRatioProjectionToTrueDistanceFromCam();
                    }
                    else
                    {
                        if (indexOfRelativeBody != CelestialBodyStateUtilities.SunIndex)
                        {
                            ratioWallDistToTrueDist =
                                (float) bodyObject.GetComponent<PlanetController>()
                                    .GetRatioProjectionToTrueDistanceFromCam();
                        }
                        else
                        {
                            ratioWallDistToTrueDist =
                                (float) bodyObject.GetComponent<SunBuilder>().GetRatioProjectionToTrueDistanceFromCam();
                        }
                        
                    }
                    ratioWallDistToTrueDist*=(float)CelestialBodyStateUtilities.GetCurrentScale();
                    Vector3 asDrawnBodyCenterUnityUnits = bodyObject.transform.position;
                    for (int i = 0; i < (relativeToBodyPositionsUnity.GetLength(0)); i++)
                    {
                        Vector3 relativeToBodyPositionUnity = new Vector3(
                            (float) relativeToBodyPositionsUnity[i, 0],
                            (float) relativeToBodyPositionsUnity[i, 1],
                            (float) relativeToBodyPositionsUnity[i, 2]
                        );
                        truePathLinePlotter.pointsToDrawOnscreen[i]=asDrawnBodyCenterUnityUnits +
                                                                 relativeToBodyPositionUnity * ratioWallDistToTrueDist; 
                    }
                    truePathLinePlotter.pointsToDrawOnscreen[currentIndex] = SpacecraftObject.transform.position;
                }
                else
                {
                    double currentScale = 1/CelestialBodyStateUtilities.GetCurrentScale();
                    if (CelestialBodyStateUtilities.ViewIsLocal)
                    {
                     Vector3 bodyCurrentRelativePosition  = OrbitVectorMath.ReturnVector3(
                         MainCameraUtilities.GetScaledObjectPositionRelToCamTgt(relativeBodyIsSpacecraft? indexOfRelativeSpacecraft: indexOfRelativeBody, relativeBodyIsSpacecraft));
                     for (int i = 0; i < relativeToBodyPositionsUnity.GetLength(0); i++)
                     {
                         Vector3 relativeToBodyPositionUnity = new Vector3(
                             (float) (relativeToBodyPositionsUnity[i, 0]*currentScale),
                             (float) (relativeToBodyPositionsUnity[i, 1]*currentScale),
                             (float) (relativeToBodyPositionsUnity[i, 2]*currentScale)
                         );
                         truePathLinePlotter.pointsToDrawOnscreen[i]=relativeToBodyPositionUnity+ bodyCurrentRelativePosition;
                     }
                    }
                    else
                    {
                        GameObject bodyObject = relativeBodyIsSpacecraft
                            ? SpacecraftStateUtilities.GetSpacecraftObject(indexOfRelativeSpacecraft)
                            : CelestialBodyStateUtilities.GetCelestialBodyObject(indexOfRelativeBody);
   
                            Vector3 bodyPosition = bodyObject.transform.position;
                            for (int i = 0; i < relativeToBodyPositionsUnity.GetLength(0); i++)
                            {
                                Vector3 relativeToBodyPositionUnity = new Vector3(
                                    (float) (relativeToBodyPositionsUnity[i, 0]*currentScale),
                                    (float) (relativeToBodyPositionsUnity[i, 1]*currentScale),
                                    (float) (relativeToBodyPositionsUnity[i, 2]*currentScale)
                                );
                                truePathLinePlotter.pointsToDrawOnscreen[i]=relativeToBodyPositionUnity+bodyPosition;
                            }
                    }
                }
            }
        }
    }

    private void CalculatePointsToPlot_FixedFrame()
    {
         if ((VizardGUISettings.FixedBodyIsSpacecraft)&&(indexOfRelativeBody==SpacecraftIndex))
         {
             truePathLinePlotter.pointsToDrawOnscreen = new Vector3[] {};
         }
         else
         {
             int currentIndex = MessageList.CurrentIndex - MessageList.FirstMessageIndexOfPlottedMessages;
             if ((currentIndex >= 0) && (currentIndex < mySpacecraftPositionHistoryBSK.GetLength(0)))
             {
                 double currentScale = CelestialBodyStateUtilities.GetCurrentScale();
                 if (!CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                 {
                     currentScale = 1f / currentScale;
                 }

                 double[] currentCamTargetPositionInertialBSK =
                     MainCameraUtilities.GetCameraTargetAbsolutePositionBSK();

                 double[] fixedBodyCurrentPositionInertialBSK = new double[3];
                 if (VizardGUISettings.FixedBodyIsSpacecraft)
                 {
                     fixedBodyCurrentPositionInertialBSK = MessageList.CurrentMessage.Spacecraft[indexOfRelativeBody].Position.ToArray();
                 }
                 else
                 {
                     fixedBodyCurrentPositionInertialBSK = MessageList.CurrentMessage.CelestialBodies[indexOfRelativeBody].Position.ToArray();
                 }
                
                 double[] DCM_T_CurrentIndex = {1,0,0,0,1,0,0,0,1};
                 try
                 {
                     for (int i = 0; i < 9; i++)
                     {
                         DCM_T_CurrentIndex[i] = fixedFrameDCMs[currentIndex,i];
                     }

                     DCM_T_CurrentIndex = OrbitVectorMath.TransposeMatrix(DCM_T_CurrentIndex);
                 }
                 catch
                 {
                     Debug.Log(
                         $"Broken for current Index of {currentIndex} given MessageList.CurrentIndex: {MessageList.CurrentIndex} and FirstIndex of Plotted messages ({MessageList.FirstMessageIndexOfPlottedMessages}");
                 }
               
                 double[] relativeBodyPosToCamTgt = OrbitVectorMath.Subtract(fixedBodyCurrentPositionInertialBSK, currentCamTargetPositionInertialBSK);

                 double[] positionRelativeToCamTgt = {0, 0, 0};
                 for (int i = 0; i < Mathf.Min(scRelativeToFixedBodyPositionsFixedBSK.GetLength(0), mySpacecraftPositionHistoryBSK.GetLength(0)); i++)
                 {
                     //Calculate relative position in the fixed frame at current index
                     for (int j = 0; j < 3; j++)
                     {
                         positionRelativeToCamTgt[j] = scRelativeToFixedBodyPositionsFixedBSK[i, j];
                     }
                     positionRelativeToCamTgt = 
                         OrbitVectorMath.ApplyTransformationMatrixToVector(DCM_T_CurrentIndex, positionRelativeToCamTgt);
                     //add the offset from the camera target to fixed frame body
                     //Scale it by the current world scale,
                     for (int j = 0; j < 3; j++)
                     {
                         positionRelativeToCamTgt[j] += relativeBodyPosToCamTgt[j];
                         positionRelativeToCamTgt[j] *= currentScale;
                     }

                     //rotate into Unity frame, convert to Vector3, and add to list of points to plot
                     Vector3 position = new Vector3(
                         (float) positionRelativeToCamTgt[1],
                         (float) positionRelativeToCamTgt[2],
                         -(float) positionRelativeToCamTgt[0]
                     );
                     truePathLinePlotter.pointsToDrawOnscreen[i] = position;
                 }

                 if (CelestialBodyStateUtilities.ViewIsSpacecraftLocal)
                 {
                     int parentBodyIndex = SpacecraftObject.GetComponent<SpacecraftController>()
                         .spacecraftParentBodyIndex;
                     GameObject parentBody = CelestialBodyStateUtilities.GetCelestialBodyObject(parentBodyIndex);
                     Vector3 planetCenterCoordAbsUnityUnits = OrbitVectorMath.ReturnVector3(
                         OrbitVectorMath.Subtract(
                             CelestialBodyStateUtilities.GetAbsolutePlanetPositionUnityCS(parentBodyIndex),
                             MainCameraUtilities.GetCameraTargetAbsolutePositionUnityCS()));
                     float ratioWallDistToTrueDist = 1f;
                     if (parentBodyIndex != CelestialBodyStateUtilities.SunIndex)
                     {
                         ratioWallDistToTrueDist = (float) parentBody.GetComponent<PlanetController>()
                             .GetRatioProjectionToTrueDistanceFromCam();
                     }
                     else
                     {
                         ratioWallDistToTrueDist = (float) parentBody.GetComponent<SunBuilder>()
                             .GetRatioProjectionToTrueDistanceFromCam();
                     }
                     Vector3 asDrawnPlanetCenterUnityUnits = parentBody.transform.position;
                     if ((MainCameraUtilities.TrueCameraDistanceToTargetMeters > MainCameraUtilities.LineAndSpriteProjectionCorrectionThreshold))
                     {
                         for (int i = 0; i < (mySpacecraftPositionHistoryBSK.GetLength(0)); i++)
                         {
                             Vector3 vectorFromPlanetCenterToPoint = truePathLinePlotter.pointsToDrawOnscreen[i] - planetCenterCoordAbsUnityUnits;
                             truePathLinePlotter.pointsToDrawOnscreen[i]=asDrawnPlanetCenterUnityUnits +
                                                                         vectorFromPlanetCenterToPoint * ratioWallDistToTrueDist;
                         }
                     }
                 }
             }
         }
    }
    
    private void AppendNewMessagePositionToLists()
    {
        int currentMsgCount = MessageList.TimestepsTotal;
        double[,] copySpacecraftPositionHistoryBSK = new double[currentMsgCount, 3];
        for (int i = 0; i < mySpacecraftPositionHistoryBSK.GetLength(0); i++)
        {
            for (int j = 0; j < 3; j++)
            {
                copySpacecraftPositionHistoryBSK[i, j] = mySpacecraftPositionHistoryBSK[i, j];
            }
        }

        for (int k = mySpacecraftPositionHistoryBSK.GetLength(0); k < currentMsgCount; k++)
        {
            alreadyInAppend = true;
            VizMessage newMsg = MessageList.GetMessageAtIndex(k);
            if (newMsg != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    copySpacecraftPositionHistoryBSK[k, i] = newMsg.Spacecraft[SpacecraftIndex].Position[i];
                }
            }
        }

        switch (lastFrameTruePathLineMode)
                {
                    case 2: //spacecraft relative
                        if (lastFrameSpacecraftRelMode == 3) //inertial relative --> Build Relative Points
                        {
                            SpacecraftStateUtilities.AppendChiefSpacecraftData(currentMsgCount);
                            AppendRelativeBodyData(currentMsgCount);
                        }
                        else //Hill Frame or Velocity Frame
                        {
                            AppendRotationFrameData(currentMsgCount);
                            
                        }

                        break;
                    case 3: //celestial body relative
                        AppendRelativeBodyData(currentMsgCount);
                        break;
                    case 4: //rotating frame
                        CelestialBodyStateUtilities.AppendCOMData(currentMsgCount);
                        AppendRotationFrameData(currentMsgCount);
                        break;
                    case 5: //fixed frame
                        AppendFixedFrameData(currentMsgCount);
                        break;
                }
        
        mySpacecraftPositionHistoryBSK = copySpacecraftPositionHistoryBSK;
        alreadyInAppend = false;
    }

    private void AppendRelativeBodyData(int currentMsgCount)
    {
        double[,] copyRelativeToBodyPositionsUnity = new double[currentMsgCount, 3];
        int oldArrayLength = relativeToBodyPositionsUnity.GetLength(0);
        for (int i = 0; i < oldArrayLength; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                copyRelativeToBodyPositionsUnity[i, j] = relativeToBodyPositionsUnity[i, j];
            }
        }

        for (int k = oldArrayLength; k < currentMsgCount; k++)
        {
            VizMessage message = MessageList.GetMessageAtIndex(k);
            if (lastFrameSpacecraftRelMode == 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    copyRelativeToBodyPositionsUnity[k, i] = mySpacecraftPositionHistoryBSK[k, i] -
                                                                    message.Spacecraft[SpacecraftIndex].Position[i];
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    copyRelativeToBodyPositionsUnity[k, i] = mySpacecraftPositionHistoryBSK[k, i] -
                                                                     message.CelestialBodies[indexOfRelativeBody]
                                                                         .Position[i];
                }
            }
        }

        relativeToBodyPositionsUnity = copyRelativeToBodyPositionsUnity;
    }
    
    private void AppendRotationFrameData(int currentMsgCount)
    {
        double[,] copyRotatingFramePositions = new double[currentMsgCount, 3];
        double[,] copyRotatingFrameDCMs = new double[currentMsgCount, 9];
        double[,] copyRelativeToRotatingFrameBodyPositionsBSK = new double[currentMsgCount, 3];
        int oldArrayLength = rotatingFramePositions.GetLength(0);

        for (int i = 0; i < oldArrayLength; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                copyRotatingFramePositions[i, j] = rotatingFramePositions[i, j];
                copyRelativeToRotatingFrameBodyPositionsBSK[i, j] = relativeToRotatingFrameBodyPositionsBSK[i, j];
            }

            for (int j = 0; j < 9; j++)
            {
                copyRotatingFrameDCMs[i, j] = rotatingFrameDCMs[i, j];
            }
        }

        for (int k = oldArrayLength; k < currentMsgCount; k++)
        {
            double[] rotMatrix = new double[9];
            if (lastFrameTruePathLineMode == 2) //spacecraft relative (Hill or velocity)
            {
                if (indexOfRelativeSpacecraft != SpacecraftIndex)
                {
                    
                    for (int j = 0; j < 3; j++)
                    {
                        copyRotatingFramePositions[k, j] = SpacecraftStateUtilities.ChiefPositions[k, j];
                    }

                    for (int j = 0; j < 9; j++)
                    {
                        copyRotatingFrameDCMs[k, j] = SpacecraftStateUtilities.ChiefDCMs[k, j];
                        rotMatrix[j] = SpacecraftStateUtilities.ChiefDCMs[k, j];
                    }
                }
                else
                {
                    return;
                }
            }
            else //rotating frame
            {
                
                for (int j = 0; j < 3; j++)
                {
                    copyRotatingFramePositions[k, j] =
                        CelestialBodyStateUtilities.CenterOfMassPositions[k, j];
                }

                for (int j = 0; j < 9; j++)
                {
                    copyRotatingFrameDCMs[k, j] = CelestialBodyStateUtilities.CenterOfMassDCMs[k, j];
                    rotMatrix[j] = CelestialBodyStateUtilities.CenterOfMassDCMs[k, j];
                }
            }

            double[] rrel = {0, 0, 0};
            for (int i = 0; i < 3; i++)
            {
                rrel[i] = mySpacecraftPositionHistoryBSK[k, i] - rotatingFramePositions[k, i];
            }

            rrel = OrbitVectorMath.ApplyTransformationMatrixToVector(rotMatrix, rrel);
            for (int i = 0; i < 3; i++)
            {
                copyRelativeToRotatingFrameBodyPositionsBSK[k, i] = rrel[i];
            }
        }

        rotatingFramePositions = copyRotatingFramePositions;
        rotatingFrameDCMs = copyRotatingFrameDCMs;
        relativeToRotatingFrameBodyPositionsBSK = copyRelativeToRotatingFrameBodyPositionsBSK;
    }

    private void AppendFixedFrameData(int currentMsgCount)
    {
        double[,] copyScRelativeToFixedBodyPositions_fixedBSK = new double[currentMsgCount, 3];
        double[,] copyFixedFrameDCMs = new double[currentMsgCount, 9];
        int oldArrayLength = scRelativeToFixedBodyPositionsFixedBSK.GetLength(0);
        
        for (int i = 0; i < oldArrayLength; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                copyScRelativeToFixedBodyPositions_fixedBSK[i, j] = scRelativeToFixedBodyPositionsFixedBSK[i, j];
            }
            for (int j = 0; j < 9; j++)
            {
                copyFixedFrameDCMs[i, j] = fixedFrameDCMs[i, j];
            }
        }

        for (int k = oldArrayLength; k < currentMsgCount; k++)
        {
            VizMessage newMsg = MessageList.GetMessageAtIndex(k);
            
            double[] fixedBodyPositionInertialBSK = VizardGUISettings.FixedBodyIsSpacecraft
                ? newMsg.Spacecraft[indexOfRelativeBody].Position.ToArray()
                : newMsg.CelestialBodies[indexOfRelativeBody].Position.ToArray();

            double[] fixedFrameDCM = VizardGUISettings.FixedBodyIsSpacecraft
                ? OrbitVectorMath.ConvertRightHandedMRPToRightHandedDCM(newMsg.Spacecraft[indexOfRelativeBody].Rotation
                    .ToArray())
                : newMsg.CelestialBodies[indexOfRelativeBody].Rotation.ToArray();

            double[] relativePositionBSK =
                OrbitVectorMath.Subtract(newMsg.Spacecraft[indexOfRelativeBody].Position.ToArray(),
                    fixedBodyPositionInertialBSK);
            double[] relativePositionFixedFrameRighthanded =
                OrbitVectorMath.ApplyTransformationMatrixToVector(fixedFrameDCM, relativePositionBSK);
            for (int j = 0; j < 3; j++)
            {
                copyScRelativeToFixedBodyPositions_fixedBSK[k, j] = relativePositionFixedFrameRighthanded[j];
            }

            for (int j = 0; j < 9; j++)
            {
                copyFixedFrameDCMs[k, j] = fixedFrameDCM[j];
            }
        }

        scRelativeToFixedBodyPositionsFixedBSK = copyScRelativeToFixedBodyPositions_fixedBSK;
        fixedFrameDCMs = copyFixedFrameDCMs;
    }

    // Unit Test Support (see TestTruePathTrajectory.cs)

    public void CallTruePathUpdate()
    {
        UpdateHistoriesAndPointsToDraw();
    }
    public int GetIndexOfRelativeSpacecraft()
    {
        return indexOfRelativeSpacecraft;
    }
    public int GetIndexOfRelativeBody()
    {
        return indexOfRelativeBody;
    }
    public int GetLastFrameTruePathLineMode()
    {
        return lastFrameTruePathLineMode;
    }
    public int GetLastFrameSpacecraftRelMode()
    {
        return lastFrameSpacecraftRelMode;
    }

    public int GetCount_pointsToDrawOnscreen()
    {
        return truePathLinePlotter.pointsToDrawOnscreen.Length;
    }
    public List<Vector3> Sample_pointsToDrawOnscreen(int startingIndex, int sizeOfSample)
    {
        UpdatePointsToDraw();
        List<Vector3> sampleForTest = new List<Vector3>();
        for (int i = startingIndex; i < sizeOfSample+startingIndex; i++)
        {
            sampleForTest.Add(truePathLinePlotter.pointsToDrawOnscreen[i]);
        }
        return sampleForTest;
    }

    public Vector3 Sample_pointToDrawOnscreen(int index)
    {
        UpdatePointsToDraw();
        return truePathLinePlotter.pointsToDrawOnscreen[index];
    }
    
    private List<double[]> Sample_Array_AsList(int startingIndex, int sizeOfSample, double[,] arrayToSample)
    {
        List<double[]> resultsList = new List<double[]>();
        
        for (int i = startingIndex; i < startingIndex + sizeOfSample; i++)
        {
            double[] result = new double[arrayToSample.GetLength(1)];
            for (int j = 0; j < result.Length; j++)
            {
                result[j] = arrayToSample[i, j];
            }
            resultsList.Add(result);
        }
        return resultsList;
    }

    private double[] SampleArray(int desiredIndex, double[,] arrayToSample)
    {
        double[] result = new double[arrayToSample.GetLength(1)];
        for (int j = 0; j < result.Length; j++)
        {
            result[j] = arrayToSample[desiredIndex, j];
        }

        return result;
    }
    public List<double[]> Sample_mySpacecraftPositionHistoryBSK(int startingIndex, int sizeOfSample)
    {
        return Sample_Array_AsList(startingIndex, sizeOfSample, mySpacecraftPositionHistoryBSK);
    }
    public List<double[]> Sample_relativeToBodyPositions_Unity(int startingIndex, int sizeOfSample)
    {
        return Sample_Array_AsList(startingIndex, sizeOfSample, relativeToBodyPositionsUnity);
    }
    public double[] Sample_relativeToRotatingFrameBodyPositions_BSK(int desiredIndex)
    {
        return SampleArray(desiredIndex,relativeToRotatingFrameBodyPositionsBSK);
    }
    public double[] Sample_rotatingFramePositions(int desiredIndex)
    {
        return SampleArray(desiredIndex,rotatingFramePositions);
    }
    public double[] Sample_rotatingFrameDCMs(int desiredIndex)
    {
        return SampleArray(desiredIndex,rotatingFrameDCMs);
    }
    public double[] Sample_scRelativeToFixedBodyPositions_fixedBSK(int desiredIndex)
    {
        return SampleArray(desiredIndex,scRelativeToFixedBodyPositionsFixedBSK);
    }
    public double[] Sample_fixedFrameDCMs(int desiredIndex)
    {
        return SampleArray(desiredIndex,fixedFrameDCMs);
    }
    
    public string GetCurrentTruePathSettings()
    {
        return $"My SC: {SpacecraftIndex} Visible: ?,Mode: {lastFrameTruePathLineMode}, SCMode: {lastFrameSpacecraftRelMode}, chiefSCindex{indexOfRelativeSpacecraft}, relBodyIndex{indexOfRelativeBody}, rotatingFrame1: ?, rotatingFrame2: ?, fixedBodyIndex ? fixedIsSC: ?";
    }

}
