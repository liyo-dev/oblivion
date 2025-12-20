// NpcAutoMoveNode_V2.cs
// Ejemplo de integración con la nueva arquitectura FSM
using System;
using System.Collections;
using Game.NPC;
using Game.NPC.States;
using UnityEngine;

/// <summary>
/// Versión mejorada del NpcAutoMoveNode que usa la nueva arquitectura FSM.
/// Este es un EJEMPLO de cómo adaptar el nodo existente.
/// </summary>
[Serializable]
public sealed class NpcAutoMoveNode_V2 : NarrativeNode
{
    public enum SequenceMode
    {
        DialogueBeforeMove,
        MoveBeforeDialogue
    }

    [Header("NPC Lookup")]
    public string npcName;
    public string npcTag;

    [Header("Diálogo")]
    public DialogueAsset dialogueOverride;
    public bool triggerInteractableIfNoOverride = true;

    [Header("Secuencia")]
    public SequenceMode sequenceMode = SequenceMode.DialogueBeforeMove;
    public bool returnToOrigin = false;

    [Header("Destino")]
    public bool moveToPlayer = false;
    public Vector3 targetPosition;
    [Min(0f)] public float stoppingDistance = 0.35f;
    public bool turnAroundOnArrival = false;

    [Header("Movimiento")]
    [Min(1f)] public float maxWalkSeconds = 12f;

    [Header("Acciones Adicionales (Opcional)")]
    [Tooltip("Añade acciones intermedias a la secuencia (animaciones, esperas, etc.)")]
    public CinematicActionConfig[] additionalActions = Array.Empty<CinematicActionConfig>();

    [Header("Control")]
    public bool lockPlayer = true;
    public ActionMode lockMode = ActionMode.Cinematic;

    [Header("Debug")]
    public bool debugLogs = false;

    void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[NpcAutoMoveV2] {message}");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx?.Runner == null)
        {
            onReadyToAdvance?.Invoke();
            return;
        }

