using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the Moth Game - a game where players reach to catch flying moths.
/// Designed for arm stretching rehabilitation.
/// </summary>
public class MothGameManager : NetworkBehaviour
{
    public static MothGameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f; // Game length in seconds
    [SerializeField] private float respawnDelay = 1.0f; // Delay before spawning a new moth
    [SerializeField] private int scorePerMoth = 1; // Points earned per catch
    [SerializeField] private float difficulty = 1.0f; // Speed multiplier (1.0 = normal)
    [SerializeField] private bool timedMode = true; // If false, continues until player quits
    [SerializeField] private bool debugMode = true; // Enable detailed logging

    [Header("Spawn Settings")]
    [SerializeField] private GameObject mothPrefab;
    [SerializeField] private int maxActiveMoths = 1; // Usually just 1 at a time
    [SerializeField] private Vector3 initialSpawnPosition = new Vector3(0, 1.5f, 1f); // Fallback spawn position

    [Header("UI Elements")]
    [SerializeField] private GameObject initialButtons;
    [SerializeField] private GameObject inGameButton;
    [SerializeField] private GameObject endgameInfo;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private TextMeshPro timeText;
    [SerializeField] private TextMeshPro finalScoreText;
    [SerializeField] private TextMeshPro highScoreText;
    [SerializeField] private NetworkVisualEffect successEffect;

    // Game state
    private GameMenuManager menuManager;
    private bool gameRunning = false;
    private float gameTimer = 0f;
    private int score = 0;
    private int totalMothsCaught = 0;
    private int totalMothsSpawned = 0;
    private List<GameObject> activeMoths = new List<GameObject>();
    private float nextSpawnTime = 0f;

    // Events
    public UnityEvent OnGameStart;
    public UnityEvent OnGameEnd;

