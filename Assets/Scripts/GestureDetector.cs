using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.InputSystem.InputAction;
using Mirror;
using VrDashboardLogger.Editor;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Handles gestures and their detection
/// </summary>
public class GestureDetector : NetworkBehaviour
{
    static public GestureDetector Instance;
    public Gesture previousGestureL, previousGestureR;
    [SyncVar]
    public HandManager handL, handR;

    [SerializeField] private float thresholdBone = 0.025f;  //threshold for one bone
    [SerializeField] private float thresholdGesture = 0.1f; //threshold for the whole hand gesture
    [SerializeField] public float timeBeforeGestureCanceled = 0.3f;
    private float timeOfNoGestureL = 0f, timeOfNoGestureR = 0f;

    private Skeleton skeletonL, skeletonR;
    private GestureListSO gestureList;    

    private Transform BGTransform;
    private GameInputSystem inputs;

    //events
    public UnityEvent<GestureType, bool> OnGestureBegin;
    public UnityEvent<GestureType, bool> OnGestureEnd;

    //enabling/disabling inputs from each hand
    public bool InputEnabledHandL { get { return inputEnabledHandL; } set { inputEnabledHandL = value; handL.SetUsage(value); } }
    public bool InputEnabledHandR { get { return inputEnabledHandR; } set { inputEnabledHandR = value; handR.SetUsage(value); } }

    private bool inputEnabledHandL = true, inputEnabledHandR = true;

    private void Awake()
    {
        if (Instance) {
            Destroy(gameObject);
            return;
        } else {
            Instance = this;
        }

        inputs = new GameInputSystem();

        inputs.GestureRecording.RecordGestureL.performed += (CallbackContext context) => { CmdSaveGesture(true, GestureType.Rock); };
        inputs.GestureRecording.RecordGestureR.performed += (CallbackContext context) => { CmdSaveGesture(false, GestureType.Rock); };

        previousGestureL = null;
        previousGestureR = null;

        gestureList = Resources.Load<GestureListSO>("GestureList");

    }


    private void Update()
    {
        if (!isServer)
            return;

        if (!skeletonL || !skeletonR) return;
        Gesture currentGestureL = null;
        Gesture currentGestureR = null;

        // Recognize gestures and perform appropriate actions
        RecognizeGesture(out currentGestureL, out currentGestureR);

        if (currentGestureL == previousGestureL) timeOfNoGestureL = 0f;
        if (currentGestureR == previousGestureR) timeOfNoGestureR = 0f;

        if (inputEnabledHandL && currentGestureL != null && currentGestureL != previousGestureL) {
            timeOfNoGestureL = 0f;
            handL.GestureChanged(currentGestureL.gestureType);
            if (previousGestureL != null) { 
                OnGestureEnd.Invoke(previousGestureL.gestureType, true); 
            }
            OnGestureBegin.Invoke(currentGestureL.gestureType, true);
            previousGestureL = currentGestureL;
        } 
        else if (inputEnabledHandL && currentGestureL == null && previousGestureL != null) {
            timeOfNoGestureL += Time.deltaTime;
            if (timeOfNoGestureL >= timeBeforeGestureCanceled)
            {
                handL.GestureChanged(null);
                OnGestureEnd.Invoke(previousGestureL.gestureType, true);
                previousGestureL = currentGestureL;
            }
        }

        if (inputEnabledHandR && currentGestureR != null && currentGestureR != previousGestureR) {
            timeOfNoGestureR = 0f;
            handR.GestureChanged(currentGestureR.gestureType);
            if (previousGestureR != null)
            {
                OnGestureEnd.Invoke(previousGestureR.gestureType, false);
            }
            OnGestureBegin.Invoke(currentGestureR.gestureType, false);
            previousGestureR = currentGestureR;
        }
        else if (inputEnabledHandR && currentGestureR == null && previousGestureR != null) {
            timeOfNoGestureR += Time.deltaTime;
            if (timeOfNoGestureR >= timeBeforeGestureCanceled)
            {
                handR.GestureChanged(null);
                OnGestureEnd.Invoke(previousGestureR.gestureType, false);
                previousGestureR = currentGestureR;
            }
        }        
        
    }

