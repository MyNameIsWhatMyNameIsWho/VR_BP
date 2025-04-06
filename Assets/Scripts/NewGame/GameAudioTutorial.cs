using System.Collections;
using UnityEngine;
using Mirror;

public class GameAudioTutorial : NetworkBehaviour
{
    [Header("Tutorial Audio Clips")]
    [SerializeField] private AudioClip calibrationInstructionClip;
    [SerializeField] private AudioClip rulesObstaclesClip;
    [SerializeField] private AudioClip rulesCollectClip;

    [Header("Settings")]
    [SerializeField] private float calibrationReminderDelay = 15f; // Seconds before reminding about calibration
    [SerializeField] private float delayAfterRules = 2f; // Seconds to wait after rules before spawning

    private AudioSource audioSource;
    private Coroutine reminderCoroutine;
    private Coroutine gameStartCoroutine;
    private bool calibrationComplete = false;

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

        // Stop any previous coroutines
        if (reminderCoroutine != null)
            StopCoroutine(reminderCoroutine);
        if (gameStartCoroutine != null)
            StopCoroutine(gameStartCoroutine);

        // Play calibration instructions
        PlayCalibrationInstructions();

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

        // Wait for audio to finish plus a small delay
        float clipLength = audioSource.clip ? audioSource.clip.length : 0;
        yield return new WaitForSeconds(clipLength + delayAfterRules);

        // Tell game manager to start spawning objects
        if (gameManager != null)
        {
            //gameManager.StartSpawningAfterTutorial();
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