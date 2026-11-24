using System;
using System.Linq;
using UnityEngine;

public sealed class PanelDock
{
    public enum Slot { Determination, AngularVelocity, Guidance, StarTrackers, ReactionWheels, MomentumAllocator, Propulsion, GimbalLimits, Storage }

    // Each column follows Slot order. A panel only occupies space after it exists.
    private readonly GUIPanelLayout cascade;
    private readonly RectTransform[] panels = new RectTransform[Enum.GetValues(typeof(Slot)).Length];
    private readonly Vector2[] positions;
    private bool checkedNativePanels;

    internal PanelDock(GUIPanelLayout cascade)
    {
        this.cascade = cascade;
        positions = new Vector2[panels.Length];
    }

    internal void Place(RectTransform panel, Slot slot)
    {
        // The native panels finish their initial layout before the first telemetry panel appears.
        if (!checkedNativePanels)
        {
            checkedNativePanels = true;
            DockSingleNativePanel<ReactionWheelPanelMethods>(Slot.ReactionWheels);
            if (MessageList.FirstMessage.Spacecraft.Count(craft => string.IsNullOrEmpty(craft.ParentSpacecraftName)) == 1)
                DockSingleNativePanel<GenericStoragePanelMethods>(Slot.Storage);
        }

        PlaceAtDefault(panel, slot);
    }

    private void DockSingleNativePanel<T>(Slot slot) where T : Component
    {
        GameObject candidate = null;
        foreach (var panel in cascade.panelList)
        {
            if (panel == null || !panel.TryGetComponent<T>(out _)) continue;
            if (candidate != null) return;
            candidate = panel;
        }
        if (candidate == null) return;

        // A later camera addition rearranges every panel that still belongs to the cascade.
        cascade.panelList.Remove(candidate);
        PlaceAtDefault(candidate.GetComponent<RectTransform>(), slot);
    }

    private void PlaceAtDefault(RectTransform panel, Slot slot)
    {
        int i = (int)slot;
        panels[i] = panel;
        positions[i] = panel.anchoredPosition;
        Reflow(slot);
    }

    internal void Reflow(Slot slot)
    {
        int split = (int)Slot.ReactionWheels;
        bool right = (int)slot >= split;
        float top = 10;
        for (int i = right ? split : 0; i < (right ? panels.Length : split); i++)
        {
            var panel = panels[i];
            if (panel == null)
                continue;

            // Preserve the position of a panel that the user moved.
            if (panel.anchoredPosition == positions[i])
            {
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(right ? 1 : 0, 1);
                panel.anchoredPosition = new Vector2(right ? -10 : 10, -top);
                positions[i] = panel.anchoredPosition;
            }
            top += panel.rect.height + 8;
        }
    }
}
