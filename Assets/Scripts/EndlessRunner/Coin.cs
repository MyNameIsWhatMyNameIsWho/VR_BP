using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;


public class Coin : NetworkBehaviour
{
    private void Start()
    {
        GetComponent<Animator>().Play("Coin", 0, Random.Range(0.0f, 1.0f));
    }

    [ClientRpc]
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
