using System.Collections;
using UnityEngine;
using Mirror;

public class GameAudioTutorial : NetworkBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip calibrationInstructionClip; // Audio for hand calibration instructions
    [SerializeField] private AudioClip rulesObstaclesClip;        // Audio for obstacle mode rules
    [SerializeField] private AudioClip rulesCollectClip;          // Audio for collection mode rules

    [Header("Timing Settings")]
    [SerializeField] private float calibrationReminderDelay = 15f; // Time between calibration reminders
    [SerializeField] private float minCalibrationDelay = 3f;       // Minimum time before allowing calibration
    [SerializeField] private float rulesStartDelay = 8f;          // Time after rules start before spawning objects

    // Private Components
    private AudioSource audioSource;
    private NewGameManager gameManager;

    // State Management
    private Coroutine reminderCoroutine;
    private Coroutine gameStartCoroutine;
    private bool calibrationComplete = false;
    private bool canCalibrate = false;

    private void Awake()
    {
        SetupAudioSource();
    }

    private void Start()
    {
        InitializeGameManager();
    }

    private void SetupAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void InitializeGameManager()
    {
        gameManager = NewGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameAudioTutorial: Could not find NewGameManager instance");
        }
    }

    public void OnGameModeSelected(bool isCollectionMode)
    {
        // Get reference to NewGameManager to check if first play
        bool isReturnPlayer = gameManager != null && !gameManager.IsFirstPlay();

        ResetGameState();
        StartGameSequence(isCollectionMode, isReturnPlayer);
    }

    private void ResetGameState()
    {
        calibrationComplete = false;
        canCalibrate = false;
        StopAllCoroutines();
    }

    private void StartGameSequence(bool isCollectionMode, bool isReturnPlayer)
    {
        PlayCalibrationInstructions();

        if (isReturnPlayer)
        {
            // Enable calibration immediately for return players
            canCalibrate = true;
            Debug.Log("Return player - calibration enabled immediately");
        }
        else
        {
            // First-time players wait for the delay
            StartCoroutine(EnableCalibrationAfterDelay());
        }

        reminderCoroutine = StartCoroutine(CalibrationReminderRoutine());
    }

    public void OnCalibrationComplete(bool isCollectionMode)
    {
        calibrationComplete = true;
        StopReminderCoroutine();

        // Always play rules track
        PlayGameRules(isCollectionMode);

        // Check if this is a return player
        bool isReturnPlayer = gameManager != null && !gameManager.IsFirstPlay();

        if (isReturnPlayer)
        {
            // For return players, start the game immediately
            // but let the rules audio continue playing in the background
            Debug.Log("Return player - starting game immediately while rules play in background");

            // Start character movement immediately
            StartCharacterMovement();

            // Start spawning immediately too
            StartObjectSpawning();
        }
        else
        {
            // First-time players still wait for rules to finish
            StartCoroutine(WaitForRulesThenStartGame(isCollectionMode));
        }
    }

    // New method for first-time players to wait for rules
    private IEnumerator WaitForRulesThenStartGame(bool isCollectionMode)
    {
        Debug.Log("First-time player - waiting for rules to finish before starting game");

        // Start the character movement right away
        StartCharacterMovement();

        // Wait for the rules delay before spawning objects
        yield return new WaitForSeconds(rulesStartDelay);

        // Start spawning after the delay
        StartObjectSpawning();
    }

    private void StopReminderCoroutine()
    {
        if (reminderCoroutine != null)
        {
            StopCoroutine(reminderCoroutine);
            reminderCoroutine = null;
        }
    }

    public bool CanCalibrate() => canCalibrate;

    private IEnumerator EnableCalibrationAfterDelay()
    {
        yield return new WaitForSeconds(minCalibrationDelay);
        canCalibrate = true;
        Debug.Log("Calibration now enabled");
    }

    [ClientRpc]
    private void PlayCalibrationInstructions()
    {
        if (calibrationInstructionClip != null)
        {
            audioSource.clip = calibrationInstructionClip;
            audioSource.Play();
            Debug.Log("Playing calibration instructions");
        }
    }

    private IEnumerator CalibrationReminderRoutine()
    {
        while (!calibrationComplete)
        {
            yield return new WaitForSeconds(calibrationReminderDelay);

            if (!calibrationComplete && !audioSource.isPlaying)
            {
                PlayCalibrationInstructions();
            }
        }
    }

    private void StartCharacterMovement()
    {
        if (gameManager != null)
        {
            gameManager.StartCharacterMovementOnly();
        }
    }

    private void StartObjectSpawning()
    {
        if (gameManager != null)
        {
            gameManager.StartSpawningOnly();
        }
    }

    [ClientRpc]
    private void PlayGameRules(bool isCollectionMode)
    {
        AudioClip ruleClip = isCollectionMode ? rulesCollectClip : rulesObstaclesClip;

        if (ruleClip != null)
        {
            audioSource.clip = ruleClip;
            audioSource.Play();
            Debug.Log($"Playing rules for {(isCollectionMode ? "collection" : "obstacles")} mode");
        }
    }
}