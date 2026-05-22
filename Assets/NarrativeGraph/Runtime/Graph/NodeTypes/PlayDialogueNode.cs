using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Nodo que reproduce un diálogo directamente desde el grafo narrativo.
/// Permite centralizar el control de diálogos sin tener que asignarlos
/// en los módulos de los NPCs.
/// Si se indica un npcId, busca el NPC en el NPCRegistry para usar
/// la cámara cinematográfica y las animaciones de diálogo.
/// </summary>
[Serializable]
public sealed class PlayDialogueNode : NarrativeNode
{
    [Header("Diálogo")]
    [Tooltip("El asset de diálogo a reproducir")]
    public DialogueAsset dialogue;

    [Header("NPC (opcional)")]
    [Tooltip("ID narrativo del NPC interlocutor (para cámara y animaciones). " +
             "Si está vacío, el diálogo se muestra sin cámara cinematográfica.")]
    public string npcId;

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
                Debug.Log($"[PlayDialogueNode] Diálogo ya reproducido (flag: {oneShotFlag}) → saltando");
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
        Transform npcTransform = null;

        if (!string.IsNullOrEmpty(npcId) && Game.NPC.NPCRegistry.HasInstance)
        {
            var npcManager = Game.NPC.NPCRegistry.Instance.GetNPCByID(npcId);
            if (npcManager != null)
            {
                npcTransform = npcManager.transform;
            }
            else
            {
                Debug.LogWarning($"[PlayDialogueNode] NPC con ID '{npcId}' no encontrado en NPCRegistry");
            }
        }

        bool completed = false;

        if (npcTransform != null)
        {
            DialogueManager.Instance.StartDialogue(dialogue, npcTransform, () => completed = true);
        }
        else
        {
            DialogueManager.Instance.StartDialogue(dialogue, () => completed = true);
        }

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
