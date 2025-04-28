using System.Collections;
using UnityEngine;
using Mirror;

public class MothGameAudioTutorial : NetworkBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip rulesClip;  // Your rules.mp3 file

    [Header("Timing Settings")]
    [SerializeField] private float initialDelay = 0.5f;  // Short delay before starting
    [SerializeField] private float motionEnableDelay = 0.5f;  // Time to wait before enabling moth collisions

    // Private Components
    private AudioSource audioSource;
    private MothGameManager gameManager;

    // State tracking
    private bool tutorialComplete = false;

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
        // Create audio source if needed
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void InitializeGameManager()
    {
        gameManager = MothGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("MothGameAudioTutorial: Could not find MothGameManager instance");
        }
    }

    // This method will be called when the game starts
    public void OnGameStart()
    {
        if (!isServer) return;

        StartCoroutine(PlayTutorialSequence());
    }

    private IEnumerator PlayTutorialSequence()
    {
        // Wait a short moment before starting
        yield return new WaitForSeconds(initialDelay);

        // Play the tutorial audio
        PlayRulesAudio();

        // Wait for the short motion enable delay
        yield return new WaitForSeconds(motionEnableDelay);

        // Enable player interaction with moths
        EnableMothInteraction();

        // Wait for audio to finish playing
        while (audioSource.isPlaying)
        {
            yield return null;
        }

        // Mark tutorial as complete
        tutorialComplete = true;
    }

    [ClientRpc]
    private void PlayRulesAudio()
    {
        if (rulesClip != null)
        {
            audioSource.clip = rulesClip;
            audioSource.Play();
            Debug.Log("Playing moth game rules audio");
        }
        else
        {
            Debug.LogWarning("Rules audio clip not assigned!");

            // If no audio clip, still enable moths after delay
            if (isServer)
            {
                StartCoroutine(EnableMothsAfterDelay());
            }
        }
    }

    private IEnumerator EnableMothsAfterDelay()
    {
        yield return new WaitForSeconds(motionEnableDelay);
        EnableMothInteraction();
    }

    private void EnableMothInteraction()
    {
        // Enable player interaction with moths by calling the MothGameManager method
        if (gameManager != null)
        {
            gameManager.EnableMothInteraction();
            Debug.Log("Moths are now interactive");
        }
        else
        {
            Debug.LogError("Cannot enable moth interaction - gameManager is null!");
        }
    }

    // You can use this to check if the tutorial is finished
    public bool IsTutorialComplete()
    {
        return tutorialComplete;
    }
}