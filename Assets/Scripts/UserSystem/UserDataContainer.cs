using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using UnityEngine.Events;
using System;
using UnityEngine.InputSystem.XR;
using UnityEditor.Rendering.Universal;
using Unity.VisualScripting.FullSerializer;

[System.Serializable]
public class UserDataContainer
{
    public List<User> _users;

    public UnityEvent OnUsersChange;

    private int _activeUserID;

    public string Nickname { get { return _users[_activeUserID].Nickname; } set { _users[_activeUserID].Nickname = value; SaveUsers(); } }

    //shooting gallery
    public float HighscoreFastTimed { get { return _users[_activeUserID].HighscoreFastTimed; } set { _users[_activeUserID].HighscoreFastTimed = value; SaveUsers(); } }
    public float HighscoreNormalTimed { get { return _users[_activeUserID].HighscoreNormalTimed; } set { _users[_activeUserID].HighscoreNormalTimed = value; SaveUsers(); } }
    public float HighscoreSlowTimed { get { return _users[_activeUserID].HighscoreSlowTimed; } set { _users[_activeUserID].HighscoreSlowTimed = value; SaveUsers(); } }
    public float HighscoreFastUntimed { get { return _users[_activeUserID].HighscoreFastUntimed; } set { _users[_activeUserID].HighscoreFastUntimed = value; SaveUsers(); } }
    public float HighscoreNormalUntimed { get { return _users[_activeUserID].HighscoreNormalUntimed; } set { _users[_activeUserID].HighscoreNormalUntimed = value; SaveUsers(); } }
    public float HighscoreSlowUntimed { get { return _users[_activeUserID].HighscoreSlowUntimed; } set { _users[_activeUserID].HighscoreSlowUntimed = value; SaveUsers(); } }

    //tangram
    public int numTimesHorseCompleted { get { return _users[_activeUserID].numTimesHorseCompleted; } set { _users[_activeUserID].numTimesHorseCompleted = value; SaveUsers(); } }
    public int numTimesHeartCompleted { get { return _users[_activeUserID].numTimesHeartCompleted; } set { _users[_activeUserID].numTimesHeartCompleted = value; SaveUsers(); } }
    public int numTimesShapesEasyCompleted { get { return _users[_activeUserID].numTimesShapesEasyCompleted; } set { _users[_activeUserID].numTimesShapesEasyCompleted = value; SaveUsers(); } }

    public float FastestHorseCompletion { get { return _users[_activeUserID].FastestHorseCompletion; } set { _users[_activeUserID].FastestHorseCompletion = value; SaveUsers(); } }
    public float FastestHeartCompletion { get { return _users[_activeUserID].FastestHeartCompletion; } set { _users[_activeUserID].FastestHeartCompletion = value; SaveUsers(); } }
    public float FastestShapesEasyCompletion { get { return _users[_activeUserID].FastestShapesEasyCompletion; } set { _users[_activeUserID].FastestShapesEasyCompletion = value; SaveUsers(); } }

    //endless runner
    public float HighscoreWithObstacles { get { return _users[_activeUserID].HighscoreWithObstacles; } set { _users[_activeUserID].HighscoreWithObstacles = value; SaveUsers(); } }
    public float HighscoreWithoutObstacles { get { return _users[_activeUserID].HighscoreWithoutObstacles; } set { _users[_activeUserID].HighscoreWithoutObstacles = value; SaveUsers(); } }

    //moth game
    public float HighscoreMothGame { get { return _users[_activeUserID].HighscoreMothGame; } set { _users[_activeUserID].HighscoreMothGame = value; SaveUsers(); } }

    public GestureList GestureList { get { return _users[_activeUserID].gestures; } }

    public bool SetActiveUser(string nickname)
    {
        _activeUserID = _users.FindIndex((u) => u.Nickname == nickname);
        return _activeUserID > -1;
    }

    public void AddUser(string nickname)
    {
        _users.Add(new User(nickname));
        SaveUsers();
        OnUsersChange.Invoke();
    }

    public void RemoveUser(string nickname)
    {
        _users.RemoveAll((u) => u.Nickname == nickname);
        SaveUsers();
        OnUsersChange.Invoke();
    }

    public void LoadUsers()
    {
        string path = Application.persistentDataPath + "/Users.save";
        Debug.Log(path);
        if (File.Exists(path))
        {
            Debug.Log(path);
            BinaryFormatter bf = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);

            UserSave[] usersArray = bf.Deserialize(stream) as UserSave[];
            _users = new List<User>();

            foreach (var item in usersArray)
            {
                _users.Add(new User(item));
            }

            stream.Close();
        }
        else
        {
            Debug.Log("Create Data");
            _users = new List<User> { new User("DefaultUser") };
            SaveUsers();
        }

        OnUsersChange.Invoke();
    }

    public void SaveUsers()
    {
        BinaryFormatter bf = new BinaryFormatter();
        string path = Application.persistentDataPath + "/Users.save";
        FileStream stream = new FileStream(path, FileMode.Create);

        UserSave[] usersArray = new UserSave[_users.Count];

        int i = 0;
        foreach (var item in _users)
        {
            usersArray[i] = new UserSave(item);
            i++;
        }

        bf.Serialize(stream, usersArray);
        stream.Close();
    }

    public void AddGesture(Gesture gesture)
    {
        _users[_activeUserID].gestures.gestures.Add(gesture);
        SaveUsers();
    }

    public void DeleteGestures(bool left, GestureType gestureType)
    {
        _users[_activeUserID].gestures.DeleteGestures(left, gestureType);
        SaveUsers();
    }
}