using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundTrigger : MonoBehaviour
{
    [SerializeField] bool isPaper;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("InvulnerabilityBubble"))
        {
            AudioManager.Instance.PlaySFX(isPaper ? "PaperSound" : "RockSound");
        }
    }
}
