using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class NetworkVisualEffect : NetworkBehaviour
{
    [SerializeField] private VisualEffect vfx;

    [ClientRpc]
    public void Play()
    {
        vfx.Play();
    }
}
