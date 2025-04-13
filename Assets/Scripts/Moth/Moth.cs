using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Moth : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float baseMovementSpeed = 0.3f;
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float minDistanceFromPlayer = 0.5f;
    [SerializeField] private float maxDistanceFromPlayer = 1.5f;

    [Header("Flying Area")]
    [SerializeField] private float flyingHeight = 0.8f;  // Controls how high/low the moth can fly (0.8 = 0.4 up and 0.4 down)
    [SerializeField] private float flyingWidth = 1.0f;   // Controls how far left/right the moth can fly (1.0 = 0.5 left and 0.5 right)

    // Current movement parameters
    private Vector3 targetPosition;
    private float currentSpeed;
    private float difficultyMultiplier = 1.0f;
    private float nextTargetChangeTime;
    private bool hasStartedMoving = false;

    // Events
    [HideInInspector] public UnityEvent<Moth> OnMothCaught = new UnityEvent<Moth>();

    public void Initialize(float difficulty)
    {
        try
        {
            difficultyMultiplier = difficulty;
            currentSpeed = baseMovementSpeed * difficultyMultiplier;

            // Force movement to start immediately
            StartCoroutine(ForceStartMovement());

            // Ensure the collider is properly set up
            if (GetComponent<Collider>() == null)
            {
                SphereCollider collider = gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.3f;
                if (debugMode) Debug.Log("Moth: Added collider");
            }

            if (debugMode) Debug.Log($"Moth: Initialized with difficulty {difficulty}, speed {currentSpeed}");
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in Initialize: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private IEnumerator ForceStartMovement()
    {
        // Wait a frame to ensure everything is set up
        yield return null;

        // Force a new target position
        GetNewTargetPosition();

        // Explicitly set that we've started moving
        hasStartedMoving = true;

        if (debugMode) Debug.Log("Moth: Forced movement start. Target: " + targetPosition);
    }

    private void Update()
    {
        if (!isServer) return;

        try
        {
            // Check if it's time to change target
            if (Time.time > nextTargetChangeTime)
            {
                GetNewTargetPosition();
            }

            // If we don't have a target yet, get one
            if (targetPosition == Vector3.zero || !hasStartedMoving)
            {
                GetNewTargetPosition();
                hasStartedMoving = true;
            }

            // Move toward target position
            if (targetPosition != Vector3.zero)
            {
                // Direction to target
                Vector3 directionToTarget = (targetPosition - transform.position).normalized;

                // Move the moth
                float actualSpeed = currentSpeed; // Use member variable for consistency
                Vector3 movement = directionToTarget * actualSpeed * Time.deltaTime;
                transform.position += movement;

                if (debugMode && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"Moth: Moving at speed {actualSpeed}. Movement this frame: {movement.magnitude}");
                }

                // Look in the movement direction
                transform.forward = directionToTarget;

                // If we're close to target, pick a new target
                if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                {
                    if (debugMode) Debug.Log("Moth: Reached target, getting new target");
                    GetNewTargetPosition();
                }

                // Add a little rotation for visual interest
                transform.Rotate(0, Time.deltaTime * 90f, 0, Space.Self);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in Update: " + e.Message);
        }
    }

    private void GetNewTargetPosition()
    {
        try
        {
            // Get camera
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (debugMode) Debug.LogWarning("Moth: Camera.main is null, using fallback position");
                targetPosition = new Vector3(UnityEngine.Random.Range(-1f, 1f), 1.5f, 1f);
                nextTargetChangeTime = Time.time + 3f;
                return;
            }

            // Get camera position
            Vector3 cameraPosition = mainCamera.transform.position;

            // Fixed forward direction (slightly upward)
            Vector3 fixedForward = Vector3.forward;
            fixedForward.y = 0.4f; // Slight upward tilt
            fixedForward.Normalize();

            // Random distance in front of camera
            float distance = UnityEngine.Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);

            // Random vertical offset using flyingHeight
            float yOffset = UnityEngine.Random.Range(-flyingHeight/2f, flyingHeight/2f);

            // Random horizontal offset using flyingWidth
            float xOffset = UnityEngine.Random.Range(-flyingWidth/2f, flyingWidth/2f);

            // Calculate target position
            targetPosition = cameraPosition + fixedForward * distance;
            targetPosition.y += yOffset;
            targetPosition.x += xOffset;

            // Ensure minimum height
            if (targetPosition.y < 0.5f)
            {
                targetPosition.y = 0.5f;
            }

            // Set time for next target change
            nextTargetChangeTime = Time.time + UnityEngine.Random.Range(2f, 4f);

            if (debugMode) Debug.Log($"Moth: New target position: {targetPosition}. Next change at: {nextTargetChangeTime}");
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in GetNewTargetPosition: " + e.Message);
            // Use a safe fallback position
            targetPosition = new Vector3(transform.position.x + 0.5f, 1.5f, transform.position.z + 0.5f);
            nextTargetChangeTime = Time.time + 2f;
        }
    }

    public void Respawn()
    {
        if (!isServer) return;

        try
        {
            if (debugMode) Debug.Log("Moth: Respawning");

            // Get a new position
            GetNewTargetPosition();

            // Immediately move to a position halfway to the target to reset
            Vector3 midPoint = Vector3.Lerp(Camera.main.transform.position, targetPosition, 0.5f);
            transform.position = midPoint;

            // Force immediate movement
            hasStartedMoving = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in Respawn: " + e.Message);
        }
    }

    public void SetDifficulty(float difficulty)
    {
        try
        {
            difficultyMultiplier = difficulty;
            currentSpeed = baseMovementSpeed * difficultyMultiplier;
            if (debugMode) Debug.Log($"Moth: Difficulty set to {difficulty}, speed is now {currentSpeed}");
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in SetDifficulty: " + e.Message);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;

        try
        {
            if (debugMode) Debug.Log($"Moth: Triggered by {other.gameObject.name} with tag {other.tag}");

            // Notify the game manager that the moth was caught
            OnMothCaught.Invoke(this);
        }
        catch (Exception e)
        {
            Debug.LogError("Moth: Error in OnTriggerEnter: " + e.Message);
        }
    }
}