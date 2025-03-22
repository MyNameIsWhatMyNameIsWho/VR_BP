using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;


public enum GestureType
{
    Rock,
    Paper
}

[System.Serializable]
public class Gesture
{
    public string name;
    public GestureType gestureType;
    public bool isLeft;
    public List<Vector3> transforms;
    public bool defaultGesture;

    public Gesture() { }

    public Gesture(GestureSave gestureSave)
    {
        name = gestureSave.name;
        gestureType = (GestureType) gestureSave.gestureType;
        isLeft = gestureSave.isLeft;
        transforms = new List<Vector3>();

        for(int i = 0; i < gestureSave.transforms.Count(); i++)
        {
            transforms.Add(gestureSave.transforms[i].ToVector3());
        }

        defaultGesture = gestureSave.defaultGesture;
    }
}

[System.Serializable]
public class GestureSave
{
    public string name;
    public int gestureType;
    public bool isLeft;
    public Vector3Save[] transforms;
    public bool defaultGesture;

    public GestureSave(Gesture gesture) { 
        name = gesture.name;
        gestureType = (int)gesture.gestureType;
        isLeft = gesture.isLeft;

        transforms = new Vector3Save[gesture.transforms.Count];
        for (int i = 0; i < gesture.transforms.Count; i++)
        {
            transforms[i] = new Vector3Save(gesture.transforms[i]);
        }

        defaultGesture = gesture.defaultGesture;
    }
}

[System.Serializable]
public class Vector3Save
{
    public float x, y, z;

    public Vector3Save(Vector3 vec) {
        x = vec.x; y = vec.y; z = vec.z;
    }

    public Vector3 ToVector3() { 
        return new Vector3(x, y, z);
    }
}

/// <summary>
/// SO saving gestures
/// </summary>
[CreateAssetMenu(fileName = "GestureList", menuName = "ScriptableObjects/GestureList")]
public class GestureListSO : ScriptableObject
{
    public List<Gesture> gestures;

    public GestureListSO()
    {
        gestures = new List<Gesture>();
    }

    public int GetNumGesturesOfType(bool isLeft, GestureType gestureType)
    {
        int num = 0;
        foreach (Gesture g in gestures)
        {
            if (g.gestureType == gestureType && g.isLeft == isLeft && !g.defaultGesture) num++;
        }
        return num;
    }

    /// <summary>
    /// Removes all gestures of given type on a given hand
    /// </summary>
    public void DeleteGestures(bool isLeft, GestureType gestureType)
    {
        gestures.RemoveAll(g => g.gestureType == gestureType && g.isLeft == isLeft && !g.defaultGesture);
    }

    public void DeleteCustomGestures()
    {
        gestures.RemoveAll(g => !g.defaultGesture);
    }
}

public class GestureList
{
    public List<Gesture> gestures;

    public GestureList()
    {
        gestures = new List<Gesture>();
    }

    public void DeleteGestures(bool isLeft, GestureType gestureType)
    {
        gestures.RemoveAll(g => g.gestureType == gestureType && g.isLeft == isLeft && !g.defaultGesture);
    }
    public void DeleteCustomGestures()
    {
        gestures.RemoveAll(g => !g.defaultGesture);
    }
}
