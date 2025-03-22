using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Delays the call of a function
/// </summary>
public class CallDelayer : MonoBehaviour
{
    public Coroutine timer;
    public UnityEvent action = new UnityEvent();

    private HandManager hand1, hand2;


    public void StartCall(float secondsBeforeCall, HandManager hand1 = null, HandManager hand2 = null)
    {
        if (timer == null)
        {
            this.hand1 = hand1;
            this.hand2 = hand2;
            timer = StartCoroutine(DelayFunctionCall(secondsBeforeCall));
        }
        
    }

    public void StopCall()
    {
        if (timer != null)
        {
            StopCoroutine(timer);
            hand1?.SetProgressHand(0.0f);
            hand2?.SetProgressHand(0.0f);
            timer = null;
            hand1 = null;
            hand2 = null;
        }
    }

    IEnumerator DelayFunctionCall(float time)
    {
        float currTime = 0.0f;
        while (currTime < time)
        {
            yield return null;
            currTime += Time.deltaTime;
            hand1?.SetProgressHand(currTime / time);
            hand2?.SetProgressHand(currTime / time);
        }
        StopCall();
        action.Invoke();
    }

}
