using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Mirror;
using Unity.VisualScripting;

/// <summary>
/// Handles saving and deleting custom gestures
/// </summary>
public class GestureSettingsManager : NetworkBehaviour
{
    [SerializeField] List<TextMeshProUGUI> numberTexts;
    [SerializeField] List<Button> saveButtons;
    [SerializeField] List<Button> deleteButtons;

    [SerializeField] private Toggle toggleL;
    [SerializeField] private Toggle toggleR;

    private void Start()
    {
        StartCoroutine(ChangeNumbers());
        toggleL.isOn = GestureDetector.Instance.InputEnabledHandL;
        toggleR.isOn = GestureDetector.Instance.InputEnabledHandR;

    }

    /*private void OnEnable()
    {
        if (!NetworkClient.active) return;
        StartCoroutine(ChangeNumbers());
    }*/

    IEnumerator ChangeNumbers ()
    {
        while (!NetworkClient.active)
        {
            yield return null;
        }
        GestureDetector.Instance.CmdSetNumOfGesturesOfType(true, GestureType.Rock, 0);
        GestureDetector.Instance.CmdSetNumOfGesturesOfType(false, GestureType.Rock, 1);
        GestureDetector.Instance.CmdSetNumOfGesturesOfType(true, GestureType.Paper, 2);
        GestureDetector.Instance.CmdSetNumOfGesturesOfType(false, GestureType.Paper, 3);
    }

    public void UpdateData(int num, int idx)
    {
        numberTexts[idx].text = num + "/3";
        saveButtons[idx].interactable = num < 3;
        deleteButtons[idx].interactable = num > 0;
    }

    public void SaveGestureLeft(int gestureType)
    {
        GestureDetector.Instance.CmdSaveGesture(true, (GestureType)gestureType);
        StartCoroutine(ChangeNumbers());
    }

    public void SaveGestureRight(int gestureType)
    {
        GestureDetector.Instance.CmdSaveGesture(false, (GestureType)gestureType);
        StartCoroutine(ChangeNumbers());
    }

    public void DeleteGesturesLeft(int gestureType)
    {
        GestureDetector.Instance.CmdDeleteGestures(true, (GestureType)gestureType);
        StartCoroutine(ChangeNumbers());
    }

    public void DeleteGesturesRight(int gestureType)
    {
        GestureDetector.Instance.CmdDeleteGestures(false, (GestureType)gestureType);
        StartCoroutine(ChangeNumbers());
    }

    [Command(requiresAuthority = false)]
    public void SetInputHandL(bool enabled)
    {
        GestureDetector.Instance.InputEnabledHandL = enabled;
    }

    [Command(requiresAuthority = false)]
    public void SetInputHandR(bool enabled)
    {
        GestureDetector.Instance.InputEnabledHandR = enabled;
    }

}

