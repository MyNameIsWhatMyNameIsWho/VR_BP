using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Character : NetworkBehaviour
{
    [SerializeField] private BoxCollider headCollider;  
    [SerializeField] public Animator animator;
    [field: SerializeField] public UnityEvent OnActionPerform { get; private set; }
    [field: SerializeField] public UnityEvent OnCharacterHit { get; private set; }
    [field: SerializeField] public UnityEvent OnCoinPickup { get; private set; }

    public float animationSpeed = 1.0f;
    public bool controllingHandIsLeft = false;

    //jump
    [SerializeField] private float heightDecrease = 0.03f;
    [SerializeField] private float jumpHeight, jumpLength;
    [SerializeField] private AnimationCurve jumpCurve;
    [SerializeField] private GameObject invulnerabilityBubble;
    public float jumpSpeed;
    private float jumpProgress, baseYPos;

    //status of character
    private bool crouching = false, jumping = false, falling = false, canPerformAction = true;

    private bool Crouching {    
        get { return crouching; }
        set { crouching = value;
            animator.SetBool("Crouching", crouching);
        } }   
    
    private bool Jumping {    
        get { return jumping; }
        set { jumping = value;
            animator.SetBool("Jumping", jumping);
        } }



    private void Start()
    {
        baseYPos = transform.position.y;
    }

    private void Update()
    {
        if (!canPerformAction) return;

        var hand = controllingHandIsLeft ? GestureDetector.Instance.handL : GestureDetector.Instance.handR;

        if (!hand.CurrentGestureType.HasValue) {
            CrouchEnd();
            JumpEnd();
        } else {
            switch (hand.CurrentGestureType)
            {
                case GestureType.Rock:
                    JumpEnd();
                    CrouchBegin();
                    break;
                case GestureType.Paper:
                    //JumpAnimationCurve();
                    CrouchEnd();
                    JumpBegin();
                    break;
            }
        }
    }

    private void FixedUpdate()
    {
        
        if (Jumping && !falling)
        {
            jumpProgress += Time.fixedDeltaTime * jumpSpeed / jumpLength;
            var newPos = transform.position;
            newPos.y = jumpCurve.Evaluate(jumpProgress) * jumpHeight + baseYPos;
            transform.position = newPos;

            if (jumpProgress >= 1)
            {
                jumpProgress = 0;
                Jumping = false;
            }
        }

        if (falling)
        {
            var newPos = transform.position;
            newPos.y -= heightDecrease * Time.fixedDeltaTime * jumpSpeed;

            if (newPos.y <= baseYPos)
            {
                newPos.y = baseYPos;
                jumpProgress = 0;
                falling = false;
                Jumping = false;
            }

            transform.position = newPos;
        }
    }

    private void JumpBegin()
    {
        if (Jumping || Crouching) return;
        
        OnActionPerform.Invoke();
        falling = false;
        Jumping = true;

        AudioManager.Instance.PlaySFX("RobotJump");

        LoggerCommunicationProvider.Instance.RecordEvent("RJB");
    }

    private void JumpEnd()
    {
        if (!Jumping) return;

        falling = true;

        LoggerCommunicationProvider.Instance.RecordEvent("RJE");
    }

    private void CrouchBegin()
    {
        if (Crouching || Jumping) return;
        
        OnActionPerform.Invoke();
        Crouching = true;

        AudioManager.Instance.PlaySFX("RobotCrouch");
        LoggerCommunicationProvider.Instance.RecordEvent("RCB");

    }

    private void CrouchEnd()
    {
        if (!Crouching) return;
        Crouching = false;
        LoggerCommunicationProvider.Instance.RecordEvent("RCE");
    }

    private void OnTriggerEnter(Collider other)
    {
        //failed to perform action on time               
        if (other.CompareTag("Coin"))
        {
            OnCoinPickup.Invoke();
            other.gameObject.GetComponent<Coin>().Hide();
            AudioManager.Instance.PlaySFX("CoinPickup");
        } else if (!other.CompareTag("SoundTrigger")) {
            AudioManager.Instance.PlaySFX("RobotDeath");
            OnCharacterHit.Invoke();
        } /*else if (other.CompareTag("RockSound"))
        {
            AudioManager.Instance.PlaySFX("RockSound");
        } else if (other.CompareTag("PaperSound"))
        {
            AudioManager.Instance.PlaySFX("PaperSound");
        }*/
    }


    public void GameOver()
    {
        //animace
        canPerformAction = false;
        Jumping = false;
        Crouching = false;
        jumpProgress = 0f;
    }

    public void Spawn()
    {
        CrouchEnd();
        canPerformAction = true;
        var newPos = transform.position;
        newPos.y = baseYPos;
        transform.position = newPos;
    }

    public void IncreaseAnimationSpeed(float speedMultiplier)
    {
        animationSpeed = speedMultiplier;
        animator.SetFloat("SpeedMultiplier", animationSpeed);
    }

    [ClientRpc]
    public void VisualizeInvulnerability(bool visualize)
    {
        invulnerabilityBubble.SetActive(visualize);
    }
}
