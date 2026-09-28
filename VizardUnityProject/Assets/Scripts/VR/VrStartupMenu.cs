using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Startup-only VR presentation, recentering and controller-operated text entry.</summary>
public sealed class VrStartupMenu : MonoBehaviour
{
    public Camera head;
    public Canvas menu;
    public GameObject mainPanel;
    public StartUpScreenManager startup;
    public InputActionReference headTracked;
    public InputActionReference leftGrip;
    public InputActionReference rightGrip;
    public VrMenuController leftController;
    public VrMenuController rightController;
    public TextMeshProUGUI help;

    GameObject keyboard;
    TextMeshProUGUI keyboardText;
    string draft;
    bool gripsWerePressed;
    bool wasHeadTracked;
    bool shifted;
    bool keyboardOpening;
    float recenterAfter = -1;

    public bool KeyboardVisible => keyboard != null && keyboard.activeSelf;

    IEnumerator Start()
    {
        // Android system keyboards are not reliably visible inside an OpenXR session.
        startup.socketAddressInput.shouldHideSoftKeyboard = true;
        startup.socketAddressInput.readOnly = true;
        startup.socketAddressInput.onSelect.AddListener(OpenKeyboard);
        BuildKeyboard();
        yield return null;
        Recenter();
        Debug.Log("VR startup menu ready: world-space canvas, tracked camera and two controller rays.");
    }

    void Update()
    {
        bool tracked = headTracked != null && headTracked.action.IsPressed();
        if (tracked && !wasHeadTracked) recenterAfter = Time.unscaledTime + .25f;
        wasHeadTracked = tracked;
        if (recenterAfter >= 0 && Time.unscaledTime >= recenterAfter)
        {
            Recenter();
            recenterAfter = -1;
        }
        bool grips = leftGrip.action.IsPressed() && rightGrip.action.IsPressed();
        if (grips && !gripsWerePressed) Recenter();
        gripsWerePressed = grips;
        help.text = leftController.Tracked || rightController.Tracked
            ? "Point and pull trigger to select  |  Thumbstick to scroll  |  Squeeze both grips to recenter"
            : "Waiting for controllers. Turn them on and bring them into view.";
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) recenterAfter = Time.unscaledTime + .25f;
    }

    public void Recenter()
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        forward.Normalize();
        menu.transform.SetPositionAndRotation(head.transform.position + forward * 1.7f,
            Quaternion.LookRotation(forward, Vector3.up));
    }

    public void OpenKeyboard(string unused)
    {
        if (keyboard == null || KeyboardVisible || keyboardOpening) return;
        StartCoroutine(OpenKeyboardAfterRelease());
    }

    IEnumerator OpenKeyboardAfterRelease()
    {
        keyboardOpening = true;
        while (leftController.ray.uiPressInput.ReadIsPerformed() || rightController.ray.uiPressInput.ReadIsPerformed())
            yield return null;
        yield return null;
        draft = startup.socketAddressInput.text;
        keyboardText.text = draft + "|";
        mainPanel.SetActive(false);
        keyboard.SetActive(true);
        keyboard.transform.SetAsLastSibling();
        keyboardOpening = false;
    }

    void CloseKeyboard(bool accept)
    {
        keyboard.SetActive(false);
        if (accept) startup.socketAddressInput.text = draft;
        mainPanel.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    void BuildKeyboard()
    {
        keyboard = new GameObject("VR Address Keyboard", typeof(RectTransform), typeof(Image));
        keyboard.transform.SetParent(menu.transform, false);
        var rect = (RectTransform)keyboard.transform;
        rect.sizeDelta = new Vector2(1120, 640);
        keyboard.GetComponent<Image>().color = new Color(.05f, .08f, .13f, 1);
        Label("Connection address", keyboard.transform, new Vector2(0, 262), new Vector2(1020, 50), 32);
        keyboardText = Label("", keyboard.transform, new Vector2(0, 196), new Vector2(1020, 65), 28);
        keyboardText.overflowMode = TextOverflowModes.Ellipsis;
        string[] rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm", ":/.-_[]" };
        for (int row = 0; row < rows.Length; row++)
        {
            string keys = rows[row];
            for (int col = 0; col < keys.Length; col++)
            {
                string key = keys[col].ToString();
                Button button = Key(key, new Vector2((col - (keys.Length - 1) / 2f) * 88, 110 - row * 64), 78);
                button.onClick.AddListener(() => { draft += shifted ? key.ToUpperInvariant() : key; keyboardText.text = draft + "|"; });
            }
        }
        var shift = Key("Shift", new Vector2(-440, -226), 140);
        shift.onClick.AddListener(() => { shifted = !shifted; shift.GetComponentInChildren<TextMeshProUGUI>().text = shifted ? "SHIFT ON" : "Shift"; });
        Key("Delete", new Vector2(-275, -226), 160).onClick.AddListener(() => { if (draft.Length > 0) draft = draft.Substring(0, draft.Length - 1); keyboardText.text = draft + "|"; });
        Key("Clear", new Vector2(-100, -226), 150).onClick.AddListener(() => { draft = ""; keyboardText.text = "|"; });
        Key("Cancel", new Vector2(190, -226), 170).onClick.AddListener(() => CloseKeyboard(false));
        Key("Done", new Vector2(410, -226), 210).onClick.AddListener(() => CloseKeyboard(true));
        keyboard.SetActive(false);
    }

    Button Key(string title, Vector2 position, float width)
    {
        var go = new GameObject("Key " + title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(keyboard.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 54);
        go.GetComponent<Image>().color = new Color(.17f, .25f, .34f, 1);
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(.3f, .9f, 1);
        colors.pressedColor = new Color(.1f, .6f, .8f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(title, go.transform, Vector2.zero, rect.sizeDelta, 25);
        return button;
    }

    TextMeshProUGUI Label(string title, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = startup.socketAddressInput.textComponent.font;
        text.text = title;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
