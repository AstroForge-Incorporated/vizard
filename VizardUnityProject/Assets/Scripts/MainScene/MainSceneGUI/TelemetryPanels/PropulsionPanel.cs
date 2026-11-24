using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VizProtobufferMessage;
using Spacecraft = VizProtobufferMessage.VizMessage.Types.Spacecraft;
using Storage = VizProtobufferMessage.VizMessage.Types.GenericStorage;

public sealed class PropulsionPanel : MonoBehaviour
{
    private const string Missing = "—";
    private const string ManeuverField = "Propulsive Maneuver time remaining";
    private const float Width = 300;
    private GameObject panel;
    private string spacecraft;
    private int tankIndex;
    private Storage firstTank;
    private TextMeshProUGUI remaining, used, maneuver, maneuverLabel;
    private readonly List<(string body, int index, string tag, TextMeshProUGUI value)> thrusters = new();
    private VizMessage lastFrame;
    private StockHud lastHud;
    private float maneuverTop;
    private RectTransform fuelRows;
    private Image fuelDivider;

    private void Update()
    {
        if (DataManager.UseVR || MessageList.TimestepsTotal == 0) return;
        var frame = MessageList.CurrentMessage;
        var hud = StockHud.Current;
        if (frame == null) return;
        bool sameHud = ReferenceEquals(hud, lastHud);
        if (sameHud && (ReferenceEquals(frame, lastFrame) || (panel == null && lastFrame != null))) return;
        lastFrame = frame;
        lastHud = hud;

        if (panel == null)
        {
            var first = MessageList.FirstMessage;
            var hub = FindHub(first, hud?.Spacecraft);
            if (hub == null) return;
            bool hasThrusters = first.Spacecraft.Any(body => body.Thrusters.Count > 0
                && (body.SpacecraftName == hub.SpacecraftName || body.ParentSpacecraftName == hub.SpacecraftName));
            if (!hasThrusters && TankIndex(hub) < 0 && hud?.Text(ManeuverField) == null) return;
            Build(first, hub);
        }

        if (firstTank != null)
        {
            var devices = FindBody(frame, spacecraft)?.StorageDevices;
            var tank = devices != null && tankIndex < devices.Count ? devices[tankIndex] : null;
            if (tank != null && tank.Label.Length > 0 && tank.Label != firstTank.Label) tank = null;
            remaining.text = tank != null && Valid(tank.CurrentValue) && Valid(tank.MaxValue)
                ? FormattableString.Invariant($"{tank.CurrentValue:F5} / {tank.MaxValue:0.#####} kg") : Missing;
            used.text = tank != null && Valid(tank.CurrentValue) && Valid(firstTank.CurrentValue)
                ? FormattableString.Invariant($"{(firstTank.CurrentValue - tank.CurrentValue) * 1000:F2} g") : Missing;
        }

        foreach (var (body, index, tag, value) in thrusters)
        {
            var current = FindBody(frame, body)?.Thrusters;
            var thruster = current != null && index < current.Count ? current[index] : null;
            value.text = thruster != null && (thruster.ThrusterTag.Length == 0 || thruster.ThrusterTag == tag)
                ? FormatThrust(thruster.CurrentThrust) : Missing;
        }

        var seconds = hud?.Spacecraft == spacecraft ? hud.Text(ManeuverField) : null;
        bool showManeuver = double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out double duration)
            && Valid(duration);
        maneuver.text = showManeuver ? seconds + " s" : Missing;
        if (showManeuver != maneuver.gameObject.activeSelf)
            SetManeuverVisible(showManeuver);
    }

    private static bool Valid(double value) => value >= 0 && !double.IsInfinity(value);

    public static string FormatThrust(double newtons)
    {
        if (!Valid(newtons)) return Missing;
        return newtons == 0 ? "Off · 0 mN" : FormattableString.Invariant($"On · {newtons * 1000:G4} mN");
    }

    private static Spacecraft FindBody(VizMessage frame, string name)
    {
        foreach (var body in frame.Spacecraft)
            if (body.SpacecraftName == name) return body;
        return null;
    }

    private static int TankIndex(Spacecraft hub)
    {
        for (int i = 0; i < hub.StorageDevices.Count; i++)
            if (hub.StorageDevices[i].Label == "Total Propellant" && hub.StorageDevices[i].Units == "kg")
                return i;
        return -1;
    }

    private static Spacecraft FindHub(VizMessage first, string hudSpacecraft)
    {
        if (hudSpacecraft != null) return FindBody(first, hudSpacecraft);
        // The stock DS2 open-loop export can lack the text HUD entirely.
        var a = FindBody(first, "gimbal_A");
        var b = FindBody(first, "gimbal_B");
        if (a == null || b == null || a.ParentSpacecraftName.Length == 0 || a.ParentSpacecraftName != b.ParentSpacecraftName)
            return null;
        var hub = FindBody(first, a.ParentSpacecraftName);
        return hub != null && TankIndex(hub) >= 0 ? hub : null;
    }

    private void Build(VizMessage first, Spacecraft hub)
    {
        spacecraft = hub.SpacecraftName;
        tankIndex = TankIndex(hub);
        panel = StockPanelView.Create("Propulsion", PanelDock.Slot.Propulsion, Width, 100);
        float y = 27;
        foreach (var body in first.Spacecraft)
        {
            if (body.SpacecraftName != spacecraft && body.ParentSpacecraftName != spacecraft) continue;
            for (int i = 0; i < body.Thrusters.Count; i++)
            {
                string tag = body.Thrusters[i].ThrusterTag;
                string name = string.IsNullOrEmpty(tag) ? $"{body.SpacecraftName} #{i + 1}" : tag;
                var value = Row(panel.transform, "Sim thrust " + name, ref y);
                thrusters.Add((body.SpacecraftName, i, tag, value));
            }
        }
        maneuverTop = y;
        maneuverLabel = StockPanelView.Label(panel.transform, "Maneuver remaining", 12, y, 125);
        maneuver = StockPanelView.Label(panel.transform, Missing, 141, y, Width - 150);
        if (tankIndex >= 0)
        {
            firstTank = hub.StorageDevices[tankIndex];
            fuelRows = new GameObject("Propellant", typeof(RectTransform)).GetComponent<RectTransform>();
            fuelRows.SetParent(panel.transform, false);
            fuelRows.anchorMin = fuelRows.anchorMax = fuelRows.pivot = Vector2.up;
            float fuelY = 0;
            remaining = Row(fuelRows, "Propellant remaining", ref fuelY);
            used = Row(fuelRows, "Used since start", ref fuelY);
            fuelRows.sizeDelta = new Vector2(Width, fuelY);

            fuelDivider = new GameObject("Fuel divider", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var line = fuelDivider.rectTransform;
            line.SetParent(fuelRows, false);
            line.anchorMin = line.anchorMax = line.pivot = Vector2.up;
            line.anchoredPosition = new Vector2(12, 5);
            line.sizeDelta = new Vector2(Width - 24, 1);
            fuelDivider.color = new Color(1, 1, 1, .25f);
            fuelDivider.raycastTarget = false;
            if (hub.StorageDevices.Count == 1)
                VizardGUISettings.PanelViewMgr.HideDuplicatePropellant(spacecraft);
        }
        SetManeuverVisible(true);
    }

    private void SetManeuverVisible(bool visible)
    {
        maneuver.gameObject.SetActive(visible);
        maneuverLabel.gameObject.SetActive(visible);
        float y = maneuverTop + (visible ? 20 : 0);
        if (fuelRows != null)
        {
            fuelDivider.enabled = visible || thrusters.Count > 0;
            if (fuelDivider.enabled) y += 8;
            fuelRows.anchoredPosition = new Vector2(0, -y);
            y += fuelRows.sizeDelta.y;
        }
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, y + 6);
        VizardGUISettings.PanelViewMgr.TelemetryDock.Reflow(PanelDock.Slot.Propulsion);
    }

    private static TextMeshProUGUI Row(Transform parent, string label, ref float y)
    {
        StockPanelView.Label(parent, label, 12, y, 125);
        var value = StockPanelView.Label(parent, Missing, 141, y, Width - 150);
        y += 20;
        return value;
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
    }
}
