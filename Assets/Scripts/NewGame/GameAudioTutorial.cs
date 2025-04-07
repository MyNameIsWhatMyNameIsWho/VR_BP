using System.Collections;
using UnityEngine;
using Mirror;

public class GameAudioTutorial : NetworkBehaviour
{
    [Header("Tutorial Audio Clips")]
    [SerializeField] private AudioClip calibrationInstructionClip; // "Calibrate your hand" clip
    [SerializeField] private AudioClip rulesObstaclesClip;         // Rules for obstacle mode 
    [SerializeField] private AudioClip rulesCollectClip;           // Rules for collection mode

    [Header("Settings")]
    [SerializeField] private float calibrationReminderDelay = 15f; // Seconds before reminding about calibration
    [SerializeField] private float delayAfterRules = 2f;           // Seconds to wait after rules before spawning
    [SerializeField] private float minCalibrationDelay = 3f;       // Minimum time before allowing calibration

    private AudioSource audioSource;
    private Coroutine reminderCoroutine;
    private Coroutine gameStartCoroutine;
    private bool calibrationComplete = false;
    private bool canCalibrate = false;  // Flag to delay calibration until after instructions

    private NewGameManager gameManager;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void Start()
    {
        gameManager = NewGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameAudioTutorial: Could not find NewGameManager instance");
            return;
        }
    }

    // Call this when game mode is selected
    public void OnGameModeSelected(bool isCollectionMode)
    {
        calibrationComplete = false;
        canCalibrate = false;

        // Stop any previous coroutines
        if (reminderCoroutine != null)
            StopCoroutine(reminderCoroutine);
        if (gameStartCoroutine != null)
            StopCoroutine(gameStartCoroutine);

        // Play calibration instructions
        PlayCalibrationInstructions();

        // Start a delay before allowing calibration
        StartCoroutine(EnableCalibrationAfterDelay());

        // Start reminder coroutine
        reminderCoroutine = StartCoroutine(CalibrationReminderRoutine());
    }

    // Call this when calibration is complete
    public void OnCalibrationComplete(bool isCollectionMode)
    {
        calibrationComplete = true;

        // Stop reminder coroutine
        if (reminderCoroutine != null)
        {
            StopCoroutine(reminderCoroutine);
            reminderCoroutine = null;
        }

        // Play appropriate rules after a short delay
        StartCoroutine(PlayRulesThenStartGame(isCollectionMode));
    }

    // Check if enough time has passed to allow calibration
    public bool CanCalibrate()
    {
        return canCalibrate;
    }

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
            // Wait for specified time
            yield return new WaitForSeconds(calibrationReminderDelay);

            // If calibration still not done and not currently playing audio
            if (!calibrationComplete && !audioSource.isPlaying)
            {
                PlayCalibrationInstructions();
            }
        }
    }

    private IEnumerator PlayRulesThenStartGame(bool isCollectionMode)
    {
        // Wait a moment before playing rules
        yield return new WaitForSeconds(0.5f);

        // Play appropriate rules
        PlayGameRules(isCollectionMode);

        // Enable character movement immediately after rules start playing
        if (gameManager != null)
        {
            // Start character movement but don't spawn obstacles yet
            gameManager.StartCharacterMovementOnly();
        }

        // Wait for audio to finish plus a small delay
        float clipLength = audioSource.clip ? audioSource.clip.length : 0;
        yield return new WaitForSeconds(clipLength + delayAfterRules);

        // Tell game manager to start spawning objects
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