    // Sets hand L after its appearance ingame
    public void SetHandL(GameObject hand)
    {
        NetworkServer.Spawn(hand);
        skeletonL = hand.GetComponent<Skeleton>();
        handL = hand.GetComponent<HandManager>();
        handL.Interactor.backgroundTransform = BGTransform;

        VrLogger.Instance.leftHand = hand.transform.GetChild(0).gameObject;
    }

    // Sets hand R after its appearance ingame
    public void SetHandR(GameObject hand)
    {
        NetworkServer.Spawn(hand);
        skeletonR = hand.GetComponent<Skeleton>();
        handR = hand.GetComponent<HandManager>();
        handR.Interactor.backgroundTransform = BGTransform;

        VrLogger.Instance.rightHand = hand.transform.GetChild(0).gameObject;
    }

    [Command(requiresAuthority = false)]
    public void CmdSaveGesture(bool left, GestureType gestureType)
    {
        Gesture gesture = new Gesture();
        string side = left ? "L" : "R";
        gesture.name = $"{gestureType} {gestureList.GetNumGesturesOfType(left, gestureType)} {side}";
        gesture.isLeft = left;
        gesture.gestureType = gestureType;

        List<Vector3> gestureTransforms = new List<Vector3>();
        Skeleton hand = left ? skeletonL : skeletonR;

        foreach (var bone in hand.Bones)
        {
            gestureTransforms.Add(hand.Bones[0].transform.InverseTransformPoint(bone.transform.position));
        }

        gesture.transforms = gestureTransforms;
        gestureList.gestures.Add(gesture);
        
        //NEW
        UserSystem.Instance.UserData.AddGesture(gesture);
    }

    [Command(requiresAuthority = false)] 
    public void CmdRemoveCustomGestures()
    {
        gestureList.DeleteCustomGestures();
    }

    [Command(requiresAuthority = false)]
    public void CmdLoadCustomGestures(GestureList gestureList)
    {
        if (gestureList == null) return;
        foreach (var item in gestureList.gestures)
        {
            this.gestureList.gestures.Add(item);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdDeleteGestures(bool left, GestureType gestureType) {
        gestureList.DeleteGestures(left, gestureType);
        UserSystem.Instance.UserData.DeleteGestures(left, gestureType);
    }
    //NEW End

    [Command(requiresAuthority = false)]
    public void CmdSetNumOfGesturesOfType(bool left, GestureType gestureType, int idx)
    {
        int num = gestureList.GetNumGesturesOfType(left, gestureType);
        UpdateGestureSettingsData(idx, num);
    }

    [ClientRpc]
    public void UpdateGestureSettingsData(int idx, int num)
    {
        if (isServer) return;
        FindObjectOfType<GestureSettingsManager>().UpdateData(num, idx);
    }

    // Finds a gesture that is close to the performed one, if there is any save it to the currentGestureL/R
    private void RecognizeGesture(out Gesture currentGestureL, out Gesture currentGestureR)
    {
        float differenceSumMinL = thresholdGesture;
        float differenceSumMinR = thresholdGesture;

        currentGestureL = null;
        currentGestureR = null;

        foreach (var gesture in gestureList.gestures)
        {
            float differenceSum = 0;
            bool notRecognized = false;
            Skeleton hand = gesture.isLeft ? skeletonL : skeletonR;

            for (int i = 1; i < hand.Bones.Count; i++)
            {
                Vector3 currentTransform = hand.Bones[0].transform.InverseTransformPoint(hand.Bones[i].transform.position);
                float difference = Vector3.Distance(currentTransform, gesture.transforms[i]);
                if (difference > thresholdBone) { //one bone too different
                    notRecognized = true;
                    break;
                }

                differenceSum += difference;
            }

            if (!notRecognized)
            {
                if (gesture.isLeft && differenceSum < differenceSumMinL)
                {
                    differenceSumMinL = differenceSum;
                    currentGestureL = gesture;
                } 
                if (!gesture.isLeft && differenceSum < differenceSumMinR)
                {
                    differenceSumMinR = differenceSum;
                    currentGestureR = gesture;
                }

            }
        }
    }

    public void SetBGTransform(Transform newBG) {
        BGTransform = newBG;

        if (handR) {
            handR.Interactor.backgroundTransform = BGTransform;
        }

        if (handL) {
            handL.Interactor.backgroundTransform = BGTransform;
        }
    }

    #region Enabler
    private void OnEnable()
    {
        inputs.Enable();
    }

    private void OnDisable()
    {
        inputs.Disable();
    }
    #endregion
}
