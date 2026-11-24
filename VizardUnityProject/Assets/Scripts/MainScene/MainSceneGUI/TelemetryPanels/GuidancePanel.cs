using System;
using TMPro;
using UnityEngine;

public sealed class GuidancePanel : StockGncPanel
{
    private static readonly string[] Rows = { "Attitude target", "Targets accepted", "Angle to target (path)", "Slew time remaining" };
    private static readonly string[] Keys = { "Attitude target type", "Num target accepted", "Angle to target", "Slew time remaining" };
    private static readonly string[] Units = { "", "", " deg", " s" };
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[Rows.Length];

    protected override void Show(StockHud hud)
    {
        if (Panel == null)
        {
            if (hud == null || Array.TrueForAll(Keys, key => hud.Text(key) == null)) return;
            CreatePanel("Guidance", 300, 110, PanelDock.Slot.Guidance);
            for (int i = 0; i < Rows.Length; i++)
            {
                float y = 26 + i * 19;
                Label(Rows[i], 10, y, 140, Color.white);
                values[i] = Label("—", 154, y, 136, Color.white);
            }
        }

        for (int i = 0; i < Keys.Length; i++)
        {
            string text = hud?.Text(Keys[i]);
            if (i == 0)
            {
                if (text == "AlignAndClock") text = "Align + clock";
                if (text == "AlignAndSpin") text = "Align + spin";
            }
            values[i].text = text == null ? "—" : text + Units[i];
        }
    }
}
