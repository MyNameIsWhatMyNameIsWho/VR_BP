using Mirror;
using System.Collections;
using UnityEngine;

public class NewGameMenuManager : GameMenuManager
{
    private NewGameManager newGameManager;

    // Define difficulty settings to allow configuration in the Inspector
    public enum DifficultyLevel
    {
        Slow,
        Medium,
        Fast
    }

    [System.Serializable]
    public class DifficultySettings
    {
        public float obstacleFallSpeed = 0.8f;
        public float obstacleSpawnInterval = 4f;
        public float playerMovementSpeed = 5f;
    }

    [Header("Difficulty Settings")]
    [SerializeField]
    private DifficultySettings slowSettings = new DifficultySettings
    {
        obstacleFallSpeed = 0.6f,
        obstacleSpawnInterval = 5.0f,
        playerMovementSpeed = 4.0f
    };

    [SerializeField]
    private DifficultySettings mediumSettings = new DifficultySettings
    {
        obstacleFallSpeed = 0.8f,
        obstacleSpawnInterval = 4.0f,
        playerMovementSpeed = 5.0f
    };

    [SerializeField]
    private DifficultySettings fastSettings = new DifficultySettings
    {
        obstacleFallSpeed = 1.2f,
        obstacleSpawnInterval = 3.0f,
        playerMovementSpeed = 6.0f
    };

    [ClientRpc]
    public void LoadCollectibleLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);

        // After loading, set up the game as collectible mode
        StartCoroutine(SetupGameAfterLoad(true, DifficultyLevel.Medium)); // Difficulty doesn't matter for collectible mode
    }

    [ClientRpc]
    public void LoadObstacleLevelSlow(int level)
    {
        base.LoadLevel(level);
        StartCoroutine(SetupGameAfterLoad(false, DifficultyLevel.Slow));
    }

    [ClientRpc]
    public void LoadObstacleLevelMedium(int level)
    {
        base.LoadLevel(level);
        StartCoroutine(SetupGameAfterLoad(false, DifficultyLevel.Medium));
    }

    [ClientRpc]
    public void LoadObstacleLevelFast(int level)
    {
        base.LoadLevel(level);
        StartCoroutine(SetupGameAfterLoad(false, DifficultyLevel.Fast));
    }

    private IEnumerator SetupGameAfterLoad(bool isCollectionMode, DifficultyLevel difficulty)
    {
        // Wait until the currentlyPlayedLevel is assigned
        while (currentlyPlayedLevel == null) yield return null;

        // Get the main game logic component from the spawned level prefab
        newGameManager = currentlyPlayedLevel.GetComponent<NewGameManager>();

        if (newGameManager != null)
        {
            // Set the game mode
            SetGameMode(isCollectionMode);

            // Set difficulty if we're in obstacle mode
            if (!isCollectionMode)
            {
                SetDifficultyLevel(difficulty);
            }

            Debug.Log($"NewGame setup completed successfully. Collection Mode: {isCollectionMode}, " +
                      (isCollectionMode ? "" : $"Difficulty: {difficulty}"));
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
    private void SetDifficultyLevel(DifficultyLevel difficulty)
    {
        if (newGameManager == null)
        {
            Debug.LogError("NewGameManager is null, cannot set difficulty");
            return;
        }

        // Use reflection to access private fields in NewGameManager
        var obstacleSpeedField = typeof(NewGameManager).GetField("obstacleFallSpeed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        var obstacleIntervalField = typeof(NewGameManager).GetField("obstacleSpawnInterval",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (obstacleSpeedField == null || obstacleIntervalField == null)
        {
            Debug.LogError("Could not find required fields in NewGameManager");
            return;
        }

        // Get the settings for the selected difficulty
        DifficultySettings settings;
        string difficultyName;

        switch (difficulty)
        {
            case DifficultyLevel.Slow:
                settings = slowSettings;
                difficultyName = "Slow";
                break;

            case DifficultyLevel.Fast:
                settings = fastSettings;
                difficultyName = "Fast";
                break;

            case DifficultyLevel.Medium:
            default:
                settings = mediumSettings;
                difficultyName = "Medium";
                break;
        }

        // Apply the settings
        obstacleSpeedField.SetValue(newGameManager, settings.obstacleFallSpeed);
        obstacleIntervalField.SetValue(newGameManager, settings.obstacleSpawnInterval);
        newGameManager.ChangePlayerSpeed(settings.playerMovementSpeed);
        newGameManager.SetDifficultyForLogging(difficultyName);

        Debug.Log($"Applied {difficulty} difficulty settings to NewGameManager: " +
                 $"Fall Speed={settings.obstacleFallSpeed}, " +
                 $"Spawn Interval={settings.obstacleSpawnInterval}, " +
                 $"Player Speed={settings.playerMovementSpeed}");
    }

    [Command(requiresAuthority = false)]
    public void CmdResetNewGame()
    {
        if (newGameManager != null)
        {
            newGameManager.RestartGame();
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
        }
        else
        {
            Debug.LogError("NewGameManager is null, cannot switch hands");
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdReplayTutorial()
    {
        if (newGameManager != null)
        {
            // Use reflection to access the private fields
            var audioTutorialField = typeof(NewGameManager).GetField("audioTutorial", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public);
                
            var isCollectionModeField = typeof(NewGameManager).GetField("isCollectionMode", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            var awaitingCalibrationField = typeof(NewGameManager).GetField("awaitingCalibration", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (audioTutorialField != null && isCollectionModeField != null && awaitingCalibrationField != null)
            {
                var audioTutorial = audioTutorialField.GetValue(newGameManager);
                bool isCollectionMode = (bool)isCollectionModeField.GetValue(newGameManager);
                
                // Reset awaitingCalibration to true so the game waits for calibration again
                awaitingCalibrationField.SetValue(newGameManager, true);
                
                if (audioTutorial != null)
                {
                    // Call the OnGameModeSelected method through reflection
                    var method = audioTutorial.GetType().GetMethod("OnGameModeSelected");
                    if (method != null)
                    {
                        method.Invoke(audioTutorial, new object[] { isCollectionMode });
                        Debug.Log("Replaying audio tutorial");
                    }
                    else
                    {
                        Debug.LogError("OnGameModeSelected method not found on audioTutorial");
                    }
                }
                else
                {
                    Debug.LogError("audioTutorial is null, cannot replay tutorial");
                }
            }
            else
            {
                Debug.LogError("Could not find required fields using reflection");
            }
        }
        else
        {
            Debug.LogError("NewGameManager is null, cannot replay tutorial");
        }
    }
}