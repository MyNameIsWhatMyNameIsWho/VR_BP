using Mirror;
using System.Collections;
using UnityEngine;

public class NewGameMenuManager : GameMenuManager
{
    private NewGameManager newGameManager;
    private bool gameSetupInitialized = false;

    [ClientRpc]
    public override void LoadLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);

        // After calling base.LoadLevel, the level is spawned asynchronously,
        // so we start a coroutine to set up the game once it's ready.
        StartCoroutine(SetupGameAfterLoad(false)); // false = obstacle mode
    }

    [ClientRpc]
    public void LoadCollectibleLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);

        // After calling base.LoadLevel, the level is spawned asynchronously,
        // so we start a coroutine to set up the game once it's ready.
        StartCoroutine(SetupGameAfterLoad(true)); // true = collection mode
    }

    private IEnumerator SetupGameAfterLoad(bool isCollectionMode)
    {
        // Wait until the currentlyPlayedLevel is assigned
        while (currentlyPlayedLevel == null) yield return null;

        // Get the main game logic component from the spawned level prefab
        newGameManager = currentlyPlayedLevel.GetComponent<NewGameManager>();

        if (newGameManager != null)
        {
            // Set the game mode
            SetGameMode(isCollectionMode);

            gameSetupInitialized = true;
            Debug.Log($"NewGame setup completed successfully. Collection Mode: {isCollectionMode}");
        }
        else
        {
            Debug.LogError("Failed to find NewGameManager component on the spawned level");
        }
    }

    [Command(requiresAuthority = false)]
    private void SetGameMode(bool collectionMode)
    {
        if (newGameManager != null)
        {
            Debug.Log($"Setting game mode: Collection Mode = {collectionMode}");
            // We need to access the internal field using reflection since it's private
            var field = typeof(NewGameManager).GetField("isCollectionMode",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            if (field != null)
            {
                field.SetValue(newGameManager, collectionMode);
            }
            else
            {
                Debug.LogError("Could not find isCollectionMode field using reflection");
            }
        }
        else
        {
            Debug.LogError("NewGameManager is null, cannot set game mode");
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