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
    [Header("User Interface References")]
    [SerializeField] private GameObject mainMenuButtons;
    [SerializeField] public UserUIVR userUIVR;
    [SerializeField] GameObject hidableMobileUI;
    [SerializeField] GameObject gestureSettingsMobileUI;
    [SerializeField] GameObject mobileMenuUI;
    [SerializeField] TMP_Text changeMobileVisibilityButtonText;
    [SerializeField] GameObject eventSystem;
    [SerializeField] Slider sliderSound;

    // Reference to upright redirector
    private UprightRedirector uprightRedirector;

    private void Start()
    {
        // Force the system to consider a user as already active
        UserSystem.Instance.HasActiveUser = true;
        CheckHasActiveUser();

        // Setup mobile UI components
        if (mobileMenuUI != null)
        {
            mobileMenuUI.SetActive(!isServer);
        }

        uprightRedirector = FindFirstObjectByType<UprightRedirector>();

        if (hidableMobileUI != null)
        {
            hidableMobileUI.SetActive(false);
        }

        if (gestureSettingsMobileUI != null)
        {
            gestureSettingsMobileUI.SetActive(false);
        }

        if (eventSystem != null && !isServer)
        {
            eventSystem.SetActive(true);
        }
    }

    [Command(requiresAuthority = false)]
    private void CheckHasActiveUser()
    {
        SetActivity(UserSystem.Instance.HasActiveUser);
    }

    [ClientRpc]
    private void SetActivity(bool hasActiveUser)
    {
        if (userUIVR != null)
        {
            userUIVR.gameObject.SetActive(!hasActiveUser);
        }

        if (mainMenuButtons != null)
        {
            mainMenuButtons.SetActive(hasActiveUser);
        }
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
        if (mainMenuButtons != null)
        {
            mainMenuButtons.SetActive(false);
        }

        UserSystem.Instance.HasActiveUser = false;

        if (userUIVR != null)
        {
            userUIVR.gameObject.SetActive(true);
        }

        if (!isServer) return;
        LoggerCommunicationProvider.Instance.StopLogging();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

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

        if (changeMobileVisibilityButtonText == null || hidableMobileUI == null)
        {
            Debug.LogError("Mobile UI references are null. Cannot change visibility.");
            return;
        }

        bool hideUI = changeMobileVisibilityButtonText.text == "Skrýt";
        hidableMobileUI.SetActive(!hideUI);

        if (gestureSettingsMobileUI != null)
        {
            gestureSettingsMobileUI.SetActive(false);
        }

        changeMobileVisibilityButtonText.text = hideUI ? "Možnosti" : "Skrýt";
    }

    public void ChangeGestureSettingsMenuVisibility()
    {
        if (isServer) return;

        if (gestureSettingsMobileUI == null || hidableMobileUI == null)
        {
            Debug.LogError("Mobile UI references are null. Cannot change gesture settings visibility.");
            return;
        }

        gestureSettingsMobileUI.SetActive(!gestureSettingsMobileUI.activeSelf);
        hidableMobileUI.SetActive(!hidableMobileUI.activeSelf);
    }

    [Command(requiresAuthority = false)]
    public void CmdPerformUprightRedirection()
    {
        if (uprightRedirector != null)
        {
            uprightRedirector.PerformUprightRedirection();
        }
        else
        {
            Debug.LogError("UprightRedirector is null. Cannot perform upright redirection.");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeTargetTransform(float valueZ)
    {
        if (uprightRedirector != null)
        {
            uprightRedirector.ChangeTargetTransform(valueZ);
        }
        else
        {
            Debug.LogError("UprightRedirector is null. Cannot change target transform.");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdSetSoundVolume()
    {
        if (sliderSound != null && AudioManager.Instance != null)
        {
            // Use the appropriate volume control method from AudioManager
            AudioManager.Instance.CmdSetSFXVolume(sliderSound.value);
            AudioManager.Instance.CmdSetMusicVolume(sliderSound.value);
        }
        else
        {
            Debug.LogError("SliderSound or AudioManager.Instance is null. Cannot set sound volume.");
        }
    }
    #endregion //OnClient
}