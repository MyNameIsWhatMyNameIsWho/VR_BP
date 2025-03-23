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
    //[SerializeField] private float leftSkewThreshold = 0.4f; // X position below which we consider "left side" 
    //[SerializeField] private float rightSkewThreshold = 0.6f; // X position above which we consider "right side"
    [SerializeField] private int missedItemsBeforeStrongBias = 1; // Number of missed items before strongly biasing spawns
    [SerializeField] private float spawnRandomness = 0.08f; // How much randomness to add to spawns (0-1)
    [SerializeField] private float problemAreaProbability = 0.85f; // Probability to spawn in problem area (0-1)
    [SerializeField] private float problemAreaExtraProbabilityPerMiss = 0.05f; // Extra probability per consecutive miss
    [SerializeField] private int maxConsecutiveMissesTracked = 5; // Cap for increasing difficulty

    // Collection tracking
    private class CollectibleInfo
    {
        public float xPosition;
        public bool isCollected;
        public bool isOutOfBounds;
    }
    private List<CollectibleInfo> activeCollectibles = new List<CollectibleInfo>();

    // Normalized range values
    private float normalizedMinX;
    private float normalizedMaxX;
    private float wallBuffer;

    // Tracking variables
    private int consecutiveLeftMisses = 0;
    private int consecutiveRightMisses = 0;

    // Progressive difficulty escalation
    private float progressiveDifficultyFactor = 0f; // 0 to 1, increases as game goes on
    private float maxProgressiveDifficulty = 0.8f; // Max progressive difficulty factor
    private float progressionRate = 0.01f; // How quickly difficulty increases

    private void Start()
    {
        if (!isServer) return;

        // Get game boundaries and normalize them to 0-1 range for easier calculations
        normalizedMinX = gameManager.GetMinX();
        normalizedMaxX = gameManager.GetMaxX();
        wallBuffer = gameManager.GetWallBuffer();
    }

    private void Update()
    {
        if (!isServer || !gameManager.IsGameRunning()) return;

        // Update progressive difficulty
        progressiveDifficultyFactor = Mathf.Min(progressiveDifficultyFactor + progressionRate * Time.deltaTime, maxProgressiveDifficulty);

        // Check for collectibles that have gone out of bounds
        //ProcessOutOfBoundsCollectibles();
    }

    

    /// <summary>
    /// Generates a spawn position for an obstacle based on current player position
    /// </summary>
    public Vector3 GetObstacleSpawnPosition(float spawnHeight)
    {
        // Simply spawn obstacles directly above the player with a small random offset
        // This forces player movement and reaction
        Vector3 playerPos = character.transform.position;
        float obstacleOffset = 0.5f; // How much obstacles can deviate from player position

        float xPos = playerPos.x + Random.Range(-obstacleOffset, obstacleOffset);
        xPos = Mathf.Clamp(xPos, normalizedMinX + wallBuffer, normalizedMaxX - wallBuffer);

        return new Vector3(xPos, spawnHeight, playerPos.z);
    }

    /// <summary>
    /// Generates a spawn position for a collectible based on player behavior
    /// and missed collectibles, with progressive adaptation for patients with limited mobility
    /// </summary>
    public Vector3 GetCollectibleSpawnPosition(float spawnHeight)
    {
        float xPos;
        float totalRange = normalizedMaxX - normalizedMinX;

        // Simple probability-based spawning
        float rand = Random.value;

        // Detect if there's a problem side that the patient struggles with
        bool leftSideIsStruggle = consecutiveLeftMisses > consecutiveRightMisses &&
                                  consecutiveLeftMisses >= missedItemsBeforeStrongBias;
        bool rightSideIsStruggle = consecutiveRightMisses > consecutiveLeftMisses &&
                                   consecutiveRightMisses >= missedItemsBeforeStrongBias;

        // If patient struggles with a side, progressively increase probability of spawning there
        if (leftSideIsStruggle || rightSideIsStruggle)
        {
            // Calculate progressive bias based on number of consecutive misses
            float missCount = leftSideIsStruggle ? consecutiveLeftMisses : consecutiveRightMisses;
            float cappedMissCount = Mathf.Min(missCount, maxConsecutiveMissesTracked);

            // Calculate adaptive probability - starts at base value and increases with consecutive misses
            float adaptiveProbability = problemAreaProbability +
                                       (cappedMissCount - 1) * problemAreaExtraProbabilityPerMiss;

            // Cap to reasonable maximum (95%)
            adaptiveProbability = Mathf.Min(adaptiveProbability, 0.95f);

            if (leftSideIsStruggle)
            {
                // Strong bias to left side (problem area) with progressive intensity
                if (rand < adaptiveProbability)
                {
                    // Use progressive bias for how far left to go
                    // More misses = further to the left side
                    float leftBias = 0.25f - (cappedMissCount - 1) * 0.03f; // 0.25 to 0.1 based on misses
                    leftBias = Mathf.Max(leftBias, 0.1f); // Don't go too close to edge

                    xPos = normalizedMinX + totalRange * leftBias;
                    Debug.Log($"Spawning on LEFT side (strong bias: {adaptiveProbability:F2}) at position {leftBias:F2}");
                }
                // Small chance to spawn in center for progression
                else if (rand < adaptiveProbability + ((1f - adaptiveProbability) * 0.7f))
                {
                    xPos = normalizedMinX + totalRange * 0.5f; // Center
                    Debug.Log("Spawning in CENTER as bridge from left side struggles");
                }
                // Smallest chance to spawn on opposite side to check if patient can reach
                else
                {
                    xPos = normalizedMinX + totalRange * 0.75f; // Right
                    Debug.Log("Testing patient RIGHT side reach");
                }
            }
            else // Right side struggle
            {
                // Strong bias to right side (problem area) with progressive intensity
                if (rand < adaptiveProbability)
                {
                    // Use progressive bias for how far right to go
                    // More misses = further to the right side
                    float rightBias = 0.75f + (cappedMissCount - 1) * 0.03f; // 0.75 to 0.9 based on misses
                    rightBias = Mathf.Min(rightBias, 0.9f); // Don't go too close to edge

                    xPos = normalizedMinX + totalRange * rightBias;
                    Debug.Log($"Spawning on RIGHT side (strong bias: {adaptiveProbability:F2}) at position {rightBias:F2}");
                }
                // Small chance to spawn in center for progression
                else if (rand < adaptiveProbability + ((1f - adaptiveProbability) * 0.7f))
                {
                    xPos = normalizedMinX + totalRange * 0.5f; // Center
                    Debug.Log("Spawning in CENTER as bridge from right side struggles");
                }
                // Smallest chance to spawn on opposite side to check if patient can reach
                else
                {
                    xPos = normalizedMinX + totalRange * 0.25f; // Left
                    Debug.Log("Testing patient LEFT side reach");
                }
            }
        }
        // No strong side bias detected yet - distribute evenly but avoid repeating same area
        else
        {
            // Get the previous spawn position if we have active collectibles
            float lastX = 0.5f; // Default to center
            if (activeCollectibles.Count > 0)
            {
                lastX = (activeCollectibles[activeCollectibles.Count - 1].xPosition - normalizedMinX) / totalRange;
            }

            // Basic random distribution across the play area
            // But avoid spawning multiple collectibles in same area to encourage movement
            if (lastX < 0.33f)
            {
                // Last one was on left, so prefer center or right
                xPos = normalizedMinX + totalRange * (0.4f + Random.value * 0.6f);
                Debug.Log("Last was LEFT, now spawning center/right");
            }
            else if (lastX > 0.66f)
            {
                // Last one was on right, so prefer left or center
                xPos = normalizedMinX + totalRange * Random.value * 0.6f;
                Debug.Log("Last was RIGHT, now spawning left/center");
            }
            else
            {
                // Last one was in center, so go to either side
                xPos = Random.value < 0.5f ?
                    normalizedMinX + totalRange * Random.value * 0.3f : // Left side
                    normalizedMinX + totalRange * (0.7f + Random.value * 0.3f); // Right side
                Debug.Log("Last was CENTER, now spawning on a side");
            }
        }

        // Add a little randomness based on spawnRandomness parameter
        // This prevents the spawns from becoming too predictable
        xPos += Random.Range(-totalRange * spawnRandomness, totalRange * spawnRandomness);

        // Ensure within bounds
        xPos = Mathf.Clamp(xPos, normalizedMinX + wallBuffer, normalizedMaxX - wallBuffer);

        // Track this collectible
        var collectibleInfo = new CollectibleInfo
        {
            xPosition = xPos,
            isCollected = false,
            isOutOfBounds = false
        };
        activeCollectibles.Add(collectibleInfo);

        return new Vector3(xPos, spawnHeight, character.transform.position.z);
    }

    /// <summary>
    /// Records a collectible as collected
    /// </summary>
    public void MarkCollectibleCollected(Vector3 collectiblePosition)
    {
        // Find the collectible in our tracking list with some tolerance for floating point differences
        float matchTolerance = 0.1f; // Increase this if matching still fails
        int foundIndex = -1;

        Debug.Log($"Trying to mark collected at {collectiblePosition.x}, tracking {activeCollectibles.Count} items");

        for (int i = 0; i < activeCollectibles.Count; i++)
        {
            float distance = Mathf.Abs(activeCollectibles[i].xPosition - collectiblePosition.x);
            if (distance < matchTolerance)
            {
                foundIndex = i;
                Debug.Log($"Found matching collectible at index {i} with distance {distance}");
                break;
            }
        }

        if (foundIndex >= 0)
        {
            // Mark it as collected
            activeCollectibles[foundIndex].isCollected = true;
            Debug.Log($"Marked collectible collected at position {collectiblePosition.x}");

            // Reset consecutive misses counter for that side
            float normalizedX = (collectiblePosition.x - normalizedMinX) / (normalizedMaxX - normalizedMinX);
            if (normalizedX < 0.5f) // Left side
            {
                consecutiveLeftMisses = 0;
                Debug.Log("Reset left side consecutive misses");
            }
            else // Right side
            {
                consecutiveRightMisses = 0;
                Debug.Log("Reset right side consecutive misses");
            }
        }
        else
        {
            Debug.LogWarning($"Could not find matching collectible for position {collectiblePosition.x}");
        }

        // Cleanup collected items from the list
        activeCollectibles.RemoveAll(c => c.isCollected);
    }

    /// <summary>
    /// Called from outside when a collectible is destroyed without being collected
    /// </summary>
    public void NotifyCollectibleDestroyed(Vector3 collectiblePosition, bool wasCollected)
    {
        float matchTolerance = 0.1f;
        int foundIndex = -1;

        for (int i = activeCollectibles.Count - 1; i >= 0; i--)
        {
            float distance = Mathf.Abs(activeCollectibles[i].xPosition - collectiblePosition.x);
            if (distance < matchTolerance)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex >= 0)
        {
            var collectible = activeCollectibles[foundIndex];

            // If it wasn't collected and not already marked as out of bounds, record it as missed
            if (!wasCollected && !collectible.isOutOfBounds)
            {
                RecordMissedCollectible(collectible.xPosition);
                Debug.Log($"Recorded miss for collectible at position {collectiblePosition.x}");
            }

            // Remove from tracking
            activeCollectibles.RemoveAt(foundIndex);
        }
        else
        {
            Debug.LogWarning($"Could not find collectible to destroy at position {collectiblePosition.x}");
        }
    }

    /// <summary>
    /// Records the position of a missed collectible and updates the adaptive behavior
    /// </summary>
    private void RecordMissedCollectible(float xPos)
    {
        // Calculate normalized position to determine which side it was on
        float normalizedX = (xPos - normalizedMinX) / (normalizedMaxX - normalizedMinX);

        // For therapy patients, we want to track each side independently
        // so we don't reset the other side's counter
        if (normalizedX < 0.5f) // Left side
        {
            consecutiveLeftMisses++;
            // Only reset right counter if they weren't struggling there too
            if (consecutiveRightMisses < missedItemsBeforeStrongBias)
            {
                consecutiveRightMisses = 0;
            }
        }
        else // Right side
        {
            consecutiveRightMisses++;
            // Only reset left counter if they weren't struggling there too
            if (consecutiveLeftMisses < missedItemsBeforeStrongBias)
            {
                consecutiveLeftMisses = 0;
            }
        }

        // Cap the consecutive misses to prevent extreme difficulty
        consecutiveLeftMisses = Mathf.Min(consecutiveLeftMisses, maxConsecutiveMissesTracked + 2);
        consecutiveRightMisses = Mathf.Min(consecutiveRightMisses, maxConsecutiveMissesTracked + 2);

        Debug.Log($"Missed collectible at x={xPos:F2}, Left misses: {consecutiveLeftMisses}, Right misses: {consecutiveRightMisses}");
    }

    /// <summary>
    /// Clear all tracking when the game ends or restarts
    /// </summary>
    public void ResetTracking()
    {
        activeCollectibles.Clear();
        consecutiveLeftMisses = 0;
        consecutiveRightMisses = 0;
        progressiveDifficultyFactor = 0f;
    }

    // Debugging helpers
    public int GetConsecutiveLeftMisses()
    {
        return consecutiveLeftMisses;
    }

    public int GetConsecutiveRightMisses()
    {
        return consecutiveRightMisses;
    }
}