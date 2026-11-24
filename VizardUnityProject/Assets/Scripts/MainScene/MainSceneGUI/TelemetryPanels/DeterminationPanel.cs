using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DeterminationPanel : StockGncPanel
{
    public static readonly Color Amber = Yellow;
    private static readonly Color Unknown = new Color32(140, 140, 140, 255);
    private static readonly string[] Rows = { "Attitude", "Sun", "Ephemeris" };
    private static readonly string[] Keys = { "Attitude determined state", "Sun determined state", "Ephemeris determined state" };

    private static readonly Dictionary<string, (Color color, string text)> States = new()
    {
        { "UndeterminedAttitude", (Red, "Undetermined") },
        { "DeterminedCoarse", (Amber, "Coarse") },
        { "DeterminedFine", (Green, "Fine") },
        { "EphemerisUndetermined", (Red, "Undetermined") },
        { "EphemerisDetermined", (Green, "Determined") },
        { "UndeterminedSunVector", (Red, "Undetermined") },
        { "DeterminedFromInertialModel", (Green, "Inertial model") },
        { "DeterminedFromFineSunSensors", (Green, "Fine sun sensors") },
        { "PropagatedFromFineSunSensors", (Amber, "Propagated FSS") }
    };

    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[Rows.Length];
    private readonly Image[] dots = new Image[Rows.Length];

    protected override void Show(StockHud hud)
    {
        if (Panel == null)
        {
            if (hud == null || System.Array.TrueForAll(Keys, key => hud.Text(key) == null)) return;
            CreatePanel("Determination", 232, 100, PanelDock.Slot.Determination);
            for (int i = 0; i < Rows.Length; i++)
            {
                float y = 30 + i * 20;
                Label(Rows[i], 12, y, 70, Color.white);
                dots[i] = Bar("Status", 84, y + 5, 10, 10, Unknown).GetComponent<Image>();
                values[i] = Label("—", 102, y, 120, Color.white);
            }
        }

        for (int i = 0; i < Keys.Length; i++)
        {
            string state = hud?.Text(Keys[i]);
            if (state != null && States.TryGetValue(state, out var status))
            {
                dots[i].color = status.color;
                values[i].text = status.text;
            }
            else
            {
                dots[i].color = Unknown;
                values[i].text = state ?? "—";
            }
        }
    }
}
