using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class AngularVelocityPanel : StockGncPanel
{
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[4];
    private readonly RectTransform[] fills = new RectTransform[4];
    private static readonly string[] Axes = { "X", "Y", "Z", "Norm" };

    protected override void Show(StockHud hud)
    {
        var components = hud?.Numbers("SC omega_b truth");
        var norm = NumberAt(hud?.Numbers("SC omega_b truth norm"), 0);
        if (Panel == null)
        {
            if (components?.Length != 3 && !norm.HasValue) return;
            Build();
        }

        for (int i = 0; i < Axes.Length; i++)
        {
            bool isNorm = i == 3;
            float? value = isNorm ? norm : NumberAt(components, i);
            fills[i].gameObject.SetActive(value.HasValue);
            values[i].text = value.HasValue
                ? value.Value.ToString(isNorm ? "F3" : "+0.000;-0.000;0.000", CultureInfo.InvariantCulture) + " deg/s"
                : "—";
            if (!value.HasValue) continue;

            float limit = isNorm ? 15 : 10;
            float width = Mathf.Clamp01(Mathf.Abs(value.Value) / limit) * (isNorm ? 90 : 45);
            float left = isNorm ? 68 : 113 - (value.Value < 0 ? width : 0);
            fills[i].sizeDelta = new Vector2(width, 7);
            fills[i].anchoredPosition = new Vector2(left, -(32 + i * 19));
        }
    }

    private void Build()
    {
        CreatePanel("Angular Velocity", 280, 112, PanelDock.Slot.AngularVelocity);
        for (int i = 0; i < Axes.Length; i++)
        {
            float y = 25 + i * 19;
            bool isNorm = i == 3;
            Color color = isNorm ? Yellow : Cyan;
            Label(Axes[i], 8, y, 34, color);
            Bar("Track", 68, y + 7, 90, 7, new Color(1, 1, 1, 0.15f));
            fills[i] = Bar("Value", 68, y + 7, 0, 7, color);
            if (!isNorm) Bar("Zero", 113, y + 4, 1, 13, Color.white);
            Label(isNorm ? "0" : "−10", 43, y, 24, Color.white, 10);
            Label(isNorm ? "15" : "+10", 162, y, 26, Color.white, 10);
            values[i] = Label("—", 190, y, 86, Color.white);
        }
    }
}
