using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.VFX;

[RequireComponent(typeof(Interactor))]
public abstract class Gun : NetworkBehaviour
{
    protected LineRenderer lineRenderer;
    /*[GradientUsage(true)]
    public Gradient lineGradient;*/

    [SerializeField] protected NetworkVisualEffect shotEffect, missEffect;

    public Transform bulletHole;
    [SerializeField] private Transform triggerTransform;
    [SerializeField] private float nonShootingPeriod = 0.75f;
    protected bool canShoot = true;
   // [SerializeField] int targetLayerNumber = 6;

    protected HandManager handL, handR;
    protected Interactor interactor;

    //smoothing
    protected List<Vector3> positions, rotations;
    [SerializeField] protected int numPositionsToSave = 20, numRotationsToSave = 20;



    protected HandManager HoldingHand { get { return holdingHand; } 
                                        set { holdingHand = value;
                                            /*transform.SetParent(holdingHand.Interactor.raycastStartingPoint);*/ } }
    protected HandManager holdingHand;


    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.008f;
        lineRenderer.endWidth = 0.008f;

        interactor = GetComponent<Interactor>();
        interactor.GetRayEndpoint = (center, background) => center.position + center.right * 5;
        interactor.GetRayStartpoint = (center) => center.position;
        interactor.GetRayDirection = (center) => center.right;

        positions = new List<Vector3>();
        rotations = new List<Vector3>();
    }
    protected virtual void Start()
    {
        GestureDetector.Instance.OnGestureBegin.AddListener(Shoot);
        //GestureDetector.Instance.OnGestureEnd.AddListener((gt, l) => print("gesture end"));

        handL = GestureDetector.Instance.handL;
        handR = GestureDetector.Instance.handR;

    }

    private void Update()
    {
        if(isServer) VisualizeGun();
    }

    private void OnDestroy()
    {
        GestureDetector.Instance.OnGestureBegin.RemoveListener(Shoot);
    }

    /// <summary>
    /// Children of this class implement this function which visualizes the guns according to the hand positions.
    /// </summary>
    protected abstract void VisualizeGun();

    /// <summary>
    /// Children of this class implement this function by playing their own sound effects.
    /// </summary>
    protected abstract void ReloadGun();

    /// <summary>
    /// Children of this class implement their own, more complex functions, this one only adds one to number of shots shot in the game.
    /// </summary>
    /// <param name="gestureType">Which type of gesture was performed by the player</param>
    /// <param name="isLeft">True if the gesture was performed by the left hand</param>
    protected virtual void Shoot(GestureType gestureType, bool isLeft)
    {
        //if (gestureType != GestureType.Rock || !canShoot) return;
        ShootingGalleryManager.Instance.numShots++;
    }

    /// <summary>
    /// Ensures the gun cannot shoot for a nonShootingPeriod of time and then it can (the gun is "reloaded")
    /// </summary>
    protected IEnumerator CanShoot()
    {
        canShoot = false;
        yield return new WaitForSeconds(nonShootingPeriod);
        ReloadGun();
        canShoot = true;
        //todo change color of the ray
    }


    /// <summary>
    /// Instantiates a VFX for when the gun hits a target.
    /// </summary>
    public void InstantiateVFX(Vector3 pos, bool shotTarget = true)
    {
        if ( pos == Vector3.zero) pos = interactor.positionOfLastInteraction;
        
        NetworkVisualEffect vfx;
        if (shotTarget) vfx = Instantiate(shotEffect, pos, Quaternion.identity);
        else vfx = Instantiate(missEffect, pos - new Vector3(0, 0, 0.2f), Quaternion.identity);
        NetworkServer.Spawn(vfx.gameObject);
        vfx.Play();
    }

    /* //These functions are used by the pistol, which is no longer included in the game
     * 
     * protected Vector3 SmoothOutPositionLinear()
    {
        Vector3 smoothedPosition = Vector3.zero;
        for (int i = 0; i < positions.Count; i++)
        {
            smoothedPosition += positions[i];
        }
        smoothedPosition /= positions.Count;
        return smoothedPosition;
    }


    protected Vector3 SmoothOutRotationLinear()
    {
        Vector3 smoothedRotation = Vector3.zero;
        for (int i = 0; i < rotations.Count; i++)
        {
            smoothedRotation += rotations[i];
        }
        smoothedRotation /= rotations.Count;
        return smoothedRotation;
    }*/

}
