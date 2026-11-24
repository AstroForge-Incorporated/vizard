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
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VizProtobufferMessage;
/// <summary>
/// Sets up, updates, and handles user input for a single EventDialog object
/// </summary>
public class EventDialogHandler : MonoBehaviour
{
    [Header("Event Dialog GUI Components")]
    public string eventHandlerID;
    public GameObject eventDialogPanel;
    public GameObject confirmationPanel;
    public Button confirmButton;
    public Button cancelButton;
    public Button exitButton;
    public TextMeshProUGUI dialogText;
    public TextMeshProUGUI confirmationText;
    public Transform optionButtonContainer;
    public GameObject eventDialogButtonBlocker;
    public GameObject confirmationDialogButtonBlocker;


    private double simTimeToStartDisplay; //nanoseconds
    private double durationOfDisplay; //nanoseconds
    private int hideOnSelection;
    private bool useSimTimeForDisplayDuration;
    private int confirmationPanelBehavior;
    public string panelType;
    private EventDialogManager myManager;
    private Dictionary<string, Button> optionButtonsDictionary = new Dictionary<string, Button>();

    private string confirmationOptionString = "";

    private bool systemClockStarted;
    private DateTime displayStartTime;
    private GameObject inventoryButton;
    private bool leavePanelOnDisplayUntilClosedByUser;
    private bool autoClickConfirm;
    private bool firstUpdate = true;
    private bool minimizedForStockTelemetry;

    private int gridPositionIndex;
    private int gridLayerIndex;
 
    void Update()
    {
        if (MessageList.CurrentIndex == 0)
        {
            systemClockStarted = false;
        }

        if (firstUpdate)
        {
            if (!DataManager.UseVR)
            {
                myManager.SetPanelPosition(this.gameObject);
            }
            firstUpdate = false;
        }

        if (!leavePanelOnDisplayUntilClosedByUser)
        {
            if ((MessageList.CurrentMessage.CurrentTime.SimTimeElapsed >= simTimeToStartDisplay))
            {
                if (useSimTimeForDisplayDuration)
                {
                    if (MessageList.CurrentMessage.CurrentTime.SimTimeElapsed >
                        simTimeToStartDisplay + durationOfDisplay)
                    {
                        DestroyPanelOnDurationOfDisplayExpiration();
                    }
                }
                else //using system clock time
                {
                    if (!systemClockStarted)
                    {
                        displayStartTime = DateTime.Now;
                        systemClockStarted = true;
                        eventDialogPanel.SetActive(true);
                    }
                    else
                    {
                        TimeSpan timeInterval = DateTime.Now - displayStartTime;

                        if (timeInterval.TotalMilliseconds * 1000000 >
                            durationOfDisplay) //using TotalMilliseconds because it is a double instead of TotalSeconds which is an int
                        {
                            DestroyPanelOnDurationOfDisplayExpiration();
                        }
                    }
                }
            }
            else
            {
                eventDialogPanel.SetActive(false);
                confirmationPanel.SetActive(false);
            }
        }

        if ((!DataManager.SocketIsReceiveOnly)&&(ArePanelsActive()==0))
        {
            myManager.SetPanelRelayCurrentStatus(eventHandlerID,-100);
        }
    }

