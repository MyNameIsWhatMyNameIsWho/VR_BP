using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;

/// <summary>
/// Handles the 3D buttons that a player can interact with in the VR scene
/// </summary>
[RequireComponent(typeof(Interactable))]
public class VRButton : NetworkBehaviour
{
    /// <value>
    /// The offset of the button that is pressed
    /// </value>
    [SerializeField] private float pressedPositionDifference = 0.025f;

    /// <value>
    /// The delay of the event the button invokes
    /// </value>
    public float secondsBeforeAction = 0f;
    public UnityEvent OnButtonPress;

    private CallDelayer callDelayer;
    private Interactable interactable;
    private Vector3 defaultPosition;

    protected void Awake()
    {
        defaultPosition = transform.position;
        callDelayer = gameObject.AddComponent<CallDelayer>();
        interactable = GetComponent<Interactable>();
    }

    private void Start()
    {
        if (!isServer) return;
        interactable.OnPhysicalCollisionEnter.AddListener(PressBegin);
        interactable.OnPhysicalCollisionExit.AddListener(PressEnd);
        interactable.OnRaycastRockEnter.AddListener(PressBegin);
        interactable.OnRaycastRockExit.AddListener(PressEnd);
        interactable.OnRaycastCollisionExit.AddListener(PressEnd);
    }

    private void OnEnable()
    {
        callDelayer.action.AddListener(OnButtonPress.Invoke);
        if (!isServer) return;
        interactable.OnPhysicalCollisionEnter.AddListener(PressBegin);
        interactable.OnPhysicalCollisionExit.AddListener(PressEnd);
        interactable.OnRaycastRockEnter.AddListener(PressBegin);
        interactable.OnRaycastRockExit.AddListener(PressEnd);
        interactable.OnRaycastCollisionExit.AddListener(PressEnd);
    }

    private void OnDisable()
    {
        transform.position = defaultPosition;
        if (isServer) callDelayer.StopCall();

        callDelayer.action.RemoveListener(OnButtonPress.Invoke);

        if (!isServer) return;
        interactable.OnPhysicalCollisionEnter.RemoveListener(PressBegin);
        interactable.OnPhysicalCollisionExit.RemoveListener(PressEnd);
        interactable.OnRaycastRockEnter.RemoveListener(PressBegin);
        interactable.OnRaycastRockExit.RemoveListener(PressEnd);
        interactable.OnRaycastCollisionExit.RemoveListener(PressEnd);
    }

    /// <summary>
    /// Handles button press depending on the input value (pressed or not)
    /// </summary>
    [ClientRpc]
    public void RpcPress(bool pressed, bool isLeft)
    {
        var handL = GestureDetector.Instance.handL;
        var handR = GestureDetector.Instance.handR;

        HandManager hand = isLeft ? handL : handR;
        
        if (pressed)
        {
            if (isServer) { 
                transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - pressedPositionDifference);
                callDelayer.StartCall(secondsBeforeAction, hand); 
            }
            AudioManager.Instance.PlaySFX("ButtonFeedback");
        } else {
            if (isServer) { 
                transform.position = defaultPosition;
                callDelayer.StopCall(); 
            }
            AudioManager.Instance.StopPlayingSFX();
        }
    }

    public void PressBegin(Interactor interactor)
    {
        var handL = GestureDetector.Instance.handL;

        RpcPress(true, interactor == handL.Interactor);
    }

    public void PressEnd(Interactor interactor)
    {
        var handL = GestureDetector.Instance.handL;

        RpcPress(false, interactor == handL.Interactor);
    }
}
