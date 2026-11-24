/*
 ISC License

 Copyright (c) 2025, Autonomous Vehicle Systems Lab, University of Colorado at Boulder

 Permission to use, copy, modify, and/or distribute this software for any
 purpose with or without fee is hereby granted, provided that the above
 copyright notice and this permission notice appear in all copies.

 THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

 */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Manages the Alert Icon Tray that shows an icon
/// onscreen for minimized Event Dialogs and allows the user
/// to maximize them again by clicking on the icon and selecting
/// the desired panel from inventory.
/// </summary>
public class AlertIconManager : MonoBehaviour
{
    [Header("Alert Icon Tray GUI Components")]
    public Button warningButton;
    public Button cautionButton;
    public Button notificationButton;
    public GameObject alertIconTrayButtonBlocker;
    
    [Header("Minimized Event Dialog Inventory")]
    public GameObject minimizedPanelInventory;
    public RectTransform inventoryContentTransform;
    public TextMeshProUGUI inventoryPanelLabel;

    private List<GameObject> cautionPanels = new List<GameObject>();
    private List<GameObject> warningPanels = new List<GameObject>();
    private List<GameObject> notificationPanels = new List<GameObject>();
    private List<GameObject> currentInventoryButtons;

    private string currentInventoryType;

    // Start is called before the first frame update
    void Start()
    {
        if (!DataManager.UseVR)
        {
            cautionButton.GetComponentInChildren<TextMeshProUGUI>().text = "\u25B2";
            notificationButton.GetComponentInChildren<TextMeshProUGUI>().text = "\u25A0";
        }

        warningButton.onClick.AddListener(DisplayAvailableWarnings);
        cautionButton.onClick.AddListener(DisplayAvailableCautions);
        notificationButton.onClick.AddListener(DisplayAvailableNotifications);
    }

    void FixedUpdate()
    {
        if (minimizedPanelInventory.activeSelf && inventoryContentTransform.childCount == 0)
        {
            minimizedPanelInventory.SetActive(false);
        }
    }

    public void DisplayAvailablePanels()
    {
        if (warningPanels.Count != 0)
        {
            DisplayAvailableWarnings();
        }
        else if (cautionPanels.Count != 0)
        {
            DisplayAvailableCautions();
        }
        else if (notificationPanels.Count != 0)
        {
            DisplayAvailableNotifications();
        }
    }

