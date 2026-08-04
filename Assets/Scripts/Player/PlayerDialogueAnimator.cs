using System.Collections;
using UnityEngine;

/// <summary>
/// Controla las animaciones corporales del jugador durante los diálogos.
/// Escucha DialogueManager.OnDialogueLineChanged y reproduce gestos cuando
/// la línea activa pertenece al jugador (isPlayerSpeaking = true).
/// Añadir este componente al GameObject raíz del player junto al Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerDialogueAnimator : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Animator del personaje jugador")]
    [SerializeField] private Animator animator;

    [Tooltip("Perfil de emociones compartido con los NPCs — define cara y animación corporal")]
    [SerializeField] private EmotionProfile emotionProfile;

    [Tooltip("Tiempo de blend al entrar en un gesto")]
    [SerializeField, Range(0f, 0.3f)] private float blendTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Estado
    private Coroutine _gestureCoroutine;
    private int _lastTalkIndex = -1;
    // ✅ FIX: si el jugador está sentado/tumbado en un NPCWorldPoint, un gesto de diálogo
    // sobreescribe el layer 0 con su propio clip y no vuelve solo al loop de la actividad al
    // terminar. Referencia cacheada para poder restaurarlo (ver PlayGestureCoroutine).
    private PlayerAmbientActivityHandler _ambientActivity;

    #region Unity Lifecycle

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        _ambientActivity = GetComponent<PlayerAmbientActivityHandler>();
    }

    void OnEnable()
    {
        DialogueManager.OnDialogueLineChanged += OnDialogueLineChanged;
        DialogueManager.OnDialogueClosed      += OnDialogueClosed;
    }

    void OnDisable()
    {
        DialogueManager.OnDialogueLineChanged -= OnDialogueLineChanged;
        DialogueManager.OnDialogueClosed      -= OnDialogueClosed;
    }

    #endregion

    #region Event Handlers

    private void OnDialogueLineChanged(DialogueLine line, Transform npcInvolved)
    {
        if (!line.isPlayerSpeaking)
            return;

        PlayBodyEmotion(line.emotion);
    }

    private void OnDialogueClosed(Transform npcInvolved)
    {
        if (_gestureCoroutine != null)
        {
            StopCoroutine(_gestureCoroutine);
            _gestureCoroutine = null;
        }
    }

    #endregion

    #region Animation

    /// <summary>
    /// Reproduce un gesto por nombre de estado. Uso desde sistemas externos (ej: ShowSpeechBubbleNode).
    /// </summary>
    public void PlayGesture(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugMode)
                Debug.LogWarning($"[PlayerDialogueAnimator] Estado '{stateName}' no encontrado en el Animator.");
#endif
            return;
        }

        if (_gestureCoroutine != null)
            StopCoroutine(_gestureCoroutine);

        _gestureCoroutine = StartCoroutine(PlayGestureCoroutine(stateHash, stateName));
    }

    private void PlayBodyEmotion(NPCEmotion emotion)
    {
        if (animator == null)
            return;

        string stateName = ResolveStateName(emotion);
        if (string.IsNullOrEmpty(stateName))
            return; // Emoción sin animación corporal asignada: el jugador mantiene el gesto actual

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugMode)
                Debug.LogWarning($"[PlayerDialogueAnimator] Estado '{stateName}' no encontrado en el Animator.");
#endif
            return;
        }

        if (_gestureCoroutine != null)
            StopCoroutine(_gestureCoroutine);

        _gestureCoroutine = StartCoroutine(PlayGestureCoroutine(stateHash, stateName));
    }

    /// <summary>
    /// Igual que NPCSimpleAnimator.ResolveBodyAnimStateName: para emociones no neutrales devuelve
    /// el bodyAnimStateName tal cual esté en el EmotionProfile (vacío = sin cambio de animación,
    /// para emociones que solo deben cambiar la cara del jugador).
    /// </summary>
    private string ResolveStateName(NPCEmotion emotion)
    {
        string[] neutralAnims = (emotionProfile != null && emotionProfile.neutralBodyAnims is { Length: > 0 })
            ? emotionProfile.neutralBodyAnims
            : new[] { "Talk01", "Talk02", "Talk03" };

        if (emotion == NPCEmotion.None || emotion == NPCEmotion.Neutral)
        {
            _lastTalkIndex = (_lastTalkIndex + 1) % neutralAnims.Length;
            return neutralAnims[_lastTalkIndex];
        }

        if (emotionProfile != null)
        {
            var data = emotionProfile.GetEmotionData(emotion);
            return data.bodyAnimStateName; // puede venir vacío a propósito: "sin cambio"
        }

        return neutralAnims[0];
    }

    private IEnumerator PlayGestureCoroutine(int stateHash, string stateName)
    {
        animator.CrossFadeInFixedTime(stateHash, blendTime, 0, 0f);

        yield return null; // esperar un frame para que comience la transición

        // Esperar a que termine el clip
        float elapsed = 0f;
        float maxWait = 5f; // timeout de seguridad

        while (elapsed < maxWait)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 0.95f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _gestureCoroutine = null;

        // ✅ FIX: si el jugador sigue sentado/tumbado (actividad ambiental activa), el gesto
        // acaba de sobreescribir el layer 0 con su propio clip — volver al loop de la actividad
        // para no dejarlo de pie/flotando tras terminar el gesto.
        if (_ambientActivity != null && _ambientActivity.IsSeated)
            _ambientActivity.ResumeActivityLoop();
    }

    #endregion
}
