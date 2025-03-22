using Mirror;
using System.Collections;
using UnityEngine;

public class NewGameMenuManager : GameMenuManager
{
    private NewGameManager newGameManager;

    [ClientRpc]
    public override void LoadLevel(int level)
    {
        // Call the base method to handle instantiating the level prefab
        base.LoadLevel(level);

        // After calling base.LoadLevel, the level is spawned asynchronously,
        // so we start a coroutine to set up the game once it's ready.
        StartCoroutine(SetupGameAfterLoad());
    }

    private IEnumerator SetupGameAfterLoad()
    {
        // Wait until the currentlyPlayedLevel is assigned
        while (currentlyPlayedLevel == null) yield return null;

        // Get the main game logic component from the spawned level prefab
        newGameManager = currentlyPlayedLevel.GetComponent<NewGameManager>();
        // At this point, newGameManager is ready and you can start your game logic if needed.
    }

    [Command(requiresAuthority = false)]
    public void CmdMoveLeft()
    {
        if (newGameManager)
        {
            //newGameManager.MoveCubeLeft();
            return;
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdMoveRight()
    {
        if (newGameManager)
        {
            //newGameManager.MoveCubeRight();
            return;
        }
    }
}
