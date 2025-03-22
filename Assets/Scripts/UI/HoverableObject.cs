using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles the visual feedback of objects - material color change and outline
/// </summary>
[RequireComponent(typeof(Outline))]
public class HoverableObject : MonoBehaviour
{
    [SerializeField] private float colorLightenFactor = 3;
    private Material material;
    private Color defaultColor;
    private HashSet<Interactor> interactors;

    public UnityEvent onHoverBegin;
    public UnityEvent onHoverEnd;

    private void Awake()
    {
        GetComponent<Outline>().enabled = false;
        material = GetComponent<Renderer>().material;
        interactors = new HashSet<Interactor>();
        defaultColor = material.color;
    }

    public void HoverBegin(Interactor interactor)
    {
        if (interactors == null) return;
        if (interactors.Count == 0) { 
            GetComponent<Outline>().enabled = true;
            material.color *= colorLightenFactor;
            onHoverBegin.Invoke();
        }
        interactors.Add(interactor);
    }

    public void HoverEnd(Interactor interactor)
    {
        if (interactors == null) return;
        if (interactors.Count == 1)
        {
            GetComponent<Outline>().enabled = false;
            material.color = defaultColor;
            onHoverEnd.Invoke();
        }
        interactors.Remove(interactor);
    }

    private void OnDisable()
    {
        GetComponent<Outline>().enabled = false;
        material.color = defaultColor;
        onHoverEnd.Invoke();

        interactors.Clear();
    }
}
