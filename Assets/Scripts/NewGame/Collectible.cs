using UnityEngine;
using Mirror;

public class Collectible : NetworkBehaviour
{
    [SerializeField] private int pointValue = 10;
    [SerializeField] private GameObject collectionEffect;

    private float fallSpeed;
    private bool isCollected = false;

    public void Initialize()
    {
        // Get the speed from the central manager - the ONLY place speeds are defined
        if (NewGameManager.Instance != null)
        {
            fallSpeed = NewGameManager.Instance.CollectibleFallSpeed;
        }
        else
        {
            fallSpeed = 1.2f; // Fallback default only used if something goes wrong
            Debug.LogError("NewGameManager.Instance is null in Collectible Initialize!");
        }
    }

    private void Update()
    {
        if (!isServer || isCollected) return;

        // Move the collectible downward
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

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

                // Auto-destroy the effect after a short delay
                Destroy(effect, 2f);
            }

            // Add points to score via NewGameManager
            if (NewGameManager.Instance != null)
            {
                NewGameManager.Instance.CollectItem(pointValue);
            }

            // Disable the collectible and destroy it
            GetComponent<Collider>().enabled = false;
            GetComponent<Renderer>().enabled = false;

            // Delay actual destruction slightly to ensure effect spawns properly
            Invoke(nameof(DestroyCollectible), 0.1f);
        }
    }

    private void DestroyCollectible()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }
}