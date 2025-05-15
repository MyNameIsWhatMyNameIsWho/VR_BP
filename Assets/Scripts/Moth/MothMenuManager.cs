using Mirror;
using System.Collections;
using UnityEngine;

public class MothMenuManager : GameMenuManager
{
    private MothGameManager mothGameManager;

    // Modified to use ClientRpc like other game managers do
    [ClientRpc]
    public void LoadMothLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);
        
        // Start coroutine to wait for level to load and then start the game
        StartCoroutine(StartGameImmediately());
    }

    private IEnumerator StartGameImmediately()
    {
        // Wait until the level is loaded
        float timeout = 5f;
        float elapsed = 0f;
        
        while (currentlyPlayedLevel == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (currentlyPlayedLevel == null)
        {
            Debug.LogError("Level failed to load after timeout!");
            yield break;
        }

        // Get the MothGameManager component
        mothGameManager = currentlyPlayedLevel.GetComponent<MothGameManager>();

        if (mothGameManager != null)
        {
            // Manually set up the UI state we want:
            // - Hide initial menu buttons
            // - Show in-game button (back to menu)
            if (mothGameManager.initialButtons != null)
            {
                mothGameManager.initialButtons.SetActive(false);
            }

            if (mothGameManager.inGameButton != null)
            {
                mothGameManager.inGameButton.SetActive(true);
            }

            // Start the game without showing the initial UI first
            mothGameManager.StartGame();
        }
        else
        {
            Debug.LogError("Failed to find MothGameManager on the spawned level!");
        }
    }

    // This method is correct - it's a Command that will run on the server
    [Command(requiresAuthority = false)]
    public void CmdRestartMothGame()
    {
        // Make sure mothGameManager is found and valid
        if (mothGameManager == null && currentlyPlayedLevel != null)
        {
            // Try to get the reference if it's null but we have a level
            mothGameManager = currentlyPlayedLevel.GetComponent<MothGameManager>();
        }
        
        // Call RestartGame on the MothGameManager
        if (mothGameManager != null)
        {
            mothGameManager.RestartGame();
            Debug.Log("Moth game restarted successfully");
        }
        else
        {
            Debug.LogError("Cannot restart Moth game - MothGameManager reference is missing");
        }
    }

    // Optional: Add this method back if you want tutorial replay functionality
    /*
    [Command(requiresAuthority = false)]
    public void CmdReplayTutorial()
    {
        if (mothGameManager != null && mothGameManager.audioTutorial != null)
        {
            // Replay the tutorial
            mothGameManager.audioTutorial.OnGameStart();
            Debug.Log("Replaying moth game tutorial");
        }
        else
        {
            Debug.LogError("MothGameManager or audioTutorial is null, cannot replay tutorial");
        }
    }
    */

    // Override LoadLevel
    [ClientRpc]
    public override void LoadLevel(int level)
    {
        // Call the base implementation 
        base.LoadLevel(level);
    }
}