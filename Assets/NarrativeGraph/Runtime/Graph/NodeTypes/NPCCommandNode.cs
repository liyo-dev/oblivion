using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que envía un comando a un NPC específico.
/// Soporta: mover a anchor, reproducir animación, unirse/abandonar equipo,
/// teletransportarse cerca del jugador y esperar N segundos.
/// El NPC se localiza vía NPCGraphBridgeRegistry o NPCRegistry.
/// El nodo espera a que la acción se complete antes de avanzar.
/// </summary>
[Serializable]
public sealed class NPCCommandNode : NarrativeNode
{
    public enum CommandType
    {
        Move              = 0,
        PlayAnimation     = 1,
        JoinParty         = 2,
        LeaveParty        = 3,
        TeleportNearPlayer = 4,
        Wait              = 5,
        SetActive         = 6,
    }

    [Header("NPC")]
    [Tooltip("ID narrativo del NPC al que se envía el comando.")]
    public string npcId;

    [Header("Comando")]
    [Tooltip("Tipo de comando a ejecutar.")]
    public CommandType command = CommandType.Move;

    [Header("Move")]
    [Tooltip("Nombre del anchor de destino (para Move).")]
    public string targetAnchorName;

    [Tooltip("Tiempo máximo del movimiento en segundos.")]
    [Min(1f)]
    public float maxMovementDuration = 15f;

    [Tooltip("Girar 180° al llegar al destino.")]
    public bool turnAroundOnArrival = false;

    [Header("Animation")]
    [Tooltip("Nombre del trigger en el Animator (para PlayAnimation).")]
    public string animationTrigger;

    [Tooltip("Duración de la animación en segundos (0 = espera a que termine).")]
    [Min(0f)]
    public float animationDuration = 0f;

    [Header("Wait")]
    [Tooltip("Tiempo de espera en segundos (para Wait).")]
    [Min(0.1f)]
    public float waitDuration = 1f;

