using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Nodo que reproduce un diálogo directamente desde el grafo narrativo.
/// El sistema de diálogos (DialogueManager) gestiona internamente la cámara,
/// los speakers y las animaciones, por lo que este nodo solo necesita
/// el asset de diálogo.
/// </summary>
[Serializable]
public sealed class PlayDialogueNode : NarrativeNode
{
    [Header("Diálogo")]
    [Tooltip("El asset de diálogo a reproducir")]
    public DialogueAsset dialogue;

    [Header("One-shot (opcional)")]
    [Tooltip("Si se establece, este diálogo solo se reproducirá una vez. " +
             "Se guarda en el blackboard del grafo.")]
    public string oneShotFlag;

    private string OneShotKey => $"__dialogue_{guid}_{oneShotFlag}_played";

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning("[PlayDialogueNode] Diálogo vacío o no asignado → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(oneShotFlag))
        {
            if (ctx.Blackboard.Get<bool>(OneShotKey, false))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[PlayDialogueNode] Diálogo ya reproducido (flag: {oneShotFlag}) → saltando");
#endif
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[PlayDialogueNode] DialogueManager.Instance es null → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(PlayAndAdvance(ctx, onReadyToAdvance));
    }

    private IEnumerator PlayAndAdvance(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool completed = false;
        DialogueManager.Instance.StartDialogue(dialogue, () => completed = true);

        while (!completed)
            yield return null;

        if (!string.IsNullOrEmpty(oneShotFlag))
        {
            ctx.Blackboard.Set(OneShotKey, true);
        }

        yield return null;

        onReadyToAdvance?.Invoke();
    }
}
