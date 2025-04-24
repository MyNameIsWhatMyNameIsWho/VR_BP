using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class MothGameManager : NetworkBehaviour
{
    public static MothGameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f; // Game length in seconds
    [SerializeField] private float respawnDelay = 1.0f; // Delay before spawning a new moth
    [SerializeField] private int scorePerMoth = 1; // Points earned per catch
    [SerializeField] private bool timedMode = true; // If false, continues until player quits

    [Header("Spawn Settings")]
    [SerializeField] private GameObject mothPrefab;
    [SerializeField] private int maxActiveMoths = 4; // Always keep exactly 4 moths
    [SerializeField] private Vector3 zoneCenter = new Vector3(0, 1.5f, 1f); // Center of moth flying zone
    [SerializeField] private Vector3 zoneSize = new Vector3(2f, 1f, 1f); // Size of moth flying zone

    [Header("UI Elements")]
    public GameObject initialButtons;
    public GameObject inGameButton;
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
    private bool isWaitingForRespawn = false;

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
    }

    private void Start()
    {
        menuManager = FindFirstObjectByType<GameMenuManager>();

        OnGameEnd.AddListener(() => {
            initialButtons.SetActive(true);
            inGameButton.SetActive(false);
        });

        OnGameStart.AddListener(() => {
            initialButtons.SetActive(false);
            inGameButton.SetActive(true);
        });

        if (endgameInfo != null)
        {
            endgameInfo.SetActive(false);
        }

        if (!isServer) return;

        // Hide timer if not in timed mode
        if (!timedMode && timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        // Initial score display
        ChangeScore(0);

        // Start with instructions
        StartCoroutine(PlayInstructions());
    }

    private IEnumerator PlayInstructions()
    {
        yield return null;
        yield return new WaitForSeconds(3.0f);
    }

    private void Update()
    {
        if (!isServer) return;
        if (!gameRunning) return;

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

        // If we need more moths and we're not waiting for respawn delay
        if (currentMothCount < 4 && !isWaitingForRespawn)
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
        if (!gameRunning) return;

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
        // Hide time text when game ends
        if (timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        // Clean up active moths
        foreach (var moth in activeMoths)
        {
            if (moth != null)
            {
                NetworkServer.Destroy(moth);
            }
        }
        activeMoths.Clear();

        // Wait a moment
        yield return new WaitForSeconds(respawnDelay);

        // Update highscore if needed
        if (Highscore < score)
        {
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
            Debug.LogError("Error logging end game data: " + e.Message);
        }
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display, float highscore, float finalScore)
    {
        if (endgameInfo == null) return;

        endgameInfo.SetActive(display);

        // Always hide time text when showing end game info
        if (display && timeText != null)
        {
            timeText.gameObject.SetActive(false);
        }

        if (!display) return;

        if (finalScoreText != null)
        {
            finalScoreText.text = Mathf.RoundToInt(finalScore).ToString();
        }

        if (highScoreText != null)
        {
            highScoreText.text = Mathf.RoundToInt(highscore).ToString();
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
        isWaitingForRespawn = false;
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
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error starting logging: " + e.Message);
        }
    }

    private void SpawnMoth()
    {
        if (mothPrefab == null)
        {
            Debug.LogError("MothPrefab is not assigned!");
            return;
        }

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
                mothComponent.Initialize();

                // Set the moth's zone properties
                mothComponent.zoneCenter = zoneCenter;
                mothComponent.zoneSize = zoneSize;

                // Connect the caught event
                mothComponent.OnMothCaught.AddListener(MothCaught);
            }
            else
            {
                Debug.LogError("Moth component not found on prefab!");
            }

            // Spawn on network
            NetworkServer.Spawn(moth);
            activeMoths.Add(moth);
            totalMothsSpawned++;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error spawning moth: " + e.Message);
        }
    }

    public void MothCaught(Moth moth)
    {
        if (!gameRunning) return;

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
                AudioManager.Instance.PlaySFX("CoinPickup");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Error playing sound: " + e.Message);
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
            Debug.LogWarning("Error recording event: " + e.Message);
        }

        // Remove from moth tracking list
        GameObject mothObj = moth.gameObject;
        if (activeMoths.Contains(mothObj))
        {
            activeMoths.Remove(mothObj);
        }

        // Destroy the caught moth
        if (moth != null)
        {
            NetworkServer.Destroy(moth.gameObject);
        }

        // Use the respawn delay before allowing new moths to spawn
        StartCoroutine(DelayRespawn());
    }

    private IEnumerator DelayRespawn()
    {
        isWaitingForRespawn = true;
        yield return new WaitForSeconds(respawnDelay);
        isWaitingForRespawn = false;
        // MaintainExactlyFourMoths() will handle the spawning in the next Update
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
                Debug.LogWarning("Error playing success effect: " + e.Message);
            }
        }
    }

    [ClientRpc]
    private void ChangeScore(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = newScore.ToString();
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
    }

    public void ReturnToMenu()
    {
        EndGame();

        if (menuManager != null)
        {
            menuManager.ReturnToGameMenu();
        }
    }

    public void RestartGame()
    {
        if (!isServer) return;

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

    // Check if game is currently running
    public bool IsGameRunning()
    {
        return gameRunning;
    }
}