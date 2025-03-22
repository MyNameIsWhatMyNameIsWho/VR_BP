using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Character_NewGame : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minX = -5f;  // Left boundary for the cube
    [SerializeField] private float maxX = 5f;   // Right boundary for the cube
    [SerializeField] private float moveSmoothing = 0.1f; // Smoothing factor for movement
    [SerializeField] private float movementSensitivity = 1.0f; // How much the cube moves relative to hand movement

    [Header("Gesture Control")]
    public bool controllingHandIsLeft = false;

    [Header("Events")]
    [Tooltip("Invoked when the hand position is updated.")]
    public UnityEvent OnHandPositionUpdated;

    private bool canMove = false;
    private Transform cachedTransform;
    private Vector3 previousHandPosition;
    private bool isHandPositionInitialized = false;

    private void Awake()
    {
        cachedTransform = transform;
    }

    private void Update()
    {
        if (!canMove) return;

        // Get the hand we are tracking
        var hand = controllingHandIsLeft ? GestureDetector.Instance.handL : GestureDetector.Instance.handR;
        if (hand == null) return;

        // Get the current hand position
        Vector3 currentHandPosition = hand.transform.position;

        // Initialize previous position if this is the first update
        if (!isHandPositionInitialized)
        {
            previousHandPosition = currentHandPosition;
            isHandPositionInitialized = true;
            return;
        }

        // Calculate the hand's movement delta
        float handDeltaX = currentHandPosition.x - previousHandPosition.x;

        // Apply sensitivity to the movement
        float movementAmount = handDeltaX * movementSensitivity;

        // Calculate new position with boundaries
        Vector3 currentCubePos = cachedTransform.position;
        float newX = Mathf.Clamp(currentCubePos.x + movementAmount, minX, maxX);

        // Set the new position with smoothing
        currentCubePos.x = Mathf.Lerp(currentCubePos.x, newX, moveSmoothing);
        cachedTransform.position = currentCubePos;

        // Update the previous hand position for next frame
        previousHandPosition = currentHandPosition;

        OnHandPositionUpdated?.Invoke();
    }

    /// <summary>
    /// Enables the cube to move according to the hand.
    /// </summary>
    public void StartMovement()
    {
        canMove = true;
        isHandPositionInitialized = false; // Reset this so we get a clean starting point
    }

    /// <summary>
    /// Disables the cube's movement.
    /// </summary>
    public void GameOver()
    {
        canMove = false;
    }

    /// <summary>
    /// Resets the cube for a new game.
    /// </summary>
    public void Spawn()
    {
        canMove = true;
        isHandPositionInitialized = false; // Reset this so we get a clean starting point

        // Reset the cube to the center between minX and maxX
        cachedTransform.position = new Vector3((minX + maxX) / 2f, cachedTransform.position.y, cachedTransform.position.z);
    }
}