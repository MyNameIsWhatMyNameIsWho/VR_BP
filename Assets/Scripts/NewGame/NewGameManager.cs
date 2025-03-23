using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class NewGameManager : NetworkBehaviour
{
    public static NewGameManager Instance;

    [Header("Object Settings")]
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject collectiblePrefab;
    [SerializeField] private List<GameObject> activeObjects = new List<GameObject>(); // Keep track of all spawned objects

    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary for the cube
    [SerializeField] private float maxX = 5f;   // Right boundary for the cube
    [SerializeField] private float wallBuffer = 0.5f; // Buffer distance from wall to prevent objects spawning in walls
    [SerializeField] private float obstacleFallSpeed = 0.8f;
    [SerializeField] private float collectibleFallSpeed = 1.2f;
    [SerializeField] private float playerMovementSpeed = 1.0f; // Base player movement speed

    [Header("Adaptive Spawning")]
    [SerializeField] public AdaptiveSpawner adaptiveSpawner; // Change to public
    [SerializeField] private bool useAdaptiveSpawning = true;

    // Public getters so other scripts can access but not modify these values
    public float ObstacleFallSpeed => obstacleFallSpeed;
    public float CollectibleFallSpeed => collectibleFallSpeed;
    public float GetMinX() { return minX; }
    public float GetMaxX() { return maxX; }
    public float GetWallBuffer() { return wallBuffer; }
    public bool IsGameRunning() { return gameRunning; }

    [Header("Spawn Settings")]
    [SerializeField] private float obstacleSpawnInterval = 4f;
    [SerializeField] private float collectibleSpawnInterval = 2f;
    [SerializeField] private float objectSpawnY = 10f;

    [Header("UI Elements")]
    [SerializeField] private GameObject initialButtons;
    [SerializeField] private GameObject inGameBackButton;
    [SerializeField] private GameObject endgameInfo;
    // [SerializeField] private GameObject calibrationPrompt; // UI element to show calibration instructions

    [Header("Game Settings")]
    [SerializeField] private float movementSpeed = 2.00f;
    [SerializeField] private bool isCollectionMode = false;
    [SerializeField] private float gameTimeLimit = 60f;
    [SerializeField] private float waitAfterDeath = 2f;

    [Header("References")]
    [SerializeField] private Character_NewGame character;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private TextMeshPro timeText;
    [SerializeField] private TextMeshPro endgameScoreText;
    [SerializeField] private TextMeshPro endgameHighScoreText;

    private GameMenuManager menuManager;

    // Game state variables
    private bool gameRunning = false;
    private bool awaitingCalibration = true; // Flag to indicate we're waiting for initial gesture
    private float gameTimer = 0f;
    private Coroutine spawnCoroutine;
    private bool gameEnding = false; // Flag to prevent multiple EndGame calls

    // Restart delay for calibration to prevent immediate restart
    private float restartCooldownTimer = 0f;
    private const float RESTART_COOLDOWN = 1.5f;

    public float score = 0f;
    public UnityEvent OnGameStart;
    public UnityEvent OnGameEnd;

    // Static variables that reset when the game restarts
    private static float sessionHighscoreCollection = 0f;
    private static float sessionHighscoreObstacles = 0f;

    // Highscore properties for different game modes
    private float Highscore
    {
        get
        {
            if (isCollectionMode)
                return sessionHighscoreCollection;
            else
                return sessionHighscoreObstacles;
        }
        set
        {
            if (isCollectionMode)
                sessionHighscoreCollection = value;
            else
                sessionHighscoreObstacles = value;

            // Optional debug log
            Debug.Log($"New highscore set: {value} for {(isCollectionMode ? "Collection" : "Obstacles")} mode");
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        activeObjects = new List<GameObject>();
    }

    private void Start()
    {
        menuManager = FindFirstObjectByType<GameMenuManager>();

        // Set up UI toggles for game start/end
        OnGameEnd.AddListener(() =>
        {
            Debug.Log("OnGameEnd invoked - showing UI buttons");
            initialButtons.SetActive(true);
            inGameBackButton.SetActive(false);
        });

        OnGameStart.AddListener(() =>
        {
            Debug.Log("OnGameStart invoked - hiding UI buttons");
            initialButtons.SetActive(false);
            inGameBackButton.SetActive(true);
            // DisplayCalibrationPrompt(false);
        });

        // Ensure endgame UI is hidden at start
        if (endgameInfo != null)
        {
            endgameInfo.SetActive(false);
        }
        else
        {
            Debug.LogError("endgameInfo reference is missing!");
        }

        // Show calibration prompt at start
        // DisplayCalibrationPrompt(true);

        if (!isServer) return;

        // Set up gesture detection to start the game
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.OnGestureBegin.RemoveListener(OnGestureDetectedHandler);
            GestureDetector.Instance.OnGestureBegin.AddListener(OnGestureDetectedHandler);

            // Make sure the character reference is valid
            if (character != null)
            {
                // Set up controlling hand
                if (GestureDetector.Instance.handL != null && GestureDetector.Instance.handR != null)
                {
                    GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
                    GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);

                    // If right hand is disabled, switch to left
                    if (GestureDetector.Instance.handR.IsDisabled)
                    {
                        character.controllingHandIsLeft = !character.controllingHandIsLeft;
                        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
                        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
                    }
                }
                else
                {
                    Debug.LogError("GestureDetector hands are null!");
                }

                // Position character
                character.Spawn();
            }
            else
            {
                Debug.LogError("Character reference is null!");
            }
        }
        else
        {
            Debug.LogError("GestureDetector.Instance is null");
        }

        // Log highscore
        Debug.Log($"Current highscore: {Highscore}");

        // Play instructions if needed
        StartCoroutine(PlayInstructions());
    }

    private IEnumerator PlayInstructions()
    {
        yield return null;
        // Add your audio instructions here if needed
    }

    private void OnGestureDetectedHandler(GestureType gesture, bool isLeft)
    {
        // Check if this is the controlling hand
        bool isControllingHand = (character.controllingHandIsLeft == isLeft);

        // Only process if we're not in the restart cooldown period
        if (restartCooldownTimer <= 0)
        {
            // Start game if awaiting calibration and not showing endgame
            if (awaitingCalibration && !endgameInfo.activeSelf && isControllingHand)
            {
                Debug.Log($"Calibration gesture detected: {gesture} from {(isLeft ? "left" : "right")} hand");
                StartGame();
                awaitingCalibration = false; // No longer awaiting calibration
            }
        }
    }

    private void Update()
    {
        // Update the restart cooldown timer if active
        if (restartCooldownTimer > 0)
        {
            restartCooldownTimer -= Time.deltaTime;
        }

        if (!gameRunning) return;

        if (isCollectionMode)
        {
            // Update timer for collection mode
            gameTimer += Time.deltaTime;

            // Check if time limit reached
            if (gameTimer >= gameTimeLimit)
            {
                EndGame();
                return;
            }

            // Update UI with remaining time
            UpdateTimeDisplay(gameTimeLimit - gameTimer);
        }
        else
        {
            // In obstacle mode, score increases over time
            score += movementSpeed * Time.deltaTime;
            ChangeScore(Mathf.RoundToInt(score));
        }
    }

    private void OnDestroy()
    {
        // Clean up all spawned objects
        if (isServer)
        {
            foreach (var obj in activeObjects)
            {
                if (obj != null)
                    NetworkServer.Destroy(obj);
            }
            activeObjects.Clear();
        }

        // Remove gesture listener
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.OnGestureBegin.RemoveListener(OnGestureDetectedHandler);
        }
    }

    [ClientRpc]
    public void EndGame()
    {
        // Prevent multiple EndGame calls
        if (gameEnding) return;
        gameEnding = true;

        Debug.Log("EndGame called - stopping all coroutines");

        // Reset adaptive spawner tracking
        if (isServer && adaptiveSpawner != null)
        {
            adaptiveSpawner.ResetTracking();
        }

        StopAllCoroutines();

        // Stop any specific spawn coroutine
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // Immediately set game state to not running
        gameRunning = false;

        // Immediately disable movement in character
        if (character != null)
        {
            character.GameOver();
        }

        // Start the end game sequence
        StartCoroutine(EndGameCoroutine());
    }

    public IEnumerator EndGameCoroutine()
    {
        Debug.Log("EndGameCoroutine started");

        if (isServer)
        {
            // Set the character to not be controlled by hands
            if (GestureDetector.Instance != null)
            {
                GestureDetector.Instance.handL.SetControllingHand(false);
                GestureDetector.Instance.handR.SetControllingHand(false);
            }

            // Clean up all active objects
            foreach (var obj in activeObjects.ToArray())
            {
                if (obj != null)
                    NetworkServer.Destroy(obj);
            }
            activeObjects.Clear();

            // Wait for the specified delay
            yield return new WaitForSeconds(waitAfterDeath);

            // Update highscore if needed
            if (Highscore < score)
            {
                Debug.Log($"New highscore: {score}");
                Highscore = score;
            }

            // Make sure score is updated
            ChangeScore(Mathf.RoundToInt(score));

            // Show end game UI with scores
            DisplayEndGameInfo(true);

            // Log game data
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.AddToCustomData("newgame_score", "\"" + score.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("newgame_highscore", "\"" + Highscore.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("newgame_mode",
                    isCollectionMode ? "\"Collection Mode\"" : "\"Obstacle Mode\"");
                LoggerCommunicationProvider.Instance.StopLogging();
            }
        }

        // Invoke the game end event to update UI
        Debug.Log("Invoking OnGameEnd");
        OnGameEnd.Invoke();
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display)
    {
        Debug.Log("DisplayEndGameInfo: " + display);

        if (endgameInfo == null)
        {
            Debug.LogError("endgameInfo is null!");
            return;
        }

        endgameInfo.SetActive(display);

        if (!display) return;

        // Set the score text
        if (endgameScoreText != null)
        {
            endgameScoreText.text = Mathf.RoundToInt(score).ToString();
            Debug.Log("Set endgame score text to: " + endgameScoreText.text);
        }
        else
        {
            Debug.LogError("endgameScoreText is null!");
        }

        // Set the highscore text
        if (endgameHighScoreText != null)
        {
            endgameHighScoreText.text = Mathf.RoundToInt(Highscore).ToString();
            Debug.Log("Set endgame highscore text to: " + endgameHighScoreText.text);
        }
        else
        {
            Debug.LogError("endgameHighScoreText is null!");
        }
    }

    // [ClientRpc]
    // private void DisplayCalibrationPrompt(bool display)
    // {
    //     if (calibrationPrompt != null)
    //     {
    //         calibrationPrompt.SetActive(display);
    //         Debug.Log($"Calibration prompt {(display ? "shown" : "hidden")}");
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Calibration prompt UI element is not assigned!");
    //     }
    // }

    [ClientRpc]
    public void StartGame()
    {
        Debug.Log("StartGame invoked");

        OnGameStart.Invoke();

        if (!isServer || gameRunning) return;

        // Reset game state
        gameRunning = true;
        gameEnding = false;
        awaitingCalibration = false; // Calibration complete
        score = 0;
        gameTimer = 0f;
        ChangeScore(0);

        // Set initial player movement speed
        if (character != null)
        {
            character.SetMovementSpeed(playerMovementSpeed);
        }

        // Hide endgame UI if it's visible
        if (endgameInfo.activeSelf)
        {
            DisplayEndGameInfo(false);
        }

        // Clear any existing objects
        foreach (var obj in activeObjects.ToArray())
        {
            if (obj != null)
                NetworkServer.Destroy(obj);
        }
        activeObjects.Clear();

        // Kill any existing spawning coroutine
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        // Start appropriate spawning routine
        if (isCollectionMode)
        {
            if (collectiblePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnCollectibles());
                Debug.Log("Started collectible spawning coroutine");
            }
            else
            {
                Debug.LogError("Collectible prefab is not assigned!");
            }
        }
        else
        {
            if (obstaclePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnObstacles());
                Debug.Log("Started obstacle spawning coroutine");
            }
            else
            {
                Debug.LogError("Obstacle prefab is not assigned!");
            }
        }

        // Start character movement
        character.StartMovement();

        // Log game start
        if (LoggerCommunicationProvider.Instance != null)
        {
            LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
            LoggerCommunicationProvider.Instance.AddToCustomData("newgame_mode",
                isCollectionMode ? "\"Collection Mode\"" : "\"Obstacle Mode\"");
        }
    }

    private IEnumerator SpawnObstacles()
    {
        Debug.Log("SpawnObstacles coroutine started");
        yield return new WaitForSeconds(1.0f); // Initial delay

        // Keep spawning as long as game is running
        while (gameRunning)
        {
            SpawnObject(obstaclePrefab, "Obstacle");
            yield return new WaitForSeconds(obstacleSpawnInterval);

            // Double check game is still running
            if (!gameRunning)
            {
                Debug.Log("Game no longer running - stopping obstacle spawning");
                yield break;
            }
        }

        Debug.Log("SpawnObstacles coroutine exited");
    }

    private IEnumerator SpawnCollectibles()
    {
        Debug.Log("SpawnCollectibles coroutine started");
        yield return new WaitForSeconds(0.5f); // Initial delay

        // Keep spawning as long as game is running
        while (gameRunning)
        {
            SpawnObject(collectiblePrefab, "Collectible");
            yield return new WaitForSeconds(collectibleSpawnInterval);

            // Double check game is still running
            if (!gameRunning)
            {
                Debug.Log("Game no longer running - stopping collectible spawning");
                yield break;
            }
        }

        Debug.Log("SpawnCollectibles coroutine exited");
    }

    [Server]
    private void SpawnObject(GameObject prefab, string objectType)
    {
        // Additional check to ensure we don't spawn after game over
        if (!gameRunning)
        {
            Debug.Log("Attempted to spawn object after game ended - ignoring");
            return;
        }

        Vector3 spawnPosition;

        // Use adaptive spawning if enabled
        if (useAdaptiveSpawning && adaptiveSpawner != null)
        {
            if (objectType == "Obstacle")
            {
                // Obstacles spawn above player to force movement
                spawnPosition = adaptiveSpawner.GetObstacleSpawnPosition(objectSpawnY);
            }
            else // Collectible
            {
                // Collectibles spawn in areas player struggles with
                spawnPosition = adaptiveSpawner.GetCollectibleSpawnPosition(objectSpawnY);
            }
        }
        else
        {
            // Original spawning logic - safe area only
            float safeMinX = minX + wallBuffer;
            float safeMaxX = maxX - wallBuffer;

            spawnPosition = new Vector3(
                Random.Range(safeMinX, safeMaxX),
                objectSpawnY,
                12f
            );
        }

        // Instantiate the object
        GameObject obj = Instantiate(prefab, spawnPosition, Quaternion.identity);

        // Initialize with appropriate settings
        if (objectType == "Obstacle")
        {
            Obstacle_NewGame obstacle = obj.GetComponent<Obstacle_NewGame>();
            if (obstacle != null)
            {
                obstacle.Initialize();
            }
        }
        else if (objectType == "Collectible")
        {
            Collectible collectible = obj.GetComponent<Collectible>();
            if (collectible != null)
            {
                collectible.Initialize();
            }
        }

        // Spawn on network and track
        NetworkServer.Spawn(obj);
        activeObjects.Add(obj);
    }


    /// <summary>
    /// Called when a collectible is collected by the player
    /// </summary>
    public void CollectItem(int points)
    {
        if (!isServer || !gameRunning) return;

        // Add points to score
        score += points;
        ChangeScore(Mathf.RoundToInt(score));

        // Note: We no longer call adaptiveSpawner.MarkLastCollectibleCollected() here
        // because the Collectible itself will notify the spawner directly

        // Play sound effect if available
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("CoinPickup");
        }
    }

    [ClientRpc]
    private void UpdateTimeDisplay(float remainingTime)
    {
        if (timeText != null)
        {
            // Format remaining time as MM:SS
            int minutes = Mathf.FloorToInt(remainingTime / 60);
            int seconds = Mathf.FloorToInt(remainingTime % 60);
            string timeDisplay = $"{minutes:00}:{seconds:00}";

            timeText.text = timeDisplay;

            // Update score display in collection mode
            if (scoreText != null && isCollectionMode)
            {
                scoreText.text = $"{Mathf.RoundToInt(score)}";
            }
        }
    }

    public void ReturnToMenu()
    {
        Debug.Log("ReturnToMenu called");

        // Make sure game is ended
        if (gameRunning)
        {
            EndGame();
        }

        // Return to menu
        if (menuManager != null)
        {
            menuManager.ReturnToGameMenu();
        }
        else
        {
            Debug.LogError("menuManager is null!");
        }
    }

    public void RestartGame()
    {
        if (!isServer) return;

        Debug.Log("RestartGame called");

        // Reset adaptive spawner tracking
        if (adaptiveSpawner != null)
        {
            adaptiveSpawner.ResetTracking();
        }

        // The rest of your existing RestartGame method...
        // Clean up active objects
        foreach (var obj in activeObjects.ToArray())
        {
            if (obj != null)
                NetworkServer.Destroy(obj);
        }
        activeObjects.Clear();

        // Reset character and game state
        character.Spawn();
        gameRunning = false;
        gameEnding = false;
        awaitingCalibration = true; // Set to awaiting calibration
        score = 0f;
        gameTimer = 0f;
        ChangeScore(0);

        // Reset hand control
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        }

        // Hide end game UI
        DisplayEndGameInfo(false);

        // Don't start the game yet - wait for calibration gesture
        StopAllCoroutines();
    }


    [ClientRpc]
    private void ChangeScore(int newScore)
    {
        if (scoreText != null && !isCollectionMode)
        {
            scoreText.text = newScore.ToString();
        }
        else if (scoreText != null && isCollectionMode)
        {
            scoreText.text = $"{newScore}";
        }
        else
        {
            Debug.LogWarning("scoreText is null, can't update score");
        }
    }

    public void SwitchHands()
    {
        if (GestureDetector.Instance == null ||
            GestureDetector.Instance.handL == null ||
            GestureDetector.Instance.handR == null)
        {
            Debug.LogError("GestureDetector or hands are null");
            return;
        }

        if (GestureDetector.Instance.handL.IsDisabled || GestureDetector.Instance.handR.IsDisabled)
            return;

        character.controllingHandIsLeft = !character.controllingHandIsLeft;
        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);

        Debug.Log($"Switched controlling hand to {(character.controllingHandIsLeft ? "LEFT" : "RIGHT")}");
    }

    // Method to change player movement speed
    public void ChangePlayerSpeed(float newSpeed)
    {
        playerMovementSpeed = Mathf.Max(0.1f, newSpeed);
        if (character != null && gameRunning)
        {
            character.SetMovementSpeed(playerMovementSpeed);
            Debug.Log($"Player speed changed to: {playerMovementSpeed}");
        }
    }
}