using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RifleOnStand : Gun
{
    public Transform stand;
    [SerializeField] private bool isRealistic;

    protected override void Start()
    {
        base.Start();
        if(isServer) transform.position = stand.position;
    }

    protected override void VisualizeGun()
    {
        Vector3 posHandL = handL.Interactor.RaycastStartingPoint.position;
        Vector3 posHandR = handR.Interactor.RaycastStartingPoint.position;

        
        var newHoldingHand = posHandL.z > posHandR.z ? handL : handR; //player is facing Z+, the hand farther to their body is the one holding the rifle

        if (HoldingHand != newHoldingHand)
        {
            HoldingHand = newHoldingHand;  
        }

        if (!isRealistic) transform.right = holdingHand.Interactor.RaycastStartingPoint.position - stand.position; //rotate
        else transform.right = stand.position - holdingHand.Interactor.RaycastStartingPoint.position;
    }

    protected override void Shoot(GestureType gestureType, bool isLeft)
    {
        if (gestureType != GestureType.Rock || !canShoot) return;
        base.Shoot(gestureType, isLeft);

        AudioManager.Instance.PlaySFX("RifleShot");
        if (!interactor.NotifyObjectOfGesture(gestureType, true))
        {
            InstantiateVFX(interactor.GetInteractionEndpoint(), false);
        }
        StartCoroutine(CanShoot());
    }

    protected override void ReloadGun()
    {
        AudioManager.Instance.PlaySFX("RifleReload");
    }
}
