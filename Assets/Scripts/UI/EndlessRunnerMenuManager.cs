using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EndlessRunnerMenuManager : GameMenuManager
{
    private EndlessRunnerManager endlessRunnerManager;

    private UnityAction removeListener;
    [SerializeField] private List<GameObject> inGameButtons;

    public void LoadObstacleLevel(int level)
    {
        base.LoadLevel(level);
        removeListener = AddNewListener(() => StartCoroutine(SetGameMode(true)));
        //onLevelLoad.AddListener(() => StartCoroutine(SetGameMode(true)));
    }

    public void LoadNoObstacleLevel(int level)
    {
        base.LoadLevel(level);
        removeListener = AddNewListener(() => StartCoroutine(SetGameMode(false)));
        //onLevelLoad.AddListener(() => StartCoroutine(SetGameMode(false)));
    }

    UnityAction AddNewListener(UnityAction fn)
    {
        onLevelLoad.AddListener(fn);
        // Return a parameterless action that, when called, will remove the listener.
        return () => onLevelLoad.RemoveListener(fn);
    }

    private IEnumerator SetGameMode(bool hasObstacles)
    {
        while (currentlyPlayedLevel == null) yield return null;

        //print("setting game with obstacles - " + hasObstacles.ToString());
        endlessRunnerManager = currentlyPlayedLevel.GetComponent<EndlessRunnerManager>();
        endlessRunnerManager.hasObstacles = hasObstacles;

        endlessRunnerManager.OnGameStart.AddListener(
            () => SetInGameButtonsVisibility(false)
            );
        endlessRunnerManager.OnGameEnd.AddListener(
            () => SetInGameButtonsVisibility(true)
            );

        removeListener();        
    }

    private void SetInGameButtonsVisibility(bool visible)
    {
        foreach (var button in inGameButtons)
        {
            button.SetActive(visible);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdSwitchHands()
    {
        if (EndlessRunnerManager.Instance)
        {
            EndlessRunnerManager.Instance.SwitchHands();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdResetEndlessRunnerGame()
    {
        if (EndlessRunnerManager.Instance)
        {
            EndlessRunnerManager.Instance.RestartGame();
        }
    }


}
