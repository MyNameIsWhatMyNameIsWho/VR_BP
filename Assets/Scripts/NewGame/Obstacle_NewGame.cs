using UnityEngine;
using Mirror; // Ensure Mirror is included if using networking

public class Obstacle_NewGame : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Obstacle collided with Player.");
            // End the game via NewGameManager
            NewGameManager.Instance.EndGame();
            /*if (NewGameManager.Instance != null)
            {
                
            }
            else
            {
                Debug.LogError("NewGameManager instance is null.");
            }*/
        }
    }
}
