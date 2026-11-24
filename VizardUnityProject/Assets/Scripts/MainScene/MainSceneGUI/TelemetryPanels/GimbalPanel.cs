using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GimbalPanel : StockGncPanel
{
    private const string Missing = "—";
    private const float Width = 324, ExpandedHeight = 257, CollapsedHeight = 160;
    private const float TrackTop = 65, TrackHeight = 74;
    // The personal panel uses 20 degrees of tilt as its default display range.
    private const float TiltScaleStep = 20;

    private sealed class Column
    {
        public float[] current, reference;
        public readonly RectTransform[] currentMarkers = new RectTransform[2];
        public readonly RectTransform[] referenceMarkers = new RectTransform[2];
        public TextMeshProUGUI tiltScale, currentText, referenceText, deltaText, errorText;
    }

    // The stock HUD specifies the column order as "Gimbal [A, B] (θ, φ) Orientation".
    private readonly Column[] columns = { new Column(), new Column() };
    private static readonly string[] CommandFields = { "Gimbal A commanded", "Gimbal B commanded" };
    private GameObject details;
    private TextMeshProUGUI targetLabel, deltaLabel;

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    protected override void Show(StockHud hud)
    {
        bool commanded = hud?.Text(CommandFields[0]) != null || hud?.Text(CommandFields[1]) != null;
        if (Panel == null)
        {
            if (!commanded && hud?.Text("Truth") == null && hud?.Text("Ref") == null) return;
            Build();
        }

        targetLabel.text = commanded ? "Commanded" : "Slew reference";
        deltaLabel.text = commanded ? "Δ to command" : "Δ to reference";
        float maxTilt = TiltScaleStep;
        for (int i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            column.current = hud?.Pair("Truth", i);
            column.reference = commanded ? hud.Pair(CommandFields[i], 0) : hud?.Pair("Ref", i);
            if (!commanded)
                maxTilt = Mathf.Max(maxTilt, Mathf.Max(column.current?[1] ?? 0, column.reference?[1] ?? 0));
        }
        // Both tilt gauges use the same plot scale. The stock HUD omits mechanical limits.
        float tiltScale = TiltScaleStep * Mathf.Ceil(maxTilt / TiltScaleStep);
        var separation = hud?.Numbers("Angular diff");
        for (int i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            column.tiltScale.text = tiltScale.ToString("0", CultureInfo.InvariantCulture);
            for (int axis = 0; axis < 2; axis++)
            {
                float scale = axis == 0 ? 360 : tiltScale;
                PlaceMarker(column.referenceMarkers[axis], column.reference, axis, scale);
                PlaceMarker(column.currentMarkers[axis], column.current, axis, scale);
            }
            column.referenceText.text = GaugeReading(column.reference, commanded);
            column.currentText.text = GaugeReading(column.current, commanded);
            column.deltaText.text = column.reference == null || column.current == null ? Missing :
                Pair(Mathf.Abs(Mathf.DeltaAngle(column.current[0], column.reference[0])),
                    Mathf.Abs(column.current[1] - column.reference[1]));
            var trackingError = NumberAt(separation, i);
            column.errorText.text = trackingError.HasValue
                ? trackingError.Value.ToString("F1", CultureInfo.InvariantCulture) + "°" : Missing;
        }
    }

    private static string Pair(float[] angles) => angles == null ? Missing : Pair(angles[0], angles[1]);
    private static string Pair(float theta, float phi) => FormattableString.Invariant($"({theta:F1}, {phi:F1})°");

    private static string GaugeReading(float[] angles, bool commanded)
    {
        bool offScale = commanded && angles != null &&
            (angles[0] < 0 || angles[0] > 360 || angles[1] < 0 || angles[1] > TiltScaleStep);
        return Pair(angles) + (offScale ? " !" : "");
    }

    private static void PlaceMarker(RectTransform marker, float[] angles, int axis, float scale)
    {
        marker.gameObject.SetActive(angles != null);
        if (angles == null) return;
        float y = TrackTop + TrackHeight * (1 - Mathf.Clamp01(angles[axis] / scale));
        marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, marker.sizeDelta.y / 2 - y);
    }

    private void Build()
    {
        CreatePanel("Gimbal Angles", Width, ExpandedHeight, PanelDock.Slot.GimbalLimits);
        details = Bar("Readings", 4, 146, Width - 8, 98, new Color(1, 1, 1, .09f)).gameObject;
        var buttonPrefab = Resources.Load<GameObject>("Prefabs/SpacecraftPanels/ReactionWheelsPanel").transform.Find("VerboseButton");
        var button = Instantiate(buttonPrefab, Panel.transform, false).GetComponent<Button>();
        button.onClick.AddListener(ToggleDetails);
        targetLabel = DetailLabel("Slew reference", 4, 20, 100, Yellow);
        DetailLabel("Current", 4, 38, 100, Cyan);
        deltaLabel = DetailLabel("Δ to reference", 4, 56, 100, Color.white);
        DetailLabel("Tracking error", 4, 74, 100, Color.white);
        Bar("Divider", Width / 2, 28, 1, 112, new Color(1, 1, 1, .25f));

        for (int i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            string id = i == 0 ? "A" : "B";
            float start = i * Width / 2;
            Label(id, start, 23, Width / 2, Color.white, 15, TextAlignmentOptions.Midline);
            for (int axis = 0; axis < 2; axis++)
            {
                float x = start + 28 + axis * 76;
                Label(axis == 0 ? "θ (°)" : "φ (°)", x - 9, 43, 49, Color.white, 12);
                Bar("Track", x, TrackTop, 12, TrackHeight, new Color(.85f, .85f, .85f));
                var scaleLabel = Label(axis == 0 ? "360" : "20", x + 16, TrackTop - 8, 40, Color.white);
                if (axis == 1) column.tiltScale = scaleLabel;
                Label("0", x + 16, TrackTop + TrackHeight - 10, 30, Color.white);
                column.referenceMarkers[axis] = Bar("Slew reference", x - 5, TrackTop, 22, 7, Yellow);
                column.currentMarkers[axis] = Bar("Current", x - 2, TrackTop, 16, 3, Cyan);
            }
            float cell = 100 + 107 * i;
            DetailLabel(id + " (θ, φ)", cell, 1, 107, Color.white, TextAlignmentOptions.Midline);
            column.referenceText = DetailLabel(Missing, cell, 20, 107, Yellow, TextAlignmentOptions.Midline);
            column.currentText = DetailLabel(Missing, cell, 38, 107, Cyan, TextAlignmentOptions.Midline);
            column.deltaText = DetailLabel(Missing, cell, 56, 107, Color.white, TextAlignmentOptions.Midline);
            column.errorText = DetailLabel(Missing, cell, 74, 107, Color.white, TextAlignmentOptions.Midline);
        }
    }

    private TextMeshProUGUI DetailLabel(string text, float x, float y, float width, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var label = StockPanelView.Label(details.transform, text, x, y, width, alignment);
        label.color = color;
        return label;
    }

    private void ToggleDetails()
    {
        details.SetActive(!details.activeSelf);
        Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, details.activeSelf ? ExpandedHeight : CollapsedHeight);
        VizardGUISettings.PanelViewMgr.TelemetryDock.Reflow(PanelDock.Slot.GimbalLimits);
    }
}
