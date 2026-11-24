using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MomentumAllocatorPanel : StockGncPanel
{
    private const float Width = 300, CollapsedHeight = 196, ExpandedHeight = 365;
    private static readonly (string key, string label, float top)[] Details =
    {
        ("Allocator COM bias", "Allocator COM bias [mm]", 5),
        ("Bias updates", "Bias updates", 24),
        ("Desat score", "Desat score", 67),
        ("Fallback entry", "Fallback entry (config)", 86),
        ("Fallback exit", "Fallback exit (config)", 105),
        ("Fallback state", "Fallback state", 124)
    };
    private readonly TextMeshProUGUI[] detailValues = new TextMeshProUGUI[Details.Length];
    private readonly TextMeshProUGUI[] readings = new TextMeshProUGUI[4];
    private readonly RectTransform[] fills = new RectTransform[4];
    private TextMeshProUGUI work, requested, recovery, detailsLabel;
    private GameObject details;

    protected override void Show(StockHud hud)
    {
        string workRequest = hud?.Text("Thruster work request");
        string desat = hud?.Text("Desat requested");
        var momentum = hud?.Numbers("Body momentum");
        if (Panel == null)
        {
            if (momentum == null && workRequest == null && desat == null && hud?.Text("Fallback state") == null) return;
            Build();
        }

        Set(work, workRequest == "NoWork" ? "No Work" : workRequest);
        Set(requested, desat);
        float? norm = null;
        if (momentum?.Length == 3 && NumberAt(momentum, 0).HasValue
            && NumberAt(momentum, 1).HasValue && NumberAt(momentum, 2).HasValue)
        {
            double x = momentum[0], y = momentum[1], z = momentum[2];
            double magnitude = Math.Sqrt(x * x + y * y + z * z);
            if (magnitude <= float.MaxValue) norm = (float)magnitude;
        }
        for (int i = 0; i < readings.Length; i++)
            ShowBar(i, i == 3 ? norm : NumberAt(momentum, i));
        for (int i = 0; i < Details.Length; i++)
            Set(detailValues[i], hud?.Text(Details[i].key));
        Set(recovery, hud?.Text("Fallback state") == "Active"
            ? $"{hud.Text("Recovery cycles") ?? "—"} / {hud.Text("Recovery dwell") ?? "—"} cycles" : null);
    }

    private static void Set(TextMeshProUGUI label, string value)
    {
        label.text = value ?? "—";
        label.color = value == null ? Color.gray : Color.white;
    }

    private void ShowBar(int index, float? value)
    {
        bool norm = index == 3;
        Set(readings[index], value?.ToString(norm ? "F3" : "+0.000;-0.000;0.000", CultureInfo.InvariantCulture));
        var fill = fills[index];
        fill.gameObject.SetActive(value.HasValue);
        if (!value.HasValue) return;
        float width = Mathf.Clamp01(Mathf.Abs(value.Value) / (norm ? 8 : 5)) * (norm ? 130 : 65);
        float start = norm ? 70 : 135;
        if (value.Value < 0) start -= width;
        Place(fill, start, 93 + 19 * index, width, 7);
    }

    private static TextMeshProUGUI Row(Transform parent, string label, float y)
    {
        StockPanelView.Label(parent, label, 12, y, 143);
        var value = StockPanelView.Label(parent, "—", 157, y, Width - 169);
        value.richText = false;
        return value;
    }

    private void Build()
    {
        CreatePanel("Momentum & Allocator", Width, CollapsedHeight, PanelDock.Slot.MomentumAllocator);
        work = Row(Panel.transform, "Work", 26);
        requested = Row(Panel.transform, "Desat requested", 45);
        Label("Momentum estimate (body) [N·m·s]", 12, 67, Width - 24, Color.white);
        for (int i = 0; i < readings.Length; i++)
        {
            bool norm = i == 3;
            Color color = norm ? Yellow : Cyan;
            float y = 86 + 19 * i;
            Label(norm ? "Norm" : "XYZ"[i].ToString(), 12, y, 32, color);
            Label(norm ? "0" : "−5", 44, y, 24, Color.white, 10);
            Label(norm ? "8" : "+5", 204, y, 26, Color.white, 10);
            Bar("Momentum track", 70, y + 7, 130, 7, new Color(1, 1, 1, .15f));
            fills[i] = Bar("Momentum value", 70, y + 7, 0, 7, color);
            if (!norm) Bar("Zero", 135, y + 4, 1, 13, Color.white);
            readings[i] = Label("—", 238, y, Width - 242, Color.white);
        }

        var prefab = Resources.Load<GameObject>("Prefabs/GUIGenerics/GenericTMPButton");
        var button = Instantiate(prefab, Panel.transform, false).GetComponent<Button>();
        Place(button.GetComponent<RectTransform>(), 12, 170, 104, 20);
        detailsLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        detailsLabel.fontSize = 11;
        detailsLabel.text = "Details";
        button.onClick.AddListener(ToggleDetails);

        details = new GameObject("Allocator details", typeof(RectTransform));
        details.transform.SetParent(Panel.transform, false);
        Place(details.GetComponent<RectTransform>(), 0, 198, Width, ExpandedHeight - 198);
        Bar("Divider", 12, 0, Width - 24, 1, new Color(1, 1, 1, .25f)).SetParent(details.transform, false);
        StockPanelView.Label(details.transform, "Score / thresholds [N·m²·s]", 12, 48, Width - 24);
        for (int i = 0; i < Details.Length; i++)
            detailValues[i] = Row(details.transform, Details[i].label, Details[i].top);
        recovery = Row(details.transform, "Recovery", 143);
        details.SetActive(false);
    }

    private void ToggleDetails()
    {
        bool expand = !details.activeSelf;
        details.SetActive(expand);
        detailsLabel.text = expand ? "Hide details" : "Details";
        Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, expand ? ExpandedHeight : CollapsedHeight);
        VizardGUISettings.PanelViewMgr.TelemetryDock.Reflow(PanelDock.Slot.MomentumAllocator);
    }
}
