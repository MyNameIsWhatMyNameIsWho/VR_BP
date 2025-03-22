using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Mirror;

[System.Serializable]
public class Sound
{
    public List<AudioClip> audioClips;
    public string name;
    public bool interuptable = true;
}

[System.Serializable]
public class Music
{
    public AudioClip audioClip;
    public string sceneName;
}

/// <summary>
/// Handles playing sounds
/// </summary>
public class AudioManager : NetworkBehaviour 
{
    public static AudioManager Instance;

    [SerializeField] private Sound[] sounds;
    [SerializeField] private Music[] musicTracks;
    [SerializeField] private List<AudioSource> sourcesSFX;

    [SerializeField] private AudioSource sourceMusic;
    [SerializeField] private AudioSource sourceInstructions;
    [SerializeField] private AudioMixer audioMixerController;
   
    
    void Awake()
    {
        if (Instance != null) { 
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    private void Start()
    {
        if (!isServer) return;
        CmdSetMusicVolume(PlayerPrefs.GetFloat("VolumeMusic", 0.5f));
        CmdSetSFXVolume(PlayerPrefs.GetFloat("VolumeSFX", 0.5f));
        SceneManager.sceneLoaded += (scene, mode) => { PlayMusic(scene.name); };
        PlayMusic("MainMenuSceneOnline");        
    }

    public float PlaySFX(string name)
    {
        if (!isServer) return 0;
        Sound s = Array.Find(sounds, s => s.name == name);
        if (s != null)
        {
            int idx = UnityEngine.Random.Range(0, s.audioClips.Count);
            var source = sourcesSFX.Find((s) => !s.isPlaying);
            if (!source)
            {
                Debug.Log("XXXXXXXX - Found no unused SFXsource");
                source = sourcesSFX.Find((src) => {
                    var s = Array.Find(sounds, sound => src.clip == sound.audioClips[0]);
                    return s.interuptable;
                });
                Debug.Log("XXXXX - The newly found source plays " + source.clip.ToString());
            }
            source.clip = s.audioClips[idx];
            source.Play();
            Debug.Log("Playing " + name);
            return source.clip.length;
        }
        return 0;
    }

    public float PlayInstruction(string name)
    {
        if (!isServer) return 0;        
        sourceInstructions.clip = Array.Find(sounds, s => s.name == name).audioClips[0];
        sourceInstructions.Play();
        return sourceInstructions.clip.length;
    }

    public void PlayMusic(string sceneName)
    {
        if (!isServer) return;
        else return;
        sourceMusic.clip = Array.Find(musicTracks, s => s.sceneName == sceneName).audioClip;
        sourceMusic.Play();
    }

    public void StopPlayingSFX()
    {
        if (!isServer) return;
        sourcesSFX.ForEach((s) => s.Stop());
    }

    public void StopPlayingInstruction()
    {
        if (!isServer) return;
        if (sourceInstructions != null && sourceInstructions.isPlaying) sourceInstructions.Stop();
    }

    public void StopPlayingMusic()
    {
        if (!isServer) return;
        sourceMusic.Stop();
    }

    [Command(requiresAuthority = false)]
    public void CmdSetSFXVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.001f, 1.0f);
        audioMixerController.SetFloat("VolumeSFX", Mathf.Log(volume) * 20);
        SetSFXVolume(volume);
        
    }

    [ClientRpc]
    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("VolumeSFX", volume);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetMusicVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.001f, 1.0f);
        audioMixerController.SetFloat("VolumeMusic", Mathf.Log(volume) * 20);
        SetMusicVolume(volume);
    }

    [ClientRpc]
    public void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat("VolumeMusic", volume);
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat("VolumeSFX", 0.5f);
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat("VolumeMusic", 0.5f);
    }
}
