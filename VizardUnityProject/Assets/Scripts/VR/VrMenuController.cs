using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>Hide and unregister a menu pointer while its controller is untracked.</summary>
public sealed class VrMenuController : MonoBehaviour
{
    public InputActionReference isTracked;
    public XRRayInteractor ray;
    public XRInteractorLineVisual line;
    public GameObject model;

    public bool Tracked => isTracked != null && isTracked.action.IsPressed();

    void Update()
    {
        bool tracked = Tracked;
        if (ray.enabled != tracked) ray.enabled = tracked;
        if (line.enabled != tracked) line.enabled = tracked;
        if (model.activeSelf != tracked) model.SetActive(tracked);
    }
}