    private float Highscore
    {
        get
        {
            try
            {
                return UserSystem.Instance.UserData.HighscoreMothGame;
            }
            catch (Exception e)
            {
                Debug.LogError("Error accessing highscore: " + e.Message);
                return 0;
            }
        }
        set
        {
            try
            {
                UserSystem.Instance.UserData.HighscoreMothGame = value;
            }
            catch (Exception e)
            {
                Debug.LogError("Error setting highscore: " + e.Message);
            }
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

        if (debugMode) Debug.Log("MothGameManager: Awake");
    }

    private void Start()
    {
        if (debugMode) Debug.Log("MothGameManager: Start");

        menuManager = FindFirstObjectByType<GameMenuManager>();
        if (menuManager == null && debugMode)
            Debug.LogWarning("MothGameManager: MenuManager not found");

        OnGameEnd.AddListener(() => {
            if (debugMode) Debug.Log("MothGameManager: OnGameEnd event triggered");
            initialButtons.SetActive(true);
            inGameButton.SetActive(false);
        });

        OnGameStart.AddListener(() => {
            if (debugMode) Debug.Log("MothGameManager: OnGameStart event triggered");
            initialButtons.SetActive(false);
            inGameButton.SetActive(true);
        });

        if (endgameInfo != null)
        {
            endgameInfo.SetActive(false);
        }
        else if (debugMode)
        {
            Debug.LogWarning("MothGameManager: endgameInfo is null");
        }

        if (!isServer)
        {
            if (debugMode) Debug.Log("MothGameManager: Not server, returning from Start");
            return;
        }

        // Hide timer if not in timed mode
        if (!timedMode && timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        // Initial score display
        ChangeScore(0);

        // Start with instructions
        StartCoroutine(PlayInstructions());

        if (debugMode) Debug.Log("MothGameManager: Start completed");

        // Check if moth prefab is assigned
        if (mothPrefab == null)
        {
            Debug.LogError("MothGameManager: mothPrefab is not assigned!");
        }
    }

    private IEnumerator PlayInstructions()
    {
        if (debugMode) Debug.Log("MothGameManager: Playing instructions");
        yield return null;

        // Simply wait a few seconds before starting
        yield return new WaitForSeconds(3.0f);

        if (debugMode) Debug.Log("MothGameManager: Instructions completed");
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (!gameRunning)
        {
            return;
        }

        if (timedMode)
        {
            // Update timer
            gameTimer += Time.deltaTime;

            // Display remaining time
            UpdateTimeDisplay(gameDuration - gameTimer);

            // Check if time is up
            if (gameTimer >= gameDuration)
            {
                if (debugMode) Debug.Log("MothGameManager: Time's up, ending game");
                EndGame();
                return;
            }
        }

        // Ensure we have active moths
        if (activeMoths.Count < maxActiveMoths && Time.time > nextSpawnTime)
        {
            if (debugMode) Debug.Log("MothGameManager: Spawning new moth from Update");
            SpawnMoth();
            nextSpawnTime = Time.time + 0.5f; // Prevent multiple spawns in same frame
        }

        // Clean up any destroyed moths from the list
        for (int i = activeMoths.Count - 1; i >= 0; i--)
        {
            if (activeMoths[i] == null)
            {
                if (debugMode) Debug.Log("MothGameManager: Removing null moth from activeMoths list");
                activeMoths.RemoveAt(i);
            }
        }
    }

    public void StartGame()
    {
        if (!isServer)
        {
            if (debugMode) Debug.Log("MothGameManager: StartGame called but not server");
            return;
        }

        if (gameRunning)
        {
            if (debugMode) Debug.Log("MothGameManager: StartGame called but game already running");
            return;
        }

        if (debugMode) Debug.Log("MothGameManager: Starting game");

        // Reset game state
        score = 0;
        gameTimer = 0f;
        totalMothsCaught = 0;
        totalMothsSpawned = 0;
        gameRunning = true;
        ChangeScore(0);

        // Clean up any existing moths
        foreach (var moth in activeMoths)
        {
            if (moth != null)
            {
                if (debugMode) Debug.Log("MothGameManager: Destroying existing moth");
                NetworkServer.Destroy(moth);
            }
        }
        activeMoths.Clear();

        // Hide end game UI if visible
        DisplayEndGameInfo(false, 0, 0);

        // Start logging
        try
        {
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_mode", timedMode ? "\"Timed\"" : "\"Untimed\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_difficulty", "\"" + difficulty.ToString("F1") + "\"");
            }
            else if (debugMode)
            {
                Debug.LogWarning("MothGameManager: LoggerCommunicationProvider.Instance is null");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("MothGameManager: Error starting logging: " + e.Message);
        }

        OnGameStart.Invoke();

        // Spawn first moth with a slight delay to ensure everything is ready
        StartCoroutine(DelayedFirstSpawn());
    }

    private IEnumerator DelayedFirstSpawn()
    {
        yield return new WaitForSeconds(0.5f);
        if (debugMode) Debug.Log("MothGameManager: Spawning first moth after delay");
        SpawnMoth();
    }

    [ClientRpc]
    public void EndGame()
    {
        if (!gameRunning)
        {
            if (debugMode && isServer) Debug.Log("MothGameManager: EndGame called but game not running");
            return;
        }

        if (debugMode && isServer) Debug.Log("MothGameManager: Ending game");

        // Stop the game
        gameRunning = false;

        if (isServer)
        {
            StartCoroutine(EndGameCoroutine());
        }

        OnGameEnd.Invoke();
    }

    private IEnumerator EndGameCoroutine()
    {
        if (debugMode) Debug.Log("MothGameManager: Running EndGameCoroutine");

        // Clean up active moths
        foreach (var moth in activeMoths)
        {
            if (moth != null)
            {
                if (debugMode) Debug.Log("MothGameManager: Destroying moth during end game");
                NetworkServer.Destroy(moth);
            }
        }
        activeMoths.Clear();

        // Wait a moment
        yield return new WaitForSeconds(respawnDelay);

        // Update highscore if needed
        if (Highscore < score)
        {
            if (debugMode) Debug.Log($"MothGameManager: New highscore: {score} (old: {Highscore})");
            Highscore = score;
        }

        // Show end game UI
        DisplayEndGameInfo(true, Highscore, score);

        // Log game data
        try
        {
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_score", "\"" + score.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_highscore", "\"" + Highscore.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_moths_caught", "\"" + totalMothsCaught.ToString() + "\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_time", "\"" + gameTimer.ToString("F1") + "\"");
                LoggerCommunicationProvider.Instance.StopLogging();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("MothGameManager: Error logging end game data: " + e.Message);
        }
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display, float highscore, float finalScore)
    {
        if (endgameInfo == null)
        {
            if (debugMode && isServer) Debug.LogWarning("MothGameManager: endgameInfo is null in DisplayEndGameInfo");
            return;
        }

        endgameInfo.SetActive(display);

        if (!display) return;

        if (finalScoreText != null)
        {
            finalScoreText.text = Mathf.RoundToInt(finalScore).ToString();
        }
        else if (debugMode && isServer)
        {
            Debug.LogWarning("MothGameManager: finalScoreText is null");
        }

        if (highScoreText != null)
        {
            highScoreText.text = Mathf.RoundToInt(highscore).ToString();
        }
        else if (debugMode && isServer)
        {
            Debug.LogWarning("MothGameManager: highScoreText is null");
        }
    }

    private void SpawnMoth()
    {
        if (!gameRunning)
        {
            Debug.Log("MothGameManager: SpawnMoth called but game not running");
            return;
        }

        if (mothPrefab == null)
        {
            Debug.LogError("MothGameManager: Cannot spawn moth - mothPrefab is null!");
            return;
        }

        Debug.Log("MothGameManager: Spawning moth");

        try
        {
            // Use a fixed position in front of the player for reliable spawning
            Vector3 spawnPosition = new Vector3(0, 1.5f, 1f);

            // If camera is available, position relative to it
            if (Camera.main != null)
            {
                // Position moth 1.5 meters in front of the camera, at eye level
                spawnPosition = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
                spawnPosition.y = Camera.main.transform.position.y; // Keep at eye level
            }

            // Create moth
            GameObject moth = Instantiate(mothPrefab, spawnPosition, Quaternion.identity);

            // Configure moth behavior
            Moth mothComponent = moth.GetComponent<Moth>();
            if (mothComponent != null)
            {
                mothComponent.Initialize(difficulty);
                mothComponent.OnMothCaught.AddListener(MothCaught);
                Debug.Log("MothGameManager: Moth component initialized");
            }
            else
            {
                Debug.LogError("MothGameManager: Moth component not found on prefab!");
            }

            // Spawn on network
            NetworkServer.Spawn(moth);
            activeMoths.Add(moth);

            totalMothsSpawned++;

            Debug.Log("MothGameManager: Spawned moth #" + totalMothsSpawned + " at position " + spawnPosition);
        }
        catch (System.Exception e)
        {
            Debug.LogError("MothGameManager: Error spawning moth: " + e.Message + "\n" + e.StackTrace);
        }
    }

