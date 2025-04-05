using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary
    [SerializeField] private float maxX = 5f;   // Right boundary
    [SerializeField] private float wallBuffer = 0.5f; // Buffer distance from walls
    [SerializeField] private float maxSpeed = 10.0f; // Maximum movement speed
    [SerializeField] private float acceleration = 30.0f; // How quickly speed increases
    [SerializeField] private float deceleration = 60.0f; // How quickly speed decreases (higher = more responsive stops)
    [SerializeField] private float directionChangeMultiplier = 2.0f; // Multiplier for direction changes (higher = more responsive)
    [SerializeField] private float handSensitivity = 50.0f; // Sensitivity of hand movement

    [Header("Balloon Rotation Animation")]
    [SerializeField] private float maxTiltAngle = 15f; // Maximum tilt angle
    [SerializeField] private float tiltSpeed = 3f; // How quickly the balloon tilts
    private Quaternion targetRotation;
    private float currentTiltAngle = 0f;

    [Header("Gesture Control")]
    public bool controllingHandIsLeft = false;

    [Header("Events")]
    public UnityEvent OnHandPositionUpdated;

    // Movement state
    private bool canMove = false;
    private Vector3 initialHandPosition;
    private bool isInitialized = false;
    private float currentSpeed = 0f;
    private float targetSpeed = 0f;
    private float lastHandX = 0f;
    private float handOffset = 0f;

    // Movement smoothing
    private float velocityXSmoothing;

    // Debugging
    private int frameCount = 0;

    private void Awake()
    {
        targetRotation = Quaternion.identity;
        transform.position = new Vector3((minX + maxX) / 2f, transform.position.y, transform.position.z);
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
            lastHandX = currentHandPosition.x;
            isInitialized = true;
            Debug.Log($"Initial hand position set: {initialHandPosition}");
            return;
        }

        // Calculate hand movement
        handOffset = (currentHandPosition.x - initialHandPosition.x) * handSensitivity;
        float handDelta = currentHandPosition.x - lastHandX;
        lastHandX = currentHandPosition.x;

        // Calculate target speed based on hand position
        // Clamp to ensure we don't exceed max speed
        targetSpeed = Mathf.Clamp(handOffset, -maxSpeed, maxSpeed);

        // Determine if we're changing direction
        bool changingDirection = (currentSpeed > 0 && targetSpeed < 0) || (currentSpeed < 0 && targetSpeed > 0);

        // Apply acceleration or deceleration based on movement state
        float accelRate = acceleration;

        // If changing direction, apply the direction change multiplier for more responsive turns
        if (changingDirection)
        {
            accelRate *= directionChangeMultiplier;
        }
        // If slowing down or stopping, use deceleration rate instead
        else if (Mathf.Abs(targetSpeed) < Mathf.Abs(currentSpeed) || Mathf.Approximately(targetSpeed, 0))
        {
            accelRate = deceleration;
        }

        // Smoothly adjust current speed toward target speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        // Apply movement
        Vector3 newPosition = transform.position;
        newPosition.x += currentSpeed * Time.deltaTime;

        // Clamp to boundaries
        newPosition.x = Mathf.Clamp(newPosition.x, minX + wallBuffer, maxX - wallBuffer);
        transform.position = newPosition;

        // Calculate tilt based on current speed and max speed
        float speedPercent = currentSpeed / maxSpeed;
        float targetTiltAngle = -maxTiltAngle * speedPercent; // Negative because moving right (positive speed) should tilt left (negative angle)

        // Smoothly adjust current tilt angle
        currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTiltAngle, tiltSpeed * Time.deltaTime);

        // Apply rotation
        transform.rotation = Quaternion.Euler(0, 0, currentTiltAngle);

        // Log information periodically
        if (shouldLog)
        {
            Debug.Log($"Hand offset: {handOffset:F2}, Target speed: {targetSpeed:F2}, " +
                      $"Current speed: {currentSpeed:F2}, Position: {transform.position.x:F2}, " +
                      $"Tilt angle: {currentTiltAngle:F2}");
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
        Debug.Log("Starting movement");
        canMove = true;
        isInitialized = false;
        currentSpeed = 0f;
        targetSpeed = 0f;
        frameCount = 0;
        currentTiltAngle = 0f;
        transform.rotation = Quaternion.identity;
    }

    // Method to change the movement speed
    public void SetMovementSpeed(float newSpeed)
    {
        maxSpeed = Mathf.Max(0.1f, newSpeed); // Ensure speed doesn't go below 0.1
        Debug.Log($"Character max speed set to: {maxSpeed}");
    }

    // Get the current movement speed
    public float GetMovementSpeed()
    {
        return maxSpeed;
    }

    public void GameOver()
    {
        Debug.Log("Game over - stopping movement");
        canMove = false;
    }

    public void Spawn()
    {
        Debug.Log("Spawning balloon");
        canMove = false;
        isInitialized = false;
        currentSpeed = 0f;
        targetSpeed = 0f;

        // Reset position to center
        transform.position = new Vector3(
            (minX + maxX) / 2f,
            transform.position.y,
            transform.position.z
        );

        // Reset rotation
        transform.rotation = Quaternion.identity;
        currentTiltAngle = 0f;
    }
}