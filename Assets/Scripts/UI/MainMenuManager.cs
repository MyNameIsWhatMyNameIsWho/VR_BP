using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Handles starting a game and mobile UI events
/// </summary>
public class MainMenuManager : NetworkBehaviour
{
    [SerializeField] private GameObject mainMenuButtons;
    [SerializeField] public UserUIVR userUIVR;

    private void Start()
    {
        // Force the system to consider a user as already active
        UserSystem.Instance.HasActiveUser = true;

        CheckHasActiveUser();
    }

    [Command(requiresAuthority =false)]
    private void CheckHasActiveUser()
    {
        SetActivity(UserSystem.Instance.HasActiveUser);
    }

    [ClientRpc]
    private void SetActivity(bool hasActiveUser)
    {
        userUIVR.gameObject.SetActive(!hasActiveUser);
        mainMenuButtons.SetActive(hasActiveUser);
    }

    /// <summary>
    /// Loads a game experience with the given ID
    /// </summary>
    public void LoadMiniGame(int game)
    {
        switch (game)
        {
            case 0:
                if (isServer) CustomNetworkManager.singleton.ServerChangeScene("TangramSceneOnline");
                break;
            case 1:
                if (isServer) CustomNetworkManager.singleton.ServerChangeScene("ShootingGallerySceneOnline");
                break;
            case 2:
                if (isServer) CustomNetworkManager.singleton.ServerChangeScene("EndlessRunnerSceneOnline");
                break;
            case 3:
                //new game scene
                if (isServer) CustomNetworkManager.singleton.ServerChangeScene("NewGameSceneOnline");
                break;
            case 4:
                //moth scene
                if (isServer) CustomNetworkManager.singleton.ServerChangeScene("MothSceneOnline");
                break;
        }
    }

    [Command(requiresAuthority = false)]
    public void LogOutUserCommand()
    {
        LogOutUser();
    }

    /// <summary>
    /// Stops logging data. Logs out the user that was used for logging data.
    /// </summary>
    [ClientRpc]
    public void LogOutUser() 
    {
        mainMenuButtons.SetActive(false);
        UserSystem.Instance.HasActiveUser = false;
        userUIVR.gameObject.SetActive(true);

        if (!isServer) return;
        LoggerCommunicationProvider.Instance.StopLogging();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    /*[SerializeField] GameObject hidableMobileUI;
    [SerializeField] GameObject gestureSettingsMobileUI;
    [SerializeField] GameObject mobileMenuUI;
    [SerializeField] TMP_Text changeMobileVisibilityButtonText;
    [SerializeField] GameObject eventSystem;
    [SerializeField] Slider sliderSound;
    private UprightRedirector uprightRedirector;

    private void Start()
    {
        mobileMenuUI.SetActive(!isServer);
        uprightRedirector = FindAnyObjectByType<UprightRedirector>();
        hidableMobileUI.SetActive(false);
        gestureSettingsMobileUI.SetActive(false);
        if (!isServer) {
            eventSystem.SetActive(true);
        }
    }*/
    /*
    #region OnClient


    [Command(requiresAuthority = false)]
    public void CmdQuitGame()
    {
        QuitGame();
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadMiniGame(int game)
    {
        LoadMiniGame(game);
    }

    public void ChangeMobileUIVisibility()
    {
        if (isServer) return;
        bool hideUI = changeMobileVisibilityButtonText.text == "Skrýt";        
        hidableMobileUI.SetActive(!hideUI);
        gestureSettingsMobileUI.SetActive(false);
        changeMobileVisibilityButtonText.text = hideUI ? "Možnosti" : "Skrýt";
    }

    public void ChangeGestureSettingsMenuVisibility()
    {
        if (isServer) return;
        gestureSettingsMobileUI.SetActive(!gestureSettingsMobileUI.activeSelf);
        hidableMobileUI.SetActive(!hidableMobileUI.activeSelf);
    }

    [Command(requiresAuthority = false)]
    public void CmdPerformUprightRedirection()
    {
        uprightRedirector.PerformUprightRedirection();
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeTargetTransform(float valueZ)
    {
        uprightRedirector.ChangeTargetTransform(valueZ);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetSoundVolume()
    {
        AudioManager.Instance.SetSoundVolume(sliderSound.value);
    }
    #endregion //OnClient
    */
}
