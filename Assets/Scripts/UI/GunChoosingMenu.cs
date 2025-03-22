using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.Rendering.DebugUI;

public class GunChoosingMenu : NetworkBehaviour
{

    [SerializeField] GameObject pistol, rifle, rifleOnStand, rifleOnStandRealistic, stand, standRealistic; //object to choose from
    [SerializeField] GameObject pistolPrefab, riflePrefab, rifleOnStandPrefab, rifleOnStandRealisticPrefab; //will be used to shoot

    private CallDelayer callDelayer;
    [SerializeField] private float secondsBeforeAction;

    public UnityEvent<string> onGunChoose;
    public GameObject gun;

    public GunType gunType;


    public enum GunType
    {
        Rifle,
        RifleOnStand,
        RifleOnStandRealistic,
        //Pistol
    }

    private void Start()
    {
        if (!isServer) return;
        //pistol.GetComponent<Interactable>().OnPhysicalRockEnter.AddListener((i) => Grab(true, GunType.Pistol, i));
        rifle.GetComponent<Interactable>().OnPhysicalRockEnter.AddListener((i) => Grab(true, GunType.Rifle, i));
        rifleOnStand.GetComponent<Interactable>().OnPhysicalRockEnter.AddListener((i) => Grab(true, GunType.RifleOnStand, i));
        rifleOnStandRealistic.GetComponent<Interactable>().OnPhysicalRockEnter.AddListener((i) => Grab(true, GunType.RifleOnStandRealistic, i));
        //pistol.GetComponent<Interactable>().OnPhysicalRockExit.AddListener((i) => Grab(false, GunType.Pistol, i));
        rifle.GetComponent<Interactable>().OnPhysicalRockExit.AddListener((i) => Grab(false, GunType.Rifle, i));
        rifleOnStand.GetComponent<Interactable>().OnPhysicalRockExit.AddListener((i) => Grab(false, GunType.RifleOnStand, i));
        rifleOnStandRealistic.GetComponent<Interactable>().OnPhysicalRockExit.AddListener((i) => Grab(false, GunType.RifleOnStandRealistic, i));

        callDelayer = gameObject.AddComponent<CallDelayer>();
        callDelayer.action.AddListener(ChooseGun);
    }

    private void OnEnable()
    {
        if (!isServer) return;
        callDelayer.action.AddListener(ChooseGun);
    }

    private void OnDisable()
    {
        if (!isServer) return;
        callDelayer.action.RemoveListener(ChooseGun);
    }

    private void OnDestroy()
    {
        
        if (gun && isServer)
        {
            NetworkServer.Destroy(gun);
        }
    }

    private void Grab(bool begins, GunType gunType, Interactor interactor)
    {
        var hand = interactor.GetComponent<HandManager>();

        this.gunType = gunType;

        if (begins)
        {
            if (isServer) callDelayer.StartCall(secondsBeforeAction, hand);
            AudioManager.Instance.PlaySFX("ButtonFeedback");
        }
        else
        {
            if (isServer) callDelayer.StopCall();
            AudioManager.Instance.StopPlayingSFX();
        }
    }

    /// <summary>
    /// Instantiates the chosen gun, and sets the variables to correct values.
    /// </summary>
    public void ChooseGun()
    {
        if (!isServer) return;
        ChangeThisActive(false);

        if (gun)
        {
            NetworkServer.Destroy(gun);
            ChangeStandActive(false, false);
            ChangeStandActive(false, true);
        }

        string gunMode = "none";

        switch (gunType)
        {
            /*case (GunType.Pistol):
                gun = Instantiate(pistolPrefab);
                ChangeStandActive(false);
                //LoggerCommunicationProvider.Instance.AddToCustomData("shooting_gun", "\"Pistol\"" );
                gunMode = "\"Pistol\"";
                break;*/
            case (GunType.Rifle):
                gun = Instantiate(riflePrefab);
                ChangeStandActive(false, false);
                ChangeStandActive(false, true);
                gunMode = "\"Rifle\"";
                break;
            case (GunType.RifleOnStand):
                gun = Instantiate(rifleOnStandPrefab);
                gun.GetComponent<RifleOnStand>().stand = stand.transform;
                ChangeStandActive(true, false);
                ChangeStandActive(false, true);
                gunMode = "\"Rifle on stand\"";
                break;
            case (GunType.RifleOnStandRealistic):
                gun = Instantiate(rifleOnStandRealisticPrefab);
                gun.GetComponent<RifleOnStand>().stand = standRealistic.transform;
                ChangeStandActive(false, false);
                ChangeStandActive(true, true);
                gunMode = "\"Rifle on stand realistic\"";
                break;
        }

        NetworkServer.Spawn(gun);
        onGunChoose.Invoke(gunMode);
    }

    /// <summary>
    /// Enables/disables the gunChoosingMenu gameobject.
    /// </summary>
    /// <param name="a">True if the gunChoosingMenu should be seen in the game.</param>
    [ClientRpc]
    public void ChangeThisActive(bool a)
    {
        gameObject.SetActive(a);
    }

    /// <summary>
    /// Enables/disables the stand gameobject.
    /// </summary>
    /// <param name="a">True if the stand should be seen in the game.</param>
    [ClientRpc]
    public void ChangeStandActive(bool a, bool isRealistic)
    {
        if (!isRealistic) stand.SetActive(a);
        else standRealistic.SetActive(a);
    }


    /// <summary>
    /// Removes the current gun and visualiyes the choosing menu
    /// </summary>
    public void ResetSelection()
    {
        gameObject.SetActive(true);
        NetworkServer.Destroy(gun);
        stand.SetActive(true);
        standRealistic.SetActive(false);
    }

}
