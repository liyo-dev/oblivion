// NpcAutoMoveNode.cs
using System;
using System.Collections;
using Alex.NPC;
using Alex.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bloquea al jugador, fuerza el diálogo del NPC y lo hace caminar hacia una posición destino.
/// En cuanto el NPC sale de cámara se le teleporta a la ubicación final y se devuelve el control.
/// </summary>
[Serializable]
public sealed class NpcAutoMoveNode : NarrativeNode
{
    [Header("NPC Lookup")]
    public string npcName;
    public string npcTag;

    [Header("Diálogo")]
    public DialogueAsset dialogueOverride;
    public bool triggerInteractableIfNoOverride = true;
    [Min(1f)] public float dialogueTimeout = 60f;

    [Header("Destino")]
    public string targetAnchorName;
    public Vector3 targetPosition;
    public Vector3 anchorOffset = Vector3.zero;
    public bool useAnchorPosition = true;
    public bool useRelativeOffset = false;
    public bool relativeOffsetIsLocal = true;
    public Vector3 relativeOffset = new Vector3(0f, 0f, 5f);
    [Min(0.1f)] public float navmeshSampleRadius = 2f;
    [Min(0f)] public float stoppingDistance = 0.35f;

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

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Compatibilidad")]
    [Tooltip("Si el agente no consigue path tras un breve intento, finalizar igualmente y warp al destino.")]
    public bool treatNoPathAsReach = true;

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

        Log($"Enter → npcName='{npcName}', npcTag='{npcTag}'");
        ctx.Runner.StartCoroutine(RunSequence(onReadyToAdvance));
    }

    IEnumerator RunSequence(Action done)
    {
        var npc = ResolveNpc();
        if (npc == null)
        {
            Debug.LogWarning("[NpcAutoMoveNode] No se encontró el NPC configurado.");
            done?.Invoke();
            yield break;
        }

        npc.EnsurePlayerReference();

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

            yield return PlayDialogue(npc);
            yield return MoveNpc(npc);
        }
        finally
        {
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

    IEnumerator MoveNpc(NPCBehaviourManager npc)
    {
        if (!TryResolveDestination(npc, out var destination))
        {
            Debug.LogWarning("[NpcAutoMoveNode] No se pudo resolver el destino. Teleportando al origen configurado.");
            npc.transform.position = targetPosition;
            EnsureNpcIdle(npc.Animator);
            yield break;
        }

        var agent = npc.Agent;
        var animator = npc.Animator;

        // Asegurar agente habilitado y en NavMesh. Si falla en origen, intenta en destino antes de rendirse.
        if (agent == null)
        {
            Debug.LogWarning("[NpcAutoMoveNode] NPC no tiene NavMeshAgent; no puede caminar. Teleport al destino.");
            npc.transform.position = destination;
            PersistNpcPositionIfNeeded(npc, destination);
            EnsureNpcIdle(animator);
            yield break;
        }

        if (!agent.enabled) agent.enabled = true;

        bool onMesh = npc.EnsureAgentOnNavMesh(navmeshSampleRadius);
        if (!onMesh)
        {
            // Reintentar capturando el punto de NavMesh más cercano al destino
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
            PersistNpcPositionIfNeeded(npc, destination);
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

            // solo permitir teletransporte por offscreen tras cierto tiempo/distancia
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

            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f)
                {
                    Log("Agent reached stopping distance");
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

        // Decidir cómo finalizar
        float reachThresh = Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f;
        bool reached = (!agent.pathPending && agent.remainingDistance <= reachThresh) || forcedReachNoPath;

        if (leftCamera)
        {
            // Fuera de cámara: teletransportar para terminar limpio
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            agent.ResetPath();
            agent.Warp(destination);
            PersistNpcPositionIfNeeded(npc, destination);
            EnsureNpcIdle(animator);
            Log("Finished by offscreen → teleported and persisted position");
        }
        else if (reached)
        {
            // Llegó al destino: detener agente y asegurar posición final
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            agent.ResetPath();
            if (Vector3.Distance(agent.transform.position, destination) > 0.2f)
                agent.Warp(destination);
            PersistNpcPositionIfNeeded(npc, destination);
            EnsureNpcIdle(animator);
            Log(forcedReachNoPath ? "Finished by forced reach (no path) → warped to destination" : "Finished by reach → stopped at destination");
        }
        else
        {
            // Caso límite: agotó el tiempo máximo visible pero sigue en cámara.
            // Para evitar carreras eternas, moverlo al destino y dejarlo en idle.
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            agent.ResetPath();
            agent.Warp(destination);
            PersistNpcPositionIfNeeded(npc, destination);
            EnsureNpcIdle(animator);
            Log("Finished by timeout/in-camera → forced warp to destination");
        }

        FaceBackTowardsStart(npc, startPos, destination);
    }

    void EnsureNpcIdle(NPCSimpleAnimator animator)
    {
        if (animator == null)
            return;

        animator.SetMovementSpeed(0f);

        if (resetAnimationOnEnd)
            animator.ResetMovement();
    }

    void FaceBackTowardsStart(NPCBehaviourManager npc, Vector3 startPosition, Vector3 destination)
    {
        if (npc == null)
            return;

        Vector3 backward = startPosition - destination;
        backward.y = 0f;

        if (backward.sqrMagnitude < 0.0001f)
        {
            backward = -npc.transform.forward;
            backward.y = 0f;
        }

        if (backward.sqrMagnitude < 0.0001f)
            return;

        backward.Normalize();

        npc.transform.rotation = Quaternion.LookRotation(backward, Vector3.up);

        var agent = npc.Agent;
        if (agent != null && agent.enabled)
            agent.nextPosition = npc.transform.position;
    }

    void PersistNpcPositionIfNeeded(NPCBehaviourManager npc, Vector3 destination)
    {
        if (npc == null || !npc.persistLastPosition) return;
        npc.SetLastPosition(destination);

        PlayerPresetSO preset = null;

        // 1) Intentar a través de GameBootService.Profile
        var gb = GameBootService.Profile;
        if (gb != null)
        {
            try { preset = gb.GetActivePresetResolved(); } catch { }
        }

        // 2) ServiceLocator
        if (preset == null)
        {
            try { preset = ServiceLocator.Get<PlayerPresetSO>(logIfMissing: false); } catch { }
        }

        // 3) Fallback: buscar en memoria
        if (preset == null)
        {
            var all = Resources.FindObjectsOfTypeAll<PlayerPresetSO>();
            if (all != null && all.Length > 0) preset = all[0];
        }

        if (preset == null) return;

        if (preset.npcPositions == null)
            preset.npcPositions = new System.Collections.Generic.List<PlayerPresetSO.NpcPosEntry>();

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
                break;
            }
        }
        if (!updated)
        {
            preset.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
            {
                npcId = id,
                position = destination
            });
        }
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
            foreach (var n in npcs)
            {
                if (n != null && string.Equals(n.name, npcName, StringComparison.OrdinalIgnoreCase))
                    return n;
            }
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
            var go = GameObject.Find(npcName);
            if (go != null)
                candidate = go.GetComponent<NPCBehaviourManager>();
        }

        return candidate;
    }

    bool TryResolveDestination(NPCBehaviourManager npc, out Vector3 destination)
    {
        destination = targetPosition;

        if (useAnchorPosition && !string.IsNullOrWhiteSpace(targetAnchorName))
        {
            var anchor = GameObject.Find(targetAnchorName);
            if (anchor != null)
                destination = anchor.transform.position;
        }
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
