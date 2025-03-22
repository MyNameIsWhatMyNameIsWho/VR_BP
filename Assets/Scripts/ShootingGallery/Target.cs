using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class Target : NetworkBehaviour
{
    public bool standing;
    [SerializeField] private bool canStandUp = false, canAutoHide = false;
    private Animator animator;
    private Interactable interactable;
    private CallDelayer callDelayerStandUp, callDelayerAutoHide;

    [SerializeField] float standUpDelayMin = 1.0f, standUpDelayMax = 5.0f;
    [SerializeField] float autoHideDelayMin = 10.0f, autoHideDelayMax = 200.0f;
    [SerializeField] int points = 1;

    //moving of target
    public bool isGoingRight, isGoingUp;
    public bool canStandUpAnim = true;


    private void Awake()
    {
        animator = GetComponent<Animator>();        
        interactable = GetComponent<Interactable>();
        callDelayerStandUp = gameObject.AddComponent<CallDelayer>();
        callDelayerAutoHide = gameObject.AddComponent<CallDelayer>();
    }

    private void Start()
    {
        standing = false;
        transform.rotation = Quaternion.Euler(90, 0, 0);

        if (!isServer) return;
        interactable.OnRaycastRockEnter.AddListener((i) => Shot());
        callDelayerStandUp.action.AddListener(StandUp);
        callDelayerAutoHide.action.AddListener(Fall);

        //callDelayer.StartCall(Random.Range(standUpDelayMin, standUpDelayMax));

        ShootingGalleryManager.Instance.onGameEnd.AddListener(GameEnded);
        ShootingGalleryManager.Instance.onGameStart.AddListener(GameStarted);
    }

    [ClientRpc]
    public void StandUp()
    {
        if (standing || !canStandUp || !canStandUpAnim) return;

        standing = true;
        animator.SetBool("Standing", standing);
        if (canAutoHide)
        {
            callDelayerAutoHide.StartCall(Random.Range(autoHideDelayMin, autoHideDelayMax));
        }
    }

    [ClientRpc]
    public void Fall()
    {
        standing = false;
        animator.SetBool("Standing", standing);
        callDelayerStandUp.StartCall(Random.Range(standUpDelayMin, standUpDelayMax));
    }

    private void Shot()
    {
        if (!standing) return;
        FindAnyObjectByType<Gun>().InstantiateVFX(Vector3.zero);
        Fall();
        ShootingGalleryManager.Instance.TargetShot(points);        
        AudioManager.Instance.PlaySFX("TargetShot");
    }

    private void GameEnded()
    {
        canStandUp = false;
        Fall();
    }

    private void GameStarted()
    {
        canStandUp = true;
        callDelayerStandUp.StartCall(Random.Range(standUpDelayMin, standUpDelayMax));
    }

}
