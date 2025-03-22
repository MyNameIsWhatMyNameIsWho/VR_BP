using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary for the cube
    [SerializeField] private float maxX = 5f;   // Right boundary for the cube
    [SerializeField] private float handMovementScale = 20.0f; // Higher = less hand movement needed
    [SerializeField] private float smoothingFactor = 0.05f; // Lower = smoother movement (0-1)

    [Header("Gesture Control")]
    public bool controllingHandIsLeft = false;

    [Header("Events")]
    public UnityEvent OnHandPositionUpdated;

    // Movement state
    private bool canMove = false;
    private Vector3 initialHandPosition;
    private bool isInitialized = false;

    // Debugging
    private int frameCount = 0;

    private void Update()
    {
        if (!canMove) return;

        // Debugging log counter
        frameCount++;
        bool shouldLog = (frameCount % 30 == 0);

        // Get hand position
        Vector3 currentHandPosition = GetHandPosition();
        if (currentHandPosition == Vector3.zero) return; // Invalid hand position

        // Initialize reference position if needed
        if (!isInitialized)
        {
            initialHandPosition = currentHandPosition;
            isInitialized = true;
            Debug.Log($"Initial hand position set: {initialHandPosition}");
            return;
        }

        // Calculate offset from initial position
        float handOffset = currentHandPosition.x - initialHandPosition.x;

        // Apply high scaling factor to make small movements go further
        float scaledOffset = handOffset * handMovementScale;

        // Calculate target position
        float centerX = (minX + maxX) / 2f;
        float targetX = Mathf.Clamp(centerX + scaledOffset, minX, maxX);

        // Smoothly move toward the target position
        transform.position = Vector3.Lerp(
            transform.position,
            new Vector3(targetX, transform.position.y, transform.position.z),
            smoothingFactor
        );

        // Log information
        if (shouldLog)
        {
            float percentToEdge = Mathf.Abs((targetX - centerX) / ((maxX - minX) / 2f)) * 100f;
            Debug.Log($"Hand offset: {handOffset:F4}, Scaled: {scaledOffset:F2}, " +
                     $"Target X: {targetX:F4}, Percent to edge: {percentToEdge:F1}%");
        }

        // Trigger event
        OnHandPositionUpdated?.Invoke();
    }

    // Helper method to get hand position
    private Vector3 GetHandPosition()
    {
        if (GestureDetector.Instance == null)
        {
            Debug.LogError("GestureDetector.Instance is null!");
            return Vector3.zero;
        }

        var interactor = controllingHandIsLeft ?
            GestureDetector.Instance.handL?.Interactor :
            GestureDetector.Instance.handR?.Interactor;

        if (interactor == null || interactor.RaycastStartingPoint == null)
        {
            Debug.LogError("Hand reference is invalid");
            return Vector3.zero;
        }

        return interactor.RaycastStartingPoint.position;
    }

    public void StartMovement()
    {
        Debug.Log("Starting movement with high sensitivity");
        canMove = true;
        isInitialized = false;
        frameCount = 0;
    }

    public void GameOver()
    {
        Debug.Log("Game over - stopping movement");
        canMove = false;
    }

    public void Spawn()
    {
        Debug.Log("Spawning cube");
        canMove = false;
        isInitialized = false;

        // Reset position to center
        transform.position = new Vector3(
            (minX + maxX) / 2f,
            transform.position.y,
            transform.position.z
        );
    }
}