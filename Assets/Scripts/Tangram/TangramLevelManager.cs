using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

/// <summary>
/// Handles the information concerning the completion of a tangram puzzle.
/// </summary>
public class TangramLevelManager : NetworkBehaviour
{
    private GameMenuManager menuManager;
    private float saveTimeBeforeGestureCanceled;
    private bool gameWon = false;

    /// <value>
    /// Threshold distance from the correct piece placement
    /// </value>
    [SerializeField] private float tolerance = 0.03f;

    [SerializeField] private float lerpAnimationDuration = 1.0f;

    /// <value>
    /// Visual effect to be played after puzzle completion
    /// </value>
    [SerializeField] private NetworkVisualEffect effect;
    [SerializeField] private Transform effectPlacement; 

    [SerializeField] private List<TangramPiece> pieces;
    [SerializeField] private List<TangramSlot> slots;

    //hinting
    [SerializeField] private List<GameObject> hintPieces;
    [SerializeField] private float hintDisplayDuration = 5.0f, hintDisplayEnabledAfter = 30f;
    [SerializeField] private GameObject hintButton;
    private Coroutine hintEnablingCoroutine;

    //timer
    private float timeTillCompletion = 0;

    public bool autoSnapping = true; ///describes whether or not the player can move tangram pieces that has beed already placed correnctly

    private void Start()
    {
        if (!isServer) return;
        menuManager = FindFirstObjectByType<GameMenuManager>();

        foreach (var piece in pieces)
        {
            piece.OnPieceDropped.AddListener(SomePieceDropped);
        }

        saveTimeBeforeGestureCanceled = GestureDetector.Instance.timeBeforeGestureCanceled;
        GestureDetector.Instance.timeBeforeGestureCanceled = 0.05f;

        hintEnablingCoroutine = StartCoroutine(EnableHintDisplay());

        LoggerCommunicationProvider.Instance.StopLogging();
        LoggerCommunicationProvider.Instance.StartLogging(SceneManager.GetActiveScene().name);
        LoggerCommunicationProvider.Instance.AddToCustomData("tangram_level_name", "\"" + menuManager.currentlyPlayedLevel.name + "\"");

        AudioManager.Instance.PlayInstruction("InstructionsTangram");
    }

    private void Update()
    {
        if (!isServer) return;
        timeTillCompletion += Time.deltaTime;
    }

    private void OnDestroy()
    {
        if (!isServer) return;
        AudioManager.Instance.StopPlayingInstruction();
    }

    [ClientRpc]
    public void DestroyPieces()
    {
        foreach (TangramPiece p in pieces)
        {
            Destroy(p.gameObject);
        }
    }

    public void ReturnToTangramMenu()
    {
        GestureDetector.Instance.timeBeforeGestureCanceled = saveTimeBeforeGestureCanceled;
        foreach (TangramPiece p in pieces)
        {
            p.transform.SetParent(transform);
        }
        if (!isServer) return;

        menuManager.ReturnToGameMenu();
        GestureDetector.Instance.handL.Interactor.searchesForCollision = true;
        GestureDetector.Instance.handR.Interactor.searchesForCollision = true;

        LoggerCommunicationProvider.Instance.StopLogging();

        AudioManager.Instance.StopPlayingSFX();
    }

    /// <summary>
    /// Checks the correct placement of all pieces on all slots and gives feedback to the player
    /// </summary>
    //[ClientRpc]
    public void SomePieceDropped(TangramPiece piece)
    {
        // FindFirstObjectByType<AudioManager>().PlaySFX("TangramPieceDropped");
        piece.transform.SetParent(transform);

        if (!isServer) return;

        bool isSolved = true;
        foreach (TangramSlot slot in slots)
        {
            bool isCorrect = false;
            foreach (TangramPiece p in pieces)
            {
                bool correctlyPlaced = CheckCorrectPiecePlacement(p, slot);                

                if (correctlyPlaced)
                {
                    isCorrect = true;
                    p.placedInSlot = slot;

                    //autosnapping feature
                    if (p == piece) {  
                        if (autoSnapping && !slot.correctPiecePlaced)
                        {
                            slot.correctPiecePlaced = true;
                            DisableInteractionOnPiece(piece);
                        }
                        StopCoroutine(hintEnablingCoroutine);
                        hintEnablingCoroutine = StartCoroutine(EnableHintDisplay());
                    }

                    break;
                }
            }
            if (!isCorrect)
            {
                isSolved = false;
                slot.correctPiecePlaced = false;

                //break;
            }
            else
            {
                slot.correctPiecePlaced = true;
            }
        }

        if (isSolved && !gameWon)
        {
            GameWon();
        }
    }

