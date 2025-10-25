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

    [Header("Cámara / Control")]
    public bool lockPlayer = true;
    public ActionMode lockMode = ActionMode.Cinematic;
    [Range(0f, 0.5f)] public float offscreenPadding = 0.05f;
    public float cameraHeightOffset = 1.5f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx?.Runner == null)
        {
            onReadyToAdvance?.Invoke();
            return;
        }

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
                }
            }

            yield return PlayDialogue(npc);
            yield return MoveNpc(npc);
        }
        finally
        {
            if (lockApplied && pam != null)
                pam.PopMode(lockMode);

            done?.Invoke();
        }
    }

    IEnumerator PlayDialogue(NPCBehaviourManager npc)
    {
        bool waited = false;

        if (dialogueOverride != null)
        {
            bool finished = false;
            npc.PlayDialogue(dialogueOverride, () => finished = true);
            waited = true;
            yield return WaitUntil(() => finished, dialogueTimeout);
        }
        else if (triggerInteractableIfNoOverride)
        {
            var interactable = npc.Interactable;
            if (interactable != null && PlayerService.TryGetPlayer(out var player, true) && player != null)
            {
                interactable.Interact(player);
                waited = true;
                var wait = npc.WaitDialogueToClose(dialogueTimeout);
                if (wait != null)
                {
                    while (wait.MoveNext())
                        yield return wait.Current;
                }
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
            yield break;
        }

        var agent = npc.Agent;
        var animator = npc.Animator;

        if (agent == null || !npc.EnsureAgentOnNavMesh(navmeshSampleRadius))
        {
            npc.transform.position = destination;
            if (resetAnimationOnEnd && animator != null)
                animator.ResetMovement();
            yield break;
        }

        NavMeshAgentUtility.SetDestination(agent, destination, stoppingDistance);

        var cam = ResolveCamera();
        float elapsed = 0f;
        bool leftCamera = false;

        while (elapsed < maxWalkSeconds)
        {
            elapsed += Time.deltaTime;

            if (animator != null)
            {
                float speed = NavMeshAgentUtility.ComputeSpeedFactor(agent);
                animator.SetMovementSpeed(Mathf.Max(speed, minAnimSpeed));
            }

            if (HasLeftCamera(cam, npc.transform, cameraHeightOffset, offscreenPadding))
            {
                leftCamera = true;
                break;
            }

            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(stoppingDistance, agent.stoppingDistance) + 0.05f)
                break;

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

        NavMeshAgentUtility.SafeSetStopped(agent, true);
        agent.ResetPath();
        agent.Warp(destination);

        if (resetAnimationOnEnd && animator != null)
            animator.ResetMovement();
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
