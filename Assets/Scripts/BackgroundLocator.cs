using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets background transform in the scene 
/// </summary>
public class BackgroundLocator : MonoBehaviour
{
    private void Start()
    {
        GestureDetector.Instance.SetBGTransform(transform);
    }
}
