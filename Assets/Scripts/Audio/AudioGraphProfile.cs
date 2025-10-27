// Assets/Scripts/Audio/AudioGraphProfile.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Audio/Audio Graph Profile")]
public class AudioGraphProfile : ScriptableObject
{
    [Serializable] public class SceneMusic { public string sceneName; public AudioClip music; }
    [Serializable] public class EventSfx   { public string eventKey;  public AudioClip sfx;   }
    
    [Serializable]
    public class BattleRule
    {
        [Tooltip("ID o subcadena del arena/batalla. Debe coincidir con el battleId que usa tu StartBattleNode o el nombre del GO/arena.")]
        public string battleId;
        public AudioClip music;
        [Min(0f)] public float fade = 0.5f;
    }

    public List<BattleRule> battles = new();
    
    [Serializable]
    public class AdditiveCinematicRule
    {
        [Tooltip("Subcadena que debe contener el nombre de la escena aditiva (p.ej. 'Cine_', 'Cinematic.DemonAppears')")]
        public string sceneName;
        [Tooltip("Música a reproducir para esta cinemática (si Replace = true)")]
        public AudioClip music;
        [Tooltip("Si está activo, no sustituye la música; hace ducking de la actual.")]
        public bool duckInsteadOfReplace = true;
        [Range(0f,1f)] public float duckTo = 0.35f;
        [Min(0f)] public float fade = 0.5f;
    }

    public List<SceneMusic> sceneMusic = new();
    public List<EventSfx>   eventSfx   = new();
    public List<AdditiveCinematicRule> additiveCinematics = new();
}