        Log($"Enter → npcName='{npcName}', npcTag='{npcTag}'");
        ctx.Runner.StartCoroutine(RunSequence(ctx, onReadyToAdvance));
    }

    IEnumerator RunSequence(NarrativeContext ctx, Action done)
    {
        // Resolver NPC
        var npc = ResolveNpc();
        if (npc == null)
        {
            Log("ERROR: NPC no encontrado");
            done?.Invoke();
            yield break;
        }

        // Obtener el nuevo manager V2
        var npcBehaviour = npc.GetComponent<NPCBehaviourManagerV2>();
        if (npcBehaviour == null)
        {
            Log("ERROR: NPC no tiene NPCBehaviourManagerV2, intentando con el viejo...");
            // Fallback al sistema antiguo si no tiene el nuevo
            done?.Invoke();
            yield break;
        }

        // Guardar posición original
        Vector3 originPosition = npc.transform.position;
        Quaternion originRotation = npc.transform.rotation;

        // Bloquear jugador si está configurado
        if (lockPlayer)
        {
            LockPlayer();
        }

        // Ejecutar secuencia según el modo
        switch (sequenceMode)
        {
            case SequenceMode.DialogueBeforeMove:
                yield return ExecuteDialogue(ctx);
                yield return ExecuteMovement(npcBehaviour);
                break;

            case SequenceMode.MoveBeforeDialogue:
                yield return ExecuteMovement(npcBehaviour);
                yield return ExecuteDialogue(ctx);
                break;
        }

        // Volver al origen si está configurado
        if (returnToOrigin)
        {
            Log("Regresando al origen...");
            var returnSequence = new Game.NPC.States.MoveToPoscionSequence(originPosition, maxWalkSeconds);
            npcBehaviour.StartCinematicSequence(returnSequence);

            while (!returnSequence.IsCompleted)
            {
                yield return null;
            }

            if (restoreRotationOnReturn)
            {
                npc.transform.rotation = originRotation;
            }
        }

        // Salir del modo cinemático
        npcBehaviour.ExitCinematic();

        // Desbloquear jugador
        if (lockPlayer)
        {
            UnlockPlayer();
        }

        Log("Secuencia completada");
        done?.Invoke();
    }

    IEnumerator ExecuteMovement(NPCBehaviourManagerV2 npcBehaviour)
    {
        Log("Iniciando movimiento...");

        // Determinar destino
        Vector3 destination = moveToPlayer && GetPlayerPosition(out var playerPos) 
            ? playerPos 
            : targetPosition;

        // Crear secuencia
        Game.NPC.States.CinematicSequence sequence;

        if (additionalActions.Length > 0)
        {
            // Secuencia compuesta con acciones adicionales
            var composite = new Game.NPC.States.CompositeSequence();

            // Añadir movimiento inicial
            composite.AddAction(new Game.NPC.States.MoveToAction(destination, maxWalkSeconds));

            // Girar si está configurado
            if (turnAroundOnArrival)
            {
                var turnRotation = Quaternion.Euler(0, 180, 0) * npcBehaviour.transform.rotation;
                composite.AddAction(new Game.NPC.States.RotateToAction(turnRotation, 0.5f));
            }

            // Añadir acciones adicionales
            foreach (var actionConfig in additionalActions)
            {
                var action = actionConfig.CreateAction();
                if (action != null)
                {
                    composite.AddAction(action);
                }
            }

            sequence = composite;
        }
        else
        {
            // Secuencia simple
            sequence = new Game.NPC.States.MoveToPoscionSequence(destination, maxWalkSeconds, turnAroundOnArrival);
        }

        // Iniciar secuencia
        npcBehaviour.StartCinematicSequence(sequence);

        // Esperar a que termine
        while (!sequence.IsCompleted)
        {
            yield return null;
        }

        Log("Movimiento completado");
    }

    IEnumerator ExecuteDialogue(NarrativeContext ctx)
    {
        if (dialogueOverride == null && !triggerInteractableIfNoOverride)
        {
            Log("Sin diálogo configurado");
            yield break;
        }

        Log("Ejecutando diálogo...");

        // TODO: Implementar lógica de diálogo
        // Por ahora es un placeholder
        yield return new WaitForSeconds(0.5f);

        Log("Diálogo completado");
    }

    GameObject ResolveNpc()
    {
        // Por nombre
        if (!string.IsNullOrEmpty(npcName))
        {
            var go = GameObject.Find(npcName);
            if (go != null) return go;
        }

        // Por tag
        if (!string.IsNullOrEmpty(npcTag))
        {
            try
            {
                var go = GameObject.FindGameObjectWithTag(npcTag);
                if (go != null) return go;
            }
            catch { }
        }

        return null;
    }

    bool GetPlayerPosition(out Vector3 pos)
    {
        pos = Vector3.zero;

        var player = PlayerService.Player;
        if (player == null)
            return false;

        pos = player.transform.position;
        return true;
    }

    void LockPlayer()
    {
        var player = PlayerService.Player;
        if (player == null) return;

        var actionManager = player.GetComponent<PlayerActionManager>();
        if (actionManager != null)
        {
            actionManager.PushMode(lockMode);
            Log("Jugador bloqueado");
        }
    }

    void UnlockPlayer()
    {
        var player = PlayerService.Player;
        if (player == null) return;

        var actionManager = player.GetComponent<PlayerActionManager>();
        if (actionManager != null)
        {
            actionManager.PopMode(lockMode);
            Log("Jugador desbloqueado");
        }
    }

    // Campos adicionales del nodo original (compatibilidad)
    [Header("Compatibilidad")]
    public bool restoreRotationOnReturn = true;
}

/// <summary>
/// Configuración serializable para acciones cinemáticas adicionales
/// </summary>
[Serializable]
public class CinematicActionConfig
{
    public enum ActionType
    {
        Wait,
        PlayAnimation,
        RotateTo,
        LookAtPlayer
    }

    public ActionType actionType;

    [Header("Wait")]
    public float waitDuration = 1f;

    [Header("Animation")]
    public string animationTrigger;
    public float animationDuration = 2f;

    [Header("Rotation")]
    public Vector3 rotationEuler = Vector3.zero;
    public float rotationDuration = 0.5f;

    public Game.NPC.States.CinematicAction CreateAction()
    {
        switch (actionType)
        {
            case ActionType.Wait:
                return new Game.NPC.States.WaitAction(waitDuration);

            case ActionType.PlayAnimation:
                if (string.IsNullOrEmpty(animationTrigger))
                    return null;
                return new Game.NPC.States.PlayAnimationAction(animationTrigger, animationDuration);

            case ActionType.RotateTo:
                return new Game.NPC.States.RotateToAction(Quaternion.Euler(rotationEuler), rotationDuration);

            case ActionType.LookAtPlayer:
                // TODO: Implementar LookAtPlayerAction
                return null;

            default:
                return null;
        }
    }
}

