using UnityEngine;
using Mirror;

public class Obstacle_NewGame : MonoBehaviour
{
    private float fallSpeed;

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
        if (!NetworkServer.active) return;

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
        if (!NetworkServer.active) return;

        if (collision.gameObject.CompareTag("Player"))
        {
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

            // End the game
            if (NewGameManager.Instance != null)
            {
                NewGameManager.Instance.EndGame();
            }
        }
    }
}