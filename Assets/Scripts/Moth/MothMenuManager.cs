using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menu manager for the Moth Game - handles level loading and difficulty settings
/// </summary>
public class MothMenuManager : GameMenuManager
{
    private MothGameManager mothGameManager;
    private bool debugMode = true;

    [System.Serializable]
    public class DifficultySettings
    {
        public float mothSpeed = 1.0f;
        public string difficultyName = "Normal";
    }

    [Header("Difficulty Settings")]
    [SerializeField]
    private DifficultySettings easySettings = new DifficultySettings
    {
        mothSpeed = 0.7f,
        difficultyName = "Easy"
    };

    [SerializeField]
    private DifficultySettings normalSettings = new DifficultySettings
    {
        mothSpeed = 1.0f,
        difficultyName = "Normal"
    };

    [SerializeField]
    private DifficultySettings hardSettings = new DifficultySettings
    {
        mothSpeed = 1.3f,
        difficultyName = "Hard"
    };

    // Public methods to be called by UI buttons
    public void LoadEasyMothLevel(int level)
    {
        if (debugMode) Debug.Log($"MothMenuManager: LoadEasyMothLevel called with level {level}");
        base.LoadLevel(level);
        StartCoroutine(SetupGameDifficulty(easySettings));
    }

    public void LoadNormalMothLevel(int level)
    {
        if (debugMode) Debug.Log($"MothMenuManager: LoadNormalMothLevel called with level {level}");
        base.LoadLevel(level);
        StartCoroutine(SetupGameDifficulty(normalSettings));
    }

    public void LoadHardMothLevel(int level)
    {
        if (debugMode) Debug.Log($"MothMenuManager: LoadHardMothLevel called with level {level}");
        base.LoadLevel(level);
        StartCoroutine(SetupGameDifficulty(hardSettings));
    }

    private IEnumerator SetupGameDifficulty(DifficultySettings settings)
    {
        if (debugMode) Debug.Log("MothMenuManager: SetupGameDifficulty started");

        // Wait until the level is loaded
        float timeout = 5f;
        float elapsed = 0f;
        while (currentlyPlayedLevel == null && elapsed < timeout)
        {
            if (debugMode) Debug.Log("MothMenuManager: Waiting for level to load...");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (currentlyPlayedLevel == null)
        {
            Debug.LogError("MothMenuManager: Level failed to load after timeout!");
            yield break;
        }

        if (debugMode) Debug.Log($"MothMenuManager: Level loaded: {currentlyPlayedLevel.name}");

        // Get the MothGameManager component
        mothGameManager = currentlyPlayedLevel.GetComponent<MothGameManager>();

        if (mothGameManager != null)
        {
            // Apply difficulty settings
            SetDifficulty(settings.mothSpeed, settings.difficultyName);
            if (debugMode) Debug.Log($"MothMenuManager: Applied {settings.difficultyName} difficulty");
        }
        else
        {
            Debug.LogError("MothMenuManager: Failed to find MothGameManager on the spawned level!");
        }
    }

    [Command(requiresAuthority = false)]
    private void SetDifficulty(float speed, string difficultyName)
    {
        if (mothGameManager == null)
        {
            Debug.LogError("MothMenuManager: mothGameManager is null in SetDifficulty");
            return;
        }

        // Use reflection to access private difficulty field
        var difficultyField = typeof(MothGameManager).GetField("difficulty",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (difficultyField != null)
        {
            difficultyField.SetValue(mothGameManager, speed);
            Debug.Log($"MothMenuManager: Set moth game difficulty to {difficultyName} (speed: {speed})");
        }
        else
        {
            Debug.LogError("MothMenuManager: Could not find 'difficulty' field in MothGameManager");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdRestartMothGame()
    {
        if (debugMode) Debug.Log("MothMenuManager: CmdRestartMothGame called");

        if (mothGameManager != null)
        {
            mothGameManager.RestartGame();
        }
        else
        {
            Debug.LogError("MothMenuManager: mothGameManager is null in CmdRestartMothGame");
        }
    }

    // Override LoadLevel to add debugging but without direct prefab access
    [ClientRpc]
    public override void LoadLevel(int level)
    {
        Debug.Log($"MothMenuManager: LoadLevel called with level {level}");

        // Call the base implementation 
        base.LoadLevel(level);

        // Log after the level is called
        Debug.Log("MothMenuManager: Base LoadLevel completed");
    }
}