using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using VizProtobufferMessage;

// AstroForge's stock exporter sends an update to a persistent "HUD" dialog at each
// actuator sample. The intervening visualization frames retain the last sample.
public sealed class StockHud
{
    private readonly Dictionary<string, string> fields = new Dictionary<string, string>();
    public string Spacecraft { get; }

    public static StockHud Current => StockHudPlayback.Current;

    internal StockHud(string source, string spacecraft)
    {
        Spacecraft = spacecraft;
        foreach (string line in source.Split('\n'))
        {
            int separator = line.IndexOf(':');
            if (separator < 0) continue;
            string value = line.Substring(separator + 1).Trim();
            if (value.Length == 0) continue;
            // Physical units follow the value in square brackets, including scalars.
            int units = value.LastIndexOf(" [", StringComparison.Ordinal);
            if (units >= 0 && value.EndsWith("]", StringComparison.Ordinal))
                value = value.Substring(0, units);
            fields[line.Substring(0, separator).Trim()] = value;
        }
    }

    public static bool IsTelemetry(VizEventDialog dialog)
    {
        return dialog.EventHandlerID == "HUD" && dialog.UserOptions.Count == 0
            && dialog.DurationOfDisplay == 0
            && dialog.DisplayString.StartsWith("Live Data:", StringComparison.Ordinal)
            && dialog.DisplayString.IndexOf("\nSC omega_b truth:", StringComparison.Ordinal) >= 0;
    }

    public string Text(string label) => fields.TryGetValue(label, out var value) ? value : null;

    private static readonly char[] brackets = { '[', ']', '(', ')' };

    private static float[] ParseNumbers(string text, bool pair = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        bool list = text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal);
        if (list) text = text.Substring(1, text.Length - 2);
        if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(brackets) >= 0) return null;

        var elements = text.Split(',');
        if (!list && !pair && elements.Length != 1) return null;
        var result = new float[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            if (float.TryParse(elements[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])
                && !float.IsNaN(result[i]) && !float.IsInfinity(result[i])) continue;
            // An invalid tracker angle does not invalidate the other tracker.
            if (!list) return null;
            result[i] = float.NaN;
        }
        return result;
    }

    public float[] Numbers(string label) => ParseNumbers(Text(label));

    public bool?[] Flags(string label)
    {
        string text = Text(label);
        if (text == null || !text.StartsWith("[", StringComparison.Ordinal)
            || !text.EndsWith("]", StringComparison.Ordinal)) return null;
        string values = text.Substring(1, text.Length - 2);
        if (string.IsNullOrWhiteSpace(values) || values.IndexOfAny(brackets) >= 0) return null;
        var elements = values.Split(',');
        var result = new bool?[elements.Length];
        for (int i = 0; i < elements.Length; i++)
            if (bool.TryParse(elements[i].Trim(), out bool value)) result[i] = value;
        return result;
    }

    private static readonly Regex gimbalEntry = new Regex(@"\G\s*(?:\(([^()]*)\)|None)\s*(?:,|$)");

    public float[] Pair(string label, int index)
    {
        string text = Text(label);
        if (text == null || index < 0) return null;
        var match = gimbalEntry.Match(text.Trim('[', ']'));
        for (int i = 0; match.Success; i++, match = match.NextMatch())
        {
            if (i != index) continue;
            var pair = ParseNumbers(match.Groups[1].Value, pair: true);
            return pair != null && pair.Length == 2 ? pair : null;
        }
        return null;
    }

    internal static string SingleSpacecraft(VizMessage firstMessage)
    {
        // The HUD does not identify its spacecraft. Only the first frame defines all parent names.
        if (firstMessage == null) return null;
        string root = null;
        foreach (var craft in firstMessage.Spacecraft)
        {
            if (!string.IsNullOrEmpty(craft.ParentSpacecraftName)) continue;
            if (root != null || string.IsNullOrEmpty(craft.SpacecraftName)) return null;
            root = craft.SpacecraftName;
        }
        return root;
    }
}
