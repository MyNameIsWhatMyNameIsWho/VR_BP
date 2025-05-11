using Mirror;
using System.Collections;
using UnityEngine;

/// <summary>
/// Menu manager for the Moth Game - handles level loading and auto-starts the game
/// </summary>
public class MothMenuManager : GameMenuManager
{
    private MothGameManager mothGameManager;

    // Public method to be called by UI button - loads and immediately starts the game
    public void LoadMothLevel(int level)
    {
        base.LoadLevel(level);
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
            // This mimics what happens in OnGameStart but we do it manually before starting
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

    [Command(requiresAuthority = false)]
    public void CmdRestartMothGame()
    {
        if (mothGameManager != null)
        {
            mothGameManager.RestartGame();
        }
    }

    //[Command(requiresAuthority = false)]
    //public void CmdReplayTutorial()
    //{
    //    if (mothGameManager != null && mothGameManager.audioTutorial != null)
    //    {
    //        // Replay the tutorial by calling the play method on the audio tutorial
    //        mothGameManager.audioTutorial.OnGameStart();
    //        Debug.Log("Replaying moth game tutorial");
    //    }
    //    else
    //    {
    //        Debug.LogError("MothGameManager or audioTutorial is null, cannot replay tutorial");
    //    }
    //}

    // Override LoadLevel
    [ClientRpc]
    public override void LoadLevel(int level)
    {
        // Call the base implementation 
        base.LoadLevel(level);
    }
}