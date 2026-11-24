using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public sealed class StarTrackerPanel : StockGncPanel
{
    private sealed class AngleColumn
    {
        public TextMeshProUGUI angle;
        public TextMeshProUGUI limit;
        public TextMeshProUGUI clearance;
        public RectTransform fill;
        public RectTransform marker;
    }

    private const float TrackerWidth = 132;
    private const float RateLimitDegPerSecond = 3f;
    // The DS2 exporter preserves the tracker order in DS2_spacecraft_config.toml and omits the IDs.
    private static readonly string[] Ds2TrackerIds = { "A", "B" };
    private readonly List<AngleColumn> columns = new();
    private RectTransform rateDivider;
    private RectTransform detailBackground;
    private TextMeshProUGUI rateText;

    protected override void Show(StockHud hud)
    {
        var sun = hud?.Numbers("STR to Sun angle");
        var sunLimits = hud?.Numbers("Sun avoidance angle");
        var sunFlags = hud?.Flags("STR to Sun illumination constraint met");
        var earth = hud?.Numbers("STR to Earth angle");
        var earthLimits = hud?.Numbers("Earth avoidance angle");
        var earthFlags = hud?.Flags("STR to Earth illumination constraint met");

        int sunCount = Math.Max(sun?.Length ?? 0, Math.Max(sunLimits?.Length ?? 0, sunFlags?.Length ?? 0));
        int earthCount = Math.Max(earth?.Length ?? 0, Math.Max(earthLimits?.Length ?? 0, earthFlags?.Length ?? 0));
        int trackers = Math.Max(sunCount, earthCount);
        if (Panel == null)
        {
            if (trackers == 0) return;
            Build(trackers);
        }
        if (columns.Count / 2 < trackers) AddTrackers(trackers, hud.Spacecraft);
        for (int i = 0; i < columns.Count / 2; i++)
        {
            UpdateColumn(columns[2 * i], NumberAt(sun, i), NumberAt(sunLimits, i), FlagAt(sunFlags, i));
            UpdateColumn(columns[2 * i + 1], NumberAt(earth, i), NumberAt(earthLimits, i), FlagAt(earthFlags, i));
        }

        float? norm = NumberAt(hud?.Numbers("SC omega_b truth norm"), 0);
        bool? withinLimit = RateConstraint(norm);
        rateText.text = "Rate constraint met (3 deg/s): " + (withinLimit?.ToString() ?? "—");
        rateText.color = StatusColor(withinLimit);
    }

    // The stock HUD rounds the norm to 0.001 deg/s. Equality conceals which side of the limit the true value occupies.
    public static bool? RateConstraint(float? recordedNorm)
    {
        if (!recordedNorm.HasValue || recordedNorm.Value == RateLimitDegPerSecond) return null;
        return recordedNorm.Value < RateLimitDegPerSecond;
    }

    private static bool? FlagAt(bool?[] values, int index) =>
        values != null && index < values.Length ? values[index] : (bool?)null;

    private static Color StatusColor(bool? pass) =>
        !pass.HasValue ? Color.gray : pass.Value ? Green : Red;

    private void Build(int trackers)
    {
        CreatePanel("Star Trackers", 16 + TrackerWidth * trackers, 264, PanelDock.Slot.StarTrackers);
        detailBackground = Bar("Details", 5, 164, 1, 70, new Color(1, 1, 1, .06f));
        rateDivider = Bar("Rate divider", 8, 238, 1, 1, new Color(1, 1, 1, .25f));
        rateText = Centered("—", 8, 242, 1, Color.white, 11);
    }

    private void AddTrackers(int trackers, string spacecraft)
    {
        float width = TrackerWidth * trackers;
        Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(16 + width, 264);
        detailBackground.sizeDelta = new Vector2(width + 6, 70);
        rateDivider.sizeDelta = new Vector2(width, 1);
        rateText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        for (int index = columns.Count / 2; index < trackers; index++)
        {
            float x = 8 + TrackerWidth * index;
            string id = spacecraft == "DeepSpace-2" && index < Ds2TrackerIds.Length
                ? Ds2TrackerIds[index] : "Tracker " + (index + 1);
            Centered(id, x, 22, TrackerWidth, Color.white, 15);
            if (index > 0) Bar("Tracker divider", x, 28, 1, 202, new Color(1, 1, 1, .25f));
            columns.Add(CreateColumn("Sun", x));
            columns.Add(CreateColumn("Earth", x + 66));
        }
    }

    private TextMeshProUGUI Centered(string text, float x, float y, float width, Color color, float size = 12) =>
        Label(text, x, y, width, color, size, TextAlignmentOptions.Midline);

    private AngleColumn CreateColumn(string body, float x)
    {
        Centered(body, x, 42, 66, Color.white);
        var angle = Centered("—", x, 60, 66, Cyan, 14);
        Centered("180", x, 78, 25, Color.white, 10);
        Centered("0", x, 146, 25, Color.white, 10);
        Bar("Track", x + 27, 84, 12, 76, new Color(.85f, .85f, .85f));
        var fill = Bar("Angle", x + 27, 84, 12, 76, Cyan);
        var marker = Bar("Limit", x + 23, 84, 20, 4, Yellow);
        Centered("Limit", x, 165, 66, Yellow);
        var limit = Centered("—", x, 181, 66, Color.white);
        Centered("Clearance", x, 199, 66, Cyan);
        var clearance = Centered("—", x, 216, 66, Color.gray);
        return new AngleColumn { angle = angle, limit = limit, clearance = clearance, fill = fill, marker = marker };
    }

    private static void UpdateColumn(AngleColumn column, float? angle, float? limit, bool? clearance)
    {
        column.angle.text = AngleText(angle);
        column.limit.text = AngleText(limit);
        if (!angle.HasValue || !limit.HasValue) clearance = null;
        column.clearance.text = clearance?.ToString() ?? "—";
        column.clearance.color = StatusColor(clearance);
        column.fill.gameObject.SetActive(angle.HasValue);
        column.marker.gameObject.SetActive(limit.HasValue);
        if (angle.HasValue)
        {
            float height = 76 * Mathf.Clamp01(angle.Value / 180);
            column.fill.sizeDelta = new Vector2(12, height);
            column.fill.anchoredPosition = new Vector2(column.fill.anchoredPosition.x, height - 160);
        }
        if (limit.HasValue)
        {
            float y = 76 * Mathf.Clamp01(limit.Value / 180) - 158;
            column.marker.anchoredPosition = new Vector2(column.marker.anchoredPosition.x, y);
        }
    }

    private static string AngleText(float? angle) =>
        angle.HasValue ? angle.Value.ToString("F1", CultureInfo.InvariantCulture) + "°" : "—";
}
