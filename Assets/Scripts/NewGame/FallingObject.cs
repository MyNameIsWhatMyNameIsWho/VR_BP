using UnityEngine;
using Mirror;

/// <summary>
/// Base class for all falling objects in the game (obstacles and collectibles)
/// </summary>
public abstract class FallingObject : NetworkBehaviour
{
    [SerializeField] protected float fallSpeed = 0.8f;

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

    protected virtual void Update()
    {
        if (!isServer) return;

        // Move the object downward
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime);

        // Destroy if it goes out of view
        if (transform.position.y < -10f)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    // Children will implement their own collision behavior
    protected abstract void HandlePlayerContact();
}