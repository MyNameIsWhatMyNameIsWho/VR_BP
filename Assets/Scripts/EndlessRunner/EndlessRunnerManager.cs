using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class EndlessRunnerManager : NetworkBehaviour
{
    public static EndlessRunnerManager Instance;

    [SerializeField] private List<GameObject> obstaclesPrefabs;
    [SerializeField] private List<GameObject> scoreModePrefabs;

    [SerializeField] private GameObject buttons;
    [SerializeField] private GameObject inGameButton;
    [SerializeField] private GameObject switchHandsButton;

    [SerializeField] private Transform spawnPoint, despawnPoint;
    [SerializeField] private float movementSpeed = 2.00f, coinValue = 5.0f;
    [SerializeField] private Character character;
    [SerializeField] private TextMeshPro scoreText;

    //endgame info
    [SerializeField] private TextMeshPro finalScoreText, highScoreText;
    [SerializeField] private GameObject endgameInfo;
    [SerializeField] private float waitAfterDeath;

    [SerializeField] private AnimationCurve gameSpeed;
    [SerializeField] private float maxSpeedMultiplier, maxSpeedScore;
    [SerializeField] private float maxAudioHelpScore = 100f, obstacleHittableMinScore = 80;

    private float SpeedMultiplier { get { return 1 + (gameSpeed.Evaluate(score / maxSpeedScore) * (maxSpeedMultiplier - 1)); } }

    private GameMenuManager menuManager;
    private List<GameObject> obstaclesInstances;
    private bool gameRunning = false;

    public float score = 0f;
    public bool hasObstacles = true; ///game either spawns obstacles with coins or only coins in prefabs
    public UnityEvent OnGameStart;
    public UnityEvent OnGameEnd;

    ///non obstacle game (score mode) timers
    [SerializeField] private float gameDuration = 120f;
    [SerializeField] private TextMeshPro timeText;
    private float currentTime = 0f;

    //data for logging
    private float coinsSpawned = 0, coinsCollected = 0;
    private float Highscore
    {
        get
        {
            if (hasObstacles) {
                return UserSystem.Instance.UserData.HighscoreWithObstacles;
            } else {
                return UserSystem.Instance.UserData.HighscoreWithoutObstacles;
            }
        }

        set
        {
            if (hasObstacles) {
                UserSystem.Instance.UserData.HighscoreWithObstacles = value;
            } else {
                UserSystem.Instance.UserData.HighscoreWithoutObstacles = value;
            }
        }
    }


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        menuManager = FindFirstObjectByType<GameMenuManager>();
        OnGameEnd.AddListener(() => { buttons.SetActive(true); inGameButton.SetActive(false); });
        OnGameStart.AddListener(() => { buttons.SetActive(false); inGameButton.SetActive(true); });
        endgameInfo.SetActive(false);

        if (!isServer) return;
        obstaclesInstances = new List<GameObject>();
        character.OnActionPerform.AddListener(StartGame);
        character.OnCharacterHit.AddListener(ResolveHit);
        character.OnCoinPickup.AddListener(() => { 
            score += coinValue; ChangeScore((int)score);
            coinsCollected++;
        });
        //UprightRedirector.Instance.PerformTranslation();

        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);

        if (GestureDetector.Instance.handR.IsDisabled) {
            character.controllingHandIsLeft = !character.controllingHandIsLeft;
            GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
            GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        }

        if (GestureDetector.Instance.handR.IsDisabled || GestureDetector.Instance.handL.IsDisabled)
        {
            switchHandsButton.SetActive(false);
        }

        StartCoroutine(PlayInstructions());
        
    }

    private IEnumerator PlayInstructions()
    {
        string mode = hasObstacles ? "Obstacles" : "Screws";
        yield return null;
        yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsER" + mode));
        yield return new WaitForSeconds(AudioManager.Instance.PlayInstruction("InstructionsEREnd"));
    }

    private void FixedUpdate()
    {
        if (!gameRunning) return;

        for (int i = 0; i < obstaclesInstances.Count; i++) //move prefabs
        {
            var newPos = obstaclesInstances[i].transform.position;
            newPos.x -= movementSpeed * SpeedMultiplier * Time.fixedDeltaTime;
            obstaclesInstances[i].transform.position = newPos;

        }

        for (int i = 0; i < obstaclesInstances.Count; i++) //destroy no longer needed prefabs
        {
            if (obstaclesInstances[i].transform.position.x <= despawnPoint.position.x)
            {
                var tmp = obstaclesInstances[i];
                obstaclesInstances.RemoveAt(i);
                NetworkServer.Destroy(tmp);        
                return;
            }
        }

        score += movementSpeed * SpeedMultiplier * Time.fixedDeltaTime;
        if (isServer && score > obstacleHittableMinScore) character.VisualizeInvulnerability(false);
        ChangeScore((int)score);
        character.jumpSpeed = movementSpeed * SpeedMultiplier;

        if (!hasObstacles) {
            int timeLeft = (int) (gameDuration - currentTime);
            DisplayTimeText(timeLeft);
            currentTime += Time.fixedDeltaTime;
            if (currentTime >= gameDuration) EndGame();
        }
    }

    private void OnDestroy()
    {
        if (isServer) foreach (var o in obstaclesInstances) NetworkServer.Destroy(o);
        AudioManager.Instance.StopPlayingInstruction();
    }


    private void ResolveHit() {
        if (!hasObstacles) return;

        if (score < obstacleHittableMinScore)
        {
            score = 0;
            scoreText.text = "0";
            if (!isServer) return;
            LoggerCommunicationProvider.Instance.RecordEvent("HOIT");
            /// TODO make some sound
        } else
        {
            if (isServer) LoggerCommunicationProvider.Instance.RecordEvent("HO");
            EndGame();  
        }
    }

    [ClientRpc]
    public void EndGame()
    {
        Debug.Log("End game");
        StopAllCoroutines();
        StartCoroutine(EndGameCoroutine());
    }

    public IEnumerator EndGameCoroutine()
    {
        if (isServer && gameRunning) { 
            character.GameOver();
            gameRunning = false;
            GestureDetector.Instance.handL.SetControllingHand(false);
            GestureDetector.Instance.handR.SetControllingHand(false);
            character.animator.SetBool("RunningBool", false);
            character.animator.SetTrigger("Dead");

            Debug.Log("End game coroutine");
            yield return new WaitForSeconds(waitAfterDeath);
            Debug.Log("End game coroutine after wait");

            if (Highscore < score) Highscore = score;
            DisplayEndGameInfo(true, (int)Highscore, (int)score);

            LoggerCommunicationProvider.Instance.AddToCustomData("endless_runner_score", "\"" + score.ToString() + "\"");
            LoggerCommunicationProvider.Instance.AddToCustomData("endless_runner_highscore", "\"" + Highscore.ToString() + "\"");
            LoggerCommunicationProvider.Instance.AddToCustomData("endless_runner_coins_collected", "\"" + coinsCollected.ToString() + "\"");
            LoggerCommunicationProvider.Instance.AddToCustomData("endless_runner_coin_collection_accuracy", "\"" + coinsCollected.ToString() + " / " + coinsSpawned.ToString() + "\"");
            LoggerCommunicationProvider.Instance.StopLogging();
        }

        OnGameEnd.Invoke();
    }

    [ClientRpc]
    private void DisplayEndGameInfo(bool display, int hiScore, int score)
    {
        Debug.Log("END GAME INFO = " + display.ToString());
        endgameInfo.SetActive(display);
        if (!display) return;
        finalScoreText.text = score.ToString();
        highScoreText.text = hiScore.ToString();
    }

    [ClientRpc]
    private void DisplayTimeText(int timeLeft)
    {
        timeText.gameObject.SetActive(true);
        timeText.text = ((timeLeft / 60) < 10 ? "0" : "") + (timeLeft / 60).ToString() + ":" + ((timeLeft % 60) < 10 ? "0" : "") + (timeLeft % 60).ToString();
    }

    [ClientRpc]
    public void StartGame()
    {
        OnGameStart.Invoke();
        if (!isServer || gameRunning) return;

        character.VisualizeInvulnerability(hasObstacles);
        coinsSpawned = 0; 
        coinsCollected = 0;
        gameRunning = true;
        AudioManager.Instance.PlaySFX("RobotReboot");
        character.animator.SetTrigger("Running");
        character.animator.SetBool("RunningBool", true);
        StartCoroutine(SpawningCoroutine(hasObstacles ? obstaclesPrefabs : scoreModePrefabs));
        LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
        LoggerCommunicationProvider.Instance.AddToCustomData("endless_runner_mode", hasObstacles ? "\"With obstacles\"" : "\"Without obstacles\"");
    }
    private IEnumerator SpawningCoroutine (List<GameObject> prefabs)
    {
        score = 0f;
        ChangeScore((int)score);

        int o = Random.Range(0, prefabs.Count);
        var obj = Instantiate(prefabs[o], new Vector3(spawnPoint.position.x, prefabs[o].GetComponent<Obstacle>().spawningHeightModifier, spawnPoint.position.z), Quaternion.identity);
        NetworkServer.Spawn(obj);
        obstaclesInstances.Add(obj);

        while (true)
        {
            o = Random.Range(0, prefabs.Count);
            obj = Instantiate(prefabs[o], new Vector3(spawnPoint.position.x, prefabs[o].GetComponent<Obstacle>().spawningHeightModifier, spawnPoint.position.z), Quaternion.identity);
            NetworkServer.Spawn(obj);

            if (score > maxAudioHelpScore && obj.GetComponent<Obstacle>().soundTrigger != null) { 
                obj.GetComponent<Obstacle>().soundTrigger.gameObject.SetActive(false);
            }
            
            yield return new WaitForSeconds(obj.GetComponent<Obstacle>().distanceBefore / (movementSpeed * SpeedMultiplier));
            obstaclesInstances.Add(obj);

            character.IncreaseAnimationSpeed(SpeedMultiplier);

            coinsSpawned += obj.GetComponent<Obstacle>().numCoins;
        }
    }

    public void ReturnToEndlessRunnerMenu()
    {
        EndGame();
        character.OnActionPerform.RemoveListener(StartGame);
        character.OnCharacterHit.RemoveListener(ResolveHit);
        menuManager.ReturnToGameMenu();
    }


    public void RestartGame()
    {
        if (!isServer) return;
        foreach (var o in obstaclesInstances) NetworkServer.Destroy(o);
        obstaclesInstances.Clear();
        character.Spawn();
        gameRunning = false;
        AudioManager.Instance.PlaySFX("RobotReboot");
        character.animator.SetTrigger("Alive");
        currentTime = 0f;
        coinsSpawned = 0f;
        coinsCollected = 0f;
        score = 0f;
        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
        DisplayEndGameInfo(false, 0, 0);
        StopAllCoroutines();
        StartGame();
    }

    [ClientRpc]
    private void ChangeScore(int score)
    {
        scoreText.text = score.ToString();
    }

    
    public void SwitchHands()
    {
        if (GestureDetector.Instance.handL.IsDisabled || GestureDetector.Instance.handR.IsDisabled) return;

        character.controllingHandIsLeft = !character.controllingHandIsLeft;
        GestureDetector.Instance.handL.SetControllingHand(character.controllingHandIsLeft);
        GestureDetector.Instance.handR.SetControllingHand(!character.controllingHandIsLeft);
    }
}
