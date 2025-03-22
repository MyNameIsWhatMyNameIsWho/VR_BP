using Mirror;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
public class TangramLevelMenuManager : GameMenuManager
{
    [SerializeField] private TextMeshPro autoSnappingtext;

    private TangramLevelManager tangramLevel;
    [field: SerializeField] public UnityEvent<bool> OnModeChange { get; private set; }

    public bool autoSnapping { get; private set; } = false;


    private void OnEnable()
    {
        onLevelLoad.AddListener(() => StartCoroutine(SetLevel()));
    }

    private IEnumerator SetLevel() {
        while (currentlyPlayedLevel == null) yield return null;
        tangramLevel = currentlyPlayedLevel.GetComponent<TangramLevelManager>();
        tangramLevel.autoSnapping = autoSnapping;
    }

    public void SwapAutoSnapping() {
        autoSnapping = !autoSnapping;
        UpdateMode(autoSnapping);
    }

    [ClientRpc]
    public void UpdateMode(bool active) {
        autoSnappingtext.text = active ? "Aktivní" : "Neaktivní";
        OnModeChange.Invoke(active);
    }

}
