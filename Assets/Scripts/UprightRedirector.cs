using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles upright redirection of the VR player in a scene
/// </summary>
public class UprightRedirector : NetworkBehaviour
{
    public static UprightRedirector Instance;

    [SerializeField] private Transform playerCurrentCameraTransform;
    [SerializeField] private XROrigin origin;
    [SerializeField] public Transform targetTransform;

    // Add this new event for audio tutorial integration
    public UnityEvent OnCalibrationComplete = new UnityEvent();

    private CallDelayer callDelayer;
    private float waitForSecondsBeforeUR = 2.0f;

    private Vector3 offsetOfTargetTransform;
    private bool URWasCalled = false;
    private bool isPaperL = false, isPaperR = false;
    private GestureDetector GDetector { get { return GestureDetector.Instance; } set { } }

    //public UnityEvent<Vector3> OnPerformUprightRedirection;

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
        }
    }

    private void OnEnable()
    {
        targetTransform = GameObject.Find("TargetTransform").transform;
        SceneManager.sceneLoaded += FindTargetTransform;
        callDelayer = gameObject.AddComponent<CallDelayer>();
        callDelayer.action.AddListener(PerformUprightRedirection);
        offsetOfTargetTransform = Vector3.zero;
        callDelayer.StartCall(1.0f);
    }
    private void Start()
    {
        if (!isServer) return;
        GDetector.OnGestureBegin.AddListener((g, l) => ValidateRecalibrationCall(g, l, true));
        GDetector.OnGestureEnd.AddListener((g, l) => ValidateRecalibrationCall(g, l, false));
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= FindTargetTransform;
    }

    /// <summary>
    /// Finds a target transform of the player in the current scene
    /// </summary>
    private void FindTargetTransform(Scene scene, LoadSceneMode sceneMode)
    {
        targetTransform = GameObject.Find("TargetTransform").transform;
        if (!URWasCalled)
        {
            PerformTranslation();
        }
    }

    public void ChangeTargetTransform(float valueZ)
    {
        offsetOfTargetTransform += new Vector3(0, 0, valueZ);
        origin.transform.position += targetTransform.transform.position - playerCurrentCameraTransform.position + offsetOfTargetTransform;
    }

    /// <summary>
    /// Rotates the XROrigin so that the player is seemingly standing in the scene
    /// </summary>
    public void PerformUprightRedirection()
    {
        LoggerCommunicationProvider.Instance.UprightRedirectionPerformed(Vector3.Angle(playerCurrentCameraTransform.forward, targetTransform.up) - 90.0f);
        //print(Vector3.Angle(playerCurrentCameraTransform.forward, targetTransform.up) - 90.0f);
        Vector3 rotationDifference = targetTransform.transform.rotation.eulerAngles - playerCurrentCameraTransform.rotation.eulerAngles;
        origin.RotateAroundCameraPosition(playerCurrentCameraTransform.right, rotationDifference.x);
        origin.RotateAroundCameraPosition(playerCurrentCameraTransform.up, rotationDifference.y);
        origin.RotateAroundCameraPosition(playerCurrentCameraTransform.forward, rotationDifference.z);

        PerformTranslation();

        // Add this line to notify listeners (like the audio tutorial) when calibration is complete
        OnCalibrationComplete.Invoke();
    }

    public void PerformTranslation()
    {
        Vector3 translationDifference = targetTransform.transform.position - playerCurrentCameraTransform.position + offsetOfTargetTransform;
        origin.transform.position += translationDifference;
    }

    private void ValidateRecalibrationCall(GestureType gestureType, bool isLeft, bool begins)
    {
        if (gestureType != GestureType.Paper) return;
        if (isLeft) isPaperL = begins;
        else isPaperR = begins;
        if (isPaperL && isPaperR)
        {
            callDelayer.StartCall(waitForSecondsBeforeUR, GDetector.handL, GDetector.handR);
            AudioManager.Instance.PlaySFX("URFeedback");
            URWasCalled = true;
            //GDetector.handL.StartShaderProgress(waitForSecondsBeforeUR);
            //GDetector.handR.StartShaderProgress(waitForSecondsBeforeUR);
        }
        else
        {
            if (callDelayer.timer != null)
            {
                URWasCalled = false;
                //GDetector.handL.EndShaderProgress();
                //GDetector.handR.EndShaderProgress();
                callDelayer.StopCall();
                AudioManager.Instance.StopPlayingSFX();
            }
        }
    }
}