using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


/// <summary>
/// Handles both VR and mobile menu interactions and events
/// </summary>
public class GameMenuManager : NetworkBehaviour
{
    #region Variables
    /// <value>
    /// The prefabs to spawn when level is chosen by the player
    /// </value>
    [SerializeField] private List<GameObject> prefabs;

    /// <value>
    /// The game menu with multiple levels to choose from; will be hdden after a level is selected for playing
    /// </value>
    [SerializeField] private GameObject objectToHide;

    [SerializeField] private float levelSpawningHeight = 2.0f, levelSpawningDepth = 0.4f;   //the spawning position of game level
    
    /// <value>
    /// The currently played game level
    /// </value>
    public GameObject currentlyPlayedLevel = null;

    /// <value>
    /// The part of the mobile UI that gets shown/hidden using a button
    /// </value>
    [SerializeField] GameObject hidableMobileUI;

    /// <value>
    /// The whole mobile UI that will not be displayed in the VR application
    /// </value>
    [SerializeField] GameObject mobileMenuUI;

    /// <value>
    /// Text on a button saying "Options"/"Hide"
    /// </value>
    [SerializeField] TMP_Text changeMobileVisibilityButtonText;

    [SerializeField] GameObject eventSystem;

    [field: SerializeField] public UnityEvent onLevelLoad { get; set; }
    [field: SerializeField] public UnityEvent onGameMenuLoad { get; set; }
    [field: SerializeField] public UnityEvent onMainMenuLoad { get; set; }
    #endregion //Variables

    private void Start()
    {
        mobileMenuUI.SetActive(!isServer);
        hidableMobileUI.SetActive(false);
        if (!isServer) { 
            eventSystem.SetActive(true);
        }
        onGameMenuLoad.Invoke();
        UprightRedirector.Instance.PerformUprightRedirection();
    }

    #region OnServer

    /// <summary>
    /// Loads a level with a given ID and hides the game menu
    /// </summary>
    [ClientRpc]
    public virtual void LoadLevel(int level)
    {
        if (currentlyPlayedLevel && isServer)
        {
            NetworkServer.Destroy(currentlyPlayedLevel);
        }
        objectToHide.SetActive(false);
        if (isServer)
        {
            StartCoroutine(SpawnLevel(level));
        }
        //change place of OnLevelLoad
    }

    private IEnumerator SpawnLevel(int level)
    {
        yield return new WaitForSecondsRealtime(0.1f);
        currentlyPlayedLevel = Instantiate(prefabs[level], new Vector3(0, levelSpawningHeight, levelSpawningDepth), Quaternion.identity);
        NetworkServer.Spawn(currentlyPlayedLevel);
        onLevelLoad.Invoke();
    } 

    /// <summary>
    /// Displays game menu and hides the currently played level
    /// </summary>
    [ClientRpc]
    public void ReturnToGameMenu()
    {
        if (isServer) {
            currentlyPlayedLevel.GetComponent<TangramLevelManager>()?.DestroyPieces();
            NetworkServer.Destroy(currentlyPlayedLevel); 
        }
        currentlyPlayedLevel = null;
        objectToHide.SetActive(true);
        onGameMenuLoad.Invoke();
    }

    public void ReturnToMainMenu()
    {
        onMainMenuLoad.Invoke();
        if (isServer) CustomNetworkManager.singleton.ServerChangeScene("MainMenuSceneOnline");
    }

    public void ChangeTangramMode(bool autoSnapping)
    {
        if (currentlyPlayedLevel) return;
        var tlm = currentlyPlayedLevel.GetComponent<TangramLevelManager>();
        if (tlm != null) tlm.autoSnapping = autoSnapping;
    }
    #endregion //OnServer

}