    public void MothCaught(Moth moth)
    {
        if (!gameRunning)
        {
            if (debugMode) Debug.Log("MothGameManager: MothCaught called but game not running");
            return;
        }

        if (debugMode) Debug.Log("MothGameManager: Moth caught!");

        // Add score
        score += scorePerMoth;
        ChangeScore(score);
        totalMothsCaught++;

        // Play success effects
        PlaySuccessEffects(moth.transform.position);
        try
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("CoinPickup"); // Use existing sound for now
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("MothGameManager: Error playing sound: " + e.Message);
        }

        // Record event
        try
        {
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.RecordEvent("MC");  // Moth Caught
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("MothGameManager: Error recording event: " + e.Message);
        }

        // Respawn after delay
        StartCoroutine(RespawnMothAfterDelay(moth));
    }

    private IEnumerator RespawnMothAfterDelay(Moth moth)
    {
        if (debugMode) Debug.Log("MothGameManager: Starting respawn delay");

        // Wait before respawning
        yield return new WaitForSeconds(respawnDelay);

        // Respawn if game still running
        if (gameRunning)
        {
            if (moth != null)
            {
                if (debugMode) Debug.Log("MothGameManager: Respawning moth");
                moth.Respawn();
            }
            else
            {
                if (debugMode) Debug.Log("MothGameManager: Moth was destroyed, spawning new one");
                SpawnMoth();
            }
        }
        else
        {
            if (debugMode) Debug.Log("MothGameManager: Game not running, skipping respawn");
        }
    }

    private void PlaySuccessEffects(Vector3 position)
    {
        if (successEffect != null)
        {
            try
            {
                NetworkVisualEffect effect = Instantiate(successEffect, position, Quaternion.identity);
                NetworkServer.Spawn(effect.gameObject);
                effect.Play();
            }
            catch (Exception e)
            {
                Debug.LogWarning("MothGameManager: Error playing success effect: " + e.Message);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("MothGameManager: successEffect is null");
        }
    }

    [ClientRpc]
    private void ChangeScore(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = newScore.ToString();
        }
        else if (debugMode && isServer)
        {
            Debug.LogWarning("MothGameManager: scoreText is null in ChangeScore");
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
        }
        else if (debugMode && isServer)
        {
            Debug.LogWarning("MothGameManager: timeText is null in UpdateTimeDisplay");
        }
    }

    public void ReturnToMenu()
    {
        if (debugMode && isServer) Debug.Log("MothGameManager: Returning to menu");
        EndGame();

        if (menuManager != null)
        {
            menuManager.ReturnToGameMenu();
        }
        else if (debugMode && isServer)
        {
            Debug.LogWarning("MothGameManager: menuManager is null in ReturnToMenu");
        }
    }

    public void RestartGame()
    {
        if (!isServer)
        {
            if (debugMode) Debug.Log("MothGameManager: RestartGame called but not server");
            return;
        }

        if (debugMode) Debug.Log("MothGameManager: Restarting game");

        // Clean up current moths
        foreach (var moth in activeMoths)
        {
            if (moth != null)
                NetworkServer.Destroy(moth);
        }
        activeMoths.Clear();

        // Hide end game UI
        DisplayEndGameInfo(false, 0, 0);

        // Start fresh game
        StartGame();
    }

    // Allows changing difficulty dynamically
    public void SetDifficulty(float difficultyLevel)
    {
        if (debugMode) Debug.Log($"MothGameManager: Setting difficulty to {difficultyLevel}");

        difficulty = difficultyLevel;

        // Update all active moths
        foreach (var moth in activeMoths)
        {
            if (moth != null)
            {
                Moth mothComponent = moth.GetComponent<Moth>();
                if (mothComponent != null)
                {
                    mothComponent.SetDifficulty(difficulty);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (isServer)
        {
            if (debugMode) Debug.Log("MothGameManager: OnDestroy");

            // Clean up active moths
            foreach (var moth in activeMoths)
            {
                if (moth != null)
                    NetworkServer.Destroy(moth);
            }

            // Stop logging if necessary
            try
            {
                if (LoggerCommunicationProvider.Instance != null && LoggerCommunicationProvider.Instance.loggingStarted)
                {
                    LoggerCommunicationProvider.Instance.StopLogging();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("MothGameManager: Error stopping logging: " + e.Message);
            }
        }
    }
}