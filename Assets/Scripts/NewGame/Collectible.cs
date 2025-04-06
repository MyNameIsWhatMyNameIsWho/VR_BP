using UnityEngine;
using Mirror;

public class Collectible : NetworkBehaviour
{
    [SerializeField] private int pointValue = 10;
    [SerializeField] private GameObject collectionEffect;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    [SerializeField] private bool rotateOnY = true; // Vertical rotation (around Y axis)

    [Header("Orientation Fix")]
    [SerializeField] private Vector3 initialRotation = new Vector3(0, 180, 0); // Y-axis 180 degrees flip

    private float fallSpeed;
    private bool isCollected = false;

    // For straight falling movement
    private Vector3 startPosition;
    private float currentY;

    public void Initialize()
    {
        // Get the speed from the central manager
        if (NewGameManager.Instance != null)
        {
            fallSpeed = NewGameManager.Instance.CollectibleFallSpeed;
        }
        else
        {
            fallSpeed = 1.2f; // Fallback default
            Debug.LogError("NewGameManager.Instance is null in Collectible Initialize!");
        }

        // Store initial X and Z position to maintain straight line movement
        startPosition = transform.position;
        currentY = startPosition.y;

        // Fix initial orientation
        transform.rotation = Quaternion.Euler(initialRotation);
    }

    private void Update()
    {
        if (!isServer || isCollected) return;

        // Update Y position for straight falling
        currentY -= fallSpeed * Time.deltaTime;

        // Apply position - maintain original X and Z for straight-line falling
        transform.position = new Vector3(startPosition.x, currentY, startPosition.z);

        // Apply vertical rotation (around Y axis)
        if (rotateOnY)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
        }

        // Destroy if it goes out of view
        if (transform.position.y < -10f)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || isCollected) return;

        if (other.CompareTag("Player"))
        {
            // Mark as collected to prevent duplicate collection
            isCollected = true;

            // Play collection effect if available
            if (collectionEffect != null)
            {
                GameObject effect = Instantiate(collectionEffect, transform.position, Quaternion.identity);
                NetworkServer.Spawn(effect);
            }

            // Notify the adaptive spawner this was collected
            if (NewGameManager.Instance != null &&
                NewGameManager.Instance.adaptiveSpawner != null)
            {
                NewGameManager.Instance.adaptiveSpawner.MarkCollectibleCollected(transform.position);
            }

            // Add points to score via NewGameManager
            if (NewGameManager.Instance != null)
            {
                NewGameManager.Instance.CollectItem(pointValue);
            }

            // Disable the collider to prevent multiple collections
            GetComponent<Collider>().enabled = false;

            // COMMENTED OUT: GetComponent<Renderer>().enabled = false;

            // Immediately destroy the collectible
            NetworkServer.Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (isServer && NewGameManager.Instance != null &&
            NewGameManager.Instance.adaptiveSpawner != null)
        {
            // Notify the adaptive spawner this was destroyed (with collection status)
            NewGameManager.Instance.adaptiveSpawner.NotifyCollectibleDestroyed(
                transform.position, isCollected);
        }
    }
}