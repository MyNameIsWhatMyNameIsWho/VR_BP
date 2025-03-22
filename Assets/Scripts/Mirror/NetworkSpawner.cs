using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Spawns a given object on the server
/// </summary>
public class NetworkSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject toSpawn;
    void Start()
    {
        if(isServer){
            var obj = Instantiate(toSpawn);
            NetworkServer.Spawn(obj);
        } 
    }
}
