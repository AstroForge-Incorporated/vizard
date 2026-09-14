using System.Linq;
using TMPro;
using UnityEngine;
using GenericSensor = VizProtobufferMessage.VizMessage.Types.GenericSensor;

/// <summary>
/// A zero-FOV GenericSensor is a custom arrow: body-frame position [m] and
/// direction, size [m], RGBA color, label text, and visibility may change every message.
/// Keep the sensor index fixed for the duration of the recording/stream.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class CustomVectorHUD : MonoBehaviour
{
    private const float ShaftWidthFraction = 0.002f;
    private const float HeadLengthFraction = 0.12f;
    private const float HeadRadiusFraction = 0.04f;
    private LineRenderer line;
    private Material lineMaterial;
    private TextMeshProUGUI label;
    private int spacecraftIndex;
    private int sensorIndex;
    private bool inSpriteMode;
    private bool visible;
    private bool labelsEnabled;

    public static bool IsCustomVector(GenericSensor message)
    {
        return message.FieldOfView.Count == 1 && message.FieldOfView[0] == 0;
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        lineMaterial = line.material;
        line.useWorldSpace = false;
        Vector3 tip = Vector3.forward;
        Vector3 headBase = (1 - HeadLengthFraction) * tip;
        line.positionCount = 5;
        line.SetPositions(new[] {
            Vector3.zero, tip,
            headBase + HeadRadiusFraction * Vector3.right, tip,
            headBase - HeadRadiusFraction * Vector3.right
        });
        line.enabled = false;
    }

    private void OnEnable()
    {
        Camera.onPreCull += OrientArrowhead;
    }

    private void OrientArrowhead(Camera camera)
    {
        if (!line.enabled) return;
        // Keep the two sides of the V visible from each camera before culling.
        Vector3 viewDirection = camera.orthographic
            ? -transform.InverseTransformDirection(camera.transform.forward)
            : transform.InverseTransformPoint(camera.transform.position) - Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.forward, viewDirection).normalized;
        if (side == Vector3.zero)
            side = transform.InverseTransformDirection(camera.transform.right);
        side *= HeadRadiusFraction;
        Vector3 headBase = (1 - HeadLengthFraction) * Vector3.forward;
        line.SetPosition(2, headBase + side);
        line.SetPosition(4, headBase - side);
    }

    public GameObject Initialize(int scIndex, int vectorIndex, bool showLabel)
    {
        labelsEnabled = showLabel;
        spacecraftIndex = scIndex;
        sensorIndex = vectorIndex;
        var spacecraft = MessageList.FirstMessage.Spacecraft[scIndex];
        var message = spacecraft.GenericSensors[vectorIndex];
        name = string.IsNullOrEmpty(message.Label) ? $"Vector {vectorIndex}" : message.Label;
        var labelAnchor = new GameObject("LabelAnchor");
        labelAnchor.transform.SetParent(transform, false);
        labelAnchor.transform.localPosition = Vector3.forward;
        label = LabelMaker.CreateLabel(message.Label, spacecraft.SpacecraftName, labelAnchor,
            new Vector2(10, -10), "GenericSensors", 0).GetComponent<TextMeshProUGUI>();
        label.richText = false;
        // The Labels panel controls this group, including recordings with wing bodies.
        if (showLabel) VizardGUISettings.ShowGenericSensorLabels = true;
        ApplyMessage(message);
        return label.gameObject;
    }

    private void FixedUpdate()
    {
        var sensors = MessageList.CurrentMessage.Spacecraft[spacecraftIndex].GenericSensors;
        if (sensorIndex < sensors.Count)
        {
            ApplyMessage(sensors[sensorIndex]);
        }
        else
        {
            visible = false;
            UpdateVisibility();
        }
    }

    public void ApplyMessage(GenericSensor message)
    {
        visible = !message.IsHidden && message.Position.Count == 3
            && message.NormalVector.Count == 3 && message.Size > 0
            && message.Size <= float.MaxValue;
        if (visible)
        {
            Vector3 position = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.TransformFromBSKCStoUnity(message.Position.ToArray()));
            Vector3 direction = OrbitVectorMath.ReturnVector3(
                OrbitVectorMath.TransformFromBSKCStoUnity(message.NormalVector.ToArray()));
            visible = IsFinite(position) && IsFinite(direction) && direction.sqrMagnitude > 0;
            if (visible)
            {
                transform.localPosition = position;
                transform.localRotation = Quaternion.LookRotation(direction.normalized);
                transform.localScale = (float)message.Size * Vector3.one;
                if (message.Color.Count >= 4)
                {
                    Color color = new Color(message.Color[0] / 255f, message.Color[1] / 255f,
                        message.Color[2] / 255f, message.Color[3] / 255f);
                    lineMaterial.color = color;
                    line.startColor = color;
                    line.endColor = color;
                    if (label != null) label.color = color;
                }
            }
        }
        if (label != null) label.text = message.Label;
        UpdateVisibility();
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    }

    private void LateUpdate()
    {
        // LineRenderer widths are world-space, while its vertices are local.
        line.startWidth = line.endWidth = ShaftWidthFraction * transform.lossyScale.x;
    }

    private void UpdateVisibility()
    {
        line.enabled = visible && !inSpriteMode;
        if (label != null)
            label.gameObject.SetActive(line.enabled && labelsEnabled && !string.IsNullOrEmpty(label.text)
                && VizardGUISettings.ShowGenericSensorLabels);
    }

    public void ConfigureHUDForSpriteMode(bool spriteOn)
    {
        inSpriteMode = spriteOn;
        UpdateVisibility();
    }

    private void OnDisable()
    {
        Camera.onPreCull -= OrientArrowhead;
        if (label != null) label.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Destroy(lineMaterial);
    }
}