    /// <summary>
    /// Disables interaction with a given tangram piece.
    /// </summary>
    /// <param name="piece">Given tangram piece that will have interaction disabled.</param>
    private void DisableInteractionOnPiece(TangramPiece piece)
    {
        StartCoroutine(LerpPieceInplace(piece));
        piece.CanInteract = false;
    }

    /// <summary>
    /// Animates all tangram pieces into their respective slots after the player completes the puzzle.
    /// </summary>
    private IEnumerator LerpPiecesInPlace() {
        float currTime = 0.0f;
        while (currTime < lerpAnimationDuration)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].transform.position = new Vector3(
                    Mathf.Lerp(pieces[i].transform.position.x, pieces[i].placedInSlot.transform.position.x, currTime / lerpAnimationDuration),
                    Mathf.Lerp(pieces[i].transform.position.y, pieces[i].placedInSlot.transform.position.y, currTime / lerpAnimationDuration),
                    Mathf.Lerp(pieces[i].transform.position.z, pieces[i].placedInSlot.transform.position.z + 0.02f, currTime / lerpAnimationDuration));

                currTime += Time.deltaTime;
                yield return null;
            }            
        }

        foreach (TangramPiece p in pieces)
        {
            p.transform.position = p.placedInSlot.transform.position;            
        }
        
        yield return null;
    }

    /// <summary>
    /// Animates a tangram piece into its respective slot after the player places it correctly.
    /// </summary>
    private IEnumerator LerpPieceInplace(TangramPiece piece)
    {
        float currTime = 0.0f;
        while (currTime < lerpAnimationDuration)
        {
            piece.transform.position = new Vector3(
                    Mathf.Lerp(piece.transform.position.x, piece.placedInSlot.transform.position.x, currTime / lerpAnimationDuration),
                    Mathf.Lerp(piece.transform.position.y, piece.placedInSlot.transform.position.y, currTime / lerpAnimationDuration),
                    Mathf.Lerp(piece.transform.position.z, piece.placedInSlot.transform.position.z + 0.02f, currTime / lerpAnimationDuration)); 
            
            currTime += Time.deltaTime;
            yield return null;
        }

        piece.transform.position = piece.placedInSlot.transform.position + new Vector3(0, 0, 0.02f);
    }

    /// <summary>
    /// Checks if the piece has a type, position and rotation corresponding to the slot.
    /// </summary>
    /// <returns>
    /// True if the values correspond, false otherwise.
    /// </returns>
    public bool CheckCorrectPiecePlacement(TangramPiece piece, TangramSlot slot)
    {
        //check piece type
        if (piece.type != slot.correctPieceType) return false;

        //check piece position
        Vector3 positionDifference = piece.transform.position - slot.transform.position;
        positionDifference.z = 0;
        if (positionDifference.magnitude > tolerance) return false;

        //check piece rotation
        float symmetryAngle = GetSymmetryAngle(piece.type);

        float pieceRotationZ = piece.transform.eulerAngles.z;
        while (pieceRotationZ < 0.1f) pieceRotationZ += 360.0f;
        while (pieceRotationZ > 360.1f) pieceRotationZ -= 360.0f;

        bool someRotationCorrect = false;

        for (float i = 0.0f; i <= 360.1f; i += symmetryAngle)
        {
            float val = slot.transform.eulerAngles.z + i;
            while (val < 0.1f) val += 360.0f;
            while (val > 360.1f) val -= 360.0f;


            if (Mathf.Abs(pieceRotationZ - val) < 0.1f)
            {
                someRotationCorrect = true;
                break;
            }
        }
        //print(someRotationCorrect);

        return someRotationCorrect;
    }

    private void GameWon()
    {   
        StartCoroutine(LerpPiecesInPlace());                //perform animation of putting all pieces where they belong
        pieces.ForEach(p => p.CanInteract = false);         //disable interaction on all pieces
        NetworkVisualEffect vfx = Instantiate(effect, effectPlacement.position, Quaternion.identity);
        NetworkServer.Spawn(vfx.gameObject);
        vfx.Play();                                      //play visual effect
        AudioManager.Instance.PlaySFX("SuccessFeedback");   //play sound
        gameWon = true;

        if (!isServer) return;
        LoggerCommunicationProvider.Instance.TangramWon();
        LoggerCommunicationProvider.Instance.AddToCustomData("tangram_completion", "\"" + timeTillCompletion.ToString() + "\"");

        //save data about player's game
        float tmpTime;
        if (menuManager.currentlyPlayedLevel.name.Contains("Horse"))
        {
            UserSystem.Instance.UserData.numTimesHorseCompleted += 1;
            tmpTime = UserSystem.Instance.UserData.FastestHorseCompletion;
            UserSystem.Instance.UserData.FastestHorseCompletion = (tmpTime > -1) ? ((tmpTime > timeTillCompletion) ? timeTillCompletion : tmpTime) : timeTillCompletion;
            LoggerCommunicationProvider.Instance.AddToCustomData("tangram_fastest_completion", "\"" + UserSystem.Instance.UserData.FastestHorseCompletion.ToString() + "\"");
        } 
        else if (menuManager.currentlyPlayedLevel.name.Contains("Heart"))
        {
            UserSystem.Instance.UserData.numTimesHeartCompleted += 1;
            tmpTime = UserSystem.Instance.UserData.FastestHeartCompletion;
            UserSystem.Instance.UserData.FastestHeartCompletion = (tmpTime > -1) ? ((tmpTime > timeTillCompletion) ? timeTillCompletion : tmpTime) : timeTillCompletion;
            LoggerCommunicationProvider.Instance.AddToCustomData("tangram_fastest_completion", "\"" + UserSystem.Instance.UserData.FastestHeartCompletion.ToString() + "\"");
        } 
        else if (menuManager.currentlyPlayedLevel.name.Contains("ShapesEasy"))
        {
            UserSystem.Instance.UserData.numTimesShapesEasyCompleted += 1;
            tmpTime = UserSystem.Instance.UserData.FastestShapesEasyCompletion;
            UserSystem.Instance.UserData.FastestShapesEasyCompletion = (tmpTime > -1) ? ((tmpTime > timeTillCompletion) ? timeTillCompletion : tmpTime) : timeTillCompletion;
            LoggerCommunicationProvider.Instance.AddToCustomData("tangram_fastest_completion", "\"" + UserSystem.Instance.UserData.FastestShapesEasyCompletion.ToString() + "\"");
        } else {
            Debug.LogError("[VR for lying patients] Name of finished tangram level not found");
        }
        
    }



    /// <summary>
    /// Takes a non-placed piece and places it where it belongs.
    /// </summary>
    public void RequestHint()
    {
        if (gameWon) return;

        hintEnablingCoroutine = StartCoroutine(EnableHintDisplay());

        TangramPiece piece = pieces.Find((p) => !p.correctlyPlaced && !p.placedInSlot);
        TangramSlot slot = slots.Find((s) => piece.type == s.correctPieceType && !s.correctPiecePlaced);

        piece.placedInSlot = slot;
        piece.correctlyPlaced = true;
        slot.correctPiecePlaced = true;

        if (autoSnapping) DisableInteractionOnPiece(piece);
        else StartCoroutine(LerpPieceInplace(piece));
        
        piece.transform.rotation = piece.placedInSlot.transform.rotation;

        if (slots.TrueForAll((s) => pieces.Exists((p) => CheckCorrectPiecePlacement(p,s)))) { 
            StartCoroutine(LerpPiecesInPlace());

            pieces.ForEach(p => p.CanInteract = false);
            effect.Play();
            AudioManager.Instance.PlaySFX("SuccessFeedback");
            gameWon = true;
        }
    }

    public void RequestHint2()
    {
        if (gameWon) return;

        hintEnablingCoroutine = StartCoroutine(EnableHintDisplay());

        TangramPiece piece = pieces.Find((p) => !p.correctlyPlaced && !p.placedInSlot);
        TangramSlot slot = slots.Find((s) => piece.type == s.correctPieceType && !s.correctPiecePlaced);

        StartCoroutine(piece.ShowHintPiece(slot, hintDisplayDuration));
    }

    public IEnumerator EnableHintDisplay() {
        hintButton.SetActive(false);
        yield return hintDisplayEnabledAfter;
        hintButton.SetActive(true);
    }

    /// <returns>
    /// A symmetry angle according to the given tangram piece type.
    /// </returns>
    private float GetSymmetryAngle(TangramPieceType type)
    {
        float angle;
        switch (type)
        {
            case TangramPieceType.Square:
                angle = 90.0f;
                break;
            case TangramPieceType.Trapezoid:
                angle = 180.0f;
                break;
            default:
                angle = 360.0f;
                break;
        }
        return angle;
    }
}
