using UnityEngine;
using Mirror;

public class Obstacle_NewGame : NetworkBehaviour
{
    // Original obstacle properties
    private float fallSpeed;
    private bool hasCollided = false;

    // Added rotation properties
    [Header("Rotation Settings")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private bool rotateX = true;
    [SerializeField] private bool rotateY = true;
    [SerializeField] private bool rotateZ = true;

    // For straight falling movement
    private Vector3 startPosition;
    private float currentY;

    public void Initialize()
    {
        // Get the speed from the central manager - the ONLY place speeds are defined
        if (NewGameManager.Instance != null)
        {
            fallSpeed = NewGameManager.Instance.ObstacleFallSpeed;
        }
        else
        {
            fallSpeed = 0.8f; // Fallback default only used if something goes wrong
            Debug.LogError("NewGameManager.Instance is null in Obstacle Initialize!");
        }

        // Store initial X and Z position to maintain straight line movement
        startPosition = transform.position;
        currentY = startPosition.y;

        // Make sure this doesn't push other objects
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Prevents physical pushing of other objects
        }
    }

    private void Update()
    {
        if (!isServer || hasCollided) return;

        // Update Y position for straight falling
        currentY -= fallSpeed * Time.deltaTime;

        // Apply position - maintain original X and Z for straight-line falling
        transform.position = new Vector3(startPosition.x, currentY, startPosition.z);

        // Apply rotation if enabled (only affects visual rotation, not movement)
        if (enableRotation)
        {
            // Simple rotation on specified axes
            float xRotation = rotateX ? rotationSpeed * Time.deltaTime : 0;
            float yRotation = rotateY ? rotationSpeed * Time.deltaTime : 0;
            float zRotation = rotateZ ? rotationSpeed * Time.deltaTime : 0;

            transform.Rotate(xRotation, yRotation, zRotation, Space.Self);
        }

        // Destroy if it goes out of view
        if (transform.position.y < -10f)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer || hasCollided) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            // Mark as collided to prevent further movement and multiple collisions
            hasCollided = true;

            // Disable the collider immediately to prevent jittering
            GetComponent<Collider>().enabled = false;

            // Play balloon pop sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("BalloonPop");
            }

            // End the game via NewGameManager
            if (NewGameManager.Instance != null)
            {
                NewGameManager.Instance.EndGame();
            }
        }
    }
}