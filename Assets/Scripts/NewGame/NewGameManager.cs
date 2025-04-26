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
    //[SerializeField] private float collectibleFallSpeed = 1.2f;
    [SerializeField] private float playerMovementSpeed = 1.0f; // Base player movement speed

    [Header("Adaptive Spawning")]
    [SerializeField] public AdaptiveSpawner adaptiveSpawner; // Change to public
    [SerializeField] private bool useAdaptiveSpawning = true;

    [Header("Combined Mode Settings")]
    [SerializeField] private bool enableCollectiblesInObstacleMode = true;
    [SerializeField] private float collectibleSpawnChance = 0.3f; // 30% chance to spawn a collectible
    [SerializeField] private float obstacleCollectibleMinDistance = 1.5f; // Minimum distance between obstacles and collectibles
    [SerializeField] private int collectibleBonusPoints = 5; // Points added for each collectible in obstacle mode
    [SerializeField] private float collectibleSpawnHeightOffset = 1.0f; // Spawn collectibles a bit lower than obstacles

    // Store the difficulty level for logging
    private string currentDifficultyLevel = "Medium"; // Default value
    private static bool hasPlayedAnyGame = false;

    // Public getters so other scripts can access but not modify these values
    public float ObstacleFallSpeed => obstacleFallSpeed;
    public float CollectibleFallSpeed => currentCollectibleFallSpeed;
    public float GetMinX() { return minX; }
    public float GetMaxX() { return maxX; }
    public float GetWallBuffer() { return wallBuffer; }
    public bool IsGameRunning() { return gameRunning; }

    [Header("Spawn Settings")]
    [SerializeField] private float obstacleSpawnInterval = 4f;
    [SerializeField] private float collectibleSpawnInterval = 2f;
    [SerializeField] private float objectSpawnY = 10f;

    [Header("Tutorial Settings")]
    [SerializeField] private bool isFirstPlay = true; // Track if this is first play
    public bool IsFirstPlay()
    {
        // Return true only if this is literally the very first play
        return !hasPlayedAnyGame;
    }

    [Header("UI Elements")]
    [SerializeField] private GameObject initialButtons;
    [SerializeField] private GameObject inGameBackButton;
    //[SerializeField] private GameObject switchHandsButton;
    [SerializeField] private GameObject endgameInfo;
    [SerializeField] private TextMeshPro calibrationSuccessText; // Reference to "Calibrated!" text
    [SerializeField] private float calibrationTextDisplayTime = 2f; // How long to show the text

    [Header("Game Settings")]
    [SerializeField] private float movementSpeed = 2.00f;
    [SerializeField] private bool isCollectionMode = false;
    [SerializeField] private float gameTimeLimit = 60f;
    [SerializeField] private float waitAfterDeath = 2f;

    [Header("Collection Mode Settings")]
    [SerializeField] private float initialCollectibleFallSpeed = 1.2f; // Starting fall speed
    [SerializeField] private float maxCollectibleFallSpeed = 2.5f;     // Maximum fall speed
    [SerializeField] private float speedIncreaseRate = 0.02f;          // Speed increase per second
    private float currentCollectibleFallSpeed;                         // Current speed

    [Header("References")]
    [SerializeField] private Character_NewGame character;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private TextMeshPro timeText;
    [SerializeField] private TextMeshPro endgameScoreText;
    [SerializeField] private TextMeshPro endgameHighScoreText;
    [SerializeField] private GameAudioTutorial audioTutorial; // Reference to audio tutorial component

    private GameMenuManager menuManager;

    // Game state variables
    private bool gameRunning = false;
    private bool awaitingCalibration = true; // Flag to indicate we're waiting for initial gesture
    //private bool tutorialComplete = false;   // Flag to indicate tutorial has finished
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

        // Initialize the current collectible fall speed
        currentCollectibleFallSpeed = initialCollectibleFallSpeed;

        // Hide time text at start
        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

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

        // Ensure audio tutorial plays the first time the game is started
        if (isServer && audioTutorial != null)
        {
            // Delay slightly to ensure everything is loaded
            StartCoroutine(DelayedFirstAudioInstructions());
        }

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
    }

    // Method to set difficulty for logging
    public void SetDifficultyForLogging(string difficultyLevel)
    {
        currentDifficultyLevel = difficultyLevel;
        Debug.Log($"Difficulty level set to: {currentDifficultyLevel}");
    }

    // Coroutine to ensure first instructions play after everything is initialized
    private IEnumerator DelayedFirstAudioInstructions()
    {
        yield return new WaitForSeconds(0.5f);
        audioTutorial.OnGameModeSelected(isCollectionMode);
    }

    private void OnGestureDetectedHandler(GestureType gesture, bool isLeft)
    {
        // Only respond to Paper gesture for calibration
        if (gesture != GestureType.Paper) return;

        // Check if this is the controlling hand
        bool isControllingHand = (character.controllingHandIsLeft == isLeft);

        // Only process if we're not in the restart cooldown period
        if (restartCooldownTimer <= 0)
        {
            // Start game if awaiting calibration, not showing endgame, using controlling hand,
            // and audio tutorial allows calibration
            if (awaitingCalibration && !endgameInfo.activeSelf && isControllingHand &&
                (audioTutorial == null || audioTutorial.CanCalibrate()))
            {
                Debug.Log($"Calibration gesture detected: {gesture} from {(isLeft ? "left" : "right")} hand");

                // IMPORTANT: Hide initialButtons and show inGameBackButton here when calibration is detected
                initialButtons.SetActive(false);
                inGameBackButton.SetActive(true);

                // Show the calibration success text
                StartCoroutine(ShowCalibrationSuccessText());

                // Call audio tutorial to start the game with rules
                if (audioTutorial != null)
                {
                    audioTutorial.OnCalibrationComplete(isCollectionMode);
                }
                else
                {
                    // If no audio tutorial, start game immediately
                    StartSpawningAfterTutorial();
                }

                awaitingCalibration = false; // No longer awaiting calibration
            }
        }
    }

    private bool spawningStarted = false;

    // Called by GameAudioTutorial when calibration is complete
    public void StartCharacterMovementOnly()
    {
        if (!isServer) return;

        Debug.Log("Starting character movement after calibration");
        gameRunning = true;

        // Start character movement only
        character.StartMovement();
    }

    // Called by GameAudioTutorial when rules audio finishes
    public void StartSpawningOnly()
    {
        if (!isServer) return;

        Debug.Log("Starting spawning after rules audio");

        // IMPORTANT: Now we're starting spawning, so enable timer and scoring
        spawningStarted = true;

        //// Show timer if in collection mode
        //if (isCollectionMode && timeText != null)
        //{
        //    timeText.gameObject.SetActive(true);
        //}

        // Start the appropriate spawning coroutine
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        if (isCollectionMode)
        {
            if (collectiblePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnCollectibles());
                Debug.Log("Started collectible spawning coroutine after rules");
            }
        }
        else
        {
            if (obstaclePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnObstacles());
                Debug.Log("Started obstacle spawning coroutine after rules");
            }
        }
    }

    // Legacy method for when there's no audio tutorial
    public void StartSpawningAfterTutorial()
    {
        if (!isServer) return;

        //tutorialComplete = true;
        gameRunning = true;
        Debug.Log("Tutorial complete, starting game all at once");

        // Start character movement
        character.StartMovement();

        // Start the appropriate spawning coroutine
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        if (isCollectionMode)
        {
            if (collectiblePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnCollectibles());
            }
        }
        else
        {
            if (obstaclePrefab != null)
            {
                spawnCoroutine = StartCoroutine(SpawnObstacles());
            }
        }
    }

    private IEnumerator ShowCalibrationSuccessText()
    {
        // Show the text
        if (calibrationSuccessText != null)
        {
            calibrationSuccessText.gameObject.SetActive(true);

            // Wait for the specified time
            yield return new WaitForSeconds(calibrationTextDisplayTime);

            // Hide the text
            calibrationSuccessText.gameObject.SetActive(false);
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

        // IMPORTANT: Only update timer and score if spawning has started
        if (!spawningStarted) return;

        if (isCollectionMode)
        {
            // Update timer for collection mode (just for tracking, no limit)
            gameTimer += Time.deltaTime;

            // Gradually increase difficulty over time
            currentCollectibleFallSpeed = Mathf.Min(
                currentCollectibleFallSpeed + (speedIncreaseRate * Time.deltaTime),
                maxCollectibleFallSpeed
            );

            // Update UI with current score
            if (scoreText != null)
            {
                scoreText.text = $"{Mathf.RoundToInt(score)}";
            }
        }
        else
        {
            // In obstacle mode, score increases over time
            score += movementSpeed * Time.deltaTime;
            ChangeScore(Mathf.RoundToInt(score));

            // Check if time limit reached in timed mode
            if (gameTimer >= gameTimeLimit)
            {
                EndGame();
                return;
            }

            // Update UI with remaining time
            UpdateTimeDisplay(gameTimeLimit - gameTimer);
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

                // Add difficulty level logging when in obstacle mode
                if (!isCollectionMode)
                {
                    LoggerCommunicationProvider.Instance.AddToCustomData("newgame_difficulty",
                        $"\"{currentDifficultyLevel}\"");
                }

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

    [ClientRpc]
    public void StartGame()
    {
        Debug.Log("StartGame invoked");

        OnGameStart.Invoke();

        if (!isServer || gameRunning) return;

        // Set hasPlayedAnyGame to true as soon as any game starts
        hasPlayedAnyGame = true;

        // Reset game state
        gameRunning = false; // We'll set this to true after calibration/tutorial
        gameEnding = false;
        awaitingCalibration = true; // Set to awaiting calibration
        //tutorialComplete = false;
        spawningStarted = false;    // Reset spawning state
        score = 0;
        gameTimer = 0f;
        ChangeScore(0);

        // Reset collectible fall speed to initial value
        currentCollectibleFallSpeed = initialCollectibleFallSpeed;

        // Set time text visibility based on game mode
        if (timeText != null)
        {
            timeText.gameObject.SetActive(false); // Hide timer until spawning starts
        }

        if (calibrationSuccessText != null)
        {
            calibrationSuccessText.gameObject.SetActive(false);
        }

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

        // IMPORTANT: Keep initialButtons visible at game start, hide inGameBackButton
        initialButtons.SetActive(true);
        inGameBackButton.SetActive(false);

        // Clear any existing objects
        foreach (var obj in activeObjects.ToArray())
        {
            if (obj != null)
                NetworkServer.Destroy(obj);
        }
        activeObjects.Clear();

        // Start audio tutorial for calibration
        if (audioTutorial != null)
        {
            audioTutorial.OnGameModeSelected(isCollectionMode);
        }

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

        // No need for initial delay - tutorial already provides delay

        // Keep track of recent obstacle positions to avoid spawning collectibles too close
        List<Vector3> recentObstaclePositions = new List<Vector3>();
        float timeSinceLastCollectible = 0f;
        float forceCollectibleAfterTime = 5f; // Force spawn a collectible if none spawned for 5 seconds

        // Keep spawning as long as game is running
        while (gameRunning)
        {
            // Spawn an obstacle
            Vector3 obstaclePosition = SpawnObject(obstaclePrefab, "Obstacle");

            // Track this obstacle's position (for collectible placement logic)
            recentObstaclePositions.Add(obstaclePosition);

            // Only keep track of the 5 most recent obstacles
            while (recentObstaclePositions.Count > 5)
            {
                recentObstaclePositions.RemoveAt(0);
            }

            // Chance to spawn a collectible in obstacle mode
            bool shouldSpawnCollectible = enableCollectiblesInObstacleMode &&
                                         (Random.value < collectibleSpawnChance || timeSinceLastCollectible >= forceCollectibleAfterTime);

            if (shouldSpawnCollectible)
            {
                // Wait a moment before spawning the collectible so it's not directly aligned with the obstacle
                yield return new WaitForSeconds(obstacleSpawnInterval * 0.3f);

                // Only spawn if the game is still running
                if (gameRunning)
                {
                    // Spawn a collectible in a safe position
                    SpawnCollectibleSafeFromObstacles(recentObstaclePositions);
                    timeSinceLastCollectible = 0f; // Reset timer
                }
            }
            else
            {
                timeSinceLastCollectible += obstacleSpawnInterval;
            }

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

        // No need for initial delay - tutorial already provides delay

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
    private Vector3 SpawnObject(GameObject prefab, string objectType)
    {
        // Additional check to ensure we don't spawn after game over
        if (!gameRunning)
        {
            Debug.Log("Attempted to spawn object after game ended - ignoring");
            return Vector3.zero;
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
                // Set fall speed before initializing
                collectible.SetFallSpeed(currentCollectibleFallSpeed);
                collectible.Initialize();
            }
        }

        // Spawn on network and track
        NetworkServer.Spawn(obj);
        activeObjects.Add(obj);

        // Return the spawn position for tracking
        return spawnPosition;
    }

    // New method to spawn collectibles away from obstacles
    private void SpawnCollectibleSafeFromObstacles(List<Vector3> obstaclePositions)
    {
        // Maximum attempts to find a safe position
        int maxAttempts = 5;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 collectiblePosition;

            // Get a potential spawn position
            if (useAdaptiveSpawning && adaptiveSpawner != null)
            {
                // Use adaptive spawner to encourage movement to challenge areas
                collectiblePosition = adaptiveSpawner.GetCollectibleSpawnPosition(objectSpawnY - collectibleSpawnHeightOffset);
            }
            else
            {
                // Use random position
                float safeMinX = minX + wallBuffer;
                float safeMaxX = maxX - wallBuffer;

                collectiblePosition = new Vector3(
                    Random.Range(safeMinX, safeMaxX),
                    objectSpawnY - collectibleSpawnHeightOffset,
                    12f
                );
            }

            // If obstacles list is empty, no need to check for safety
            if (obstaclePositions.Count == 0)
            {
                SpawnCollectibleAtPosition(collectiblePosition);
                return;
            }

            // Check if position is safe from obstacles
            bool isSafe = true;
            foreach (Vector3 obstaclePos in obstaclePositions)
            {
                // Only check X distance since they're at different heights
                float xDistance = Mathf.Abs(obstaclePos.x - collectiblePosition.x);

                if (xDistance < obstacleCollectibleMinDistance)
                {
                    isSafe = false;
                    break;
                }
            }

            // If position is safe, spawn collectible
            if (isSafe)
            {
                SpawnCollectibleAtPosition(collectiblePosition);
                return;
            }
        }

        // If we couldn't find a safe position after max attempts, spawn anyway in a random position
        // to ensure collectibles don't stop appearing
        float fallbackMinX = minX + wallBuffer;
        float fallbackMaxX = maxX - wallBuffer;

        Vector3 fallbackPosition = new Vector3(
            Random.Range(fallbackMinX, fallbackMaxX),
            objectSpawnY - collectibleSpawnHeightOffset,
            12f
        );

        SpawnCollectibleAtPosition(fallbackPosition);
        Debug.Log("Using fallback position for collectible after failed safe placement attempts");
    }

    // Helper method to avoid code duplication
    private void SpawnCollectibleAtPosition(Vector3 position)
    {
        GameObject obj = Instantiate(collectiblePrefab, position, Quaternion.identity);

        Collectible collectible = obj.GetComponent<Collectible>();
        if (collectible != null)
        {
            collectible.SetFallSpeed(currentCollectibleFallSpeed);
            collectible.Initialize();
        }

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
        if (isCollectionMode)
        {
            // In collection mode, points are added directly from the collectible value
            score += points;
        }
        else
        {
            // In obstacle mode, add a fixed bonus to the continuously increasing score
            score += collectibleBonusPoints;
        }

        // Update the score display
        ChangeScore(Mathf.RoundToInt(score));

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
        //Debug.Log("ReturnToMenu called");

        // Make sure game is ended
        if (gameRunning)
        {
            EndGame();
        }

        // Fix for stuck rays - reset the hand rays before returning to menu
        if (GestureDetector.Instance != null)
        {
            // Reset ray visualization on both hands
            if (GestureDetector.Instance.handL != null && GestureDetector.Instance.handL.Interactor != null)
            {
                GestureDetector.Instance.handL.Interactor.SetRayEndpoints(Vector3.zero, Vector3.zero);
                GestureDetector.Instance.handL.Interactor.searchesForCollision = true;
                GestureDetector.Instance.handL.Interactor.visualizesRaycast = true;
            }

            if (GestureDetector.Instance.handR != null && GestureDetector.Instance.handR.Interactor != null)
            {
                GestureDetector.Instance.handR.Interactor.SetRayEndpoints(Vector3.zero, Vector3.zero);
                GestureDetector.Instance.handR.Interactor.searchesForCollision = true;
                GestureDetector.Instance.handR.Interactor.visualizesRaycast = true;
            }
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

        // Set isFirstPlay to false since this is a restart
        isFirstPlay = false;
        hasPlayedAnyGame = true;

        // Reset adaptive spawner tracking
        if (adaptiveSpawner != null)
        {
            adaptiveSpawner.ResetTracking();
        }

        if (calibrationSuccessText != null)
        {
            calibrationSuccessText.gameObject.SetActive(false);
        }

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
        //tutorialComplete = false;   // Reset tutorial completion
        spawningStarted = false;    // Reset spawning started flag
        score = 0f;
        gameTimer = 0f;
        currentCollectibleFallSpeed = initialCollectibleFallSpeed; // Reset fall speed
        ChangeScore(0);

        // Hide timer
        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        // Reset hand control
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        }

        // Hide end game UI
        DisplayEndGameInfo(false);

        // IMPORTANT: Show initialButtons, hide inGameBackButton for restart
        initialButtons.SetActive(true);
        inGameBackButton.SetActive(false);

        // Start audio tutorial for calibration again
        if (audioTutorial != null)
        {
            audioTutorial.OnGameModeSelected(isCollectionMode);
        }

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