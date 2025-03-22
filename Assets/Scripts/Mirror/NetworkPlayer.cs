using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using VrDashboardLogger.Editor;

/// <summary>
/// NetworkVRPlayer:
///     * Host side: will disable its hostCamera but after being authorized (OnStartAuthority) it will turn it on again.
///     * Client side: will disable its hostCamera, but it will never enable it again, because it doesn't have the authority.
/// The client (mobile) player doesn't have host/client camera assigned, so nothing happens.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    public NetworkConnectionToClient conn;

    [SerializeField] private Camera hostCamera;
    [SerializeField] private Camera clientCamera;
    [SerializeField] private GameObject eventSystem;
    [SerializeField] private VrLogger vrLogger;

    private void Awake()
    {
        if(hostCamera)
            hostCamera.enabled = false;
        DontDestroyOnLoad(this);
    }

    public override void OnStartAuthority() 
    {
        if (hostCamera)
        {
            hostCamera.enabled = true;
            clientCamera.enabled = false;
            eventSystem.SetActive(true);
        }
    }
}
