using System;
using UnityEngine;

/// <summary>
/// Muestra un bocadillo de cómic flotante sobre un personaje.
/// Si duration > 0 y waitForCompletion, el grafo espera a que se oculte automáticamente.
/// </summary>
[Serializable]
public sealed class ShowSpeechBubbleNode : NarrativeNode
{
    [Tooltip("Tag del GameObject sobre el que aparece el bocadillo (ej: 'Player').")]
    public string targetTag = "Player";

    [TextArea(1, 3)]
    public string text;

    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;

    [Tooltip("Segundos antes de que el bocadillo desaparezca automáticamente. 0 = permanece.")]
    [Min(0f)]
    public float duration = 3f;

    [Tooltip("Si true y duration > 0, el grafo espera a que el bocadillo desaparezca.")]
    public bool waitForCompletion = true;

    [Header("Personaje")]
    [Tooltip("Nombre exacto del estado/animación del Animator del personaje (ej: 'Talk', 'Wave'). Vacío = sin animación.")]
    public string animTrigger;

    [Tooltip("Si true, usa el sprite de énfasis (burbuja explosiva) en lugar del normal.")]
    public bool emphasis;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = SpeechBubbleUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ShowSpeechBubbleNode] ❌ SpeechBubbleUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        GameObject target = string.IsNullOrEmpty(targetTag)
            ? null
            : GameObject.FindWithTag(targetTag);

        if (target == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[ShowSpeechBubbleNode] ❌ No se encontró objeto con tag '{targetTag}'.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        string resolved = string.IsNullOrEmpty(textId)
            ? text
            : LocalizationManager.Instance?.Get(textId, text) ?? text;

        Action callback = (waitForCompletion && duration > 0f) ? onReadyToAdvance : null;

        ui.Show(target.transform, resolved, duration, callback, animTrigger, emphasis);

        if (!waitForCompletion || duration <= 0f)
            onReadyToAdvance?.Invoke();
    }
}
