using System;
using System.Collections;
using EasyTransition;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que teletransporta al jugador a un SpawnAnchor,
/// opcionalmente con una transición visual (fade, cortinilla, etc.).
/// También puede emitir un evento custom al completarse.
/// </summary>
[Serializable]
[UnsafeForSave("No guardar durante la teletransportación")]
public sealed class TeleportPlayerNode : NarrativeNode
{
    [Header("Destino")]
    [Tooltip("Nombre del SpawnAnchor de destino.")]
    public string targetAnchorName;

    [Header("Transición (opcional)")]
    [Tooltip("Asset de transición visual. Si es null, el teletransporte es instantáneo.")]
    public TransitionSettings teleportTransition;

    [Header("Evento al completar (opcional)")]
    [Tooltip("Clave del evento custom a emitir tras el teletransporte.")]
    public string eventKeyOnComplete;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(targetAnchorName))
        {
            Debug.LogWarning("[TeleportPlayer] targetAnchorName vacío → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(DoTeleport(ctx, onReadyToAdvance));
    }

    IEnumerator DoTeleport(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var anchor = SpawnAnchor.FindById(targetAnchorName);
        if (anchor == null)
        {
            Debug.LogWarning($"[TeleportPlayer] Anchor '{targetAnchorName}' no encontrado → avanzando");
            onReadyToAdvance?.Invoke();
            yield break;
        }

        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            Debug.LogWarning("[TeleportPlayer] Jugador no encontrado → avanzando");
            onReadyToAdvance?.Invoke();
            yield break;
        }

        Vector3 targetPos = anchor.transform.position;
        Quaternion targetRot = anchor.GetCharacterRotation();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TeleportPlayer:{guid}] Teletransportando jugador a '{targetAnchorName}' ({targetPos})");
#endif

        // Transición de entrada (cubre la pantalla)
        var tm = TransitionManager.Instance();
        if (teleportTransition != null && tm != null)
        {
            tm.Transition(teleportTransition, 0f);

            float coverTime = teleportTransition.autoAdjustTransitionTime
                ? teleportTransition.transitionTime / teleportTransition.transitionSpeed
                : teleportTransition.transitionTime;

            yield return new WaitForSecondsRealtime(coverTime);
        }

        // Teletransportar al jugador
        var charCtrl = player.GetComponent<UnityEngine.CharacterController>();
        if (charCtrl != null) charCtrl.enabled = false;
        player.transform.position = targetPos;
        player.transform.rotation = targetRot;
        if (charCtrl != null) charCtrl.enabled = true;

        // La transición hace el fade-out automáticamente

        // Emitir evento si está configurado
        if (!string.IsNullOrWhiteSpace(eventKeyOnComplete))
        {
            ctx.Signals.RaiseCustom(eventKeyOnComplete, $"TeleportPlayer:{guid}");
        }

        onReadyToAdvance?.Invoke();
    }
}
