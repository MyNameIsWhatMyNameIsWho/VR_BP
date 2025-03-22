using Mirror;
using System.Collections;
using UnityEngine;

public class NewGameMenuManager : GameMenuManager
{
    private NewGameManager newGameManager;
    private bool gameSetupInitialized;

    [ClientRpc]
    public override void LoadLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);

        // After calling base.LoadLevel, the level is spawned asynchronously,
        // so we start a coroutine to set up the game once it's ready.
        StartCoroutine(SetupGameAfterLoad());
    }

    private IEnumerator SetupGameAfterLoad()
    {
        // Wait until the currentlyPlayedLevel is assigned
        while (currentlyPlayedLevel == null) yield return null;

        // Get the main game logic component from the spawned level prefab
        newGameManager = currentlyPlayedLevel.GetComponent<NewGameManager>();

        if (newGameManager != null)
        {
            // Configure the game based on the level number if needed
            // For example, set difficulty, obstacles, etc.
            gameSetupInitialized = true;
            Debug.Log("NewGame setup completed successfully");
        }
        else
        {
            Debug.LogError("Failed to find NewGameManager component on the spawned level");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdResetNewGame()
    {
        if (newGameManager != null)
        {
            newGameManager.RestartGame();
            return;
        }
        else
        {
            Debug.LogError("NewGameManager is null, cannot restart game");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdSwitchHands()
    {
        if (newGameManager != null)
        {
            newGameManager.SwitchHands();
            return;
        }
        else
        {
            Debug.LogError("NewGameManager is null, cannot switch hands");
        }
    }
}