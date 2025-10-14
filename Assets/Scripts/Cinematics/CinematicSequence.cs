using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCinematicSequence", menuName = "Cinematics/Sequence")]
public class CinematicSequence : ScriptableObject
{
    [Tooltip("Velocidad global de la cámara (multiplicador). 1 = normal")]
    public float timeScale = 1f;

    [Tooltip("Permitir saltar la cinemática con la tecla indicada")]
    public bool skippable = true;
    public KeyCode skipKey = KeyCode.Escape;

    [Tooltip("Deshabilitar input del jugador mientras dura la cinemática")]
    public bool lockPlayerInput = true;

    [Tooltip("Al terminar: activar gameplay, habilitar PlayerInput, etc.")]
    public bool handOffToGameplayOnEnd = true;

    [Header("Playback Policy")]
    [Tooltip("Si está activo, esta cinemática solo se reproducirá una vez por perfil de juego.")]
    public bool playOnlyOnce;

    [Tooltip("Identificador único de la cinemática. Si está vacío, se usa el nombre del asset.")]
    public string sequenceId = string.Empty;

    [SerializeReference] public List<Shot> shots = new();

    [Serializable] public class Shot
    {
        public string name = "Shot";
        public ShotType type = ShotType.CameraMove;
        [Tooltip("Duración del shot en segundos")]
        public float duration = 2f;

        [Tooltip("Curva de interpolación del shot")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

        [Header("Camera Move / Focus")]
        public Transform from;           // si es nulo, se toma la posición/rotación actual de la cámara
        public Transform to;             // destino del movimiento
        public Transform lookAt;         // objetivo opcional a mirar durante el movimiento
        public float targetFOV = 60f;    // FOV al final del shot (si < 1, se ignora)

        [Header("Path (opcional). Si se rellena, el movimiento sigue estos puntos en vez de 'to' directo.")]
        public Transform pathRoot;       // hijos = waypoints

        [Header("Fade / Text / Audio")]
        public bool doFadeIn;
        public bool doFadeOut;
        public float fadeDuration = 0.75f;

        [TextArea] public string subtitleText;
        public float subtitleLeadIn;  // retraso antes de mostrar
        public float subtitleHold = 2.5f;  // tiempo en pantalla
        public float subtitleFade = 0.25f;

        public AudioClip sfx;
        [Range(0f,1f)] public float sfxVolume = 1f;

        [Header("Events / Hooks")]
        public UnityEngine.Events.UnityEvent onStart;
        public UnityEngine.Events.UnityEvent onEnd;
    }

    public enum ShotType
    {
        Wait,           // sólo espera (útil para timing o pausas dramáticas)
        CameraMove,     // mueve / interpola cámara + lookAt + FOV
        FocusTarget,    // sólo reorienta cámara hacia lookAt durante duración
        ShowText,       // muestra texto (subtítulos) con fade in/out
        FadeOnly,       // fundido a negro/desde negro
        PlaySfx         // reproduce sonido
    }
}
