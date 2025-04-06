using UnityEngine;

public class ThreadRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 60f; // Degrees per second
    [SerializeField] private Transform balloonTransform; // Reference to the balloon
    [SerializeField] private bool rotateOnlyWhenMoving = true; // Only rotate when the balloon is moving
    [SerializeField] private float movementThreshold = 0.01f; // Minimum movement to trigger rotation

    // Try each of these axes to find the correct horizontal rotation
    [Header("Rotation Axis")]
    [SerializeField] private bool rotateAroundX = false;
    [SerializeField] private bool rotateAroundY = true; // Default
    [SerializeField] private bool rotateAroundZ = false;

    private Vector3 lastBalloonPosition;
    private bool isRotating = false;

    private void Start()
    {
        if (balloonTransform == null)
        {
            // Try to find the balloon (assuming this script is on the thread and parent is balloon)
            balloonTransform = transform.parent;

            if (balloonTransform == null)
            {
                Debug.LogWarning("ThreadRotation: No balloon transform assigned or found");
            }
        }

        lastBalloonPosition = balloonTransform != null ? balloonTransform.position : Vector3.zero;
    }

    private void Update()
    {
        if (balloonTransform == null) return;

        // Check if the balloon is moving
        float movement = Vector3.Distance(balloonTransform.position, lastBalloonPosition);
        lastBalloonPosition = balloonTransform.position;

        // Determine if we should be rotating
        if (rotateOnlyWhenMoving)
        {
            isRotating = movement > movementThreshold;
        }
        else
        {
            isRotating = true; // Always rotate
        }

        // Apply rotation if needed
        if (isRotating)
        {
            // Calculate rotation speed proportional to movement speed if desired
            float speedFactor = rotateOnlyWhenMoving ? Mathf.Clamp(movement * 10f, 0.5f, 2f) : 1f;
            float rotationAmount = rotationSpeed * speedFactor * Time.deltaTime;

            // Apply rotation based on selected axis
            if (rotateAroundX)
            {
                transform.Rotate(rotationAmount, 0, 0, Space.Self);
            }

            if (rotateAroundY)
            {
                transform.Rotate(0, rotationAmount, 0, Space.Self);
            }

            if (rotateAroundZ)
            {
                transform.Rotate(0, 0, rotationAmount, Space.Self);
            }
        }
    }
}