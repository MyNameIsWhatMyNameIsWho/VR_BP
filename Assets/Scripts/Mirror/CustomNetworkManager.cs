using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/// <summary>
/// This class serves for managing the network, specifically overrides a function to change player spawning
/// </summary>
public class CustomNetworkManager : NetworkManager
{
    [SerializeField] private GameObject HostPlayer;
    [SerializeField] private GameObject ClientPlayer;

    private List<NetworkPlayer> players = new List<NetworkPlayer>();

    /// <summary>
    /// Manages players; the first player on server is a host player, the next ones are client players
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {        
        NetworkPlayer newPlayer;

        if (players.Count == 0)
            newPlayer = Instantiate(HostPlayer).GetComponent<NetworkPlayer>();
        else
            newPlayer = Instantiate(ClientPlayer).GetComponent<NetworkPlayer>();

        newPlayer.conn = conn;
        players.Add(newPlayer);
            
        NetworkServer.AddPlayerForConnection(newPlayer.conn, newPlayer.gameObject);        
    }
}
