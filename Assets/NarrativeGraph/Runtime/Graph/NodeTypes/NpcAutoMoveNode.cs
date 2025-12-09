// NpcAutoMoveNode.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using Game.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bloquea al jugador, fuerza el diálogo del NPC y lo hace caminar hacia una posición destino.
/// En cuanto el NPC sale de cámara se le teleporta a la ubicación final y se devuelve el control.
/// </summary>
[Serializable]
public sealed class NpcAutoMoveNode : NarrativeNode
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
    [Min(1f)] public float dialogueTimeout = 60f;

    [Header("Secuencia")]
    public SequenceMode sequenceMode = SequenceMode.DialogueBeforeMove;
    [Tooltip("Si está activo, el NPC regresará a su posición inicial al terminar la secuencia.")]
    public bool returnToOrigin = false;
    [Tooltip("Si el NPC vuelve al origen, restaurar también su rotación inicial.")]
    public bool restoreRotationOnReturn = true;

    [Header("Destino")]
    [Tooltip("Si está activo, el NPC se moverá hacia la posición actual del jugador en lugar del destino configurado.")]
    public bool moveToPlayer = false;
    public string targetAnchorName;
    public Vector3 targetPosition;
    public Vector3 anchorOffset = Vector3.zero;
    public bool useAnchorPosition = true;
    public bool useRelativeOffset = false;
    public bool relativeOffsetIsLocal = true;
    public Vector3 relativeOffset = new Vector3(0f, 0f, 5f);
    [Min(0.1f)] public float navmeshSampleRadius = 2f;
    [Min(0f)] public float stoppingDistance = 0.35f;
    [Tooltip("Si está activo, el NPC se dará la vuelta 180° al llegar al destino.")]
    public bool turnAroundOnArrival = false;

    [Header("Movimiento")]
    [Min(1f)] public float maxWalkSeconds = 12f;
    [Range(0f, 1f)] public float minAnimSpeed = 0.25f;
    public bool resetAnimationOnEnd = true;
    [Tooltip("Tiempo mínimo que el NPC debe permanecer en pantalla caminando antes de permitir teletransporte por 'offscreen'.")]
    [Min(0f)] public float minVisibleWalkSeconds = 0.5f;
    [Tooltip("Distancia mínima a recorrer antes de permitir teletransporte por 'offscreen'.")]
    [Min(0f)] public float minDistanceBeforeTeleport = 0.5f;

    [Header("Cámara / Control")]
    public bool lockPlayer = true;
    public ActionMode lockMode = ActionMode.Cinematic;
    [Range(0f, 0.5f)] public float offscreenPadding = 0.05f;
    public float cameraHeightOffset = 1.5f;
    
    [Header("Desactivar NPC")]
    [Tooltip("Si está marcado, el NPC se desactivará cuando llegue al destino o salga de cámara.")]
    public bool deactivateOnComplete = false;
    [Tooltip("Si está marcado, guardará el estado desactivado del NPC para que no vuelva a aparecer.")]
    public bool persistDeactivation = false;

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Compatibilidad")]
    [Tooltip("Si el agente no consigue path tras un breve intento, finalizar igualmente y warp al destino.")]
    public bool treatNoPathAsReach = true;

    [Header("Persistencia")]
    [Tooltip("Si está marcado, este nodo solo se ejecutará una vez por perfil y se saltará en cargas futuras.")]
    public bool runOnlyOncePerProfile = true;
    [Tooltip("Persistir el flag de finalización dentro del PlayerPreset para evitar re-ejecuciones tras cargar partidas.")]
    public bool persistCompletionToPreset = true;
    [Tooltip("Guarda la nueva posición del NPC cuando llegue al destino. Si está desactivado, al cargar una partida volverá a su posición previa.")]
    public bool persistPositionToSave = false;

    void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[NpcAutoMoveNode] {message}");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx?.Runner == null)
        {
            onReadyToAdvance?.Invoke();
            return;
        }

        var completionKey = GetCompletionKey();
        if (runOnlyOncePerProfile && ShouldSkipBecauseCompleted(ctx.Blackboard, completionKey))
        {
            Log($"Skip → runOnlyOncePerProfile=true y flag '{completionKey}' ya está marcado/persistido");
            onReadyToAdvance?.Invoke();
            return;
        }

        Log($"Enter → npcName='{npcName}', npcTag='{npcTag}'");
        ctx.Runner.StartCoroutine(RunSequence(ctx, onReadyToAdvance, completionKey));
    }

    IEnumerator RunSequence(NarrativeContext ctx, Action done, string completionKey)
    {
        var npc = ResolveNpc();
        if (npc == null)
        {
            // El warning detallado ya se mostró en ResolveNpc()
            done?.Invoke();
            yield break;
        }
        
        // Si el NPC fue desactivado permanentemente por un nodo previo, saltar ejecución
        // Solo verificar si el GameObject específico está desactivado (no padres)
        if (!npc.gameObject.activeSelf)
        {
            // Verificar si fue desactivado por persistencia (tiene entrada en preset con isActive=false)
            PlayerPresetSO preset = null;
            var gb = GameBootService.Profile;
            if (gb != null)
            {
                try { preset = gb.GetActivePresetResolved(); } catch { }
            }
            
            if (preset != null && preset.npcPositions != null)
            {
                var entry = preset.npcPositions.Find(e => e.npcId == npc.gameObject.name);
                if (entry.hasActiveState && !entry.isActive)
                {
                    Log($"NPC '{npc.name}' fue desactivado permanentemente, saltando ejecución del nodo");
                    done?.Invoke();
                    yield break;
                }
            }
        }

        npc.EnsurePlayerReference();

        // Asegurar que el NPC pueda persistir su posición si el nodo lo requiere.
        if (persistPositionToSave && !npc.persistLastPosition)
        {
            npc.persistLastPosition = true;
            npc.SetLastPosition(npc.transform.position);
            Log($"Forzando persistencia de posición en NPC '{npc.name}' (persistLastPosition habilitado)");
        }
        Vector3 originalPosition = npc.transform.position;
        Quaternion originalRotation = npc.transform.rotation;

        PlayerActionManager pam = null;
        bool lockApplied = false;

        try
        {
            if (lockPlayer)
            {
                pam = npc.GetActionManager() ?? ResolvePlayerActionManager();
                if (pam != null)
                {
                    pam.PushMode(lockMode);
                    lockApplied = true;
                    Log($"Player lock PUSH → mode={lockMode}");
                }
                else
                {
                    Log("Player lock requested but PlayerActionManager not found");
                }
            }

            bool persistForwardMovement = persistPositionToSave && !returnToOrigin;
            if (sequenceMode == SequenceMode.MoveBeforeDialogue)
            {
                yield return MoveNpc(npc, null, persistForwardMovement);
                yield return PlayDialogue(npc);
            }
            else
            {
                yield return PlayDialogue(npc);
                yield return MoveNpc(npc, null, persistForwardMovement);
            }

            if (returnToOrigin)
            {
                Log($"Returning NPC to origin: {originalPosition}");
                yield return MoveNpc(npc, originalPosition, persistPosition: persistPositionToSave, faceBackTowardsStart: false);
                if (restoreRotationOnReturn)
                {
                    RestoreNpcRotation(npc, originalRotation);
                    Log($"Restored original rotation");
                }
            }
            
            // Desactivar NPC si está configurado
            if (deactivateOnComplete)
            {
                Log($"Desactivando NPC '{npc.name}' (persistDeactivation={persistDeactivation})");
                
                if (persistDeactivation)
                {
                    PersistNpcDeactivation(npc);
                }
                
                npc.gameObject.SetActive(false);
            }
        }
        finally
        {
            if (runOnlyOncePerProfile)
            {
                MarkCompleted(ctx?.Blackboard, completionKey);
            }

            if (lockApplied && pam != null)
            {
                pam.PopMode(lockMode);
                Log($"Player lock POP → mode={lockMode}");
            }

            done?.Invoke();
        }
    }

    IEnumerator PlayDialogue(NPCBehaviourManager npc)
    {
        bool waited = false;

        if (dialogueOverride != null)
        {
            bool finished = false;
            Log($"Playing dialogue override '{dialogueOverride.name}' (timeout {dialogueTimeout}s)");
            npc.PlayDialogue(dialogueOverride, () => finished = true);
            waited = true;
            yield return WaitUntil(() => finished, dialogueTimeout);
            Log("Dialogue override finished or timed out");
        }
        else if (triggerInteractableIfNoOverride)
        {
            var interactable = npc.Interactable;
            if (interactable != null && PlayerService.TryGetPlayer(out var player, true) && player != null)
            {
                Log("Triggering NPC Interactable (no override)");
                interactable.Interact(player);
                waited = true;
                var wait = npc.WaitDialogueToClose(dialogueTimeout);
                if (wait != null)
                {
                    while (wait.MoveNext())
                        yield return wait.Current;
                }
                Log("Interactable dialogue finished or timed out");
            }
        }

        if (!waited)
            yield break;

        float timer = 0f;
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen && timer < dialogueTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator MoveNpc(NPCBehaviourManager npc, Vector3? forcedDestination = null, bool persistPosition = true, bool faceBackTowardsStart = true)
    {
        Vector3 destination;
        if (forcedDestination.HasValue)
        {
            destination = forcedDestination.Value;
            if (NavMesh.SamplePosition(destination, out var forcedHit, navmeshSampleRadius, NavMesh.AllAreas))
                destination = forcedHit.position;
        }
        else if (!TryResolveDestination(npc, out destination))
        {
            Debug.LogWarning("[NpcAutoMoveNode] No se pudo resolver el destino. Teleportando al origen configurado.");
            npc.transform.position = targetPosition;
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, npc.transform.position, persistPosition);
            EnsureNpcIdle(npc.Animator);
            yield break;
        }

        var agent = npc.Agent;
        var animator = npc.Animator;

        if (agent == null)
        {
            Debug.LogWarning("[NpcAutoMoveNode] NPC no tiene NavMeshAgent; no puede caminar. Teleport al destino.");
            npc.transform.position = destination;
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, destination, persistPosition);
            EnsureNpcIdle(animator);
            yield break;
        }

        if (!agent.enabled) agent.enabled = true;

        bool onMesh = npc.EnsureAgentOnNavMesh(navmeshSampleRadius);
        if (!onMesh)
        {
            if (NavMesh.SamplePosition(destination, out var hitNav, Mathf.Max(2f, navmeshSampleRadius * 2f), NavMesh.AllAreas))
            {
                agent.Warp(hitNav.position);
                onMesh = true;
                Log("Agent warped to nearest NavMesh point near destination");
            }
        }
        if (!onMesh)
        {
            Debug.LogWarning("[NpcAutoMoveNode] No se pudo proyectar el NPC en NavMesh; teletransportando al destino.");
            npc.transform.position = destination;
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, destination, persistPosition);
            EnsureNpcIdle(animator);
            yield break;
        }

        NavMeshAgentUtility.SetDestination(agent, destination, stoppingDistance);
        Log($"Move → destination={destination} stoppingDist={stoppingDistance}");

        var cam = ResolveCamera();
        float elapsed = 0f;
        bool leftCamera = false;
        Vector3 startPos = npc.transform.position;
        bool offscreenAllowed = false;
        bool forcedReachNoPath = false;

        while (elapsed < maxWalkSeconds)
        {
            elapsed += Time.deltaTime;

            if (animator != null)
            {
                float speed = NavMeshAgentUtility.ComputeSpeedFactor(agent);
                float minSpeed = speed > 0.01f ? minAnimSpeed : 0f;
                animator.SetMovementSpeed(Mathf.Max(speed, minSpeed));
            }

            if (!offscreenAllowed)
            {
                if (elapsed >= Mathf.Max(0f, minVisibleWalkSeconds) || Vector3.Distance(npc.transform.position, startPos) >= Mathf.Max(0f, minDistanceBeforeTeleport))
                {
                    offscreenAllowed = true;
                    Log("Offscreen-teleport now allowed (time/distance reached)");
                }
            }

            if (offscreenAllowed && HasLeftCamera(cam, npc.transform, cameraHeightOffset, offscreenPadding))
            {
                leftCamera = true;
                Log("NPC left camera frustum → will teleport to destination");
                break;
            }

            if (!agent.pathPending && agent.isOnNavMesh && agent.enabled)
            {
                float distToDestination = Vector3.Distance(npc.transform.position, destination);
                float agentSpeed = agent.velocity.magnitude;
                bool reachedDistance = agent.remainingDistance <= Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f;
                bool almostStopped = agentSpeed < 0.15f && distToDestination < stoppingDistance + 0.5f;
                
                if (reachedDistance || almostStopped)
                {
                    Log($"Agent reached (dist={distToDestination:F2}m, speed={agentSpeed:F2}m/s, remaining={agent.remainingDistance:F2}m)");
                    break;
                }

                if (treatNoPathAsReach && !agent.hasPath && elapsed >= 0.5f)
                {
                    forcedReachNoPath = true;
                    Log($"No valid path after {elapsed:0.00}s (status={agent.pathStatus}). Forcing completion.");
                    break;
                }
            }

            yield return null;
        }

        if (!leftCamera && cam != null)
        {
            float extra = 0f;
            while (extra < 2f)
            {
                if (HasLeftCamera(cam, npc.transform, cameraHeightOffset, offscreenPadding))
                    break;

                extra += Time.deltaTime;
                yield return null;
            }
        }

        float reachThresh = Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f;
        bool reached = (!agent.pathPending && agent.isOnNavMesh && agent.enabled && agent.remainingDistance <= reachThresh) || forcedReachNoPath;

        // Detener el agente de forma agresiva
        if (agent.isOnNavMesh && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
        
        // Esperar 1 frame para que Unity procese el stop
        yield return null;
        
        // Forzar velocidad a cero de nuevo
        if (agent.isOnNavMesh && agent.enabled)
            agent.velocity = Vector3.zero;
        
        // Resetear animación
        EnsureNpcIdle(animator);
        
        // Esperar otro frame para que la animación se actualice
        yield return null;

        if (leftCamera)
        {
            if (agent.isOnNavMesh)
            {
                agent.Warp(destination);
                // Sincronizar velocidad y posición tras warp
                yield return null; // Frame para que el warp se aplique
                agent.velocity = Vector3.zero;
                agent.nextPosition = destination;
            }
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, destination, persistPosition);
            Log(persistPosition
                ? "Finished by offscreen → teleported and persisted position"
                : "Finished by offscreen → teleported (no persistence).");
        }
        else if (reached)
        {
            if (agent.isOnNavMesh && Vector3.Distance(agent.transform.position, destination) > 0.2f)
            {
                agent.Warp(destination);
                yield return null;
                agent.velocity = Vector3.zero;
                agent.nextPosition = destination;
            }
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, destination, persistPosition);
            Log(forcedReachNoPath ? "Finished by forced reach (no path) → warped to destination" : "Finished by reach → stopped at destination");
        }
        else
        {
            if (agent.isOnNavMesh)
            {
                agent.Warp(destination);
                yield return null;
                agent.velocity = Vector3.zero;
                agent.nextPosition = destination;
            }
            if (persistPosition)
                PersistNpcPositionIfNeeded(npc, destination, persistPosition);
            Log("Finished by timeout/in-camera → forced warp to destination");
        }

        // Solo orientar hacia el jugador en el movimiento inicial, no en el retorno
        if (faceBackTowardsStart && forcedDestination == null)
        {
            Vector3 forward;
            
            if (turnAroundOnArrival)
            {
                // Girar 180° respecto a la dirección de llegada (darse la vuelta)
                forward = startPos - destination;
            }
            else
            {
                // Mirar hacia donde venía (dirección del movimiento)
                forward = destination - startPos;
            }
            
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                npc.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                if (agent.isOnNavMesh)
                {
                    agent.velocity = Vector3.zero;
                    agent.nextPosition = npc.transform.position;
                }
            }
        }
    }

    void EnsureNpcIdle(NPCSimpleAnimator animator)
    {
        if (animator == null)
            return;

        animator.SetMovementSpeed(0f, 0f);

        if (resetAnimationOnEnd)
            animator.ResetMovement();
    }


    void RestoreNpcRotation(NPCBehaviourManager npc, Quaternion targetRotation)
    {
        if (npc == null)
            return;

        npc.transform.rotation = targetRotation;

        var agent = npc.Agent;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // Sincronizar la rotación y posición del agente después de restaurar
            agent.nextPosition = npc.transform.position;
            agent.velocity = Vector3.zero;
        }
    }

    void PersistNpcPositionIfNeeded(NPCBehaviourManager npc, Vector3 destination, bool allowPersistence)
    {
        if (!allowPersistence) return;
        if (npc == null || !npc.persistLastPosition) return;
        
        // Actualizar lastPosition en el componente
        npc.SetLastPosition(destination);
        
        // CRÍTICO: Actualizar también el preset directamente para que se guarde en el próximo SavePoint
        if (!TryResolveActivePreset(out var preset))
        {
            Log("No se pudo obtener preset para persistir posición del NPC");
            return;
        }

        if (preset.npcPositions == null)
            preset.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();

        var id = npc.gameObject.name;
        bool updated = false;
        
        for (int i = 0; i < preset.npcPositions.Count; i++)
        {
            if (preset.npcPositions[i].npcId == id)
            {
                var e = preset.npcPositions[i];
                e.position = destination;
                preset.npcPositions[i] = e;
                updated = true;
                Log($"Actualizada posición de NPC '{id}' en preset: {destination}");
                break;
            }
        }
        
        if (!updated)
        {
            preset.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
            {
                npcId = id,
                position = destination,
                hasActiveState = false,
                isActive = true
            });
            Log($"Añadido NPC '{id}' al preset con posición: {destination}");
        }
    }
    
    void PersistNpcDeactivation(NPCBehaviourManager npc)
    {
        if (npc == null) return;

        if (!TryResolveActivePreset(out var preset))
        {
            Debug.LogWarning("[NpcAutoMoveNode] No se pudo obtener PlayerPresetSO para persistir desactivación del NPC");
            return;
        }

        if (preset.npcPositions == null)
            preset.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();

        var id = npc.gameObject.name;
        bool updated = false;
        
        for (int i = 0; i < preset.npcPositions.Count; i++)
        {
            if (preset.npcPositions[i].npcId == id)
            {
                var e = preset.npcPositions[i];
                e.hasActiveState = true; // Marcar que se guardó el estado
                e.isActive = false; // Marcar como inactivo
                preset.npcPositions[i] = e;
                updated = true;
                Log($"Actualizado estado de NPC '{id}' → isActive=false en preset");
                break;
            }
        }
        
        if (!updated)
        {
            preset.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
            {
                npcId = id,
                position = npc.transform.position,
                hasActiveState = true,
                isActive = false
            });
            Log($"Añadido nuevo NPC '{id}' al preset con isActive=false");
        }
    }

    bool TryResolveActivePreset(out PlayerPresetSO preset)
    {
        preset = null;

        var gb = GameBootService.Profile;
        if (gb != null)
        {
            try { preset = gb.GetActivePresetResolved(); } catch { }
        }

        if (preset != null)
            return true;

        if (preset == null)
        {
            try { preset = ServiceLocator.Get<PlayerPresetSO>(logIfMissing: false); } catch { }
        }

        if (preset != null)
            return true;

        var all = Resources.FindObjectsOfTypeAll<PlayerPresetSO>();
        if (all != null && all.Length > 0)
        {
            preset = all[0];
            return true;
        }

        return false;
    }

    NPCBehaviourManager ResolveNpc()
    {
        NPCBehaviourManager candidate = null;

#if UNITY_2022_3_OR_NEWER
        var npcs = UnityEngine.Object.FindObjectsByType<NPCBehaviourManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var npcs = UnityEngine.Object.FindObjectsOfType<NPCBehaviourManager>(true);
#endif

        if (!string.IsNullOrWhiteSpace(npcName))
        {
            var trimmedName = npcName.Trim();
            foreach (var n in npcs)
            {
                if (n != null)
                {
                    var npcGameObjectName = n.name.Trim();
                    if (string.Equals(npcGameObjectName, trimmedName, StringComparison.OrdinalIgnoreCase))
                        return n;
                }
            }
            // Log para debugging si no se encuentra
            Debug.LogWarning($"[NpcAutoMoveNode] No se encontró NPC con nombre exacto '{trimmedName}'. NPCs disponibles: {string.Join(", ", npcs.Where(n => n != null).Select(n => $"'{n.name}'"))}");
        }

        if (!string.IsNullOrWhiteSpace(npcTag))
        {
            GameObject[] tagged = Array.Empty<GameObject>();
            try
            {
                tagged = GameObject.FindGameObjectsWithTag(npcTag);
            }
            catch (UnityException)
            {
                tagged = Array.Empty<GameObject>();
            }

            foreach (var go in tagged)
            {
                if (!go) continue;
                candidate = go.GetComponent<NPCBehaviourManager>();
                if (candidate != null)
                    return candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(npcName))
        {
            var trimmedName = npcName.Trim();
            var go = GameObject.Find(trimmedName);
            if (go != null)
                candidate = go.GetComponent<NPCBehaviourManager>();
        }

        return candidate;
    }

    bool TryResolveDestination(NPCBehaviourManager npc, out Vector3 destination)
    {
        destination = targetPosition;

        // Prioridad 1: Ir hacia el jugador
        if (moveToPlayer)
        {
            if (PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
            {
                destination = player.transform.position;
                Log($"moveToPlayer=true → destino establecido en posición del jugador: {destination}");
            }
            else
            {
                Debug.LogWarning("[NpcAutoMoveNode] moveToPlayer=true pero no se pudo encontrar al jugador. Usando destino configurado.");
            }
        }
        // Prioridad 2: Usar anchor
        else if (useAnchorPosition && !string.IsNullOrWhiteSpace(targetAnchorName))
        {
            var anchor = GameObject.Find(targetAnchorName);
            if (anchor != null)
                destination = anchor.transform.position;
        }
        // Prioridad 3: Offset relativo al NPC
        else if (useRelativeOffset && npc != null)
        {
            destination = relativeOffsetIsLocal
                ? npc.transform.TransformPoint(relativeOffset)
                : npc.transform.position + relativeOffset;
        }

        destination += anchorOffset;

        if (NavMesh.SamplePosition(destination, out var hit, navmeshSampleRadius, NavMesh.AllAreas))
        {
            destination = hit.position;
            return true;
        }

        return false;
    }

    PlayerActionManager ResolvePlayerActionManager()
    {
        if (PlayerService.TryGetComponent(out PlayerActionManager pam, true, true))
            return pam;
        return UnityEngine.Object.FindFirstObjectByType<PlayerActionManager>();
    }

    string GetCompletionKey()
    {
        return string.IsNullOrEmpty(guid)
            ? $"npcAutoMoveNode__{GetHashCode()}__completed"
            : $"npcAutoMoveNode__{guid}__completed";
    }

    bool ShouldSkipBecauseCompleted(SimpleBlackboard blackboard, string key)
    {
        if (!runOnlyOncePerProfile || string.IsNullOrEmpty(key))
            return false;

        if (IsMarkedCompleted(blackboard, key))
            return true;

        return persistCompletionToPreset && IsCompletionFlagInPreset(key);
    }

    bool IsMarkedCompleted(SimpleBlackboard blackboard, string key)
    {
        if (blackboard == null || string.IsNullOrEmpty(key))
            return false;

        try { return blackboard.Get<bool>(key, false); } catch { return false; }
    }

    bool IsCompletionFlagInPreset(string key)
    {
        if (!persistCompletionToPreset || string.IsNullOrEmpty(key))
            return false;

        if (!TryResolveActivePreset(out var preset))
            return false;

        if (preset.flags == null)
            return false;

        return preset.flags.Contains(key);
    }

    void MarkCompleted(SimpleBlackboard blackboard, string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (blackboard != null)
        {
            try
            {
                blackboard.Set(key, true);
                Log($"Persistencia → marcado flag '{key}' en blackboard");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NpcAutoMoveNode] No se pudo marcar persistencia en blackboard: {ex.Message}");
            }
        }

        PersistCompletionFlag(key);
    }

    void PersistCompletionFlag(string key)
    {
        if (!persistCompletionToPreset || string.IsNullOrEmpty(key))
            return;

        if (!TryResolveActivePreset(out var preset))
            return;

        if (preset.flags == null)
            preset.flags = new List<string>();

        if (preset.flags.Contains(key))
            return;

        preset.flags.Add(key);
        Log($"Persistencia → flag '{key}' guardado en PlayerPreset");
    }

    Camera ResolveCamera()
    {
        var cam = Camera.main;
        if (cam != null) return cam;

        var t = PlayerLocator.ResolvePlayerCamera();
        if (t != null)
            return t.GetComponent<Camera>();

        return null;
    }

    static bool HasLeftCamera(Camera cam, Transform target, float heightOffset, float padding)
    {
        if (!cam || target == null)
            return false;

        var point = target.position + Vector3.up * heightOffset;
        var viewport = cam.WorldToViewportPoint(point);
        if (viewport.z <= 0f)
            return true;

        float pad = Mathf.Max(0f, padding);
        return viewport.x < -pad || viewport.x > 1f + pad || viewport.y < -pad || viewport.y > 1f + pad;
    }

    IEnumerator WaitUntil(Func<bool> predicate, float timeout)
    {
        float timer = 0f;
        while (!predicate() && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }
}
