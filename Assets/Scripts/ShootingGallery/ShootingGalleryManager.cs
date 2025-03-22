using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using TMPro;
using System.Globalization;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the shooting game actions, keeps track of score/shot targets, begins and ends the game
/// </summary>
public class ShootingGalleryManager : NetworkBehaviour
{

    public static ShootingGalleryManager Instance;
    public UnityEvent onGameEnd, onGameStart;
    private GameMenuManager menuManager;

    private bool gameBegun = false;
    private float currTime = 0.0f;
    private float Highscore { 
        get {
            switch (GameMode)
            {
                case "Slow Timed":
                    return UserSystem.Instance.UserData.HighscoreSlowTimed;
                case "Normal Timed":
                    return UserSystem.Instance.UserData.HighscoreNormalTimed;
                case "Fast Timed":
                    return UserSystem.Instance.UserData.HighscoreFastTimed;
                case "Slow Untimed":
                    return UserSystem.Instance.UserData.HighscoreSlowUntimed;
                case "Normal Untimed":
                    return UserSystem.Instance.UserData.HighscoreNormalUntimed;
                case "Fast Untimed":
                    return UserSystem.Instance.UserData.HighscoreFastUntimed;
                default:
                    Debug.LogError("[VR for lying patients] Game Mode name does not match any known one.");
                    return -1;
            }
        }

        set {
            switch (GameMode)
            {
                case "Slow Timed":
                    UserSystem.Instance.UserData.HighscoreSlowTimed = value;
                    break;
                case "Normal Timed":
                    UserSystem.Instance.UserData.HighscoreNormalTimed = value;
                    break;
                case "Fast Timed":
                    UserSystem.Instance.UserData.HighscoreFastTimed = value;
                    break;
                case "Slow Untimed":
                    UserSystem.Instance.UserData.HighscoreSlowUntimed = value;
                    break;
                case "Normal Untimed":
                    UserSystem.Instance.UserData.HighscoreNormalUntimed = value;
                    break;
                case "Fast Untimed":
                    UserSystem.Instance.UserData.HighscoreFastUntimed = value;
                    break;
                default:
                    Debug.LogError("[VR for lying patients] Game Mode name does not match any known one.");
                    break;
            }
        } 
    }

    private int score = 0;
    public int numShots = 0;
    private int numSuccesfulShots = 0;

    public string GunMode {  get; set; }
    [field:SerializeField] public string GameMode {  get; set; }

    [SerializeField] private bool timedMode = true;

    public GunChoosingMenu gunChoosingMenu;
    [SerializeField] private TextMeshPro scoreText, timeText;
    [SerializeField] private float secondsAfterGameEnd;
    [SerializeField] private NetworkVisualEffect endGameEffect;

    //timed
    [SerializeField] float maxTime;

    //non-timed
    [SerializeField] int scoreToWin;

    //endgame info
    [SerializeField] private TextMeshPro finalScoreText, highScoreText;
    [SerializeField] private GameObject endgameInfo;


    private void Awake() {
        if (Instance != null) { 
            Destroy(gameObject);
            return;
        }

        Instance = this;        
    }

    private void Start()
    {  
        menuManager = FindFirstObjectByType<GameMenuManager>();
        if (!timedMode) timeText.gameObject.SetActive(false);

        endgameInfo.SetActive(false);
        if (!isServer) return;

        gunChoosingMenu.onGunChoose.AddListener(GunChosen);
        GestureDetector.Instance.handL.Interactor.SetVisualizeRaycast(false);
        GestureDetector.Instance.handR.Interactor.SetVisualizeRaycast(false);
        menuManager.onMainMenuLoad.AddListener(PerformLeavingActions);

        StartCoroutine(PlayInstructions());
    }

    private IEnumerator PlayInstructions()
    {
        string mode = (timedMode ? "Timed" : "Untimed");
        yield return null;
        yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsSG" + mode));
        yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsSGEnd"));
    }

    [ClientRpc]
    private void GunChosen(string gunMode)
    {
        GunMode = gunMode;
        gameBegun = false;
        score = 0;
        ChangeScore(score);
        onGameStart.Invoke();
    }

    void Update()
    {
       if (gameBegun && isServer)  currTime += Time.deltaTime;
        if (!timedMode || !gameBegun || !isServer) return;

        ChangeTime(currTime);   
        if (currTime >= maxTime) EndGame();
    }

