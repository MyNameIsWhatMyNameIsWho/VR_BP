using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(HoverableObject))]
public class Interactable : MonoBehaviour
{
    public bool canInteractPhysical = true;
    public bool canInteractRaycast = true;
    [SerializeField] private bool isHoverable = true;

    public UnityEvent<Interactor> OnPhysicalRockEnter;
    public UnityEvent<Interactor> OnRaycastRockEnter;
    public UnityEvent<Interactor> OnPhysicalRockExit;
    public UnityEvent<Interactor> OnRaycastRockExit;
                    
    public UnityEvent<Interactor> OnPhysicalCollisionEnter;
    public UnityEvent<Interactor> OnRaycastCollisionEnter;
    public UnityEvent<Interactor> OnPhysicalCollisionExit;
    public UnityEvent<Interactor> OnRaycastCollisionExit;

    private void Awake()
    {
        if (!isHoverable) return;

        var hoverable = GetComponent<HoverableObject>();
        OnPhysicalCollisionEnter.AddListener(hoverable.HoverBegin);
        OnRaycastCollisionEnter.AddListener(hoverable.HoverBegin);
        OnPhysicalCollisionExit.AddListener(hoverable.HoverEnd);
        OnRaycastCollisionExit.AddListener(hoverable.HoverEnd);
    }

    public void DisableAllEvent()
    {
        OnPhysicalCollisionEnter.RemoveAllListeners();
        OnRaycastCollisionEnter.RemoveAllListeners();
        OnPhysicalCollisionExit.RemoveAllListeners();
        OnRaycastCollisionExit.RemoveAllListeners();

        OnPhysicalRockEnter.RemoveAllListeners();
        OnRaycastRockEnter.RemoveAllListeners();
        OnPhysicalRockExit.RemoveAllListeners();
        OnRaycastRockExit.RemoveAllListeners();
    }

    internal void gestureEvent(GestureType type, bool begins, bool physical, Interactor interactor)
    {
        switch (type)
        {
            case GestureType.Rock:
                if (begins && physical)
                {
                    OnPhysicalRockEnter.Invoke(interactor);
                } else if (begins && !physical)
                {
                    OnRaycastRockEnter.Invoke(interactor);
                } else if (!begins && physical)
                {
                    OnPhysicalRockExit.Invoke(interactor);
                } else
                {
                    OnRaycastRockExit.Invoke(interactor);
                }
                break;
            case GestureType.Paper:
                break;
            default:
                break;
        }
    }

    internal void collisionEvent(bool begins, bool physical, Interactor interactor)
    {
        if (begins && physical)
        {
            OnPhysicalCollisionEnter.Invoke(interactor);
        }
        else if (begins && !physical)
        {
            OnRaycastCollisionEnter.Invoke(interactor);
        }
        else if (!begins && physical)
        {
            OnPhysicalCollisionExit.Invoke(interactor);
        }
        else
        {
            OnRaycastCollisionExit.Invoke(interactor);
        }
    }
}
