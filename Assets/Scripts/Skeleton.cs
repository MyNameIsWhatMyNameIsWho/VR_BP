using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saves all bone transforms of the hand
/// </summary>
public class Skeleton : MonoBehaviour
{
    [field:SerializeField] public List<Transform> Bones { get; private set; }
}
