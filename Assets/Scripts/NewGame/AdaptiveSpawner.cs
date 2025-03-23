using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Handles adaptive spawning of game objects based on player behavior
/// and specifically targets areas where collectibles are missed
/// </summary>
public class AdaptiveSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NewGameManager gameManager;
    [SerializeField] private Character_NewGame character;

    [Header("Adaptive Parameters")]
    [SerializeField] private int historyLength = 20; // How many player positions to remember
    [SerializeField] private float adaptiveRate = 0.1f; // How quickly to adapt (0-1)
    [SerializeField] private float obstaclePlayerOffset = 0.5f; // How far above player to spawn obstacles
    [SerializeField] private float initialRandomness = 1.0f; // Initial random distribution (0-1)
    [SerializeField] private float minRandomness = 0.3f; // Minimum randomness to maintain
    [SerializeField] private float leftSkewThreshold = 0.4f; // X position below which we consider "left side" 
    [SerializeField] private float rightSkewThreshold = 0.6f; // X position above which we consider "right side"
    [SerializeField] private int missedItemsBeforeStrongBias = 3; // Number of missed items before strongly biasing spawns

    // Tracking variables
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private float leftSideTime = 0f;
    private float rightSideTime = 0f;
    private float centerTime = 0f;
    private float totalTime = 0f;
    private float leftSpawnBias = 0.5f; // 0 = spawn right, 1 = spawn left
    private float randomnessFactor;

    // Track missed collectibles
    private Queue<Vector3> missedCollectiblePositions = new Queue<Vector3>();
    private int consecutiveLeftMisses = 0;
    private int consecutiveRightMisses = 0;
    private float lastCollectibleSpawnX = 0;
    private bool lastCollectibleWasCollected = true;

    // Progressive difficulty escalation
    private float progressiveDifficultyFactor = 0f; // 0 to 1, increases as game goes on
    private float maxProgressiveDifficulty = 0.8f; // Max progressive difficulty factor
    private float progressionRate = 0.01f; // How quickly difficulty increases

    // Normalized range values
    private float normalizedMinX;
    private float normalizedMaxX;

    private void Start()
    {
        if (!isServer) return;

        randomnessFactor = initialRandomness;

        // Get game boundaries and normalize them to 0-1 range for easier calculations
        normalizedMinX = gameManager.GetMinX() + gameManager.GetWallBuffer();
        normalizedMaxX = gameManager.GetMaxX() - gameManager.GetWallBuffer();
    }

    private void Update()
    {
        if (!isServer || !gameManager.IsGameRunning()) return;

        // Update position history
        UpdatePositionHistory();

        // Update time spent in different regions
        UpdateRegionTimes();

        // Update spawn bias based on player behavior
        UpdateSpawnBias();

        // Update progressive difficulty
        progressiveDifficultyFactor = Mathf.Min(progressiveDifficultyFactor + progressionRate * Time.deltaTime, maxProgressiveDifficulty);
    }

    /// <summary>
    /// Updates the queue of recent player positions
    /// </summary>
    private void UpdatePositionHistory()
    {
        // Add current position to history
        positionHistory.Enqueue(character.transform.position);

        // Keep history at desired length
        while (positionHistory.Count > historyLength)
        {
            positionHistory.Dequeue();
        }
    }

    /// <summary>
    /// Updates tracking of how much time is spent in each region
    /// </summary>
    private void UpdateRegionTimes()
    {
        Vector3 currentPos = character.transform.position;
        float totalRange = normalizedMaxX - normalizedMinX;
        float normalizedPos = (currentPos.x - normalizedMinX) / totalRange; // 0-1 range

        // Update time counters
        float deltaTime = Time.deltaTime;
        totalTime += deltaTime;

        if (normalizedPos < leftSkewThreshold)
        {
            leftSideTime += deltaTime;
        }
        else if (normalizedPos > rightSkewThreshold)
        {
            rightSideTime += deltaTime;
        }
        else
        {
            centerTime += deltaTime;
        }

        // Reset counters if they get too large to prevent overflow
        if (totalTime > 1000f)
        {
            float scaleFactor = 0.5f;
            leftSideTime *= scaleFactor;
            rightSideTime *= scaleFactor;
            centerTime *= scaleFactor;
            totalTime *= scaleFactor;
        }
    }

    /// <summary>
    /// Updates the spawn bias based on where the player spends time
    /// and missed collectibles
    /// </summary>
    private void UpdateSpawnBias()
    {
        if (totalTime < 1f) return; // Need some data first

        float leftRatio = leftSideTime / totalTime;
        float rightRatio = rightSideTime / totalTime;

        // Factor in missed collectibles more heavily
        if (consecutiveLeftMisses >= missedItemsBeforeStrongBias)
        {
            // Player is consistently missing left side collectibles
            // Strong bias toward left side
            leftSpawnBias = Mathf.Lerp(leftSpawnBias, 0.8f, adaptiveRate * 2f * Time.deltaTime);
            randomnessFactor = Mathf.Lerp(randomnessFactor, minRandomness, adaptiveRate * Time.deltaTime);
        }
        else if (consecutiveRightMisses >= missedItemsBeforeStrongBias)
        {
            // Player is consistently missing right side collectibles
            // Strong bias toward right side
            leftSpawnBias = Mathf.Lerp(leftSpawnBias, 0.2f, adaptiveRate * 2f * Time.deltaTime);
            randomnessFactor = Mathf.Lerp(randomnessFactor, minRandomness, adaptiveRate * Time.deltaTime);
        }
        // Regular time-based bias as a fallback
        else if (leftRatio < rightRatio * 0.7f) // Player struggles with left
        {
            leftSpawnBias = Mathf.Lerp(leftSpawnBias, 0.8f, adaptiveRate * Time.deltaTime);
            randomnessFactor = Mathf.Lerp(randomnessFactor, minRandomness, adaptiveRate * Time.deltaTime);
        }
        else if (rightRatio < leftRatio * 0.7f) // Player struggles with right
        {
            leftSpawnBias = Mathf.Lerp(leftSpawnBias, 0.2f, adaptiveRate * Time.deltaTime);
            randomnessFactor = Mathf.Lerp(randomnessFactor, minRandomness, adaptiveRate * Time.deltaTime);
        }
        // If player moves evenly, gradually return to balanced spawning
        else
        {
            leftSpawnBias = Mathf.Lerp(leftSpawnBias, 0.5f, adaptiveRate * 0.5f * Time.deltaTime);
            randomnessFactor = Mathf.Lerp(randomnessFactor, initialRandomness, adaptiveRate * 0.5f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Generates a spawn position for an obstacle based on current player position
    /// </summary>
    public Vector3 GetObstacleSpawnPosition(float spawnHeight)
    {
        Vector3 playerPos = character.transform.position;

        // Get position directly above player, with small random offset
        // Use obstaclePlayerOffset to control how much the obstacle can deviate from player position
        float xPos = playerPos.x + Random.Range(-obstaclePlayerOffset, obstaclePlayerOffset) * randomnessFactor;
        xPos = Mathf.Clamp(xPos, normalizedMinX, normalizedMaxX);

        return new Vector3(xPos, spawnHeight, playerPos.z);
    }

    /// <summary>
    /// Generates a spawn position for a collectible based on player behavior
    /// and missed collectibles
    /// </summary>
    public Vector3 GetCollectibleSpawnPosition(float spawnHeight)
    {
        float totalRange = normalizedMaxX - normalizedMinX;
        float xPos;

        // Record the previous collectible as missed if it wasn't marked as collected
        if (!lastCollectibleWasCollected && lastCollectibleSpawnX != 0)
        {
            RecordMissedCollectible(lastCollectibleSpawnX);
        }

        // Set up for tracking this collectible
        lastCollectibleWasCollected = false;

        // Progressive logic - occasionally return to center or opposite side
        // to check if player has improved
        bool doProgressiveCheck = (Random.value < 0.2f);

        // Occasionally test the opposite side to see if player has improved there
        if (doProgressiveCheck)
        {
            // If we've been spawning on left, spawn on right to check skill
            if (leftSpawnBias > 0.7f)
            {
                float rightPos = normalizedMinX + totalRange * 0.75f; // Toward right
                xPos = rightPos + Random.Range(-0.5f, 0.5f) * totalRange * 0.2f;
            }
            // If we've been spawning on right, spawn on left to check skill
            else if (leftSpawnBias < 0.3f)
            {
                float leftPos = normalizedMinX + totalRange * 0.25f; // Toward left
                xPos = leftPos + Random.Range(-0.5f, 0.5f) * totalRange * 0.2f;
            }
            // Otherwise, pick a truly random position
            else
            {
                xPos = Random.Range(normalizedMinX, normalizedMaxX);
            }
        }
        // Use the bias for normal spawning
        else
        {
            // Biased random based on player behavior
            float bias = leftSpawnBias; // 0.5 = neutral, >0.5 = left bias, <0.5 = right bias

            // Progressive intensity based on how long the game has been running
            float intensity = Mathf.Lerp(0.3f, 0.7f, progressiveDifficultyFactor);

            // Apply the bias more strongly as the game progresses
            if (bias > 0.5f) // Left bias
            {
                bias = Mathf.Lerp(0.5f, bias, intensity);
            }
            else if (bias < 0.5f) // Right bias
            {
                bias = Mathf.Lerp(0.5f, bias, intensity);
            }

            // Calculate position with added randomness
            float basePosition = normalizedMinX + totalRange * bias;
            float randomOffset = (Random.value - 0.5f) * totalRange * 0.3f * randomnessFactor;
            xPos = basePosition + randomOffset;
        }

        // Ensure within bounds
        xPos = Mathf.Clamp(xPos, normalizedMinX, normalizedMaxX);

        // Save this position for tracking
        lastCollectibleSpawnX = xPos;

        return new Vector3(xPos, spawnHeight, character.transform.position.z);
    }

    /// <summary>
    /// Records a collectible as collected
    /// </summary>
    public void MarkLastCollectibleCollected()
    {
        lastCollectibleWasCollected = true;

        // Reset consecutive misses counters
        float normalizedX = (lastCollectibleSpawnX - normalizedMinX) / (normalizedMaxX - normalizedMinX);

        if (normalizedX < 0.5f) // Left side
        {
            consecutiveLeftMisses = 0;
        }
        else // Right side
        {
            consecutiveRightMisses = 0;
        }
    }

    /// <summary>
    /// Records the position of a missed collectible
    /// </summary>
    private void RecordMissedCollectible(float xPos)
    {
        // Add to missed collectibles queue
        missedCollectiblePositions.Enqueue(new Vector3(xPos, 0, 0));

        // Keep history at a reasonable length
        while (missedCollectiblePositions.Count > historyLength)
        {
            missedCollectiblePositions.Dequeue();
        }

        // Calculate normalized position to determine which side it was on
        float normalizedX = (xPos - normalizedMinX) / (normalizedMaxX - normalizedMinX);

        // Update consecutive misses counters
        if (normalizedX < 0.5f) // Left side
        {
            consecutiveLeftMisses++;
            consecutiveRightMisses = 0; // Reset opposite side counter
        }
        else // Right side
        {
            consecutiveRightMisses++;
            consecutiveLeftMisses = 0; // Reset opposite side counter
        }
    }

    // Debugging helpers
    public float GetLeftSpawnBias()
    {
        return leftSpawnBias;
    }

    public float GetRandomnessFactor()
    {
        return randomnessFactor;
    }

    public int GetConsecutiveLeftMisses()
    {
        return consecutiveLeftMisses;
    }

    public int GetConsecutiveRightMisses()
    {
        return consecutiveRightMisses;
    }
}