using UnityEngine;
using Mirror; // Ensure Mirror is included if using networking

public class Obstacle_NewGame : NetworkBehaviour
{
    [SerializeField] private float fallSpeed = 5f; // Speed at which the obstacle falls

    private void Update()
    {
        if (!isServer) return;

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
        if (!isServer) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Obstacle collided with Player.");
            // End the game via NewGameManager
            if (NewGameManager.Instance != null)
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
                NewGameManager.Instance.EndGame();
            }
            else
            {
                Debug.LogError("NewGameManager instance is null.");
            }
        }
    }
}