    public void InitializePanel(VizEventDialog dialogMsg, double simTimeAtDialogMsg,
        EventDialogManager eventManager, string prefabType)
    {
        myManager = eventManager;
        this.name = dialogMsg.EventHandlerID;
        eventHandlerID = dialogMsg.EventHandlerID;
        dialogText.text = dialogMsg.DisplayString;
        panelType = prefabType;
        foreach (string option in dialogMsg.UserOptions)
        {
            GameObject optionButton;
            if (!DataManager.UseVR)
            {
                optionButton =
                    Instantiate(Resources.Load("Prefabs/GUIGenerics/TMPLabeledButtonCentered") as GameObject,
                        optionButtonContainer);
                optionButton.GetComponentInChildren<TextMeshProUGUI>().text = option;
            }
            else
            {
                optionButton =
                    Instantiate(Resources.Load("Prefabs/VR/VizardVR_GenericLabeledButtonCentered") as GameObject,
                        optionButtonContainer);
                optionButton.GetComponentInChildren<TextMeshProUGUI>().text = option;
            }

            optionButton.name = option;
            optionButton.GetComponent<Button>().onClick.AddListener(() => OptionButtonWasClicked(option));
            optionButtonsDictionary.Add(option, optionButton.GetComponent<Button>());
        }

        confirmButton.onClick.AddListener(ConfirmButtonWasClicked);
        cancelButton.onClick.AddListener(CancelButtonWasClicked);
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(PanelMinimizeButtonWasClicked);
        }

        hideOnSelection = dialogMsg.HideOnSelection;
        if (hideOnSelection == -1) //Take away ability to minimize the panel
        {
            exitButton.gameObject.SetActive(false);
        }
        confirmationPanelBehavior = dialogMsg.UseConfirmationPanel;

        simTimeToStartDisplay = simTimeAtDialogMsg;
        durationOfDisplay =
            dialogMsg.DurationOfDisplay; //If duration of display = 0, leave this panel open indefinitely, if -1 close immediately
        if (durationOfDisplay >= 0)
        {
            eventDialogPanel.SetActive(true);
            confirmationPanel.SetActive(false);
            displayStartTime = DateTime.Now;
            systemClockStarted = true;
            if (durationOfDisplay == 0)
            {
                leavePanelOnDisplayUntilClosedByUser = true;
            }
        }

        useSimTimeForDisplayDuration = dialogMsg.UseSimElapsedTimeForDuration;

