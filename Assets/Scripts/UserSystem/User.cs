using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class User
{
    public string Nickname;

    //shooting gallery
    public float HighscoreFastTimed = 0;
    public float HighscoreNormalTimed = 0;
    public float HighscoreSlowTimed = 0;
    public float HighscoreFastUntimed = 2147483647;
    public float HighscoreNormalUntimed = 2147483647;
    public float HighscoreSlowUntimed = 2147483647;

    //tangram
    public int numTimesHorseCompleted = 0;
    public int numTimesHeartCompleted = 0;
    public int numTimesShapesEasyCompleted = 0;

    public float FastestHorseCompletion = -1;
    public float FastestHeartCompletion = -1;
    public float FastestShapesEasyCompletion = -1;

    //endless runner
    public float HighscoreWithObstacles = 0;
    public float HighscoreWithoutObstacles = 0;

    public GestureList gestures;

    public User() {
        gestures = new GestureList();
    }

    public User(string nickname)
    {
        gestures = new GestureList();
        Nickname = nickname;
    }

    public User(UserSave userSave) {
        gestures = new GestureList();
        Nickname = userSave.Nickname;

        //shooting gallery
        HighscoreFastTimed = userSave.HighscoreFastTimed;
        HighscoreNormalTimed = userSave.HighscoreNormalTimed;
        HighscoreSlowTimed = userSave.HighscoreSlowTimed;
        HighscoreFastUntimed = userSave.HighscoreFastUntimed;
        HighscoreNormalUntimed = userSave.HighscoreNormalUntimed;
        HighscoreSlowUntimed = userSave.HighscoreSlowUntimed;

        //tangram
        numTimesHorseCompleted = userSave.numTimesHorseCompleted;
        numTimesHeartCompleted = userSave.numTimesHeartCompleted;
        numTimesShapesEasyCompleted = userSave.numTimesShapesEasyCompleted;

        FastestHorseCompletion = userSave.FastestHorseCompletion;
        FastestHeartCompletion = userSave.FastestHeartCompletion;
        FastestShapesEasyCompletion = userSave.FastestShapesEasyCompletion;

        //endless runner
        HighscoreWithObstacles = userSave.HighscoreWithObstacles;
        HighscoreWithoutObstacles = userSave.HighscoreWithoutObstacles;

        if (userSave.gestures == null) return;

        foreach (var item in userSave.gestures)
        {
            gestures.gestures.Add(new Gesture(item));           
        };
    }
}

[System.Serializable]
public class UserSave
{
    public string Nickname;

    //shooting gallery
    public float HighscoreFastTimed = 0;
    public float HighscoreNormalTimed = 0;
    public float HighscoreSlowTimed = 0;
    public float HighscoreFastUntimed = 2147483647;
    public float HighscoreNormalUntimed = 2147483647;
    public float HighscoreSlowUntimed = 2147483647;

    //tangram
    public int numTimesHorseCompleted = 0;
    public int numTimesHeartCompleted = 0;
    public int numTimesShapesEasyCompleted = 0;

    public float FastestHorseCompletion = -1;
    public float FastestHeartCompletion = -1;
    public float FastestShapesEasyCompletion = -1;

    //endless runner
    public float HighscoreWithObstacles = 0;
    public float HighscoreWithoutObstacles = 0;

    public GestureSave[] gestures;

    public UserSave(User user)
    {
        Nickname = user.Nickname;

        //shooting gallery
        HighscoreFastTimed = user.HighscoreFastTimed;
        HighscoreNormalTimed = user.HighscoreNormalTimed;
        HighscoreSlowTimed = user.HighscoreSlowTimed;
        HighscoreFastUntimed = user.HighscoreFastUntimed;
        HighscoreNormalUntimed = user.HighscoreNormalUntimed;
        HighscoreSlowUntimed = user.HighscoreSlowUntimed;

        //tangram
        numTimesHorseCompleted = user.numTimesHorseCompleted;
        numTimesHeartCompleted = user.numTimesHeartCompleted;
        numTimesShapesEasyCompleted = user.numTimesShapesEasyCompleted;

        FastestHorseCompletion = user.FastestHorseCompletion;
        FastestHeartCompletion = user.FastestHeartCompletion;
        FastestShapesEasyCompletion = user.FastestShapesEasyCompletion;

        //endless runner
        HighscoreWithObstacles = user.HighscoreWithObstacles;
        HighscoreWithoutObstacles = user.HighscoreWithoutObstacles;

        if (user.gestures == null) return;
        
        gestures = new GestureSave[user.gestures.gestures.Count];

        int i = 0;
        foreach (var item in user.gestures.gestures)
        {
            gestures[i] = new GestureSave(item);
            i++;
        };
    }
}
