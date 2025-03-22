using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ShootingMenuManager : GameMenuManager
{
    List<string> mode = new List<string> {
        "Slow Timed", "Normal Timed", "Fast Timed", "Slow Untimed", "Normal Untimed", "Fast Untimed"
    };

    [Command(requiresAuthority = false)]
    public void CmdChangeWeapon(int type) {
        if (ShootingGalleryManager.Instance)
        {
            //ShootingGalleryManager.Instance.RestartGame();
            ShootingGalleryManager.Instance.gunChoosingMenu.gunType = (GunChoosingMenu.GunType) type;
            ShootingGalleryManager.Instance.gunChoosingMenu.ChooseGun();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdResetShootingGame()
    {
        if (ShootingGalleryManager.Instance)
        {
            ShootingGalleryManager.Instance.RestartGame();
        }
    }

    [ClientRpc]
    public override void LoadLevel(int level)
    {
        base.LoadLevel(level);
        //LoggerCommunicationProvider.Instance.AddToCustomData("shooting_mode", "\"" + mode[level] + "\"");
    }
}