    [Header("SetActive")]
    [Tooltip("Activar (true) o desactivar (false) el GameObject del NPC.")]
    public bool setActiveValue = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[NPCCommand] npcId vacío → avanzando");
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(ExecuteCommand(ctx, onReadyToAdvance));
    }

    IEnumerator ExecuteCommand(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Buscar NPC en ambos registros
        Game.NPC.NPCBehaviourManagerV2 npcManager = null;
        Transform npcTransform = null;

        var bridge = Game.NPC.NPCGraphBridgeRegistry.Get(npcId);
        if (bridge != null)
        {
            npcManager = bridge.NpcManager;
            npcTransform = bridge.NpcTransform;
        }

        if (npcManager == null && Game.NPC.NPCRegistry.HasInstance)
        {
            npcManager = Game.NPC.NPCRegistry.Instance.GetNPCByID(npcId);
            if (npcManager != null)
                npcTransform = npcManager.transform;
        }

        if (npcTransform == null)
        {
            Debug.LogWarning($"[NPCCommand] NPC '{npcId}' no encontrado → avanzando");
            onReadyToAdvance?.Invoke();
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NPCCommand:{guid}] Ejecutando {command} en NPC '{npcId}'");
#endif

        switch (command)
        {
            case CommandType.Move:
                yield return DoMove(npcManager, npcTransform);
                break;

            case CommandType.PlayAnimation:
                yield return DoAnimation(npcManager);
                break;

            case CommandType.JoinParty:
                DoJoinParty(npcManager);
                break;

            case CommandType.LeaveParty:
                DoLeaveParty(npcManager);
                break;

            case CommandType.TeleportNearPlayer:
                DoTeleportNearPlayer(npcManager, npcTransform);
                break;

            case CommandType.Wait:
                yield return new WaitForSeconds(waitDuration);
                break;

            case CommandType.SetActive:
                npcTransform.gameObject.SetActive(setActiveValue);
                break;
        }

        onReadyToAdvance?.Invoke();
    }

    IEnumerator DoMove(Game.NPC.NPCBehaviourManagerV2 npcManager, Transform npcTransform)
    {
        if (string.IsNullOrWhiteSpace(targetAnchorName))
        {
            Debug.LogWarning($"[NPCCommand] Move sin targetAnchorName → saltando");
            yield break;
        }

        var anchor = SpawnAnchor.FindById(targetAnchorName);
        Vector3 targetPos;

        if (anchor != null)
        {
            targetPos = anchor.transform.position;
        }
        else
        {
            Debug.LogWarning($"[NPCCommand] Anchor '{targetAnchorName}' no encontrado → saltando Move");
            yield break;
        }

        if (npcManager != null)
        {
            // Usar el sistema de movimiento del NPC
            if (npcManager.SimpleAnimator != null)
            {
                npcManager.SimpleAnimator.AllowManualRotation = false;
                npcManager.SimpleAnimator.EnableAutoRotation();

                Vector3 dir = targetPos - npcTransform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    npcManager.SimpleAnimator.FaceDirection(dir.normalized);
            }

            var moveSeq = new Game.NPC.States.MoveToPositionSequence(npcManager, targetPos, maxMovementDuration,
                turnAroundOnArrival, 999f, anchor);
            npcManager.StartCinematicSequence(moveSeq);
            while (!moveSeq.IsCompleted) yield return null;

            if (npcManager.SimpleAnimator != null)
            {
                npcManager.SimpleAnimator.AllowManualRotation = true;
            }
        }
        else
        {
            // Fallback sin NPCBehaviourManagerV2: mover directamente
            npcTransform.position = targetPos;
        }
    }

    IEnumerator DoAnimation(Game.NPC.NPCBehaviourManagerV2 npcManager)
    {
        if (string.IsNullOrWhiteSpace(animationTrigger))
        {
            Debug.LogWarning($"[NPCCommand] PlayAnimation sin trigger → saltando");
            yield break;
        }

        if (npcManager?.SimpleAnimator != null)
        {
            npcManager.SimpleAnimator.PlayOneShot(animationTrigger);

            if (animationDuration > 0f)
                yield return new WaitForSeconds(animationDuration);
            else
                yield return new WaitForSeconds(0.5f); // Espera mínima para que arranque la animación
        }
    }

    void DoJoinParty(Game.NPC.NPCBehaviourManagerV2 npcManager)
    {
        if (npcManager == null) return;

        var partyMember = npcManager.GetComponent<Game.NPC.NPCPartyMember>();
        if (partyMember != null && Game.NPC.PlayerParty.Instance != null)
        {
            Game.NPC.PlayerParty.Instance.AddMember(partyMember);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCCommand] '{npcId}' se unió al equipo");
#endif
        }
        else
        {
            Debug.LogWarning($"[NPCCommand] NPC '{npcId}' no tiene NPCPartyMember o PlayerParty no disponible");
        }
    }

    void DoLeaveParty(Game.NPC.NPCBehaviourManagerV2 npcManager)
    {
        if (npcManager == null) return;

        var partyMember = npcManager.GetComponent<Game.NPC.NPCPartyMember>();
        if (partyMember != null && Game.NPC.PlayerParty.Instance != null)
        {
            Game.NPC.PlayerParty.Instance.RemoveMember(partyMember);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCCommand] '{npcId}' abandonó el equipo");
#endif
        }
    }

    void DoTeleportNearPlayer(Game.NPC.NPCBehaviourManagerV2 npcManager, Transform npcTransform)
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            Debug.LogWarning($"[NPCCommand] TeleportNearPlayer: jugador no encontrado");
            return;
        }

        Vector3 offset = player.transform.right * 1.5f;
        Vector3 targetPos = player.transform.position + offset;

        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            targetPos = hit.position;

        npcTransform.position = targetPos;

        // Mirar al jugador
        Vector3 toPlayer = player.transform.position - npcTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
            npcTransform.rotation = Quaternion.LookRotation(toPlayer.normalized);
    }
}
