using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple color combo system for moth game
/// </summary>
public class MothColorCombo : MonoBehaviour
{
    [Header("Color Definitions")]
    [SerializeField]
    private List<Color> mothColors = new List<Color> {
        new Color(1.0f, 0.3f, 0.3f),  // Red
        new Color(0.3f, 0.8f, 0.3f),  // Green
        new Color(0.3f, 0.3f, 1.0f),  // Blue
        new Color(1.0f, 1.0f, 0.3f),  // Yellow
        new Color(0.8f, 0.3f, 0.8f)   // Purple
    };

    [Header("Combo Settings")]
    [SerializeField] private int minComboLength = 2;               // Minimum combo length to get any reward
    [SerializeField] private float baseTimeReward = 3.0f;          // Base time reward for minimum combo
    [SerializeField] private float additionalTimePerCombo = 2.0f;  // Additional time per extra moth in combo
    [SerializeField] private float pointsPerCombo = 5.0f;          // Points per combo moth
    [SerializeField] private bool showDebugMessages = true;        // Whether to show debug messages

    // Combo tracking
    private Color currentComboColor;
    private int comboCount = 0;
    private bool isComboActive = false;

    // Reference to main game manager
    private MothGameManager gameManager;

    private void Start()
    {
        gameManager = MothGameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("MothGameManager not found!");
        }
    }

    /// <summary>
    /// Returns a random color from the defined moth colors
    /// </summary>
    public Color GetRandomMothColor()
    {
        return mothColors[Random.Range(0, mothColors.Count)];
    }

    /// <summary>
    /// Process a caught moth and update combo tracking
    /// </summary>
    /// <param name="mothColor">Color of the caught moth</param>
    /// <returns>Bonus points to award for this catch based on combo</returns>
    public int ProcessCaughtMoth(Color mothColor)
    {
        string colorName = ColorToName(mothColor);
        int bonusPoints = 0;

        // Case 1: First moth or different color than current combo
        if (!isComboActive || !ColorMatches(mothColor, currentComboColor))
        {
            // If we had an active combo, finish it first
            if (isComboActive && comboCount >= minComboLength)
            {
                // Calculate rewards
                float timeReward = baseTimeReward + (additionalTimePerCombo * (comboCount - minComboLength));
                bonusPoints = Mathf.RoundToInt(pointsPerCombo * comboCount);

                if (showDebugMessages)
                {
                    Debug.Log($"COMBO COMPLETED: {comboCount}x {ColorToName(currentComboColor)} → " +
                             $"Awarding {timeReward:F1}s and {bonusPoints} points");
                }

                // Award the time
                if (gameManager != null)
                {
                    gameManager.AddTimeReward(timeReward);
                }
            }
            else if (isComboActive)
            {
                // Combo was too short
                if (showDebugMessages)
                {
                    Debug.Log($"Combo too short: {comboCount}x {ColorToName(currentComboColor)} " +
                             $"(min {minComboLength} needed)");
                }
            }

            // Start new combo with this color
            currentComboColor = mothColor;
            comboCount = 1;
            isComboActive = true;

            if (showDebugMessages)
            {
                Debug.Log($"NEW COMBO STARTED: {colorName} (1)");
            }
        }
        // Case 2: Same color as current combo
        else
        {
            // Continue the combo
            comboCount++;

            if (showDebugMessages)
            {
                Debug.Log($"COMBO CONTINUED: {colorName} ({comboCount})");
            }
        }

        // Return bonus points (if any)
        return bonusPoints;
    }

    /// <summary>
    /// Check if two colors match within a tolerance
    /// </summary>
    private bool ColorMatches(Color a, Color b)
    {
        // Simple comparison with small tolerance
        float tolerance = 0.1f;
        return Vector4.Distance(new Vector4(a.r, a.g, a.b, a.a),
                               new Vector4(b.r, b.g, b.b, b.a)) < tolerance;
    }

    /// <summary>
    /// Reset combo tracking (used when game restarts)
    /// </summary>
    public void ResetCombo()
    {
        isComboActive = false;
        comboCount = 0;

        if (showDebugMessages)
        {
            Debug.Log("COMBO SYSTEM RESET");
        }
    }

    /// <summary>
    /// Converts a color to a readable name for debug purposes
    /// </summary>
    private string ColorToName(Color color)
    {
        // Find the closest predefined color
        float minDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < mothColors.Count; i++)
        {
            float distance = Vector4.Distance(
                new Vector4(color.r, color.g, color.b, color.a),
                new Vector4(mothColors[i].r, mothColors[i].g, mothColors[i].b, mothColors[i].a)
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }

        // Return the appropriate name
        switch (closestIndex)
        {
            case 0: return "Red";
            case 1: return "Green";
            case 2: return "Blue";
            case 3: return "Yellow";
            case 4: return "Purple";
            default: return "Unknown";
        }
    }

    /// <summary>
    /// Get the current combo information for debug or display
    /// </summary>
    public (Color color, int count) GetCurrentComboInfo()
    {
        return (currentComboColor, comboCount);
    }
}