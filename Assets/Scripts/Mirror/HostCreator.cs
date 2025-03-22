using Mirror;
using Mirror.Discovery;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Starts hosting the server and advertises it on a local network
/// </summary>
public class HostCreator : MonoBehaviour
{
    public NetworkDiscovery networkDiscovery;
    void Start()
    {
        NetworkManager.singleton.StartHost();
        networkDiscovery.AdvertiseServer();
    }
}
