using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UserUIVR : NetworkBehaviour
{
    [SerializeField] private List<TextMeshPro> _buttonTexts;
    [SerializeField] private List<GameObject> _buttons;
    [SerializeField] private MobileMenuManager mobileMenu;

    [SerializeField] private GameObject _mainMenuButtons;


    private void Start()
    {       
        mobileMenu.ShowUserSelection(true);

        UserSystem.Instance.OnDataUpdate.AddListener(UpdateButtons);
        UserSystem.Instance.OnUserNotFound.AddListener(UserNotFoundError);
        UserSystem.Instance.OnUserSet.AddListener(StartMainMenu);
        UserSystem.Instance.ForceDataUpdate();

        if (!isServer) return;
        if (UserSystem.Instance.HasActiveUser)
            StartMainMenu();
        
    }

    private void OnEnable()
    {
        //if (!isServer) return;
        //UserSystem.Instance.OnDataUpdate.AddListener(UpdateButtons);
        //UserSystem.Instance.OnUserNotFound.AddListener(UserNotFoundError);
        //UserSystem.Instance.OnUserSet.AddListener(StartMainMenu);
        if (NetworkClient.active)
        {
            UserSystem.Instance.ForceDataUpdate();
            mobileMenu.ShowUserSelection(true);
        }

        //if (UserSystem.Instance.HasActiveUser)
        //    StartMainMenu();
    }

    private void OnDisable()
    {
        //UserSystem.Instance.OnDataUpdate.RemoveListener(UpdateButtons);
        //UserSystem.Instance.OnUserNotFound.RemoveListener(UserNotFoundError);
        //UserSystem.Instance.OnUserSet.RemoveListener(StartMainMenu);

        //TODO možná if (isServer) return;
        mobileMenu.ShowUserSelection(false);

    }

    [Command(requiresAuthority = false)]
    void UpdateButtons(List<User> users) {
        RpcUpdateButtons(users);
    }


    /// <summary>
    /// Shows buttons with nicknames of valid users for logging data.
    /// </summary>
    /// <param name="users">List of users used for logging data.</param>
    [ClientRpc]
    public void RpcUpdateButtons(List<User> users)
    {
        mobileMenu.EnableAddUserButton(false);
        
        for (int i = 0; i < users.Count; i++)
        {
            _buttons[i].SetActive(true);
            _buttonTexts[i].text = users[i].Nickname;
             
            mobileMenu.Buttons[i].SetActive(true);
            mobileMenu.ButtonTexts[i].text = users[i].Nickname;
        }

        for (int i = users.Count; i < _buttons.Count; i++)
        {
            mobileMenu.Buttons[i].SetActive(false);
            _buttons[i].SetActive(false);
        }
    }

    /// <summary>
    /// Selects a user with given nickname on the button that was pressed by the player.
    /// </summary>
    /// <param name="buttonIndex">Index of the pressed button.</param>
    public void SelectUser(int buttonIndex) {
        if (!isServer) return;

        UserSystem.Instance.SetActiveUser(_buttonTexts[buttonIndex].text);    
    }

    [Command(requiresAuthority = false)]
    public void StartMainMenu()
    {
        RpcStartMainMenu();
    }

    [ClientRpc]
    public void RpcStartMainMenu()
    {
        mobileMenu.ShowUserSelection(false);
        gameObject.SetActive(false);
        _mainMenuButtons.SetActive(true);
    }

    [Command(requiresAuthority = false)]
    public void UserNotFoundError()
    {
        RpcUserNotFoundError();
    }

    [ClientRpc]
    public void RpcUserNotFoundError()
    {
        mobileMenu.EnableAddUserButton(true);
        Debug.LogError("User not found");
    }
}
