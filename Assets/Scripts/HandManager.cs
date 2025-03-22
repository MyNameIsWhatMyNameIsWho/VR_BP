using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;

/// <summary>
/// Handles actions invoked by gestures. 
/// Saves what the hand holds, touches; visualizes progress of actions; displays cast rays.
/// </summary>
[RequireComponent(typeof(Interactor))]
public class HandManager : NetworkBehaviour
{
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    public Interactor Interactor { get; private set; }

    public GestureType? CurrentGestureType { get; private set; }

    //shader values
    private float shaderProgress = 0.0f;
    private float? progressDuration;
    //private float currTime = 0.0f;
    [SerializeField] private Color defaultBaseColor = new Color(1f, 1f, 1f, 0.1f);
    [SerializeField] private Color disableColor = new Color(1f, 0.5f, 0f, 0.1f);
    [SerializeField] private Color selectedBaseColor = new Color(1f, 0.8f, 0f, 0.1f);
    private bool IsShaderDone { get { return shaderProgress >= 1.0f; } }
    public bool IsDisabled { get; private set; }
    private void Awake()
    {
        Interactor = GetComponent<Interactor>();
        CurrentGestureType = null;
        IsDisabled = false;

        Interactor.GetRayEndpoint = (center, background) => new Vector3(center.position.x, center.position.y, background.position.z);
        Interactor.GetRayStartpoint = (center) => center.position - Vector3.forward;
        Interactor.GetRayDirection = (center) => Vector3.forward;

    }

    /*private void Update()
    {
        if (!isServer) return;
        UpdateShaderProgress(Time.deltaTime);
    }

    public void StartShaderProgress(float duration)
    {
        progressDuration = duration;
    }

    public void EndShaderProgress()
    {
        progressDuration = null;
    }

    public void UpdateShaderProgress(float deltaTime)
    {
        var prevShaderProgress = shaderProgress;

        if (progressDuration.HasValue)
        {
            shaderProgress = currTime / progressDuration.Value;
            currTime += deltaTime;

            if (IsShaderDone)
            {
                EndShaderProgress();
                shaderProgress = 1.0f;
            }
        }
        else
        {
            currTime = 0.0f;
            shaderProgress = 0.0f;
        }

        if (prevShaderProgress != shaderProgress) SetProgressHand(shaderProgress);

    }*/

    public void GestureChanged(GestureType? newGestureType)
    {
        if (CurrentGestureType.HasValue)
        {
            Interactor.NotifyObjectOfGesture(CurrentGestureType.Value, false);
        }

        if (newGestureType.HasValue)
        {
            Interactor.NotifyObjectOfGesture(newGestureType.Value, true);
        }
        CurrentGestureType = newGestureType;
    }

    [ClientRpc]
    public void SetProgressHand(float shaderProgress)
    {
        skinnedMeshRenderer.material.SetFloat("_Progress", shaderProgress);

    }

    /// <summary>
    /// Sets if this hand is selected and thus should change its visuals to provide feedback to the player
    /// </summary>
    /// <param name="selected"></param>
    [ClientRpc]
    public void SetControllingHand(bool selected)
    {
        if (IsDisabled) return;
        if (selected) skinnedMeshRenderer.material.SetColor("_BaseColor", selectedBaseColor);
        else skinnedMeshRenderer.material.SetColor("_BaseColor", defaultBaseColor);
    }

    [ClientRpc]
    internal void SetUsage(bool canBeUsed)
    {
        IsDisabled = !canBeUsed;
        if (IsDisabled) {
            skinnedMeshRenderer.material.SetColor("_BaseColor", disableColor);
            CurrentGestureType = null;
            Interactor.enabled = false;
        } else { 
            skinnedMeshRenderer.material.SetColor("_BaseColor", defaultBaseColor);
            Interactor.enabled = true;
        }
    }

    /*public void PerformGestureAction(GestureType gestureType)
    {
        switch (gestureType)
        {
            case GestureType.Rock:
                if (touchedObject && touchedObject.gameObject.layer == 6) //Grabbable
                {
                    heldObject = touchedObject;
                    heldObject.transform.parent = center;
                    heldObject.transform.position = center.position;

                    heldObject.GetComponent<TangramPiece>().GrabPiece(this);
                    touchedObject = null;
                } else if (touchedObject && touchedObject.gameObject.layer == 7) //Hittable
                {
                    var button = touchedObject.GetComponent<VRButton>();
                    if (button) {
                        button.Press(true);
                        StartShaderProgress(button.secondsBeforeAction);
                        
                    }
                }
                break;
            case GestureType.Paper:
                break;
            default:
                print("UNRECOGNIZED GESTURE");
                break;
        }
    }

    public void CancelGestureAction()
    {
        if (heldObject)
        {
            heldObject.transform.parent = null;
            heldObject.GetComponent<TangramPiece>().PieceDropped();
            heldObject.HoverEnd();
            heldObject = null;
        } else if (touchedObject && touchedObject.gameObject.layer == 7)
        {
            var button = touchedObject.GetComponent<VRButton>();
            if (button) {
                button.Press(false);
                EndShaderProgress();
            }
        }
    }*/


}
