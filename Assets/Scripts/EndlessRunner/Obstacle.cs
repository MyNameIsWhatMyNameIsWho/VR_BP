using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : NetworkBehaviour
{
    public SoundTrigger soundTrigger;
    public float distanceBefore = 2.0f;
    public float spawningHeightModifier = 0.0f;
    public int numCoins;
    [SerializeField] float soundTriggerObjectPosX = -3.5f;

    private void Start()
    {
        if (soundTrigger != null)
        {
            soundTrigger.gameObject.transform.localPosition = new Vector3(soundTriggerObjectPosX, soundTrigger.gameObject.transform.localPosition.y, soundTrigger.gameObject.transform.localPosition.z);
        }
    }

}
