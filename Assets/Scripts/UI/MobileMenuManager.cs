using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using TMPro;
using UnityEngine.UI;
using Microsoft.Win32.SafeHandles;

public class MobileMenuManager : NetworkBehaviour
{
    #region Variables

    private MainMenuManager mainMenu; //for calling server functions
    [SerializeField] private GameMenuManager gameMenu; //for calling server functions

    [SerializeField] private GameObject eventSystem;
    [SerializeField] private GameObject gestureSettingsUI;
    [SerializeField] public GameObject userSelectionUI;
    [SerializeField] private GameObject hidableUI; //all main menu UI except for the "Options" button
    [SerializeField] private TMP_Text optionsButtonText;
    [SerializeField] private Slider sliderSFX, sliderMusic; //sound sliders

    [field: SerializeField] public List<TextMeshProUGUI> ButtonTexts { get; set; } //texts on user selection buttons
    [field: SerializeField] public List<GameObject> Buttons { get; set; } //user UI buttons
    [SerializeField] private TMP_InputField userNicknameInput;
    [SerializeField] private Button addButton;
    [SerializeField] private List<GameObject> errorTexts; //error textsa for user selection for logging

    [SerializeField] private Toggle modeToggle;

    #endregion //Variables

    void Start()
    {
        mainMenu = transform.parent.GetComponent<MainMenuManager>();
       // gameMenu = transform.parent.GetComponent<GameMenuManager>();

        hidableUI.SetActive(false);
        if (gestureSettingsUI) gestureSettingsUI.SetActive(false);
        if (userSelectionUI) userSelectionUI.SetActive(false);
        
        if (gameMenu is TangramLevelMenuManager)
        {
            (gameMenu as TangramLevelMenuManager).OnModeChange.AddListener(modeToggle.SetIsOnWithoutNotify);
            modeToggle.SetIsOnWithoutNotify((gameMenu as TangramLevelMenuManager).autoSnapping);
        }

        gameObject.SetActive(!isServer);
        if (isServer) return;

        sliderMusic.value = AudioManager.Instance.GetMusicVolume();
        sliderSFX.value = AudioManager.Instance.GetSFXVolume();

        sliderMusic.onValueChanged.AddListener(AudioManager.Instance.CmdSetMusicVolume);
        sliderSFX.onValueChanged.AddListener(AudioManager.Instance.CmdSetSFXVolume);


        eventSystem.SetActive(true);
    }

