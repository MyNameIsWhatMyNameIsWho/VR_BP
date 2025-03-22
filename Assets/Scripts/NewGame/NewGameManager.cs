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

    [Header("Obstacle Settings")]
    [SerializeField] private GameObject obstaclePrefab; // Assign the Obstacle prefab here
    [SerializeField] private float obstacleSpawnInterval = 2f; // Time between spawns
    [SerializeField] private float obstacleSpawnRangeX = 5f; // Horizontal range for spawning
    [SerializeField] private float obstacleSpawnY = 10f; // Y position to spawn obstacles
    [SerializeField] private List<GameObject> obstaclesInstances; // Keep track of spawned obstacles

    [Header("UI Elements")]
    [SerializeField] private GameObject initialButtons; // Contains Back, Repeat, Switch Hands
    [SerializeField] private GameObject inGameBackButton; // Single Back button during game
    [SerializeField] private GameObject endgameInfo; // Endgame UI panel

    [Header("Game Settings")]
    [SerializeField] private float movementSpeed = 2.00f;

    [Header("References")]
    [SerializeField] private Character_NewGame character;
    [SerializeField] private TextMeshPro scoreText; // In-game score display

    [Header("Endgame UI Elements")]
    [SerializeField] private TextMeshPro endgameScoreText; // Separate Score Text
    [SerializeField] private TextMeshPro endgameHighScoreText; // Separate High Score Text

    [SerializeField] private float waitAfterDeath = 2f;

    private GameMenuManager menuManager;

    private bool gameRunning = false;

    public float score = 0f;
    public bool hasObstacles = false; // No obstacles for now
    public UnityEvent OnGameStart;
    public UnityEvent OnGameEnd;

    // ======= Highscore Logic =======
    private float Highscore
    {
        get
        {
            if (hasObstacles)
                return PlayerPrefs.GetFloat("HighscoreWithObstacles", 0f);
            else
                return PlayerPrefs.GetFloat("HighscoreWithoutObstacles", 0f);
        }
        set
        {
            if (hasObstacles)
                PlayerPrefs.SetFloat("HighscoreWithObstacles", value);
            else
                PlayerPrefs.SetFloat("HighscoreWithoutObstacles", value);
            PlayerPrefs.Save();
        }
    }

    private Coroutine obstacleSpawningCoroutine; // Reference to the obstacle spawning coroutine

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        menuManager = FindFirstObjectByType<GameMenuManager>();

        // Set up UI toggles for game start/end
        OnGameEnd.AddListener(() =>
        {
            Debug.Log("OnGameEnd invoked: Showing initial buttons, hiding in-game back button");
            initialButtons.SetActive(true);
            inGameBackButton.SetActive(false);
        });
        OnGameStart.AddListener(() =>
        {
            Debug.Log("OnGameStart invoked: Hiding initial buttons, showing in-game back button");
            initialButtons.SetActive(false);
            inGameBackButton.SetActive(true);
        });
        endgameInfo.SetActive(false);

        if (!isServer) return;

        obstaclesInstances = new List<GameObject>();

        // Make sure the character reference is valid
        if (character == null)
        {
            Debug.LogError("Character reference is missing in NewGameManager!");
            return;
        }

        // Set up gesture detection to start the game
        if (GestureDetector.Instance != null)
        {
            // Remove any existing listeners to avoid duplicates
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
                Debug.Log("Right hand disabled: switched controlling hand");
            }

            // Debug which hand is controlling
            Debug.Log("Controlling hand is " + (character.controllingHandIsLeft ? "LEFT" : "RIGHT"));
        }
        else
        {
            Debug.LogError("GestureDetector.Instance is null. Ensure it is correctly set up.");
        }

        // Start with default cube position
        character.Spawn();

        // Reset Highscore to 0 at the beginning of the game
        ResetHighscoreIfNotZero();

        // Play instructions if needed
        StartCoroutine(PlayInstructions());
    }

    private IEnumerator PlayInstructions()
    {
        string mode = hasObstacles ? "Obstacles" : "Screws";
        yield return null;
        // Uncomment below if you want to play audio instructions
        // yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsNewGame" + mode));
        // yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsNewGameEnd"));
    }

    /// <summary>
    /// Resets the high score to 0 if it's not already 0.
    /// </summary>
    private void ResetHighscoreIfNotZero()
    {
        if (Highscore != 0f)
        {
            Debug.Log($"Highscore before reset: {Highscore}. Resetting to 0.");
            Highscore = 0f;
        }
    }

    /// <summary>
    /// Handler invoked when a gesture is detected.
    /// Starts the game if not already running and endgame UI is not active.
    /// </summary>
    private void OnGestureDetectedHandler(GestureType gesture, bool isLeft)
    {
        // Check if this is the controlling hand
        bool isControllingHand = (character.controllingHandIsLeft == isLeft);

        // Start game if not already running and if it's the controlling hand
        if (!gameRunning && !endgameInfo.activeSelf && isControllingHand)
        {
            Debug.Log($"Gesture detected from controlling hand: {gesture}");
            StartGame();
        }
    }

    private void FixedUpdate()
    {
        if (!gameRunning) return;

        score += movementSpeed * Time.fixedDeltaTime;
        ChangeScore(Mathf.RoundToInt(score)); // Round the score
    }

    private void OnDestroy()
    {
        if (isServer)
        {
            foreach (var o in obstaclesInstances)
                NetworkServer.Destroy(o);
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
        Debug.Log("End game");
        StopAllCoroutines();
        StartCoroutine(EndGameCoroutine());
    }

    public IEnumerator EndGameCoroutine()
    {
        if (isServer && gameRunning)
        {
            character.GameOver();
            gameRunning = false;

            if (GestureDetector.Instance != null)
            {
                GestureDetector.Instance.handL.SetControllingHand(false);
                GestureDetector.Instance.handR.SetControllingHand(false);
            }

            Debug.Log("End game coroutine started");
            yield return new WaitForSeconds(waitAfterDeath);
            Debug.Log("End game coroutine after wait");

            if (Highscore < score)
                Highscore = score;

            Debug.Log($"Highscore updated to: {Highscore}");

            DisplayEndGameInfo(true);

            // Log game data
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.AddToCustomData("newgame_score", "\"" + score.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("newgame_highscore", "\"" + Highscore.ToString() + "\"");
                LoggerCommunicationProvider.Instance.StopLogging();
            }
        }
        OnGameEnd.Invoke();
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display)
    {
        Debug.Log("END GAME INFO = " + display.ToString());
        endgameInfo.SetActive(display);
        if (!display) return;

        if (endgameScoreText != null)
            endgameScoreText.text = Mathf.RoundToInt(score).ToString();

        if (endgameHighScoreText != null)
            endgameHighScoreText.text = Mathf.RoundToInt(Highscore).ToString();

        Debug.Log($"Displayed Score: {endgameScoreText.text}, Highscore: {endgameHighScoreText.text}");
    }

    [ClientRpc]
    public void StartGame()
    {
        Debug.Log("StartGame invoked");

        // Trigger UI changes
        OnGameStart.Invoke();

        if (!isServer || gameRunning) return;

        // Ensure character reference is valid
        if (character == null)
        {
            Debug.LogError("Character reference is missing in StartGame!");
            return;
        }

        // Double-check GestureDetector is available
        if (GestureDetector.Instance == null)
        {
            Debug.LogError("GestureDetector.Instance is null in StartGame!");
            return;
        }

        // Set game state to running
        gameRunning = true;
        score = 0;
        ChangeScore(0);

        // Start obstacle spawning if enabled
        if (hasObstacles && obstaclePrefab != null)
        {
            if (obstacleSpawningCoroutine != null)
                StopCoroutine(obstacleSpawningCoroutine);
            obstacleSpawningCoroutine = StartCoroutine(SpawnObstacles());
        }

        // Initialize movement on the character
        Debug.Log("Telling character to start movement");
        character.StartMovement();

        // Log the start of a new game
        if (LoggerCommunicationProvider.Instance != null)
        {
            LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
            LoggerCommunicationProvider.Instance.AddToCustomData("newgame_mode", hasObstacles ? "\"With obstacles\"" : "\"Without obstacles\"");
        }
    }

    private IEnumerator SpawnObstacles()
    {
        while (gameRunning)
        {
            SpawnObstacle();
            yield return new WaitForSeconds(obstacleSpawnInterval);
        }
    }

    [Server]
    private void SpawnObstacle()
    {
        Vector3 spawnPosition = new Vector3(
            Random.Range(-obstacleSpawnRangeX, obstacleSpawnRangeX),
            obstacleSpawnY,
            12f
        );

        GameObject obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
        NetworkServer.Spawn(obstacle);
        obstaclesInstances.Add(obstacle);

        Debug.Log($"Spawned obstacle at {spawnPosition}");
    }

    public void ReturnToEndlessRunnerMenu()
    {
        EndGame();
        menuManager.ReturnToGameMenu();
    }

    public void RestartGame()
    {
        if (!isServer) return;

        foreach (var o in obstaclesInstances)
            NetworkServer.Destroy(o);
        obstaclesInstances.Clear();

        character.Spawn();
        gameRunning = false;

        score = 0f;
        ChangeScore(Mathf.RoundToInt(score));

        ResetHighscoreIfNotZero();

        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        }

        DisplayEndGameInfo(false);

        StopAllCoroutines();
        StartGame();
    }

    [ClientRpc]
    private void ChangeScore(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = newScore.ToString();
            //Debug.Log($"In-game score updated to: {newScore}");
        }
        else
        {
            Debug.LogError("scoreText is not assigned in the Inspector.");
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