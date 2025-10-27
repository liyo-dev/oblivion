using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Audio/Audio Graph Profile")]
public class AudioGraphProfile : ScriptableObject
{
    [Serializable] public class SceneMusic { public string sceneName; public AudioClip music; }
    [Serializable] public class EventSfx   { public string eventKey;  public AudioClip sfx;   }

    public List<SceneMusic> sceneMusic = new();
    public List<EventSfx>   eventSfx   = new();
}