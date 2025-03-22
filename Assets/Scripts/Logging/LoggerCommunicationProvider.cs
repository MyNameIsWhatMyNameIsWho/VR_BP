using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.XR.CoreUtils.Datums;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VrDashboardLogger.Editor;
using VrDashboardLogger.Editor.Classes;

public class LoggerCommunicationProvider : NetworkBehaviour
{
    public static LoggerCommunicationProvider Instance { get; private set; }

    [field:SerializeField]
    private UnityEvent<Participant> OnParticipantListReceive { get; set; }

    [field: SerializeField]
    private UnityEvent OnParticipantSet {  get; set; }

    private VrLogger vrLogger;

    private string customDataRuntime;

    public bool loggingStarted = false;


    /* Custom data variables */
    /// <summary>
    /// Number of Upright Redirections performed during the activity.
    /// </summary>
    private int numUR = 0;


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

    void Start()
    {
        if (!isServer) return;

        SceneManager.activeSceneChanged += SceneChanged;
        OnParticipantSet.AddListener(() => StartLogging(SceneManager.GetActiveScene().name));
        vrLogger = (VrLogger)VrLogger.Instance;
        
        vrLogger.SetOrganisation("89pQEN"); //DP Langova
    }

    private void OnApplicationQuit()
    {
        if (!isServer) return;

        StopLogging();
    }

    private void SetParticipant(string participantID)
    {
        if (!isServer) return;

        vrLogger.SetParticipant(participantID);
        OnParticipantSet.Invoke();
    }

    public void StartLogging(string environmentName)
    {
        if (!isServer) return;

        if (loggingStarted) { 
            return; 
        }
        loggingStarted = true;
        customDataRuntime = "";
        vrLogger.InitializeLogger();
        vrLogger.StartLogging(environmentName);
    }

    public void RecordEvent(string eventName) 
    {
        if (!isServer) return;

        vrLogger.SetEvent(eventName);
    }

    public void StopLogging() 
    {
        if (!isServer) return;

        if (!loggingStarted)
        {
            return;
        }
        loggingStarted = false;
        SetCustomData();
        vrLogger.StopLogging(); 
        
        vrLogger.SendActivity(response => 
        {
            if (!response)
            {
                Debug.LogWarning("[VR For Lying Patients] Activity not sent! " + Time.realtimeSinceStartup);
            } else
            {
                //Debug.Log("[VR For Lying Patients] Activity sent");
            }
        }, true);        
    }

    /// <summary>
    /// Tries to find a participant/user id by nickname, if successful, sets the user to the logging session.
    /// </summary>
    /// <param name="nickname">Nickname of the user to find (and set)</param>
    /// <param name="AfterPlayerFound">Action to perform if user with given nickname was found.</param>
    /// <param name="IfPlayerNotFound">Action to perform if user with given nickname was not found.</param>
    public void FindParticipantIDByNickname (string nickname, UnityAction AfterPlayerFound = null, UnityAction IfPlayerNotFound = null)
    {
        if (!isServer) return;

        vrLogger.GetParticipants(list =>
        {
            if (list == null) {
                Debug.LogWarning("[VR For Lying Patients] WARNING: No participants exist!!!");
                return; 
            }       

            foreach (var participant in list)
            {
                if (participant.nickname == nickname) {
                    AfterPlayerFound?.Invoke();
                    SetParticipant(participant.id);
                    return;
                }
                //print(participant.id + " has nickname " + participant.nickname);
            }

            IfPlayerNotFound?.Invoke();
        });
        
    }

    /// <summary>
    /// Tries to find a participant/user id by nickname, performs actions depending on the result.
    /// </summary>
    /// <param name="nickname">Nickname of the user to find (and set)</param>
    /// <param name="AfterPlayerFound">Action to perform if user with given nickname was found.</param>
    /// <param name="IfPlayerNotFound">Action to perform if user with given nickname was not found.</param>
    public void DoesParticipantNicknameExist(string nickname, UnityAction AfterPlayerFound = null, UnityAction IfPlayerNotFound = null)
    {
        if (!isServer) return;

        vrLogger.GetParticipants(list =>
        {
            if (list == null)
            {
                Debug.LogWarning("[VR For Lying Patients] WARNING: No participants exist!!!");
                return;
            }

            foreach (var participant in list)
            {
                if (participant.nickname == nickname)
                {
                    AfterPlayerFound?.Invoke();
                    return;
                }
                //print(participant.id + " has nickname " + participant.nickname);
            }

            IfPlayerNotFound?.Invoke();
        });

    }

    public void AddToCustomData(string variable, string value)
    {
        if (!isServer) return;

        customDataRuntime += ", \"" + variable + "\": " + value;
    }
    private void SetCustomData()
    {
        if (!isServer) return;

        string customData = "{ ";
        customData += "\"num_upright_redirection\": " + numUR.ToString();
        customData += customDataRuntime;
        customData += " }";
        vrLogger.SetCustomData(customData);
    }

    private void SceneChanged(Scene prev, Scene next)
    {
        if (!isServer) return;

        StopLogging();
        numUR = 0;
        StartLogging(next.name);
    }



    /// <summary>
    /// Records and event about the change of recline angle by upright redirection.
    /// Also stores the angle into the name of the event.
    /// </summary>
    /// <param name="reclineChange">Difference between target and current recline. Positive: Player laid down; Negative: Player sat up.</param>
    public void UprightRedirectionPerformed(float reclineChange)
    {
        if (!isServer) return;

        numUR++;
        string angleText, sign;
        if (reclineChange < 0.0f) { 
            reclineChange *= -1.0f; 
            sign = "-";
        } else {
            sign = "+";
        }

        if (reclineChange < 15.0f)
        {
            angleText = "<15";
        } else if (reclineChange < 45.0f)
        {
            angleText = "<45";
        } else if (reclineChange < 90.0f)
        {
            angleText = "<90";
        } else
        {
            angleText = ">90";
        }


        RecordEvent("UR" + angleText + sign);       
    }


    /// <summary>
    /// Records an event about a target being shot and how many points it added to the score.
    /// </summary>
    /// <param name="points">Points can be {1, 2, 3, 6, 10} </param>
    public void TargetShot(int points)
    {
        if (!isServer) return;

        RecordEvent("TS"+points);
    }


    public void TangramPieceGrabbed()
    {
        if (!isServer) return;

        RecordEvent("TPG");
    }

    public void TangramPieceDropped()
    {
        if (!isServer) return;

        RecordEvent("TPD");
    }

    public void TangramWon()
    {
        if (!isServer) return;

        RecordEvent("TW");
    }

    /*
    // This cannot be used due to the logging being saved on stack, which would eventually take up all the memory in the device.
    // It is preferable to send the Activity data as frequently as possible, so with the change of environment I plan to stop and start logging again.
     public void ChangeEnvironment(string environmentName)
    {
        vrLogger.SetEnvironment(environmentName);
    }
    */

}
