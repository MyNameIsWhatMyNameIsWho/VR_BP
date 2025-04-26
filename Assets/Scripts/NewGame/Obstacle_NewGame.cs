using UnityEngine;
using Mirror;
using System.Collections;

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

    [Header("Visual Effects")]
    [SerializeField] private GameObject balloonPopEffect; // Assign in inspector

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

            // Get the character/balloon reference
            Character_NewGame character = collision.gameObject.GetComponent<Character_NewGame>();
            if (character != null)
            {
                // Get the center position of the balloon for the effect
                // This is better than using the collision point which might be at the edge
                Vector3 balloonCenter = character.transform.position;

                // If balloonPopEffect is a NetworkVisualEffect prefab, use it directly
                if (balloonPopEffect != null && balloonPopEffect.TryGetComponent<NetworkVisualEffect>(out var _))
                {
                    // Spawn the effect at the center of the balloon
                    NetworkVisualEffect vfx = Instantiate(balloonPopEffect, balloonCenter, Quaternion.identity)
                        .GetComponent<NetworkVisualEffect>();

                    NetworkServer.Spawn(vfx.gameObject);
                    vfx.Play();

                    Debug.Log($"Spawned balloon pop effect at balloon center: {balloonCenter}");
                }

                // Hide all renderers on the balloon
                HideBalloon(character.gameObject);
            }
            else
            {
                Debug.LogError("Could not find Character_NewGame component on collided object!");
            }

            // Play balloon pop sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("BalloonPop");
            }

            // End the game via NewGameManager - with a small delay to let particle effect play
            if (NewGameManager.Instance != null)
            {
                StartCoroutine(DelayedGameEnd(0.2f));
            }
        }
    }

    // Helper to hide balloon renderers
    private void HideBalloon(GameObject balloon)
    {
        // Hide all renderers in the balloon and its children
        Renderer[] renderers = balloon.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }
    }

    private IEnumerator DelayedGameEnd(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NewGameManager.Instance != null)
        {
            NewGameManager.Instance.EndGame();
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
}