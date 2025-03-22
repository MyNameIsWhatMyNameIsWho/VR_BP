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
    [SerializeField] private List<GameObject> activeObjects; // Keep track of all spawned objects

    // SINGLE PLACE to define speeds - only place they're defined in the codebase
    [Header("Speed Settings")]
    [SerializeField] private float obstacleFallSpeed = 0.8f;
    [SerializeField] private float collectibleFallSpeed = 1.2f;
    // Public getters so other scripts can access but not modify these values
    public float ObstacleFallSpeed => obstacleFallSpeed;
    public float CollectibleFallSpeed => collectibleFallSpeed;

    [Header("Spawn Settings")]
    [SerializeField] private float obstacleSpawnInterval = 4f;
    [SerializeField] private float collectibleSpawnInterval = 2f;
    [SerializeField] private float objectSpawnRangeX = 5f;
    [SerializeField] private float objectSpawnY = 10f;

    [Header("UI Elements")]
    [SerializeField] private GameObject initialButtons;
    [SerializeField] private GameObject inGameBackButton;
    [SerializeField] private GameObject endgameInfo;

    [Header("Game Settings")]
    [SerializeField] private float movementSpeed = 2.00f;
    [SerializeField] private bool isCollectionMode = false;
    [SerializeField] private float gameTimeLimit = 60f;
    [SerializeField] private float waitAfterDeath = 2f;

    [Header("References")]
    [SerializeField] private Character_NewGame character;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private TextMeshPro endgameScoreText;
    [SerializeField] private TextMeshPro endgameHighScoreText;

    private GameMenuManager menuManager;
    private bool gameRunning = false;
    private float gameTimer = 0f;
    private Coroutine spawnCoroutine;

    public float score = 0f;
    public UnityEvent OnGameStart;
    public UnityEvent OnGameEnd;

    // Highscore properties for different game modes
    private float Highscore
    {
        get
        {
            if (isCollectionMode)
                return PlayerPrefs.GetFloat("HighscoreCollection", 0f);
            else
                return PlayerPrefs.GetFloat("HighscoreObstacles", 0f);
        }
        set
        {
            if (isCollectionMode)
                PlayerPrefs.SetFloat("HighscoreCollection", value);
            else
                PlayerPrefs.SetFloat("HighscoreObstacles", value);
            PlayerPrefs.Save();
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
        });
        endgameInfo.SetActive(false);

        if (!isServer) return;

        // Set up gesture detection to start the game
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.OnGestureBegin.RemoveListener(OnGestureDetectedHandler);
            GestureDetector.Instance.OnGestureBegin.AddListener(OnGestureDetectedHandler);

            // Set up controlling hand
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);

            // If right hand is disabled, switch to left
            if (GestureDetector.Instance.handR != null && GestureDetector.Instance.handR.IsDisabled)
            {
                character.controllingHandIsLeft = !character.controllingHandIsLeft;
                GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
                GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
            }
        }
        else
        {
            Debug.LogError("GestureDetector.Instance is null");
        }

        // Position character
        character.Spawn();

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
        // Start game on any gesture if not already running
        if (!gameRunning && !endgameInfo.activeSelf)
        {
            Debug.Log($"Starting game with gesture: {gesture}");
            StartGame();
        }
    }

    private void FixedUpdate()
    {
        if (!gameRunning) return;

        if (isCollectionMode)
        {
            // Update timer for collection mode
            gameTimer += Time.fixedDeltaTime;

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
            score += movementSpeed * Time.fixedDeltaTime;
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
        Debug.Log("End game called - stopping all coroutines");

        // It's important to stop ALL coroutines to ensure spawning stops
        StopAllCoroutines();

        // Stop any specific spawn coroutine
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // Immediately disable movement in character
        if (character != null)
        {
            character.GameOver();
        }

        // Immediately set game state to not running
        gameRunning = false;

        StartCoroutine(EndGameCoroutine());
    }

    public IEnumerator EndGameCoroutine()
    {
        if (isServer && gameRunning)
        {
            Debug.Log("EndGameCoroutine started");

            // Set game state to not running immediately
            gameRunning = false;

            // Stop player movement
            character.GameOver();

            // Set the character to not be controlled by hands
            if (GestureDetector.Instance != null)
            {
                GestureDetector.Instance.handL.SetControllingHand(false);
                GestureDetector.Instance.handR.SetControllingHand(false);
            }

            // Destroy all active objects to immediately stop them
            foreach (var obj in activeObjects)
            {
                if (obj != null)
                    NetworkServer.Destroy(obj);
            }
            activeObjects.Clear();

            yield return new WaitForSeconds(waitAfterDeath);

            // Update highscore if needed
            if (Highscore < score)
            {
                Debug.Log($"New highscore: {score}");
                Highscore = score;
            }

            // Make sure score is updated before showing end screen
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
        endgameInfo.SetActive(display);

        if (!display) return;

        // Set the score text - make sure it's not null first
        if (endgameScoreText != null)
        {
            endgameScoreText.text = Mathf.RoundToInt(score).ToString();
            Debug.Log("Set endgame score text to: " + endgameScoreText.text);
        }
        else
        {
            Debug.LogError("endgameScoreText is null!");
        }

        // Set the highscore text - make sure it's not null first
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

        // Reset game state
        gameRunning = true;
        score = 0;
        gameTimer = 0f;
        ChangeScore(0);

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

        // Calculate random position
        Vector3 spawnPosition = new Vector3(
            Random.Range(-objectSpawnRangeX, objectSpawnRangeX),
            objectSpawnY,
            12f
        );

        // Instantiate the object
        GameObject obj = Instantiate(prefab, spawnPosition, Quaternion.identity);

        // The prefabs should not have their own speeds set - they receive it directly from here
        // We will check object type and set appropriate speed
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

        // Play sound effect if available
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("CoinPickup");
        }
    }

    [ClientRpc]
    private void UpdateTimeDisplay(float remainingTime)
    {
        if (scoreText != null)
        {
            // Format remaining time as MM:SS
            int minutes = Mathf.FloorToInt(remainingTime / 60);
            int seconds = Mathf.FloorToInt(remainingTime % 60);
            string timeDisplay = $"{minutes:00}:{seconds:00}";

            // Show both score and time
            scoreText.text = $"Score: {Mathf.RoundToInt(score)} - Time: {timeDisplay}";
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

        // Clean up active objects
        foreach (var obj in activeObjects)
        {
            if (obj != null)
                NetworkServer.Destroy(obj);
        }
        activeObjects.Clear();

        // Reset character and game state
        character.Spawn();
        gameRunning = false;
        score = 0f;
        ChangeScore(0);

        // Reset hand control
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        }

        // Hide end game UI
        DisplayEndGameInfo(false);

        // Restart game
        StopAllCoroutines();
        StartGame();
    }

    [ClientRpc]
    private void ChangeScore(int newScore)
    {
        if (scoreText != null && !isCollectionMode)
        {
            scoreText.text = newScore.ToString();
            Debug.Log("Changed in-game score to: " + newScore);
        }
        else if (scoreText != null && isCollectionMode)
        {
            // For collection mode, score is part of time display which is handled separately
            Debug.Log("Updated collection score (will be shown with time): " + newScore);
        }
    }

    public void SwitchHands()
    {
        if (GestureDetector.Instance.handL.IsDisabled || GestureDetector.Instance.handR.IsDisabled)
            return;

        character.controllingHandIsLeft = !character.controllingHandIsLeft;
        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
    }
}