    private void OnDestroy()
    {
        if (!isServer) return;
        AudioManager.Instance.StopPlayingInstruction();
    }

    [ClientRpc]
    private void StartGame()
    {

        numShots = 0; numSuccesfulShots = 0;
        currTime = 0;
        gameBegun = true;

        if (!isServer) return;
        if (LoggerCommunicationProvider.Instance.loggingStarted) LoggerCommunicationProvider.Instance.StopLogging();
        LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
    }

    [ClientRpc]
    private void EndGame()
    {
        gameBegun = false;
        onGameEnd.Invoke();

        if (!isServer) return;
        if (timedMode && score > Highscore) Highscore = score;
        else if (!timedMode && currTime < Highscore) Highscore = currTime;
        DisplayEndGameInfo(true, Highscore, timedMode ? score : currTime);
        //Debug.Log("END of isTimed = " + timedMode.ToString() + " score was " + score);        
    }

    [ClientRpc]
    public void RestartGame()
    {
        gameBegun = false;
        score = 0;
        ChangeScore(score);
        if (isServer) DisplayEndGameInfo(false, 0, 0);
        gunChoosingMenu.ResetSelection();
        onGameEnd.Invoke();
    }


    public void ReturnToShootingGalleryMenu()
    {
        PerformLeavingActions();
        menuManager.ReturnToGameMenu();
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display, float hiScore, float score)
    {
        StartCoroutine(DisplayEndGameInfoCoroutine(display, hiScore, score));
    }

    private IEnumerator DisplayEndGameInfoCoroutine(bool display, float hiScore, float score)
    {
        if (isServer) { 
            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_score", "\"" + score.ToString() + "\"");

            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_highscore", "\"" + Highscore.ToString() + "\"");

            string value = timedMode ? "120.00" : currTime.ToString("F2", CultureInfo.InvariantCulture);
            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_time", "\"" + value + "\"");

            value = numShots == 0 ? "\"none\"" : "\"" + numSuccesfulShots.ToString() + " / " + numShots.ToString() + "\"";
            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_accuracy", value);

            value = numShots == 0 ? "\"none\"" : "\"" + (score / numShots).ToString() + "\"";
            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_avg_score", value);

            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_gun", GunMode);
            LoggerCommunicationProvider.Instance.AddToCustomData("shooting_mode", "\"" + GameMode + "\"");


            LoggerCommunicationProvider.Instance.StopLogging();
            LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
        }

        if (display) {
            NetworkVisualEffect vfx = Instantiate(endGameEffect, endgameInfo.transform.position, Quaternion.identity);
            NetworkServer.Spawn(vfx.gameObject);
            vfx.transform.localScale *= 2;
            vfx.Play();
            AudioManager.Instance.PlaySFX("SuccessFeedback");

            yield return new WaitForSeconds(secondsAfterGameEnd);
        }

        endgameInfo.SetActive(display);
        finalScoreText.text = timedMode ? score.ToString() : score.ToString("F2", CultureInfo.InvariantCulture);

        highScoreText.text = hiScore.ToString("F2", CultureInfo.InvariantCulture);

    }

    private void PerformLeavingActions()
    {
        GestureDetector.Instance.handL.Interactor.SetVisualizeRaycast(true);
        GestureDetector.Instance.handR.Interactor.SetVisualizeRaycast(true);
        if (isServer && gunChoosingMenu && gunChoosingMenu.gun) NetworkServer.Destroy(gunChoosingMenu.gun);
    }

    public void TargetShot(int points)
    {
        if (!isServer) return;
        if (!gameBegun) StartGame();
        score += points;
        ChangeScore(score);
        numSuccesfulShots++;

        if (isServer) LoggerCommunicationProvider.Instance.TargetShot(points);

        if (!timedMode && score >= scoreToWin) EndGame();
    } 

    [ClientRpc]
    private void ChangeScore(int score)
    {
        scoreText.text = score.ToString();
    }

    [ClientRpc]
    private void ChangeTime(float time)
    {
        int diffTime = (int)maxTime - (int)time;
        var seconds = (diffTime % 60).ToString();
        timeText.text = (diffTime / 60).ToString() + ":" + ((seconds.Length == 1) ? "0" + seconds : seconds);
    }
}
