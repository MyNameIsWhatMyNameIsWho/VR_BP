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

    // Renderer cache
    private MeshRenderer meshRenderer;

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
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // Check boundaries and bounce if needed
        CheckBoundaries();

        // Make the moth face the direction it's moving
        if (moveDirection != Vector3.zero)
        {
            transform.forward = moveDirection;
        }

        // Add some rotation for visual interest
        transform.Rotate(0, Time.deltaTime * 90f, 0, Space.Self);
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
        if (transform.position.x < zoneCenter.x - halfSize.x || transform.position.x > zoneCenter.x + halfSize.x)
        {
            moveDirection.x = -moveDirection.x;
            needNewDirection = true;
        }

        if (transform.position.y < zoneCenter.y - halfSize.y || transform.position.y > zoneCenter.y + halfSize.y)
        {
            moveDirection.y = -moveDirection.y;
            needNewDirection = true;
        }

        if (transform.position.z < zoneCenter.z - halfSize.z || transform.position.z > zoneCenter.z + halfSize.z)
        {
            moveDirection.z = -moveDirection.z;
            needNewDirection = true;
        }

        // Ensure we stay in boundaries
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, zoneCenter.x - halfSize.x, zoneCenter.x + halfSize.x),
            Mathf.Clamp(transform.position.y, zoneCenter.y - halfSize.y, zoneCenter.y + halfSize.y),
            Mathf.Clamp(transform.position.z, zoneCenter.z - halfSize.z, zoneCenter.z + halfSize.z)
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
            Vector3 awayDirection = (transform.position - other.transform.position).normalized;
            transform.position += awayDirection * 0.1f;
            moveDirection = awayDirection;
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