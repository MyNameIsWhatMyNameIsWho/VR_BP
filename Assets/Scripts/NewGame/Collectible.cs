using UnityEngine;
using Mirror;
using System.Collections;

public class Collectible : NetworkBehaviour
{
    [SerializeField] private int pointValue = 10;

    [Header("Visual Effects")]
    [SerializeField] private GameObject collectionEffect;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    [SerializeField] private bool rotateOnY = true; // Vertical rotation (around Y axis)

    [Header("Orientation Fix")]
    [SerializeField] private Vector3 initialRotation = new Vector3(0, 180, 0); // Y-axis 180 degrees flip

    [SyncVar] private float fallSpeed;
    [SyncVar] private bool isCollected = false;

    // For straight falling movement
    private Vector3 startPosition;
    [SyncVar] private float currentY;
    
    private Quaternion currentRotation;

    private void Awake()
    {
        // We're going to handle position updates ourselves instead of using NetworkTransform
        if (TryGetComponent<NetworkTransform>(out var networkTransform))
        {
            Destroy(networkTransform);
        }
    }

    // Method for NewGameManager to set the fall speed
    public void SetFallSpeed(float newSpeed)
    {
        fallSpeed = newSpeed;
    }

    // Get current fall speed
    public float GetFallSpeed()
    {
        return fallSpeed;
    }

    public void Initialize()
    {
        // Get the speed from the central manager
        if (fallSpeed <= 0 && NewGameManager.Instance != null)
        {
            fallSpeed = NewGameManager.Instance.CollectibleFallSpeed;
        }
        else if (fallSpeed <= 0)
        {
            fallSpeed = 1.2f; // Fallback default
            Debug.LogError("NewGameManager.Instance is null in Collectible Initialize!");
        }

        // Store initial X and Z position to maintain straight line movement
        startPosition = transform.position;
        currentY = startPosition.y;
        
        // Fix initial orientation
        transform.rotation = Quaternion.Euler(initialRotation);
        currentRotation = transform.rotation;
    }

    private void Update()
    {
        if (!isServer || isCollected) return;

        // Update Y position for straight falling
        currentY -= fallSpeed * Time.deltaTime;

        // Create new position - maintain original X and Z for straight-line falling
        Vector3 newPosition = new Vector3(startPosition.x, currentY, startPosition.z);

        // Calculate rotation
        if (rotateOnY)
        {
            // Calculate new rotation
            currentRotation *= Quaternion.Euler(0, rotationSpeed * Time.deltaTime, 0);
        }

        // Send updated position and rotation to clients
        RpcUpdatePositionAndRotation(newPosition, currentRotation);

        // Update position and rotation on server
        transform.position = newPosition;
        transform.rotation = currentRotation;

        // Destroy if it goes out of view
        if (transform.position.y < -10f)
        {
            NetworkServer.Destroy(gameObject);
        }
    }
    
    [ClientRpc]
    private void RpcUpdatePositionAndRotation(Vector3 newPosition, Quaternion newRotation)
    {
        if (isServer) return; // Server already updated position in Update
        
        // Apply on clients
        transform.position = newPosition;
        transform.rotation = newRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || isCollected) return;

        if (other.CompareTag("Player"))
        {
            // Mark as collected to prevent duplicate collection
            isCollected = true;

            // Get exact position for the effect
            Vector3 effectPosition = transform.position;

            // If collectionEffect is a NetworkVisualEffect prefab, use it directly
            if (collectionEffect != null && collectionEffect.TryGetComponent<NetworkVisualEffect>(out var _))
            {
                // Spawn the effect at the collectible position
                NetworkVisualEffect vfx = Instantiate(collectionEffect, effectPosition, Quaternion.identity)
                    .GetComponent<NetworkVisualEffect>();

                NetworkServer.Spawn(vfx.gameObject);
                vfx.Play();

                Debug.Log($"Spawned collection effect at position: {effectPosition}");
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

            // Immediately destroy the collectible
            NetworkServer.Destroy(gameObject);
        }
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && isServer)
        {
            NetworkServer.Destroy(obj);
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