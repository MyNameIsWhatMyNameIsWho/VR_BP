using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyButtons;
using Unity.VisualScripting;

/// <summary>
/// Class that helps with piece management in the scene when creating new tangram puzzle
/// </summary>
public class TangramPieceEditorActions : MonoBehaviour
{
    TangramPiece piece;

    /// <summary>
    /// Editor button for easily creating empty object that saves this piece's correct transform
    /// </summary>
    [Button]
    private void CreatePrefab()
    {
        if (!piece) piece = GetComponent<TangramPiece>();
        
        GameObject newPlacement = new GameObject(string.Format("Placement-{0}", piece.name));
        newPlacement.transform.position = transform.position;

        var rot = transform.rotation.eulerAngles;
        /*while (rot.z < 0.1f) rot.z += 360.0f;
        while (rot.z > 360.1f) rot.z -= 360.0f;*/
        newPlacement.transform.rotation = Quaternion.Euler(rot);

        newPlacement.AddComponent<TangramSlot>();
        newPlacement.GetComponent<TangramSlot>().SetValues(piece.type);

        newPlacement.transform.SetParent(transform.parent.GetChild(8));

        print("prefab ok");
    }

    /// <summary>
    /// Editor button for easily rotating piece by angleFactor when creating level asset
    /// </summary>
    [Button]
    private void RotateObject()
    {
        if (!piece) piece = GetComponent<TangramPiece>();

        Vector3 rotation = transform.rotation.eulerAngles;
        rotation.z = (rotation.z + piece.angleFactor) % 360;
        transform.rotation = Quaternion.Euler(rotation);
    }

#if UNITY_EDITOR 

    [Button]
    private void CreateHintPiece()
    {
        GameObject placements = null, hintPieces = null;
        foreach (Transform o in transform.parent.transform)
        {
            if (o.name == "Placements")
            {
                placements = o.gameObject;
            }
            if (o.name == "HintPieces")
            {
                hintPieces = o.gameObject;
            }
        }

        if (!hintPieces) { 
            Debug.LogError("No empty object called HintPieces found");
            return;
        }

        if (!piece) piece = GetComponent<TangramPiece>();

        GameObject correctPlacement = null;
        foreach (Transform o in placements.transform)
        {
            if (o.name == string.Format("Placement-{0}", piece.name))
            {
                correctPlacement = o.gameObject;
                break;
            }
        }

        if (correctPlacement)
        {
            GameObject newHintPiece = new GameObject(string.Format("Hint-{0}", piece.name));
            newHintPiece.transform.SetParent(hintPieces.transform);
            newHintPiece.transform.position = correctPlacement.transform.position;
            newHintPiece.transform.rotation = correctPlacement.transform.rotation;
            newHintPiece.transform.localScale = this.gameObject.transform.localScale;


            var meshRenderer = newHintPiece.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = piece.ghostPiece.gameObject.GetComponent<MeshRenderer>().sharedMaterial;

            var meshFilter = newHintPiece.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = piece.gameObject.GetComponent<MeshFilter>().sharedMesh;
            piece.hintPiece = newHintPiece;
            UnityEditor.PrefabUtility.InstantiatePrefab(newHintPiece);


        } else
        {
            Debug.LogError("Prefab piece and placement doesn't exist yet for " + piece.name);
        }


    }
# endif
}