    private void DisplayAvailableWarnings()
    {
        if (!DataManager.UseVR)
        {
            if (warningPanels.Count == 1)
            {
                warningPanels[0].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
            else
            {
                currentInventoryType = "WARNING";
                ShowEventDialogInventory(warningPanels);
            }
        }
        else
        {
            for (int i = warningPanels.Count - 1; i >= 0; i--)
            {
                warningPanels[i].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
        }
    }

    private void DisplayAvailableCautions()
    {
        if (!DataManager.UseVR)
        {
            if (cautionPanels.Count == 1)
            {
                cautionPanels[0].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
            else
            {
                currentInventoryType = "CAUTION";
                ShowEventDialogInventory(cautionPanels);
            }
        }
        else
        {
            for (int i = cautionPanels.Count - 1; i >= 0; i--)
            {
                cautionPanels[i].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
        }
    }

    private void DisplayAvailableNotifications()
    {
        if (!DataManager.UseVR)
        {
            if (notificationPanels.Count == 1)
            {
                notificationPanels[0].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
            else
            {
                currentInventoryType = "NOTIFICATION";
                ShowEventDialogInventory(notificationPanels);
            }
        }
        else
        {
            for (int i = notificationPanels.Count - 1; i >= 0; i--)
            {
                notificationPanels[i].GetComponent<EventDialogHandler>().TogglePanelDisplay();
            }
        }
    }

    public void DockReplayNotificationButton(Transform playArea)
    {
        var rect = (RectTransform)notificationButton.transform;
        rect.SetParent(playArea, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        // Align with the minus control at the lower left of the play area.
        rect.anchoredPosition = new Vector2(18f, 30f);
    }

    public void AddMinimizedPanel(GameObject newPanel, string type)
    {
        bool updateInventory = (minimizedPanelInventory.activeSelf && (type == currentInventoryType));
        if (type == "WARNING")
        {
            warningButton.transform.gameObject.SetActive(true);
            if (!warningPanels.Contains(newPanel))
            {
                warningPanels.Add(newPanel);
                if (updateInventory)
                {
                    ShowEventDialogInventory(warningPanels);
                }
            }
        }
        else if (type == "CAUTION")
        {
            cautionButton.transform.gameObject.SetActive(true);
            if (!cautionPanels.Contains(newPanel))
            {
                cautionPanels.Add(newPanel);
                if (updateInventory)
                {
                    ShowEventDialogInventory(cautionPanels);
                }
            }
        }
        else
        {
            notificationButton.transform.gameObject.SetActive(true);
            if (!notificationPanels.Contains(newPanel))
            {
                notificationPanels.Add(newPanel);
                if (updateInventory)
                {
                    ShowEventDialogInventory(notificationPanels);
                }
            }
        }
    }

    public void RemoveButtonAndPanelFromInventory(GameObject deadPanel)
    {
        EventDialogHandler deadHandler = deadPanel.GetComponent<EventDialogHandler>();
        GameObject deadButton = deadHandler.GetInventoryButton();
        string type = deadHandler.GetDialogType();
        if (type == "WARNING")
        {
            warningPanels.Remove(deadPanel);
            warningButton.gameObject.SetActive(warningPanels.Count >= 1);
        }
        else if (type == "CAUTION")
        {
            cautionPanels.Remove(deadPanel);
            cautionButton.gameObject.SetActive(cautionPanels.Count >= 1);
        }
        else
        {
            notificationPanels.Remove(deadPanel);
            notificationButton.gameObject.SetActive(notificationPanels.Count >= 1);
        }

        if ((deadButton != null) && (currentInventoryButtons.Contains(deadButton)))
        {
            currentInventoryButtons.Remove(deadButton);
            ReorderButtonsInInventoryContent();
            Destroy(deadButton);
        }
    }

    private void ShowEventDialogInventory(List<GameObject> panelsToInventory)
    {
        inventoryPanelLabel.text = "Available " + currentInventoryType[0].ToString().ToUpper() +
                                   currentInventoryType.Substring(1).ToLower() + "s";
        currentInventoryButtons = new List<GameObject>();
        foreach (Transform child in inventoryContentTransform)
        {
            Destroy(child.gameObject);
        }

        Vector2 existingSize = minimizedPanelInventory.GetComponent<RectTransform>().sizeDelta;
        minimizedPanelInventory.GetComponent<RectTransform>().sizeDelta =
            new Vector2(existingSize.x, (panelsToInventory.Count + 1) * 30);

        int count = 0;
        foreach (GameObject panel in panelsToInventory)
        {
            GameObject newButton = Instantiate(Resources.Load("Prefabs/GUIGenerics/GenericTMPButton") as GameObject,
                inventoryContentTransform);
            newButton.GetComponentInChildren<TMP_Text>().text = panel.name;
            newButton.name = panel.name + "InvButton";
            panel.GetComponent<EventDialogHandler>().AddInventoryButton(newButton);
            newButton.transform.SetParent(inventoryContentTransform, false);
            newButton.GetComponentInChildren<RectTransform>().anchoredPosition = new Vector2(0, -count * 30);
            currentInventoryButtons.Add(newButton);
            count++;
        }

        minimizedPanelInventory.SetActive(true);
    }

    public void HideDialogInventory()
    {
        minimizedPanelInventory.SetActive(false);
    }

    private void ReorderButtonsInInventoryContent()
    {
        int count = 0;
        foreach (GameObject button in currentInventoryButtons)
        {
            button.GetComponentInChildren<RectTransform>().anchoredPosition = new Vector2(0, -count * 30);
        }
    }

    public void BlockButtonAccess(bool blockButtons)
    {
        alertIconTrayButtonBlocker.SetActive(blockButtons);
    }
}
