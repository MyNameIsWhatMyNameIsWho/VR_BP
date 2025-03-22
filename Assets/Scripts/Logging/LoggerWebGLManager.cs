using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VrDashboardLogger.Editor.Classes;
using VrDashboardLogger.Editor;

public class LoggerWebGLManager : MonoBehaviour
{
    void Start()
    {
        var vrLogger = gameObject.AddComponent<VrLogger>();
        vrLogger.GetVrData(HandleVrData);
    }

    
    private void HandleVrData(VrData data)
    {
        Debug.Log(data);

        foreach (var record in data.records)
        {
            Debug.Log(record);
                        
            /*foreach (var e in record.events)
            {
                if (e.StartsWith("UR")) DisplayURInfo(e);
            }*/

        }
    }

    /*private void DisplayURInfo(string e)
    {
        e.TrimStart('U'); e.TrimStart('R'); //e.TrimStart("UR");
        if (e.StartsWith("<"))
        {
            e.Remove(0, 1);
            //TODO show in UI that recalibration was < e
        }
        else
        {
            //TODO show in UI that recalibration was > 90 
        }

        if (e.EndsWith("+"))
        {
            //TODO show in UI that recalibration was to lay down more
        } else
        {
            //TODO show in UI that recalibration was to sit up more
        }

    }    */
}


