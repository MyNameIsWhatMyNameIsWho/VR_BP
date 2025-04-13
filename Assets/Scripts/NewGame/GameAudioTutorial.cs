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
        ResetGameState();
        StartGameSequence(isCollectionMode);
    }

    private void ResetGameState()
    {
        calibrationComplete = false;
        canCalibrate = false;
        StopAllCoroutines();
    }

    private void StartGameSequence(bool isCollectionMode)
    {
        PlayCalibrationInstructions();
        StartCoroutine(EnableCalibrationAfterDelay());
        reminderCoroutine = StartCoroutine(CalibrationReminderRoutine());
    }

    public void OnCalibrationComplete(bool isCollectionMode)
    {
        calibrationComplete = true;
        StopReminderCoroutine();
        StartCoroutine(PlayRulesThenStartGame(isCollectionMode));
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

    private IEnumerator PlayRulesThenStartGame(bool isCollectionMode)
    {
        yield return new WaitForSeconds(0.5f);
        PlayGameRules(isCollectionMode);
        StartCharacterMovement();
        yield return new WaitForSeconds(rulesStartDelay);
        StartObjectSpawning();
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