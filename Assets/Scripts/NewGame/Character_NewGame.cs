using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary for the cube
    [SerializeField] private float maxX = 5f;   // Right boundary for the cube
    [SerializeField] private float handMovementScale = 50.0f; // Much higher multiplier for less hand movement required
    [SerializeField] private float wallBuffer = 0.5f; // Buffer distance from wall to prevent jittering
    [SerializeField] private float movementSpeed = 10.0f; // Units per second the cube will move
    [SerializeField] private float smoothingFactor = 0.05f; // How much smoothing to apply (0-1, lower is smoother)

    [Header("Gesture Control")]
    public bool controllingHandIsLeft = false;

    [Header("Events")]
    public UnityEvent OnHandPositionUpdated;

    // Movement state
    private bool canMove = false;
    private Vector3 initialHandPosition;
    private bool isInitialized = false;
    private Vector3 lastValidPosition;

    // For smoothing
    private static Vector3 filteredOffset = Vector3.zero;

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

        // Apply hand movement scale
        float rawTargetOffset = handOffset * handMovementScale;

        // Calculate target position with center-relative positioning
        float centerX = (minX + maxX) / 2f;

        // Hard clamp the cube position to prevent going through walls
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, minX + wallBuffer, maxX - wallBuffer),
            transform.position.y,
            transform.position.z
        );

        // Apply heavy smoothing to the raw input to reduce tremors/jitter
        // We use a running average approach
        filteredOffset = Vector3.Lerp(filteredOffset, new Vector3(rawTargetOffset, 0, 0), smoothingFactor);

        // Calculate target position within allowed boundaries using the smoothed offset
        float targetX = Mathf.Clamp(centerX + filteredOffset.x, minX + wallBuffer, maxX - wallBuffer);

        // Check if we're at a wall boundary and trying to go further
        bool atLeftWall = transform.position.x <= minX + wallBuffer + 0.01f;
        bool atRightWall = transform.position.x >= maxX - wallBuffer - 0.01f;
        bool tryingToGoLeft = targetX < transform.position.x;
        bool tryingToGoRight = targetX > transform.position.x;

        // Only move if we're not at a wall or we're moving away from the wall
        if ((!atLeftWall || !tryingToGoLeft) && (!atRightWall || !tryingToGoRight))
        {
            // Calculate desired position with full responsiveness to hand movement
            Vector3 desiredPosition = new Vector3(
                targetX,
                transform.position.y,
                transform.position.z
            );

            // Calculate the maximum distance we can move this frame based on movementSpeed
            float maxMoveDistance = movementSpeed * Time.deltaTime;

            // Get the direction to the target
            Vector3 moveDirection = desiredPosition - transform.position;
            float distanceToTarget = moveDirection.magnitude;

            // If we're not at the target, move toward it at a controlled speed
            if (distanceToTarget > 0.001f)
            {
                // Normalize direction and multiply by max distance
                moveDirection.Normalize();

                // Create new position by moving a limited distance toward target
                Vector3 newPosition = transform.position;

                // Either move the max distance or the full distance if it's smaller
                if (distanceToTarget <= maxMoveDistance)
                {
                    newPosition = desiredPosition;
                }
                else
                {
                    newPosition += moveDirection * maxMoveDistance;
                }

                // Final safety clamp to ensure we never go outside bounds
                newPosition.x = Mathf.Clamp(newPosition.x, minX + wallBuffer, maxX - wallBuffer);

                transform.position = newPosition;
                lastValidPosition = newPosition;
            }
        }

        // Log information periodically
        if (shouldLog)
        {
            float percentToEdge = Mathf.Abs((transform.position.x - centerX) / ((maxX - minX) / 2f)) * 100f;
            Debug.Log($"Hand offset: {handOffset:F4}, Filtered: {filteredOffset.x:F2}, " +
                     $"Pos X: {transform.position.x:F2}, At wall: {atLeftWall || atRightWall}, " +
                     $"Percent to edge: {percentToEdge:F1}%");
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
        // Reset the filtered offset when starting
        filteredOffset = Vector3.zero;
    }

    // Method to change the movement speed
    public void SetMovementSpeed(float newSpeed)
    {
        movementSpeed = Mathf.Max(0.1f, newSpeed); // Ensure speed doesn't go below 0.1
        Debug.Log($"Character movement speed set to: {movementSpeed}");
    }

    // Get the current movement speed
    public float GetMovementSpeed()
    {
        return movementSpeed;
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
        filteredOffset = Vector3.zero;

        // Reset position to center
        transform.position = new Vector3(
            (minX + maxX) / 2f,
            transform.position.y,
            transform.position.z
        );
    }
}