using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.Discovery;
using System;

/// <summary>
/// Manages server discovery performed by a client
/// </summary>
public class DiscoverConnection : MonoBehaviour
{
    public NetworkDiscovery networkDiscovery;

    private void Awake()
    {
        networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
    }

    public void OnButtonPress()
    {
       networkDiscovery.StartDiscovery();
    }

    public void OnDiscoveredServer(ServerResponse response)
    {
        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartClient(response.uri);
    }
}
