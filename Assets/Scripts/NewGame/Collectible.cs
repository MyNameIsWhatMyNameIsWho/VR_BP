using UnityEngine;
using Mirror;

public class Collectible : MonoBehaviour
{
    [SerializeField] private int pointValue = 10;
    [SerializeField] private GameObject collectionEffect;

    private float fallSpeed;

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
        if (!NetworkServer.active) return;

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
        if (!NetworkServer.active) return;

        if (other.CompareTag("Player"))
        {
            // Play collection effect if available
            if (collectionEffect != null)
            {
                GameObject effect = Instantiate(collectionEffect, transform.position, Quaternion.identity);
                NetworkServer.Spawn(effect);
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
            NetworkServer.Destroy(gameObject);
        }
    }
}