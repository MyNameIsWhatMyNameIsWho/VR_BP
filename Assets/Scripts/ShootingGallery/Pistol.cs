

///This class is no longer used in the game.

/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pistol : Gun
{

    protected override void VisualizeGun()
    {
        Vector3 posHandL = handL.Interactor.RaycastStartingPoint.position;
        Vector3 posHandR = handR.Interactor.RaycastStartingPoint.position;
        Vector3 posBackHand, posFrontHand;

        bool holdingHandisL = posHandL.z > posHandR.z;  //player is facing Z+, the hand farther from their body is the one holding the pistol

        posBackHand = holdingHandisL ? posHandL : posHandR;
        posFrontHand = holdingHandisL ? posHandR : posHandL;

        var newHoldingHand = holdingHandisL ? handL : handR;

        if (HoldingHand != newHoldingHand)
        {
            HoldingHand = newHoldingHand;
            transform.localPosition = Vector3.zero; //translate
            positions.Clear();
            rotations.Clear();
        }

        //transform.right = HoldingHand.Interactor.raycastStartingPoint.forward; //rotate
        //transform.up = HoldingHand.Interactor.raycastStartingPoint.up;
        //transform.forward = -HoldingHand.Interactor.raycastStartingPoint.right;

        //data smoothing
        while (positions.Count >= numPositionsToSave) positions.RemoveAt(0);
        while (rotations.Count >= numRotationsToSave) rotations.RemoveAt(0);

        positions.Add(HoldingHand.Interactor.RaycastStartingPoint.position);
        transform.position = SmoothOutPositionLinear();

        rotations.Add(HoldingHand.Interactor.RaycastStartingPoint.forward);
        transform.right = SmoothOutRotationLinear();
    }

    protected override void Shoot(GestureType gestureType, bool isLeft)
    {
        if (gestureType != GestureType.Rock || !canShoot) return;

        AudioManager.Instance.PlaySFX("PistolShot");
        interactor.NotifyObjectOfGesture(gestureType, true);
        shotEffect.Play();
        StartCoroutine(CanShoot());
    }

    protected override void ReloadGun()
    {
        AudioManager.Instance.PlaySFX("PistolReload");
    }
}*/
