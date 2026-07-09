using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Tiempo de blend al entrar en un gesto")]
    [SerializeField, Range(0f, 0.3f)] private float blendTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Estado
    private Coroutine _gestureCoroutine;
    private int _lastTalkIndex = -1;

    // Mapeo emoción → estado del animator (deben existir en Invector@BasicLocomotion)
    private static readonly Dictionary<NPCEmotion, string> EmotionBodyAnimMap = new Dictionary<NPCEmotion, string>
    {
        { NPCEmotion.Happy,     "Cheer01"    },
        { NPCEmotion.Angry,     "Angry01"    },
        { NPCEmotion.Sad,       "Cry01"      },
        { NPCEmotion.Scared,    "Fear01"     },
        { NPCEmotion.Thinking,  "Question01" },
        { NPCEmotion.Surprised, "Question02" },
        { NPCEmotion.Smirk,     "Laugh01"    },
        { NPCEmotion.Tired,     "Talk02"     },
    };

    private static readonly string[] NeutralTalkAnims = { "Talk01", "Talk02", "Talk03" };

    #region Unity Lifecycle

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
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

    private void PlayBodyEmotion(NPCEmotion emotion)
    {
        if (animator == null)
            return;

        string stateName;
        if (emotion == NPCEmotion.None || emotion == NPCEmotion.Neutral)
        {
            _lastTalkIndex = (_lastTalkIndex + 1) % NeutralTalkAnims.Length;
            stateName = NeutralTalkAnims[_lastTalkIndex];
        }
        else if (EmotionBodyAnimMap.TryGetValue(emotion, out string mapped))
        {
            stateName = mapped;
        }
        else
        {
            stateName = NeutralTalkAnims[0];
        }

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
    }

    #endregion
}
