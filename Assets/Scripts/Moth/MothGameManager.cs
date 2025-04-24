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
    [SerializeField] private int maxActiveMoths = 4; // Always keep exactly 4 moths
    [SerializeField] private Vector3 zoneCenter = new Vector3(0, 1.5f, 1f); // Center of moth flying zone
    [SerializeField] private Vector3 zoneSize = new Vector3(2f, 1f, 1f); // Size of moth flying zone

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

        // Maintain exactly 4 moths at all times
        MaintainExactlyFourMoths();

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
    }

    // Function to maintain exactly 4 moths
    private void MaintainExactlyFourMoths()
    {
        if (!isServer) return;

        // Clean up any null references
        for (int i = activeMoths.Count - 1; i >= 0; i--)
        {
            if (activeMoths[i] == null)
            {
                activeMoths.RemoveAt(i);
            }
        }

        // Count how many moths are currently active
        int currentMothCount = activeMoths.Count;

        // If we have too many moths, remove extras
        if (currentMothCount > 4)
        {
            for (int i = currentMothCount - 1; i >= 4; i--)
            {
                if (activeMoths[i] != null)
                {
                    NetworkServer.Destroy(activeMoths[i]);
                    activeMoths.RemoveAt(i);
                }
            }
        }

        // If we need more moths, add them
        if (currentMothCount < 4)
        {
            for (int i = 0; i < 4 - currentMothCount; i++)
            {
                SpawnMoth();
            }
        }
    }

    private void OnDestroy()
    {
        if (isServer)
        {
            // Clean up all active moths
            foreach (var moth in activeMoths)
            {
                if (moth != null)
                    NetworkServer.Destroy(moth);
            }
            activeMoths.Clear();
        }
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

    [ClientRpc]
    public void StartGame()
    {
        OnGameStart.Invoke();

        if (!isServer || gameRunning) return;

        // Reset game state
        score = 0;
        gameTimer = 0f;
        totalMothsCaught = 0;
        totalMothsSpawned = 0;
        gameRunning = true;
        ClearActiveMoths();
        ChangeScore(0);

        // Show timer if in timed mode
        if (timedMode && timeText != null)
        {
            timeText.gameObject.SetActive(true);
        }

        // Spawn initial moths
        for (int i = 0; i < maxActiveMoths; i++)
        {
            SpawnMoth();
        }

        // Play some audio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("RobotReboot");
        }

        // Start logging
        try
        {
            if (LoggerCommunicationProvider.Instance != null)
            {
                LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_mode", timedMode ? "\"Timed\"" : "\"Untimed\"");
                LoggerCommunicationProvider.Instance.AddToCustomData("moth_game_difficulty", "\"" + difficulty.ToString("F1") + "\"");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("MothGameManager: Error starting logging: " + e.Message);
        }
    }

    private void SpawnMoth()
    {
        if (mothPrefab == null)
        {
            Debug.LogError("MothGameManager: mothPrefab is not assigned!");
            return;
        }

        if (debugMode) Debug.Log("MothGameManager: Spawning moth");

        try
        {
            // Get a random position in the spawn zone
            Vector3 halfSize = zoneSize * 0.5f;
            Vector3 spawnPosition = new Vector3(
                UnityEngine.Random.Range(zoneCenter.x - halfSize.x, zoneCenter.x + halfSize.x),
                UnityEngine.Random.Range(zoneCenter.y - halfSize.y, zoneCenter.y + halfSize.y),
                UnityEngine.Random.Range(zoneCenter.z - halfSize.z, zoneCenter.z + halfSize.z)
            );

            // Try to avoid overlapping with other moths
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool validPosition = true;

                // Check distance from other moths
                foreach (var otherMoth in activeMoths)
                {
                    if (otherMoth == null) continue;

                    float distance = Vector3.Distance(spawnPosition, otherMoth.transform.position);
                    if (distance < 0.5f) // Minimum separation
                    {
                        validPosition = false;
                        break;
                    }
                }

                if (validPosition)
                {
                    break;
                }

                // Try another position
                spawnPosition = new Vector3(
                    UnityEngine.Random.Range(zoneCenter.x - halfSize.x, zoneCenter.x + halfSize.x),
                    UnityEngine.Random.Range(zoneCenter.y - halfSize.y, zoneCenter.y + halfSize.y),
                    UnityEngine.Random.Range(zoneCenter.z - halfSize.z, zoneCenter.z + halfSize.z)
                );
            }

            // Create moth
            GameObject moth = Instantiate(mothPrefab, spawnPosition, Quaternion.identity);

            // Set the moth's zone properties to match game manager's zone
            Moth mothComponent = moth.GetComponent<Moth>();
            if (mothComponent != null)
            {
                mothComponent.Initialize(difficulty);

                // Set the moth's zone properties
                mothComponent.zoneCenter = zoneCenter;
                mothComponent.zoneSize = zoneSize;

                // Connect the caught event
                mothComponent.OnMothCaught.AddListener(MothCaught);

                if (debugMode) Debug.Log("MothGameManager: Moth component initialized");
            }
            else
            {
                Debug.LogError("MothGameManager: Moth component not found on prefab!");
            }

            // Spawn on network
            NetworkServer.Spawn(moth);
            activeMoths.Add(moth);
            totalMothsSpawned++;

            if (debugMode) Debug.Log($"MothGameManager: Spawned moth at position {spawnPosition}");
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

        // Remove from moth tracking list
        GameObject mothObj = moth.gameObject;
        if (activeMoths.Contains(mothObj))
        {
            activeMoths.Remove(mothObj);
        }

        // Immediately destroy the caught moth
        if (moth != null)
        {
            NetworkServer.Destroy(moth.gameObject);
        }

        // No need to spawn a replacement - MaintainExactlyFourMoths() will handle this
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
        ClearActiveMoths();

        // Hide end game UI
        DisplayEndGameInfo(false, 0, 0);

        // Start fresh game
        StartGame();
    }

    // Clean up all active moths
    private void ClearActiveMoths()
    {
        foreach (var moth in activeMoths)
        {
            if (moth != null)
                NetworkServer.Destroy(moth);
        }
        activeMoths.Clear();
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

    // Check if game is currently running
    public bool IsGameRunning()
    {
        return gameRunning;
    }
}