    public void ChangeMobileUIVisibility()
    {
        if (isServer) return;
        if (userSelectionUI && !UserSystem.Instance.HasActiveUser) {
            userSelectionUI.SetActive(!userSelectionUI.activeSelf);
            optionsButtonText.text = userSelectionUI.activeSelf ? "Skr�t" : "Mo�nosti";
        } else {
            hidableUI.SetActive(!hidableUI.activeSelf);
            if (gestureSettingsUI) gestureSettingsUI.SetActive(false);
            optionsButtonText.text = hidableUI.activeSelf ? "Skr�t" : "Mo�nosti";
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdPerformUprightRedirection()
    {
        UprightRedirector.Instance.PerformUprightRedirection();
    }

    [Command(requiresAuthority = false)]
    public void CmdChangeTargetTransform(float valueZ)
    {
        UprightRedirector.Instance.ChangeTargetTransform(valueZ);
    }

    #region MainMenu

    [Command(requiresAuthority = false)]
    public void CmdQuitGame()
    {
        mainMenu.QuitGame();
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadMiniGame(int game)
    {
        mainMenu.LoadMiniGame(game);
        hidableUI.SetActive(false);
        optionsButtonText.text = "Mo�nosti";
    }

    public void ChangeGestureSettingsMenuVisibility()
    {
        if (isServer || !gestureSettingsUI) return;
        gestureSettingsUI.SetActive(!gestureSettingsUI.activeSelf);
        hidableUI.SetActive(!hidableUI.activeSelf);
    }

    /// <summary>
    /// Removes a user with a nickname matching the text in the input field (if exists and is not DefaultUser).
    /// </summary>
    public void RemoveUser() {
        if (userNicknameInput.text == "DefaultUser")
        {
            Debug.LogWarning("Removing DefaultUser forbidden.");
            StartCoroutine(ShowError(2));
            return;
        }

        if (ButtonTexts.Exists((t) => t.transform.parent.gameObject.activeSelf && t.text == userNicknameInput.text)) { 
            UserSystem.Instance.RemoveUser(userNicknameInput.text);
            userNicknameInput.text = string.Empty;
            return;
        }

        StartCoroutine(ShowError(1));
        userNicknameInput.text = string.Empty;
    }


    [Command(requiresAuthority = false)]
    public void ShowUserSelection(bool show)
    {
        RpcShowUserSelection(show);
    }

    /// <summary>
    /// Shows/hides a sceen for selecting a user for which the data will be logged.
    /// </summary>
    /// <param name="show">True to show, false to hide.</param>
    [ClientRpc]
    public void RpcShowUserSelection(bool show) {
        Debug.LogWarning($"Show user selection {show}");
        if (show)
        {
            userSelectionUI.SetActive(true);
            hidableUI.SetActive(false);
            gestureSettingsUI.SetActive(false);
        }
        else {
            userSelectionUI.SetActive(false);
            hidableUI.SetActive(true);
            gestureSettingsUI.SetActive(false);
        }
    }


    /// <summary>
    /// Adds another user account toi the device with a name written in the input field (if exists and isnt in the device already).
    /// </summary>
    public void AddUser()
    {
        if (ButtonTexts.Exists((t) => t.transform.parent.gameObject.activeSelf && t.text == userNicknameInput.text)) return;        
        
        UserSystem.Instance.CreateUser(userNicknameInput.text);
        userNicknameInput.text = string.Empty;
        addButton.interactable = false;
    }


    public void EnableAddUserButton(bool error) {
        if (error) {
            StartCoroutine(ShowError(addButton.interactable ? 0 : 1));
        } else
        {
            addButton.interactable = true;
        }
    }

    /// <summary>
    /// Shows an error for a limited amout of time.
    /// </summary>
    /// <param name="i">Index of the error text in SerializeField list.</param>
    /// <returns>Coroutine</returns>
    IEnumerator ShowError(int i) {
        errorTexts[i].SetActive(true);
        yield return new WaitForSeconds(1);
        addButton.interactable = true;
        yield return new WaitForSeconds(4);
        errorTexts[i].SetActive(false);
    }

    /// <summary>
    /// Selects a user for logging following data.
    /// </summary>
    /// <param name="i">Index of user (according to the buttons for selection).</param>
    public void SelectUser(int i)
    {
        UserSystem.Instance.SetActiveUser(ButtonTexts[i].text);
    }

    #endregion //MainMenu

    #region GameMenu

    [Command(requiresAuthority = false)]
    public void CmdReturnToMainMenu()
    {
        gameMenu.ReturnToMainMenu();
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadLevel(int level)
    {
        gameMenu.LoadLevel(level);
    }

    #endregion //GameMenu

    #region TangramMenu

    [Command(requiresAuthority = false)]
    public void CmdChangeTangramMode(bool autoSnapping)
    {
        if (gameMenu is TangramLevelMenuManager) { 
            (gameMenu as TangramLevelMenuManager).SwapAutoSnapping();
        }
        //gameMenu.ChangeTangramMode(autoSnapping);
    }

    #endregion //TangramMenu

    #region EndlessRunnerMenu

    [Command(requiresAuthority = false)]
    public void CmdLoadNoObstacleLevel(int level)
    {
        EndlessRunnerMenuManager ermm = (EndlessRunnerMenuManager)gameMenu;
        ermm.LoadNoObstacleLevel(level);
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadObstacleLevel(int level)
    {
        EndlessRunnerMenuManager ermm = (EndlessRunnerMenuManager)gameMenu;
        ermm.LoadObstacleLevel(level);
    }

    #endregion //EndlessRunnerMenu

    #region MothMenu

    [Command(requiresAuthority = false)]
    public void CmdLoadMothLevel(int level)
    {
        Debug.Log($"Loading Moth level: {level}");
        MothMenuManager mmm = (MothMenuManager)gameMenu;
        mmm.LoadMothLevel(level);
    }

    [Command(requiresAuthority = false)]
    public void CmdRestartMothGame()
    {
        Debug.Log("Restarting Moth game");
        MothMenuManager mmm = (MothMenuManager)gameMenu;
        mmm.CmdRestartMothGame();
    }
    
    //[Command(requiresAuthority = false)]
    //public void CmdReplayMothTutorial()
    //{
    //    Debug.Log("CmdReplayMothTutorial called in MobileMenuManager");
    //    MothMenuManager mmm = (MothMenuManager)gameMenu;
    //    mmm.CmdReplayTutorial();
    //}
    
    // Additional method with no parameters in case Unity isn't showing the others
    [Command(requiresAuthority = false)]
    public void LoadMothGame()
    {
        Debug.Log("Loading Moth game with default level");
        MothMenuManager mmm = (MothMenuManager)gameMenu;
        mmm.LoadMothLevel(1); // Default level
    }
    
    // Simple public methods without Command attribute - these should be visible in Unity inspector
    public void StartMothGame()
    {
        Debug.Log("Starting Moth game");
        CmdLoadMothLevel(1);
    }
    
    public void RestartMoth()
    {
        Debug.Log("Restarting Moth game (simple method)");
        CmdRestartMothGame();
    }
    
    //public void ReplayMothAudio()
    //{
    //    Debug.Log("ReplayMothAudio called (simple method)");
    //    CmdReplayMothTutorial();
    //}

    #endregion //MothMenu

    #region NewGameMenu

    [Command(requiresAuthority = false)]
    public void CmdLoadCollectibleLevel(int level)
    {
        NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
        ngmm.LoadCollectibleLevel(level);
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadObstacleLevelSlow(int level)
    {
        NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
        ngmm.LoadObstacleLevelSlow(level);
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadObstacleLevelMedium(int level)
    {
        NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
        ngmm.LoadObstacleLevelMedium(level);
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadObstacleLevelFast(int level)
    {
        NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
        ngmm.LoadObstacleLevelFast(level);
    }

    // [Command(requiresAuthority = false)]
    // public void CmdResetNewGame()
    // {
    //     NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
    //     ngmm.CmdResetNewGame();
    // }

    // [Command(requiresAuthority = false)]
    // public void CmdSwitchHandsNewGame()
    // {
    //     NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
    //     ngmm.CmdSwitchHands();
    // }

    [Command(requiresAuthority = false)]
    public void CmdReplayTutorial()
    {
        NewGameMenuManager ngmm = (NewGameMenuManager)gameMenu;
        ngmm.CmdReplayTutorial();
    }

    #endregion //NewGameMenu
}
