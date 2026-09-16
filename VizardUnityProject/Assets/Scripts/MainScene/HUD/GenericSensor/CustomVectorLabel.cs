using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Run after camera motion; all custom-vector labels share the same canvas parent.
[DefaultExecutionOrder(1000)]
public class CustomVectorLabel : MonoBehaviour
{
    private static readonly List<CustomVectorLabel> labels = new List<CustomVectorLabel>();
    private static readonly List<Rect> occupied = new List<Rect>();
    private TextMeshProUGUI text;
    private ObjectLabel tracker;
    private Image connector;
    private int spacecraftIndex;
    private int sensorIndex;
    private bool separated;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        tracker = GetComponent<ObjectLabel>();
        tracker.updatePosition = false;
        var line = new GameObject("Tip connector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.transform.SetParent(transform, false);
        connector = line.GetComponent<Image>();
        connector.raycastTarget = false;
        connector.enabled = false;
        connector.rectTransform.anchorMin = connector.rectTransform.anchorMax = text.rectTransform.pivot;
    }

    public void Initialize(int spacecraft, int sensor)
    {
        spacecraftIndex = spacecraft;
        sensorIndex = sensor;
        SortLabels();
    }

    private static void SortLabels()
    {
        labels.Sort((a, b) => a.spacecraftIndex != b.spacecraftIndex
            ? a.spacecraftIndex.CompareTo(b.spacecraftIndex)
            : a.sensorIndex.CompareTo(b.sensorIndex));
    }

    private void OnEnable()
    {
        labels.Add(this);
        SortLabels();
    }

    private void OnDisable()
    {
        labels.Remove(this);
        separated = false;
        connector.enabled = false;
    }

    private void LateUpdate()
    {
        if (labels[0] != this) return;
        occupied.Clear();
        foreach (var item in labels) item.PlaceLabel();
    }

    private void PlaceLabel()
    {
        var camera = tracker.cameraToUse;
        if (camera == null) return;
        Vector3 screenTip = camera.WorldToScreenPoint(tracker.targetTransform.position);
        text.enabled = screenTip.z > 0;
        connector.enabled = false;
        if (!text.enabled)
        {
            separated = false;
            return;
        }

        var parent = (RectTransform)transform.parent;
        var canvas = text.canvas.rootCanvas;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenTip, uiCamera, out var tip);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
            (Vector2)screenTip + tracker.screenOffset, uiCamera, out var origin);
        text.rectTransform.localPosition = origin;
        text.ForceMeshUpdate();
        Bounds bounds = text.textBounds;
        var desired = new Rect(origin + (Vector2)bounds.min, bounds.size);
        Rect placed = PlaceBelow(desired, occupied, separated);
        Vector2 displacement = placed.position - desired.position;
        separated = displacement.sqrMagnitude > 0;
        text.rectTransform.localPosition = origin + displacement;
        occupied.Add(placed);

        if (!separated) return;
        Vector2 end = new Vector2(Mathf.Clamp(tip.x, placed.xMin, placed.xMax),
            Mathf.Clamp(tip.y, placed.yMin, placed.yMax));
        Vector2 direction = end - tip;
        connector.enabled = direction.magnitude > 12;
        connector.color = text.color;
        connector.rectTransform.localPosition = (tip + end) / 2 - origin - displacement;
        connector.rectTransform.localRotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        connector.rectTransform.sizeDelta = new Vector2(direction.magnitude, 1);
    }

    internal static Rect PlaceBelow(Rect desired, IList<Rect> previous, bool wasSeparated)
    {
        const float gap = 6;
        // Extra clearance before returning to the tip prevents threshold flicker.
        float clearance = gap + (wasSeparated ? 8 : 0);
        Rect placed = desired;
        // ponytail: quadratic packing is sufficient for a handful of direction labels.
        bool moved;
        do
        {
            moved = false;
            foreach (Rect other in previous)
            {
                var padded = new Rect(other.xMin - clearance, other.yMin - clearance,
                    other.width + 2 * clearance, other.height + 2 * clearance);
                float below = other.yMin - gap - placed.height;
                if (placed.Overlaps(padded) && below < placed.yMin)
                {
                    placed.y = below;
                    moved = true;
                }
            }
        } while (moved);
        return placed;
    }
}
