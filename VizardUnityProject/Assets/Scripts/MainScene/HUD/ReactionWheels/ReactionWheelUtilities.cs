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

using System.Linq;
using UnityEngine;

/// <summary>
/// Static class providing methods to build and update reaction wheel display panels
/// </summary>
public static class ReactionWheelUtilities
{
    public static bool HUDShowSpeed = true;
    public static double[] MaxTorque;
    public static double[] MaxSpeed;
    private const double MillinewtonsPerNewton = 1000.0;

    public static void InitializeMaxTorqueAndSpeedArrays()
    {
        //This is some setup work for the max speed/torque arrays that has to be done at start to correctly size the arrays for the spacecraft count
        int scCount = MessageList.FirstMessage.Spacecraft.Count;
        MaxTorque = new double[scCount];
        MaxSpeed = new double[scCount];
        for (int i = 0; i < scCount; i++)
        {
            foreach (var wheel in MessageList.FirstMessage.Spacecraft[i].ReactionWheels)
            {
                MaxTorque[i] = System.Math.Max(MaxTorque[i], MillinewtonsPerNewton * wheel.MaxTorque);
                MaxSpeed[i] = System.Math.Max(MaxSpeed[i], wheel.MaxSpeed);
            }
            // The optional limits can be absent until the first nonzero sample.
            if (MaxTorque[i] <= 0) MaxTorque[i] = 1;
            if (MaxSpeed[i] <= 0) MaxSpeed[i] = 0.001;
        }
    }

    public static double[] GetReactionWheelSpeeds(int spacecraftIndex)
    {
        int wheelCount = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].ReactionWheels.Count;
        double[] wheelSpeedArray = new double[wheelCount];
        for (int i = 0; i < wheelCount; i++)
        {
            wheelSpeedArray[i] = GetReactionWheelSpeed(spacecraftIndex, i);
        }

        return wheelSpeedArray;
    }

    public static double[] GetReactionWheelTorques(int spacecraftIndex)
    {
        int wheelCount = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].ReactionWheels.Count;
        double[] wheelTorqueArray = new double[wheelCount];
        for (int i = 0; i < wheelCount; i++)
        {
            wheelTorqueArray[i] = GetReactionWheelTorque(spacecraftIndex, i);
        }

        return wheelTorqueArray;
    }

    //The following methods are for retrieving only one wheel's data at a time (used primarily for the HUD display)
    public static double GetReactionWheelTorque(int spacecraftIndex, int wheelIndex)
    {
        double currentTorque = MillinewtonsPerNewton * MessageList.CurrentMessage.Spacecraft[spacecraftIndex].ReactionWheels[wheelIndex].WheelTorque;
        MaxTorque[spacecraftIndex] = System.Math.Max(MaxTorque[spacecraftIndex], System.Math.Abs(currentTorque));
        return currentTorque;
    }

    public static double GetReactionWheelSpeed(int spacecraftIndex, int wheelIndex)
    {
        double currentSpeed = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].ReactionWheels[wheelIndex].WheelSpeed;
        MaxSpeed[spacecraftIndex] = System.Math.Max(MaxSpeed[spacecraftIndex], System.Math.Abs(currentSpeed));
        return currentSpeed;
    }

    public static Vector3 GetReactionWheelPosition(int spacecraftIndex, int wheelIndex)
    {
        Vector3 wheelPosition = OrbitVectorMath.ReturnVector3(OrbitVectorMath.TransformFromBSKCStoUnity(MessageList
            .FirstMessage.Spacecraft[spacecraftIndex].ReactionWheels[wheelIndex].Position.ToArray()));
        return wheelPosition;
    }

    public static Vector3 GetReactionWheelSpinAxis(int spacecraftIndex, int wheelIndex)
    {
        Vector3 wheelPosition = OrbitVectorMath.ReturnVector3(OrbitVectorMath.TransformFromBSKCStoUnity(MessageList
            .FirstMessage.Spacecraft[spacecraftIndex].ReactionWheels[wheelIndex].SpinAxisVector.ToArray()));
        return wheelPosition;
    }

    public static void ResetReactionWheelUtilities()
    {
        HUDShowSpeed = true;
        MaxTorque = new double[] { };
        MaxSpeed = new double[] { };
    }
}