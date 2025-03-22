using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Mirror;
using UnityEngine.Events;
using Unity.VisualScripting;

public enum TangramPieceType
{
    LargeTriangle,
    MediumTriangle,
    SmallTriangle,
    Square,
    Trapezoid
}

/// <summary>
/// Implements tangram piece and its functionalities
/// </summary>
[RequireComponent(typeof (TangramPieceEditorActions), typeof(Interactable))]
public class TangramPiece : NetworkBehaviour
{
    /// <value>
    /// Its Z-coordinate is the coordinate the piece to snaps to
    /// </value>
    [SerializeField] private Transform tangramBoardTransform;

    /// <value>
    /// The rotation will snap to multiplies of this value
    /// </value>
    public float angleFactor = 45.0f;
    private float pieceRotationWhenGrabbed, pieceRotationPrev;
    private float rotationHelpMultiplier = 2.0f;

    /// <value>
    /// The piece that will be displayed when this tangram piece is held far from the tangram board
    /// </value>
    [SerializeField] public GameObject ghostPiece, hintPiece;
    [SerializeField] public TangramPieceType type;
    private Color materialColor;

    private Interactor holdingHandInteractor;
    private Interactable interactable;
    
    public UnityEvent<TangramPiece> OnPieceDropped;

    public TangramSlot placedInSlot;
    public bool correctlyPlaced;
    public bool CanInteract { get; set; }

    private void Start()
    {
        correctlyPlaced = false;
        holdingHandInteractor = null;
        placedInSlot = null;
        CanInteract = true;

        var networkTransform = GetComponent<NetworkTransformReliable>();
        networkTransform.positionPrecision = 0.001f; //TODO do in prefab
        networkTransform.rotationSensitivity = 0.1f;

        materialColor = GetComponent<MeshRenderer>().material.color;

        ghostPiece.SetActive(false);

        interactable = GetComponent<Interactable>();
        interactable.OnRaycastRockEnter.AddListener(GrabPiece);
        interactable.OnRaycastRockExit.AddListener(DropPiece);
        GestureDetector.Instance.OnGestureBegin.AddListener((gt, b) => CheckDropPiece(gt, b));
    }

    private void Update()
    {
        if (interactable && !CanInteract) {
            interactable.DisableAllEvent();
            CanInteract = true;
        }

        if (!holdingHandInteractor) return;

        if (holdingHandInteractor && holdingHandInteractor.GetComponent<HandManager>().CurrentGestureType != GestureType.Rock)
        {
            if (isServer) DropPiece(holdingHandInteractor);
            return;
        }

        //update scaled rotation of held piece
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, (transform.rotation.eulerAngles.z - pieceRotationPrev) * rotationHelpMultiplier + pieceRotationPrev);
        pieceRotationPrev = transform.rotation.eulerAngles.z;

        if (!ghostPiece.activeSelf && holdingHandInteractor.IsHandFarFromBackground()) //start displaying ghost piece
        {
            GetComponent<MeshRenderer>().material.color = new Color(materialColor.r, materialColor.g, materialColor.b, 0.5f);
            ghostPiece.SetActive(true);
            ghostPiece.transform.position = new Vector3(transform.position.x, transform.position.y, tangramBoardTransform.position.z);
            ghostPiece.transform.rotation = Quaternion.Euler(0, 0, FindNearestMultiple(transform.rotation.eulerAngles.z));
        } else if (ghostPiece.activeSelf) {
            if (holdingHandInteractor.IsHandFarFromBackground()) //update ghost piece
            {
                ghostPiece.transform.position = new Vector3(transform.position.x, transform.position.y, tangramBoardTransform.position.z); //snap to board
                ghostPiece.transform.rotation = Quaternion.Euler(0, 0, FindNearestMultiple(transform.rotation.eulerAngles.z)); //preserve only z-rotation
            } else { //stop displaying ghost piece
                GetComponent<MeshRenderer>().material.color = materialColor;
                ghostPiece.SetActive(false);
            }                      
        }
    }

    /// <summary>
    /// Handles piece grabbing
    /// </summary>
    [ClientRpc]
    public void GrabPiece(Interactor newInteractor)
    {
        if (!CanInteract) return;


        if (holdingHandInteractor && holdingHandInteractor != newInteractor)
        {
            holdingHandInteractor.searchesForCollision = true;
            //GetComponent<HoverableObject>().HoverEnd();
        }

        holdingHandInteractor = newInteractor;
        holdingHandInteractor.searchesForCollision = false;

        transform.SetParent(holdingHandInteractor.RaycastStartingPoint);
        transform.localPosition = Vector3.zero;

        pieceRotationWhenGrabbed = transform.rotation.eulerAngles.z;        
        pieceRotationPrev = pieceRotationWhenGrabbed;

        LoggerCommunicationProvider.Instance.TangramPieceGrabbed();
    }

    /// <summary>
    /// Handles dropping the piece and snapping it onto the tangram board
    /// </summary>
    [ClientRpc]
    public void DropPiece(Interactor interactor)
    {
        if (interactor != holdingHandInteractor) return;

        transform.position = new Vector3(transform.position.x, transform.position.y, tangramBoardTransform.position.z); //snap to board

        float rotationZ = FindNearestMultiple(transform.rotation.eulerAngles.z);
        transform.rotation = Quaternion.Euler(0, 0, rotationZ); //preserve only z-rotation

        OnPieceDropped.Invoke(this);
        holdingHandInteractor = null;
        interactor.searchesForCollision = true;

        GetComponent<MeshRenderer>().material.color = materialColor;
        ghostPiece.SetActive(false);

        LoggerCommunicationProvider.Instance.TangramPieceDropped(); 
    }

    [ClientRpc]
    private void CheckDropPiece(GestureType gt, bool begins)
    {
        if (gt != GestureType.Paper || !begins) return;
        if (GestureDetector.Instance.handL.CurrentGestureType == GestureType.Paper) DropPiece(GestureDetector.Instance.handL.Interactor);
        if (GestureDetector.Instance.handR.CurrentGestureType == GestureType.Paper) DropPiece(GestureDetector.Instance.handR.Interactor);
    }

    /// <summary>
    /// Finds nearest multiple of angleFactor for the input value and returns it
    /// </summary>
    private float FindNearestMultiple(float value)
    {
        float nearestMultiple;
        nearestMultiple = (int)System.Math.Round((value / (double)angleFactor), System.MidpointRounding.AwayFromZero) * angleFactor;
        return nearestMultiple;
    }

    /// <summary>
    /// Makes the hint piece representing this piece visible for a given period of time.
    /// </summary>
    /// <param name="slot">Correct slot for this piece to be placed in</param>
    /// <returns></returns>
    public IEnumerator ShowHintPiece(TangramSlot slot, float durationOfDisplay)
    {
        hintPiece.transform.rotation = slot.transform.rotation;
        hintPiece.transform.position = slot.transform.position;
        hintPiece.SetActive(true);

        float currTime = 0f;
        while (currTime < durationOfDisplay)
        {
            currTime += Time.deltaTime;
            if (slot.correctPiecePlaced) break;
            yield return null;
        }
        hintPiece.SetActive(false);
    }
}
