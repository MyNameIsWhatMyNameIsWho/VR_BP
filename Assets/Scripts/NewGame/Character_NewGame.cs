using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary for the cube
    [SerializeField] private float maxX = 5f;   // Right boundary for the cube
    [SerializeField] private float handMovementScale = 50.0f; // Much higher multiplier for less hand movement required
    [SerializeField] private float smoothingFactor = 0.05f; // Lower = smoother movement (0-1)
    [SerializeField] private float wallBuffer = 0.1f; // Increased buffer distance from wall to prevent jittering

    [Header("Gesture Control")]
    public bool controllingHandIsLeft = false;

    [Header("Events")]
    public UnityEvent OnHandPositionUpdated;

    // Movement state
    private bool canMove = false;
    private Vector3 initialHandPosition;
    private bool isInitialized = false;
    private Vector3 lastValidPosition;
    private bool isAtLeftWall = false;
    private bool isAtRightWall = false;

    // Debugging
    private int frameCount = 0;

    private void Awake()
    {
        lastValidPosition = new Vector3((minX + maxX) / 2f, transform.position.y, transform.position.z);
    }

    private void Update()
    {
        if (!canMove) return;

        // Debugging log counter
        frameCount++;
        bool shouldLog = (frameCount % 60 == 0);

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

        // Determine if we're at or very near a wall boundary
        isAtLeftWall = (transform.position.x <= minX + wallBuffer);
        isAtRightWall = (transform.position.x >= maxX - wallBuffer);

        // Check if we're trying to move away from a wall
        bool movingAwayFromLeftWall = (isAtLeftWall && handOffset > 0.01f);
        bool movingAwayFromRightWall = (isAtRightWall && handOffset < -0.01f);

        if ((!isAtLeftWall && !isAtRightWall) || movingAwayFromLeftWall || movingAwayFromRightWall)
        {
            // We're not at a wall or moving away from it, so apply regular movement
            Vector3 newPosition = new Vector3(
                Mathf.Lerp(transform.position.x, targetX, smoothingFactor),
                transform.position.y,
                transform.position.z
            );

            transform.position = newPosition;
            lastValidPosition = newPosition;
        }
        else
        {
            // We're at a wall and trying to go further in that direction - snap exactly to the wall
            if (isAtLeftWall)
            {
                transform.position = new Vector3(minX, transform.position.y, transform.position.z);
            }
            else if (isAtRightWall)
            {
                transform.position = new Vector3(maxX, transform.position.y, transform.position.z);
            }
        }

        // Log information periodically
        if (shouldLog)
        {
            Debug.Log($"Hand offset: {handOffset:F4}, Scaled: {scaledOffset:F2}, " +
                     $"At Left Wall: {isAtLeftWall}, At Right Wall: {isAtRightWall}");
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
        isAtLeftWall = false;
        isAtRightWall = false;
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
        isAtLeftWall = false;
        isAtRightWall = false;

        // Reset position to center
        transform.position = new Vector3(
            (minX + maxX) / 2f,
            transform.position.y,
            transform.position.z
        );
    }
}