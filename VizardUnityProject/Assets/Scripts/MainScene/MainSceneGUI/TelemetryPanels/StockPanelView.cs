using TMPro;
using UnityEngine;

internal static class StockPanelView
{
    public static GameObject Create(string title, PanelDock.Slot slot, float width, float height)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/GUIGenerics/GenericSubpanel");
        var panel = Object.Instantiate(prefab);
        panel.name = title;
        panel.transform.Find("PanelLabel").GetComponent<TextMeshProUGUI>().text = title;
        var manager = VizardGUISettings.PanelViewMgr;
        manager.RegisterTelemetryPanel(panel, title);
        var rect = panel.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(width, height);
        manager.TelemetryDock.Place(rect, slot);
        return panel;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float x, float y,
        float width, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
        float size = 11, float height = 19)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/GUIGenerics/GUILabel");
        var label = Object.Instantiate(prefab, parent, false).GetComponent<TextMeshProUGUI>();
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        label.text = text;
        label.color = Color.white;
        label.fontSize = size;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }
}
