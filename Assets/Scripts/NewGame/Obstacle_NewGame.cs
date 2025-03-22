using UnityEngine;
using Mirror;

public class Obstacle_NewGame : NetworkBehaviour
{
    private float fallSpeed;
    private bool hasCollided = false;

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
    }

    private void Update()
    {
        if (!isServer || hasCollided) return;

        // Move the obstacle downward
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

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
            Debug.Log("Obstacle collided with Player.");

            // Mark as collided to prevent further movement and multiple collisions
            hasCollided = true;

            // Disable the collider immediately to prevent jittering
            GetComponent<Collider>().enabled = false;

            // Freeze the obstacle in place
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // End the game via NewGameManager
            if (NewGameManager.Instance != null)
            {
                NewGameManager.Instance.EndGame();
            }
            else
            {
                Debug.LogError("NewGameManager instance is null on collision.");
            }
        }
    }
}