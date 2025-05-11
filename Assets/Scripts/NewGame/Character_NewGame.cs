using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary
    [SerializeField] private float maxX = 5f;   // Right boundary
    [SerializeField] private float fixedY = 0f; // Fixed Y position - set this to your desired height
    [SerializeField] private float fixedZ = 0f; // Fixed Z position as well
    [SerializeField] private float wallBuffer = 0.5f; // Buffer distance from walls
    [SerializeField] private float maxSpeed = 10.0f; // Maximum movement speed
    [SerializeField] private float acceleration = 30.0f; // How quickly speed increases
    [SerializeField] private float deceleration = 60.0f; // How quickly speed decreases (higher = more responsive stops)
    [SerializeField] private float directionChangeMultiplier = 2.0f; // Multiplier for direction changes (higher = more responsive)
    [SerializeField] private float handSensitivity = 50.0f; // Sensitivity of hand movement

    [Header("Balloon Rotation Animation")]
    [SerializeField] private float maxTiltAngle = 15f; // Maximum tilt angle
    [SerializeField] private float tiltSpeed = 3f; // How quickly the balloon tilts
    [SyncVar] private float currentTiltAngle = 0f;

    [Header("Gesture Control")]
    [SyncVar] public bool controllingHandIsLeft = false;

    [Header("Events")]
    public UnityEvent OnHandPositionUpdated;

    // Movement state
    private bool canMove = false;
    private Vector3 initialHandPosition;
    private bool isInitialized = false;
    [SyncVar] private float currentSpeed = 0f;
    private float targetSpeed = 0f;
    private float lastHandX = 0f;
    private float handOffset = 0f;

    // Debugging
    private int frameCount = 0;

    // Center position (exact middle)
    private Vector3 centerPosition;

    // Rigidbody reference (if any)
    private Rigidbody rb;

    // For position and rotation sync
    private Vector3 currentPosition;
    private Quaternion currentRotation;

    // Add NetworkTransform component in Awake if it doesn't exist
    private void Awake()
    {
        // We're going to handle position updates ourselves instead of using NetworkTransform
        if (TryGetComponent<NetworkTransform>(out var networkTransform))
        {
            Destroy(networkTransform);
        }

        // Store the exact center position
        fixedY = fixedY != 0 ? fixedY : transform.position.y;
        fixedZ = fixedZ != 0 ? fixedZ : transform.position.z;
        centerPosition = new Vector3((minX + maxX) / 2f, fixedY, fixedZ);

        // Get rigidbody if there is one
        rb = GetComponent<Rigidbody>();

        // Force initial position
        ForceResetPosition();
    }

    // Helper method to completely reset the balloon position and physics
    private void ForceResetPosition()
    {
        // Reset position to exact center
        currentPosition = centerPosition;
        transform.position = currentPosition;

        // Reset rotation
        currentRotation = Quaternion.identity;
        transform.rotation = currentRotation;
        currentTiltAngle = 0f;

        // Reset any physics forces if there's a rigidbody
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); // Force physics engine to reset completely
        }
        
        // Synchronize to clients if we're server
        if (isServer)
        {
            RpcUpdatePositionAndRotation(currentPosition, currentRotation);
        }
    }

    private void Update()
    {
        // Always ensure Y position is fixed, even outside of movement
        if (transform.position.y != fixedY || transform.position.z != fixedZ)
        {
            currentPosition = transform.position;
            currentPosition.y = fixedY;
            currentPosition.z = fixedZ;
            transform.position = currentPosition;
        }

        if (!isServer || !canMove) return;

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

        // Apply movement - only on X axis, maintain fixed Y and Z
        currentPosition = transform.position;
        currentPosition.x += currentSpeed * Time.deltaTime;
        currentPosition.y = fixedY; // Always maintain fixed Y position
        currentPosition.z = fixedZ; // Always maintain fixed Z position

        // Clamp to boundaries
        currentPosition.x = Mathf.Clamp(currentPosition.x, minX + wallBuffer, maxX - wallBuffer);

        // Calculate tilt based on current speed and max speed
        float speedPercent = currentSpeed / maxSpeed;
        float targetTiltAngle = -maxTiltAngle * speedPercent; // Negative because moving right (positive speed) should tilt left (negative angle)

        // Smoothly adjust current tilt angle
        currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTiltAngle, tiltSpeed * Time.deltaTime);

        // Apply rotation
        currentRotation = Quaternion.Euler(0, 0, currentTiltAngle);

        // Update position and rotation on server
        transform.position = currentPosition;
        transform.rotation = currentRotation;
        
        // Send updates to clients
        RpcUpdatePositionAndRotation(currentPosition, currentRotation);

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
    
    [ClientRpc]
    private void RpcUpdatePositionAndRotation(Vector3 newPosition, Quaternion newRotation)
    {
        if (isServer) return; // Server already updated position in Update
        
        // Apply on clients
        transform.position = newPosition;
        transform.rotation = newRotation;
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

        // Force reset position and physics first
        ForceResetPosition();

        // Then enable movement
        canMove = true;
        isInitialized = false;
        currentSpeed = 0f;
        targetSpeed = 0f;
        frameCount = 0;
        currentTiltAngle = 0f;
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

        // Force complete reset of position and physics
        ForceResetPosition();

        // Re-enable all renderers that might have been disabled
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in allRenderers)
        {
            renderer.enabled = true;
        }

        canMove = false;
        isInitialized = false;
        currentSpeed = 0f;
        targetSpeed = 0f;
    }
}