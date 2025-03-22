using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VrDashboardLogger.Editor;

public class UserSystem : NetworkBehaviour
{
    public static UserSystem Instance;

    [field: SerializeField] public UserDataContainer UserData { get; set; }
    [field: SerializeField] public UnityEvent OnUserSet;
    [field: SerializeField] public UnityEvent OnUserNotFound;
    [field: SerializeField] public UnityEvent<List<User>> OnDataUpdate;

    public bool HasActiveUser = false;

    private void Awake()
    {
        if (Instance) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        if (!isServer) yield break;

        UserData.OnUsersChange.AddListener(()=>UpdateUserData(UserData._users));

        yield return null;

        UserData.LoadUsers();
    }


    /// <summary>
    /// Sets a user with a given nickname for logging data (if exists).
    /// </summary>
    /// <param name="nickname">Nicklname of the user.</param>
    [Command(requiresAuthority = false)]
    public void SetActiveUser(string nickname) {
        if (UserData.SetActiveUser(nickname)) { 
            LoggerCommunicationProvider.Instance.FindParticipantIDByNickname(nickname, OnPlayerSetNetworkCall, OnPlayerNotFoundNetworkCall);
        }
    }

    [ClientRpc]
    public void OnPlayerSetNetworkCall() {
        HasActiveUser = true;
        OnUserSet.Invoke();

        if (isServer) { 
            GestureDetector.Instance.CmdRemoveCustomGestures();
            GestureDetector.Instance.CmdLoadCustomGestures(UserData.GestureList);
        }
    }

    [ClientRpc]
    public void OnPlayerNotFoundNetworkCall()
    {
        OnUserNotFound.Invoke();
    }

    /// <summary>
    /// Creates a new user in the device with the given kickname (if exists on the logging server)
    /// </summary>
    /// <param name="nickname">Nicklname of the user.</param>
    [Command(requiresAuthority = false)]
    public void CreateUser(string nickname) {
        //check on server
        LoggerCommunicationProvider.Instance.DoesParticipantNicknameExist(nickname, () => AddUser(nickname), OnPlayerNotFoundNetworkCall);
    }

    [Command(requiresAuthority = false)]
    public void AddUser(string nickname) { 
        UserData.AddUser(nickname);
    }

    [Command(requiresAuthority = false)]
    public void ForceDataUpdate()
    {
        UpdateUserData(UserData._users);
    }

    [ClientRpc]
    public void UpdateUserData(List<User> users) { 
        OnDataUpdate.Invoke(users);
    }

    [Command(requiresAuthority = false)]
    public void RemoveUser(string nickname)
    {
        UserData.RemoveUser(nickname);
    }
}
