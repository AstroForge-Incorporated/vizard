using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class StockGncPanel : MonoBehaviour
{
    protected GameObject Panel { get; private set; }
    protected static readonly Color Cyan = new Color32(0, 185, 244, 255);
    protected static readonly Color Yellow = new Color32(255, 224, 65, 255);
    public static readonly Color Red = new Color32(244, 67, 54, 255);
    public static readonly Color Green = new Color32(76, 217, 100, 255);
    private StockHud previous;
    private Sprite barSprite;

    protected void Update()
    {
        var hud = StockHud.Current;
        if (ReferenceEquals(previous, hud)) return;
        previous = hud;
        Show(hud);
    }

    protected abstract void Show(StockHud hud);

    protected void CreatePanel(string title, float width, float height, PanelDock.Slot slot)
    {
        Panel = StockPanelView.Create(title, slot, width, height);
    }

    protected TextMeshProUGUI Label(string text, float x, float y, float width, Color color,
        float fontSize = 11, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var label = StockPanelView.Label(Panel.transform, text, x, y, width, alignment, fontSize);
        label.color = color;
        label.richText = false;
        return label;
    }

    protected RectTransform Bar(string name, float x, float y, float width, float height, Color color)
    {
        if (barSprite == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/GUIGenerics/BarDisplay");
            barSprite = prefab.transform.GetChild(1).GetChild(0).GetComponent<Image>().sprite;
        }

        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(Panel.transform, false);
        Place(image.rectTransform, x, y, width, height);
        image.sprite = barSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    protected static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    protected static float? NumberAt(float[] numbers, int index)
    {
        if (numbers == null || index >= numbers.Length) return null;
        float value = numbers[index];
        return float.IsNaN(value) || float.IsInfinity(value) ? (float?)null : value;
    }

    protected void OnDestroy()
    {
        if (Panel != null) Destroy(Panel);
    }
}
