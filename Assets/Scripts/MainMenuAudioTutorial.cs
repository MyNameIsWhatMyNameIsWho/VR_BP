using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the audio tutorial for new users in the main menu.
/// Teaches users how to use gestures for navigation and control.
/// </summary>
public class MainMenuAudioTutorial : NetworkBehaviour
{
    public static MainMenuAudioTutorial Instance;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip welcomeClip;                  // Initial welcome message
    [SerializeField] private AudioClip calibrationInstructionClip;   // Instructions for calibration
    [SerializeField] private AudioClip calibrationReminderClip;      // Reminder if calibration not done
    [SerializeField] private AudioClip calibrationSuccessClip;       // After successful calibration
    [SerializeField] private AudioClip rockGestureInstructionClip;   // Instructions for rock gesture
    [SerializeField] private AudioClip rockGestureReminderClip;      // Reminder if rock gesture not done
    [SerializeField] private AudioClip tutorialCompletedClip;        // Final message

    [Header("Tutorial Settings")]
    [SerializeField] private float reminderDelay = 15f;              // Time before playing reminder
    [SerializeField] private float initialDelay = 1.0f;              // Delay before starting tutorial

    // Add debug text to see the current timer value
    private float currentReminderTimer = 0f;

    // Player prefs key to track if tutorial has been completed
    private const string TUTORIAL_COMPLETED_KEY = "MainMenuTutorialCompleted";

    private AudioSource audioSource;
    private TutorialState currentState = TutorialState.NotStarted;
    private Coroutine activeCoroutine;
    private bool isWaitingForGesture = false;
    private bool calibrationCompleted = false;
    private bool rockGestureCompleted = false;

    // Tutorial states
    private enum TutorialState
    {
        NotStarted,
        Welcome,
        CalibrationInstructions,
        WaitingForCalibration,
        CalibrationSuccess,
        RockGestureInstructions,
        WaitingForRockGesture,
        TutorialCompleted
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Add audio source if needed
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        // Don't destroy when loading new scenes if playing the tutorial completed audio
        // This ensures the audio finishes playing even when transitioning to a new scene
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If we're playing the tutorial completed audio, make this object persist
        if (IsTutorialCompletedAudioPlaying())
        {
            DontDestroyOnLoad(gameObject);
            
            // Set a timer to destroy this object after audio is done
            StartCoroutine(DestroyAfterAudio());
        }
    }
    
    private IEnumerator DestroyAfterAudio()
    {
        // Wait until audio finishes playing
        yield return new WaitUntil(() => !audioSource.isPlaying);
        
        // Add a small delay to ensure it completes properly
        yield return new WaitForSeconds(0.5f);
        
        // Destroy the object
        Destroy(gameObject);
    }

    // Static flag to track if tutorial has played in this session
    private static bool hasPlayedInThisSession = false;

