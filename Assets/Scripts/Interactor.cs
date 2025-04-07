using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;

public class Interactor : NetworkBehaviour
{
    public bool searchesForCollision = true;
    public bool visualizesRaycast = true;

    //raycasting visualization
    [field:SerializeField] public Transform RaycastStartingPoint { get; set; }
    public Func<Transform, Transform, Vector3> GetRayEndpoint;
    public Func<Transform, Vector3> GetRayStartpoint;
    public Func<Transform, Vector3> GetRayDirection;

    public Transform backgroundTransform;    
    [SerializeField] private float rayDisplayDistance;
    [SerializeField] private LayerMask hittableLayer;
    private LineRenderer lineRenderer;
    public Vector3 positionOfLastInteraction;

    //in-collision objects
    private Interactable objCollisionPhysical;
    private Interactable objCollisionRaycast;


    void Start()
    {
        objCollisionPhysical = null;
        objCollisionRaycast = null;

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.0001f;
        lineRenderer.endWidth = 0.01f;
    }

    /// <summary>
    /// Informs the currently interacted-with object of the type of gesture used in the interaction
    /// </summary>
    /// <param name="type">Type of gesture used in the interaction</param>
    public bool NotifyObjectOfGesture(GestureType type, bool begins)
    {
        if (objCollisionPhysical)
        {           
            objCollisionPhysical.gestureEvent(type, begins, true, this);
            return true;
        } 
        else if (objCollisionRaycast) 
        {
            objCollisionRaycast.gestureEvent(type, begins, false, this);
            return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!searchesForCollision) return;

        var interactable = other.gameObject.GetComponent<Interactable>();
        if (interactable && interactable.canInteractPhysical)
        {
            objCollisionPhysical?.collisionEvent(false, true, this);
            objCollisionPhysical = interactable;
            objCollisionPhysical.collisionEvent(true, true, this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (objCollisionPhysical && other.gameObject == objCollisionPhysical.gameObject)
        {
            objCollisionPhysical.collisionEvent(false, true, this);
            objCollisionPhysical = null;
        }
    }

    private void Update()
    {
        if (!isServer) return;
        if (objCollisionPhysical && !objCollisionPhysical.gameObject.activeInHierarchy)
        {
            objCollisionPhysical.collisionEvent(false, true, this);
            objCollisionPhysical = null;
        }

        if (objCollisionRaycast && !objCollisionRaycast.gameObject.activeInHierarchy)
        {
            objCollisionRaycast.collisionEvent(false, false, this);
            objCollisionRaycast = null;
        }


        if (!searchesForCollision || !visualizesRaycast) {
            StopShowingRay();
            return; 
        }

        RaycastHit hit;
        //Vector3 startPoint = raycastStartingPoint.position - Vector3.forward;
        if (Physics.Raycast(GetRayStartpoint(RaycastStartingPoint), GetRayDirection(RaycastStartingPoint), out hit, 50.0f, hittableLayer))
        {
            positionOfLastInteraction = hit.point;
            var interactable = hit.collider.gameObject.GetComponent<Interactable>();
            if (interactable && interactable != objCollisionRaycast && interactable.canInteractRaycast)
            {
                objCollisionRaycast?.collisionEvent(false, false, this);
                objCollisionRaycast = interactable;
                objCollisionRaycast.collisionEvent(true, false, this);
            }
            if ((!interactable || !interactable.canInteractRaycast) && objCollisionRaycast)
            {
                objCollisionRaycast.collisionEvent(false, false, this);
                objCollisionRaycast = null;
            }
        }
        else if (objCollisionRaycast)
        {
            objCollisionRaycast.collisionEvent(false, false, this);
            objCollisionRaycast = null;
        }

        VisualizeRay();
    }

    private void VisualizeRay()
    {
        if (backgroundTransform && IsHandFarFromBackground())
        {
            StartShowingRay();
        }
        else if (backgroundTransform)
        {
            StopShowingRay();
        }
    }


    public void SetVisualizeRaycast (bool vis)
    {
        visualizesRaycast = vis;
    }

    public Vector3 GetInteractionEndpoint()
    {
        RaycastHit hit;
        if (Physics.Raycast(GetRayStartpoint(RaycastStartingPoint), GetRayDirection(RaycastStartingPoint), out hit, 200.0f))
            return hit.point;
        return Vector3.zero;
    }

    private void StartShowingRay()
    {
        if (GetRayEndpoint == null) return;
        SetRayEndpoints(RaycastStartingPoint.position, GetRayEndpoint(RaycastStartingPoint, backgroundTransform));
    }

    private void StopShowingRay()
    {
        SetRayEndpoints(backgroundTransform.position, backgroundTransform.position);
    }

    [ClientRpc]
    public void SetRayEndpoints(Vector3 start, Vector3 end)
    {
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }

    public bool IsHandFarFromBackground()
    {
        if (!backgroundTransform || backgroundTransform == RaycastStartingPoint) return true;
        return Mathf.Abs(backgroundTransform.position.z - RaycastStartingPoint.position.z) > rayDisplayDistance;
    }

    private void OnDisable()
    {
        if (objCollisionPhysical)
        {
            objCollisionPhysical.collisionEvent(false, true, this);
            objCollisionPhysical = null;
        }

        if (objCollisionRaycast)
        {
            objCollisionRaycast.collisionEvent(false, false, this);
            objCollisionRaycast = null;
        }
    }

}