        if (hideOnSelection == -100)
        {
            PanelMinimizeButtonWasClicked();
        }
        else if (!DataManager.IsLiveSim && !DataManager.UseVR && !DataManager.InNoDisplayMode
            && CanStartMinimized(dialogMsg) && StockHud.SingleSpacecraft(MessageList.FirstMessage) != null)
        {
            // A user can reopen the original text from Notifications. Initial layout sends no reply.
            minimizedForStockTelemetry = true;
            eventDialogPanel.SetActive(false);
            myManager.alertIconMgr.DockReplayNotificationButton(myManager.eventDialogPanels.transform);
            myManager.EventDialogMinimized(gameObject, panelType);
        }
    }

    private static bool CanStartMinimized(VizEventDialog dialog)
    {
        return StockHud.IsTelemetry(dialog) && !dialog.ForceEventDialogMaximize && dialog.HideOnSelection == 0
            && dialog.UseConfirmationPanel == 0 && !dialog.UseSimElapsedTimeForDuration
            && (dialog.DialogFormat.Length == 0 || string.Equals(dialog.DialogFormat, "none", StringComparison.OrdinalIgnoreCase));
    }

    private void OptionButtonWasClicked(string buttonOption)
    {
        if (confirmationPanelBehavior != -1) //Require confirmation of option choice
        {
            confirmationPanel.SetActive(true);
            myManager.SetPanelRelayCurrentStatus(eventHandlerID, -102);
            confirmationPanel.transform.parent.transform.parent.SetAsLastSibling();
            if (hideOnSelection != -1)
            {
                eventDialogPanel.SetActive(false);
            }
            confirmationOptionString = buttonOption;
            confirmationText.text =
                "Please confirm your choice: " + confirmationOptionString;

            if (autoClickConfirm)
            {
                StartCoroutine(AutoButtonPress(confirmButton));
                autoClickConfirm = false;
            }
        }
        else //Do not show confirmation panel, just send back event
        {
            bool selfDestructOnSelection = (hideOnSelection!= -1);
            SendDialogEvent(buttonOption, selfDestructOnSelection);
        }
    }

    private IEnumerator AutoButtonPress(Button buttonToPress)
    {
        yield return new WaitForSecondsRealtime(0.2f);
        EventSystem.current.SetSelectedGameObject(buttonToPress.gameObject, new BaseEventData(EventSystem.current));
        yield return new WaitForSecondsRealtime(0.5f);
        ExecuteEvents.Execute(buttonToPress.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    private void ConfirmButtonWasClicked()
    {
        if (hideOnSelection != -1)
        {
            eventDialogPanel.SetActive(false);
        }
        confirmationPanel.SetActive(false);
        bool selfDestructOnSelection = (hideOnSelection!= -1);
        SendDialogEvent(confirmationOptionString, selfDestructOnSelection);
        
        confirmationOptionString = "";

    }

    private void CancelButtonWasClicked()
    {
        confirmationPanel.SetActive(false);
        eventDialogPanel.SetActive(true);
        if (!DataManager.SocketIsReceiveOnly)
        {
            myManager.SetPanelRelayCurrentStatus(eventHandlerID, -101);
        }
        confirmationOptionString = "";
    }

    public void PanelMinimizeButtonWasClicked()
    {
        SendDialogEvent("", false);
        confirmationOptionString = "";
        eventDialogPanel.SetActive(false);
        if (!DataManager.SocketIsReceiveOnly)
        {
            myManager.SetPanelRelayCurrentStatus(eventHandlerID, -100);
        }
        myManager.EventDialogMinimized(this.gameObject, panelType);
    }
    
    private void DestroyPanelOnDurationOfDisplayExpiration()
    {
        confirmationPanel.SetActive(false);
        eventDialogPanel.SetActive(false);
        hideOnSelection = 2; //Destroy panel because it has run out of display time
        SendDialogEvent("", true);
        SendDialogEvent("", true);
    }

    private void SendDialogEvent(string option, bool panelIsDestroyed)
    {
        VizEventReply newReply = new VizEventReply()
        {
            EventHandlerID = eventHandlerID,
            Reply = option,
            EventHandlerDestroyed = panelIsDestroyed
        };

        myManager.ReceiveEventReply(newReply, this.gameObject);
        confirmationPanel.SetActive(false);
        eventDialogPanel.SetActive(hideOnSelection==-1);
    }

    public void UpdateEventDialogPanel(VizEventDialog updateMsg, double simTimeAtUpdateMsg)
    {
        durationOfDisplay = updateMsg.DurationOfDisplay;
        if (durationOfDisplay == -1)
        {
            hideOnSelection = 2; //Destroy panel because it has run out of display time
            SendDialogEvent("", true); //Tell Basilisk the panel has been destroyed
        }
        else
        {
            leavePanelOnDisplayUntilClosedByUser = durationOfDisplay == 0;

            dialogText.text = updateMsg.DisplayString;
            if (minimizedForStockTelemetry && !CanStartMinimized(updateMsg))
            {
                minimizedForStockTelemetry = false;
                if (updateMsg.HideOnSelection != -100)
                {
                    eventDialogPanel.SetActive(true);
                    RemovePanelFromInventory();
                }
            }

            //Replace the options
            foreach (Transform child in optionButtonContainer) //Get rid of the old option buttons
            {
                GameObject childObject = child.gameObject;
                childObject.GetComponent<Button>().onClick
                    .RemoveListener(() => OptionButtonWasClicked(childObject.name));
                childObject.SetActive(false);
                Destroy(childObject);
            }

            optionButtonsDictionary = new Dictionary<string, Button>();

            foreach (string option in updateMsg.UserOptions) //Build the new options
            {
                GameObject optionButton;
                if (!DataManager.UseVR)
                {
                    optionButton =
                        Instantiate(Resources.Load("Prefabs/GUIGenerics/TMPLabeledButtonCentered") as GameObject,
                            optionButtonContainer);
                    optionButton.GetComponentInChildren<TextMeshProUGUI>().text = option;
                }
                else
                {
                    optionButton =
                        Instantiate(Resources.Load("Prefabs/VR/VizardVR_GenericLabeledButtonCentered") as GameObject,
                            optionButtonContainer);
                    optionButton.GetComponentInChildren<TextMeshProUGUI>().text = option;
                }
                optionButton.name = option;
                optionButton.GetComponent<Button>().onClick.AddListener(() => OptionButtonWasClicked(option));
                optionButtonsDictionary.Add(option, optionButton.GetComponent<Button>());
            }

            hideOnSelection = updateMsg.HideOnSelection;
            confirmationPanelBehavior = updateMsg.UseConfirmationPanel;

            simTimeToStartDisplay = simTimeAtUpdateMsg;
            
            useSimTimeForDisplayDuration = updateMsg.UseSimElapsedTimeForDuration;
            if (!useSimTimeForDisplayDuration)
            {
                displayStartTime=DateTime.Now;
            }

        }
    }

    public string GetDialogType()
    {
        return panelType;
    }

    public void AddInventoryButton(GameObject button)
    {
        inventoryButton = button;
        inventoryButton.GetComponent<Button>().onClick.AddListener(TogglePanelDisplay);
    }

    public GameObject GetInventoryButton()
    {
        return inventoryButton;
    }

    private void RemovePanelFromInventory()
    {
        if (inventoryButton != null)
        {
            inventoryButton.GetComponent<Button>().onClick.RemoveListener(TogglePanelDisplay);
        }

        myManager.RemoveButtonAndPanelFromInventory(this.gameObject);
        inventoryButton = null;
    }

    public void TogglePanelDisplay()
    {
        minimizedForStockTelemetry = false;
        eventDialogPanel.SetActive(!eventDialogPanel.activeSelf);
        if (eventDialogPanel.activeSelf)
        {
            RemovePanelFromInventory();
            if (DataManager.IsLiveSim && !DataManager.SocketIsReceiveOnly)
            {
                VizEventReply newReply = new VizEventReply()
                {
                    EventHandlerID = eventHandlerID,
                    Reply = "VizBroadcastReopenPanel"
                };

                myManager.ReceiveEventReply(newReply, this.gameObject);
                myManager.SetPanelRelayCurrentStatus(eventHandlerID, -101);
            }
        }else
        {
            myManager.SetPanelRelayCurrentStatus(eventHandlerID, -100);
        }
    }

    private int ArePanelsActive()
    {
        if (confirmationPanel.activeSelf)
        {
            return 2;
        }

        if (eventDialogPanel.activeSelf)
        {
            return 1;
        }

        return 0;
    }

    public void ApplyBroadcastPanelChoice(string option)
    {
        if (option == "")
        {
            if (eventDialogPanel.activeSelf)
            {
                StartCoroutine(AutoButtonPress(exitButton));
            }
        }
        else if (option == "VizBroadcastReopenPanel"){
            eventDialogPanel.SetActive(true);
            RemovePanelFromInventory();
        }
        else
        {
            eventDialogPanel.SetActive(true);
            RemovePanelFromInventory();
            if (optionButtonsDictionary.TryGetValue(option, out var buttonToClick))
            {
                autoClickConfirm = true;
                StartCoroutine(AutoButtonPress(buttonToClick));
            }
        }
    }

    public void BlockButtonAccess(bool blockButtons)
    {
        if (!DataManager.UseVR)
        {
            eventDialogButtonBlocker.SetActive(blockButtons);
            confirmationDialogButtonBlocker.SetActive(blockButtons);
        }
    }

    public void SetGridLayoutStartingPositionIndices(int gridPosition, int offsetMultiplier)
    {
        gridPositionIndex = gridPosition;
        gridLayerIndex = offsetMultiplier;
    }

    public int[] GetGridLayoutStartingPositionIndices()
    {
        return new [] {gridPositionIndex, gridLayerIndex};
    }
}
