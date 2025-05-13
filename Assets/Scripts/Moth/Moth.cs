using Mirror;
using System;
using UnityEngine;
using UnityEngine.Events;

public class Moth : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 0.5f;
    [SerializeField] private float changeDirectionTime = 3f;

    // Zone settings (set by MothGameManager)
    [HideInInspector] public Vector3 zoneCenter = new Vector3(0, 1.5f, 1f);
    [HideInInspector] public Vector3 zoneSize = new Vector3(2f, 1f, 1f);

    // Events
    [HideInInspector] public UnityEvent<Moth> OnMothCaught = new UnityEvent<Moth>();

    // Private variables
    private Vector3 moveDirection;
    private float nextChangeTime;
    private bool isActive = true;

    // Color of this moth - primarily for combo tracking
    [SyncVar] private Color mothColor;
    public Color MothColor => mothColor;

    // For position and rotation sync
    private Vector3 currentPosition;
    private Quaternion currentRotation;
    
    // Sync update frequency
    private float syncInterval = 0.05f; // 20 updates per second
    private float lastSyncTime = 0f;

    // Renderer cache
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        // We're going to handle position updates ourselves instead of using NetworkTransform
        if (TryGetComponent<NetworkTransform>(out var networkTransform))
        {
            Destroy(networkTransform);
        }
    }

    public void Initialize(Color? colorOverride = null)
    {
        // Cache renderer reference
        meshRenderer = GetComponent<MeshRenderer>();

        // Set color (either from provided value or from color manager)
        if (colorOverride.HasValue)
        {
            mothColor = colorOverride.Value;
        }
        else if (MothGameManager.Instance != null &&
                 MothGameManager.Instance.colorComboManager != null)
        {
            // Get random color from the color combo manager
            mothColor = MothGameManager.Instance.colorComboManager.GetRandomMothColor();
            Debug.Log($"Moth spawned with color: {ColorToName(mothColor)}");
        }
        else
        {
            // Fallback to random color in case combo manager isn't available
            mothColor = new Color(
                UnityEngine.Random.Range(0.5f, 1.0f),
                UnityEngine.Random.Range(0.5f, 1.0f),
                UnityEngine.Random.Range(0.5f, 1.0f)
            );
            Debug.Log("Using fallback random color for moth");
        }

        // Apply color to renderer
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = mothColor;
        }
        else
        {
            Debug.LogWarning("Could not find MeshRenderer to apply color");
        }

        // Pick a random direction
        PickNewDirection();

        // Set next direction change time
        nextChangeTime = Time.time + UnityEngine.Random.Range(changeDirectionTime * 0.5f, changeDirectionTime * 1.5f);

        // Set up collider if needed
        if (GetComponent<Collider>() == null)
        {
            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.3f;
        }
        
        // Initialize position and rotation sync variables
        currentPosition = transform.position;
        currentRotation = transform.rotation;
    }

    private void Update()
    {
        if (!isServer || !isActive) return;

        // Change direction occasionally
        if (Time.time > nextChangeTime)
        {
            PickNewDirection();
            nextChangeTime = Time.time + UnityEngine.Random.Range(changeDirectionTime * 0.5f, changeDirectionTime * 1.5f);
        }

        // Move in current direction
        currentPosition += moveDirection * moveSpeed * Time.deltaTime;

        // Check boundaries and bounce if needed
        CheckBoundaries();

        // Make the moth face the direction it's moving
        if (moveDirection != Vector3.zero)
        {
            currentRotation = Quaternion.LookRotation(moveDirection);
        }

        // Add some rotation for visual interest
        currentRotation *= Quaternion.Euler(0, Time.deltaTime * 90f, 0);
        
        // Apply position and rotation to server object
        transform.position = currentPosition;
        transform.rotation = currentRotation;
        
        // Sync position and rotation to clients at regular intervals
        if (Time.time - lastSyncTime > syncInterval)
        {
            RpcUpdatePositionAndRotation(currentPosition, currentRotation);
            lastSyncTime = Time.time;
        }
    }
    
    [ClientRpc]
    private void RpcUpdatePositionAndRotation(Vector3 newPosition, Quaternion newRotation)
    {
        if (isServer) return; // Server already updated in Update
        
        // Apply position and rotation on clients
        transform.position = newPosition;
        transform.rotation = newRotation;
    }

    private void PickNewDirection()
    {
        moveDirection = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f)
        ).normalized;
    }

    private void CheckBoundaries()
    {
        Vector3 halfSize = zoneSize * 0.5f;
        bool needNewDirection = false;

        // Check if we're outside the zone and bounce
        if (currentPosition.x < zoneCenter.x - halfSize.x || currentPosition.x > zoneCenter.x + halfSize.x)
        {
            moveDirection.x = -moveDirection.x;
            needNewDirection = true;
        }

        if (currentPosition.y < zoneCenter.y - halfSize.y || currentPosition.y > zoneCenter.y + halfSize.y)
        {
            moveDirection.y = -moveDirection.y;
            needNewDirection = true;
        }

        if (currentPosition.z < zoneCenter.z - halfSize.z || currentPosition.z > zoneCenter.z + halfSize.z)
        {
            moveDirection.z = -moveDirection.z;
            needNewDirection = true;
        }

        // Ensure we stay in boundaries
        currentPosition = new Vector3(
            Mathf.Clamp(currentPosition.x, zoneCenter.x - halfSize.x, zoneCenter.x + halfSize.x),
            Mathf.Clamp(currentPosition.y, zoneCenter.y - halfSize.y, zoneCenter.y + halfSize.y),
            Mathf.Clamp(currentPosition.z, zoneCenter.z - halfSize.z, zoneCenter.z + halfSize.z)
        );

        // Apply the new direction if we bounced
        if (needNewDirection)
        {
            moveDirection.Normalize();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer || !isActive) return;

        // Check for player hands
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerHands"))
        {
            // Check if moth interaction is allowed (via MothGameManager)
            if (MothGameManager.Instance != null && !MothGameManager.Instance.AreMothsInteractive())
            {
                // Moths not yet interactive - ignore collision
                return;
            }

            isActive = false;
            OnMothCaught.Invoke(this);
        }
        // Avoid other moths
        else if (other.gameObject.layer == LayerMask.NameToLayer("Moth"))
        {
            // Move away from other moth
            Vector3 awayDirection = (currentPosition - other.transform.position).normalized;
            currentPosition += awayDirection * 0.1f;
            moveDirection = awayDirection;
            
            // Update transform position to match
            transform.position = currentPosition;
            
            // Sync to clients
            RpcUpdatePositionAndRotation(currentPosition, currentRotation);
        }
    }

    // Helper method to convert color to name for debugging
    private string ColorToName(Color color)
    {
        // Simple color name approximation
        if (color.r > 0.7f && color.g < 0.5f && color.b < 0.5f) return "Red";
        if (color.r < 0.5f && color.g > 0.7f && color.b < 0.5f) return "Green";
        if (color.r < 0.5f && color.g < 0.5f && color.b > 0.7f) return "Blue";
        if (color.r > 0.7f && color.g > 0.7f && color.b < 0.5f) return "Yellow";
        if (color.r > 0.7f && color.g < 0.5f && color.b > 0.7f) return "Purple";
        return "Unknown";
    }
}