using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

    [Header("UI")]
    [SerializeField] private TextMeshPro comboDisplayText; // Changed from TextMeshProUGUI to TextMeshPro

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
        if (comboDisplayText == null)
        {
            Debug.LogWarning("Combo Display Text not assigned in the inspector!");
        }
        else
        {
            comboDisplayText.text = ""; // Initialize text
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
        string newMothColorName = ColorToName(mothColor); // Name of the currently caught moth's color
        int bonusPoints = 0;
        bool comboAwardedThisCatch = false; // Flag to track if KOMBO message was just shown

        // Case 1: First moth, or different color than current combo
        if (!isComboActive || !ColorMatches(mothColor, currentComboColor))
        {
            // Store details of the combo that might be ending
            Color previousActiveComboColor = currentComboColor; 
            string previousActiveComboColorName = isComboActive ? ColorToName(previousActiveComboColor) : "None";

            // If there was an active combo, try to complete it
            if (isComboActive && comboCount >= minComboLength)
            {
                float timeReward = baseTimeReward + (additionalTimePerCombo * (comboCount - minComboLength));
                bonusPoints = Mathf.RoundToInt(pointsPerCombo * comboCount);

                if (showDebugMessages)
                {
                    Debug.Log($"COMBO COMPLETED: {comboCount}x {previousActiveComboColorName} → " +
                             $"Awarding {timeReward:F1}s and {bonusPoints} points");
                }

                if (comboDisplayText != null)
                {
                    comboDisplayText.text = $"KOMBO! +{bonusPoints} bodů";
                    comboDisplayText.color = Color.yellow; // Set KOMBO! text color to yellow
                }
                comboAwardedThisCatch = true;

                if (gameManager != null)
                {
                    gameManager.AddTimeReward(timeReward);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SuccessFeedback");
                }
            }
            else if (isComboActive) // Combo was active but too short
            {
                if (showDebugMessages)
                {
                    Debug.Log($"Combo too short: {comboCount}x {previousActiveComboColorName} " +
                             $"(min {minComboLength} needed)");
                }
                if (comboDisplayText != null)
                {
                    comboDisplayText.text = ""; // Clear text for short/broken combo
                }
            }

            // Start new combo with the newly caught moth
            currentComboColor = mothColor;
            comboCount = 1;
            isComboActive = true;

            if (showDebugMessages)
            {
                Debug.Log($"NEW COMBO STARTED: {newMothColorName} (1)");
            }

            // Update display text:
            // If a combo was just awarded, the "KOMBO!" message is already shown and should persist.
            // Otherwise, show the start of the new combo.
            if (comboDisplayText != null)
            {
                if (!comboAwardedThisCatch)
                {
                    comboDisplayText.text = $"{newMothColorName} x{comboCount}";
                    comboDisplayText.color = currentComboColor; // This is the new moth's color
                }
                // If comboAwardedThisCatch is true, the text is already "KOMBO!" with the previous combo's color.
                // It will be updated by the next moth catch.
            }
        }
        // Case 2: Same color as current combo (continuing existing combo)
        else
        {
            comboCount++;
            if (showDebugMessages)
            {
                Debug.Log($"COMBO CONTINUED: {newMothColorName} ({comboCount})");
            }
            if (comboDisplayText != null)
            {
                comboDisplayText.text = $"{newMothColorName} x{comboCount}";
                comboDisplayText.color = currentComboColor; 
            }
        }
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
        if (comboDisplayText != null)
        {
            comboDisplayText.text = ""; // Clear UI text on reset
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

        // Return the appropriate name in Czech
        switch (closestIndex)
        {
            case 0: return "Červená"; // Red
            case 1: return "Zelená";  // Green
            case 2: return "Modrá";   // Blue
            case 3: return "Žlutá";   // Yellow
            case 4: return "Fialová"; // Purple
            default: return "Neznámá"; // Unknown
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