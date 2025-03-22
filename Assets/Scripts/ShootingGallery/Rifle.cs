using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rifle : Gun
{
    protected override void VisualizeGun()
    {
        Vector3 posHandL = handL.Interactor.RaycastStartingPoint.position;
        Vector3 posHandR = handR.Interactor.RaycastStartingPoint.position;
        Vector3 posBackHand, posFrontHand;

        bool holdingHandisL = posHandL.z < posHandR.z;  //player is facing Z+, the hand closer to their body is the one holding the rifle

        posBackHand = holdingHandisL ? posHandL : posHandR;
        posFrontHand = holdingHandisL ? posHandR : posHandL;

        var newHoldingHand = holdingHandisL ? handL : handR;

        if (HoldingHand != newHoldingHand)
        {
            HoldingHand = newHoldingHand;
            transform.SetParent(holdingHand.Interactor.RaycastStartingPoint);
            transform.localPosition = Vector3.zero; //translate        
        }

        transform.right = posFrontHand - posBackHand; //rotate
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