    private void Start()
    {
        if (!isServer) return;

        // Only play if it hasn't been played yet in this session
        if (!hasPlayedInThisSession)
        {
            // Start the tutorial with a slight delay
            activeCoroutine = StartCoroutine(StartTutorialSequence());
            hasPlayedInThisSession = true;
            Debug.Log("Starting tutorial for the first time in this session");
        }
        else
        {
            Debug.Log("Tutorial already played in this session, skipping");
        }

        // Listen to gesture events
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.OnGestureBegin.AddListener(OnGestureDetected);

            // Subscribe to UprightRedirector for calibration completion
            if (UprightRedirector.Instance != null)
            {
                UprightRedirector.Instance.OnCalibrationComplete.AddListener(() => {
                    if (currentState == TutorialState.WaitingForCalibration)
                    {
                        calibrationCompleted = true;
                    }
                });
            }
            else
            {
                Debug.LogError("UprightRedirector.Instance is null! Cannot detect calibration completion.");
            }
        }
        else
        {
            Debug.LogError("GestureDetector.Instance is null! Cannot listen for gestures.");
        }
    }

    private void OnDestroy()
    {
        if (GestureDetector.Instance != null)
        {
            GestureDetector.Instance.OnGestureBegin.RemoveListener(OnGestureDetected);
        }

        if (UprightRedirector.Instance != null && UprightRedirector.Instance.OnCalibrationComplete != null)
        {
            UprightRedirector.Instance.OnCalibrationComplete.RemoveAllListeners();
        }

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
        
        // Unsubscribe from scene loaded event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private IEnumerator StartTutorialSequence()
    {
        // Wait initial delay before starting
        yield return new WaitForSeconds(initialDelay);

        // Welcome message
        currentState = TutorialState.Welcome;
        PlayAudioClipServerRpc(0); // welcomeClip

        // Wait for welcome message to finish completely
        yield return new WaitUntil(() => audioSource.clip == welcomeClip);
        yield return new WaitUntil(() => !audioSource.isPlaying);

        yield return new WaitForSeconds(0.5f); // Brief pause between messages

        // Start calibration instructions
        StartCalibrationInstructions();
    }

    private void StartCalibrationInstructions()
    {
        if (!isServer) return;

        currentState = TutorialState.CalibrationInstructions;
        PlayAudioClipServerRpc(1); // calibrationInstructionClip

        // After instructions finish, wait for calibration
        activeCoroutine = StartCoroutine(WaitForCalibration());
    }

    private IEnumerator WaitForCalibration()
    {
        yield return new WaitUntil(() => !audioSource.isPlaying);

        currentState = TutorialState.WaitingForCalibration;
        isWaitingForGesture = true;

        // Reset the timer each time we start waiting
        currentReminderTimer = 0f;

        // Wait for calibration or remind if taking too long
        while (!calibrationCompleted)
        {
            // Increment timer with actual elapsed time
            currentReminderTimer += Time.deltaTime;

            // Debug to see the current timer value
            if (currentReminderTimer % 5 < 0.1f) // Log about every 5 seconds
            {
                Debug.Log($"Calibration reminder timer: {currentReminderTimer:F1}/{reminderDelay:F1}");
            }

            // Only play reminder if we're still waiting for calibration, no audio is playing,
            // and the full reminder delay has passed
            if (currentReminderTimer >= reminderDelay && !audioSource.isPlaying && currentState == TutorialState.WaitingForCalibration)
            {
                Debug.Log($"Playing calibration reminder after {currentReminderTimer:F1} seconds");
                PlayAudioClipServerRpc(2); // calibrationReminderClip
                currentReminderTimer = 0f; // Reset timer after playing reminder
            }

            yield return null;
        }

        // Calibration completed
        currentState = TutorialState.CalibrationSuccess;
        isWaitingForGesture = false;
        PlayAudioClipServerRpc(3); // calibrationSuccessClip

        // Wait for success message to finish
        yield return new WaitUntil(() => !audioSource.isPlaying);
        yield return new WaitForSeconds(0.5f); // Brief pause

        // Move to rock gesture instructions
        StartRockGestureInstructions();
    }

    private void StartRockGestureInstructions()
    {
        if (!isServer) return;

        currentState = TutorialState.RockGestureInstructions;
        PlayAudioClipServerRpc(4); // rockGestureInstructionClip

        // After instructions finish, wait for rock gesture
        activeCoroutine = StartCoroutine(WaitForRockGesture());
    }

    private IEnumerator WaitForRockGesture()
    {
        yield return new WaitUntil(() => !audioSource.isPlaying);

        currentState = TutorialState.WaitingForRockGesture;
        isWaitingForGesture = true;

        // Reset the timer each time we start waiting
        currentReminderTimer = 0f;

        // Wait for rock gesture or remind if taking too long
        while (!rockGestureCompleted)
        {
            // Increment timer with actual elapsed time
            currentReminderTimer += Time.deltaTime;

            // Debug to see the current timer value
            if (currentReminderTimer % 5 < 0.1f) // Log about every 5 seconds
            {
                Debug.Log($"Rock gesture reminder timer: {currentReminderTimer:F1}/{reminderDelay:F1}");
            }

            // Only play reminder if we're still waiting for rock gesture, no audio is playing,
            // and the full reminder delay has passed
            if (currentReminderTimer >= reminderDelay && !audioSource.isPlaying && currentState == TutorialState.WaitingForRockGesture)
            {
                Debug.Log($"Playing rock gesture reminder after {currentReminderTimer:F1} seconds");
                PlayAudioClipServerRpc(5); // rockGestureReminderClip
                currentReminderTimer = 0f; // Reset timer after playing reminder
            }

            yield return null;
        }

        // Rock gesture completed
        currentState = TutorialState.TutorialCompleted;
        isWaitingForGesture = false;

        // Play final message
        Debug.Log("Rock gesture completed, playing final tutorial message");
        PlayAudioClipServerRpc(6); // tutorialCompletedClip
        
        // Mark tutorial as completed - even if the audio might be interrupted by scene change
        PlayerPrefs.SetInt(TUTORIAL_COMPLETED_KEY, 1);
        PlayerPrefs.Save();
        
        Debug.Log("Tutorial completed");
    }

    private void OnGestureDetected(GestureType gestureType, bool isLeft)
    {
        if (!isServer || !isWaitingForGesture) return;

        // Check for calibration gesture (both hands paper)
        if (currentState == TutorialState.WaitingForCalibration)
        {
            if (gestureType == GestureType.Paper)
            {
                bool otherHandIsPaper = isLeft
                    ? GestureDetector.Instance.handR?.CurrentGestureType == GestureType.Paper
                    : GestureDetector.Instance.handL?.CurrentGestureType == GestureType.Paper;

                if (otherHandIsPaper)
                {
                    // Both hands are doing Paper gesture - calibration detected
                    // The UprightRedirector's event will mark calibration as complete
                    Debug.Log("Calibration gesture detected - waiting for UprightRedirector to complete");
                }
            }
        }
        // Check for rock gesture
        else if (currentState == TutorialState.WaitingForRockGesture)
        {
            if (gestureType == GestureType.Rock)
            {
                // Rock gesture detected
                Debug.Log("Rock gesture detected in tutorial");
                // The button press will be handled by NotifyButtonPressed
            }
        }
    }

    // Method to check if a specific VR button was pressed (called from VRButton)
    public void NotifyButtonPressed()
    {
        if (!isServer) return;

        if (currentState == TutorialState.WaitingForRockGesture)
        {
            Debug.Log("Button press detected - completing rock gesture tutorial");
            rockGestureCompleted = true;
        }
    }

    // Check if we're waiting for the rock gesture (used by VRButton)
    public bool IsWaitingForRockGesture()
    {
        return currentState == TutorialState.WaitingForRockGesture;
    }
    
    // Check if tutorial completion audio is currently playing
    public bool IsTutorialCompletedAudioPlaying()
    {
        return audioSource != null && 
               audioSource.isPlaying && 
               audioSource.clip == tutorialCompletedClip;
    }

    // Play audio by index instead of passing AudioClip objects
    private void PlayAudioClip(int clipIndex)
    {
        AudioClip clip = GetAudioClipByIndex(clipIndex);
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log($"Playing audio: {clip.name}");
        }
        else
        {
            Debug.LogWarning($"Attempted to play null audio clip at index {clipIndex}");
        }
    }

    [ClientRpc]
    private void PlayAudioClipServerRpc(int clipIndex)
    {
        PlayAudioClip(clipIndex);
    }

    // Helper method to get audio clip by index
    private AudioClip GetAudioClipByIndex(int index)
    {
        switch (index)
        {
            case 0: return welcomeClip;
            case 1: return calibrationInstructionClip;
            case 2: return calibrationReminderClip;
            case 3: return calibrationSuccessClip;
            case 4: return rockGestureInstructionClip;
            case 5: return rockGestureReminderClip;
            case 6: return tutorialCompletedClip;
            default: return null;
        }
    }

    // Public method to manually reset tutorial progress (can be called from a UI button if needed)
    public void ResetTutorialProgress()
    {
        if (!isServer) return;

        PlayerPrefs.SetInt(TUTORIAL_COMPLETED_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("Tutorial progress reset");
    }
}