using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saves information about the tangram slot
/// </summary>
public class TangramSlot : MonoBehaviour
{
    [SerializeField] public TangramPieceType correctPieceType;
    public bool correctPiecePlaced = false;

    public void SetValues(TangramPieceType correctPieceType)
    {
        this.correctPieceType = correctPieceType;
    }

}
