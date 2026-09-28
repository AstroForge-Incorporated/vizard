#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>Editor-only controller simulation through real Input System XR device events.</summary>
public class VrMenuDesktopSimulator : MonoBehaviour
{
    protected XRSimulatedHMD hmd;
    protected XRSimulatedController left;
    protected XRSimulatedController right;
    protected XRSimulatedHMDState headState;
    protected XRSimulatedControllerState leftState;
    protected XRSimulatedControllerState rightState;
    protected VrStartupMenu menu;

    protected virtual void Awake()
    {
        menu = FindFirstObjectByType<VrStartupMenu>();
        InputSystem.RegisterLayout<XRSimulatedHMD>();
        InputSystem.RegisterLayout<XRSimulatedController>();
        hmd = InputSystem.AddDevice<XRSimulatedHMD>();
        left = InputSystem.AddDevice<XRSimulatedController>();
        right = InputSystem.AddDevice<XRSimulatedController>();
        InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
        InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        headState.Reset();
        headState.isTracked = true;
        headState.trackingState = 3;
        leftState.Reset();
        rightState.Reset();
        leftState.isTracked = rightState.isTracked = true;
        leftState.trackingState = rightState.trackingState = 3;
        leftState.devicePosition = new Vector3(-.2f, -.25f, .15f);
        rightState.devicePosition = new Vector3(.2f, -.25f, .15f);
        Queue();
    }

    protected virtual void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;
        bool useLeft = Keyboard.current.leftShiftKey.isPressed;
        var ray = menu.head.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plane = new Plane(menu.menu.transform.forward, menu.menu.transform.position);
        var target = ray.GetPoint(plane.Raycast(ray, out float distance) ? distance : 1.7f);
        Aim(target, useLeft);
        leftState.buttons = rightState.buttons = 0;
        leftState.trigger = rightState.trigger = 0;
        if (Mouse.current.leftButton.isPressed)
        {
            if (useLeft) { leftState.buttons = (ushort)(1 << (int)ControllerButton.TriggerButton); leftState.trigger = 1; }
            else { rightState.buttons = (ushort)(1 << (int)ControllerButton.TriggerButton); rightState.trigger = 1; }
        }
        leftState.primary2DAxis = rightState.primary2DAxis = Vector2.zero;
        if (useLeft) leftState.primary2DAxis = new Vector2(0, Mouse.current.scroll.ReadValue().y / 120f);
        else rightState.primary2DAxis = new Vector2(0, Mouse.current.scroll.ReadValue().y / 120f);
        if (Keyboard.current.rKey.wasPressedThisFrame) menu.Recenter();
        float yaw = (Keyboard.current.rightArrowKey.isPressed ? 1 : 0) - (Keyboard.current.leftArrowKey.isPressed ? 1 : 0);
        headState.centerEyeRotation *= Quaternion.Euler(0, yaw * Time.unscaledDeltaTime * 45, 0);
        Queue();
    }

    protected void Aim(Vector3 target, bool useLeft)
    {
        var offset = menu.head.transform.parent;
        var state = useLeft ? leftState : rightState;
        state.deviceRotation = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation(target - offset.TransformPoint(state.devicePosition));
        if (useLeft) leftState = state; else rightState = state;
    }

    protected void Queue()
    {
        InputSystem.QueueStateEvent(hmd, headState);
        InputSystem.QueueStateEvent(left, leftState);
        InputSystem.QueueStateEvent(right, rightState);
    }

    protected virtual void OnDestroy()
    {
        if (hmd != null && hmd.added) InputSystem.RemoveDevice(hmd);
        if (left != null && left.added) InputSystem.RemoveDevice(left);
        if (right != null && right.added) InputSystem.RemoveDevice(right);
    }
}
#endif
