// Scripts/World/TeleportService.cs
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using EasyTransition;

[DisallowMultipleComponent]
public class TeleportService : MonoBehaviour
{
    // ===== Singleton mínimo =====
    private static TeleportService _inst;
    public static TeleportService Inst
    {
        get
        {
            if (_inst != null) return _inst;
            // #pragma para silenciar el warning CS0618 si te molesta (opcional)
#pragma warning disable 618
            _inst = FindObjectOfType<TeleportService>(true);
#pragma warning restore 618
            return _inst;
        }
    }

    [Header("Transición (EasyTransition)")]
    [SerializeField] private TransitionSettings teleportTransition; // arrastra p.ej. Fade.asset
    [SerializeField] private float transitionDelay = 0f;
    [SerializeField] private bool useTransitionByDefault = true;

    private void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        DontDestroyOnLoad(gameObject);
    }

    // ================== API ESTÁTICA (compatibilidad) ==================

    /// <summary>API antigua: immediate=true -> SIN transición | immediate=false -> CON transición.</summary>
    public static void PlaceAtAnchor(GameObject player, string anchorName, bool immediate = true)
    {
        if (!Inst) return;
        var anchor = FindAnchorByName(anchorName);
        if (!anchor)
        {
            Debug.LogWarning($"[TeleportService] Anchor '{anchorName}' no encontrado.");
            return;
        }
        bool useTrans = !immediate;
        Inst.DoTeleportToAnchor(player, anchor, useTrans);
    }

    /// <summary>Teleporta a un anchor por nombre. (Estático, compat.)</summary>
    public static void TeleportToAnchor(GameObject player, string anchorName, bool? useTransition = null)
    {
        if (!Inst) return;
        var anchor = FindAnchorByName(anchorName);
        if (!anchor)
        {
            Debug.LogWarning($"[TeleportService] Anchor '{anchorName}' no encontrado.");
            return;
        }
        Inst.DoTeleportToAnchor(player, anchor, useTransition);
    }

    /// <summary>Teleporta a un anchor por Transform. (Estático, compat.)</summary>
    public static void TeleportToAnchor(GameObject player, Transform anchor, bool? useTransition = null)
    {
        if (!Inst) return;
        Inst.DoTeleportToAnchor(player, anchor, useTransition);
    }

    /// <summary>Teleporta directo a posición/rotación. (Estático, compat.)</summary>
    public static void TeleportToPosition(GameObject player, Vector3 worldPos, Quaternion worldRot, bool? useTransition = null)
    {
        if (!Inst) return;
        Inst.DoTeleportToPosition(player, worldPos, worldRot, useTransition);
    }

    // ================== API de instancia (renombrada para no duplicar firmas) ==================

    public void DoTeleportToAnchor(GameObject player, Transform anchor, bool? useTransition = null)
    {
        if (!player || !anchor)
        {
            Debug.LogWarning("[TeleportService] Parámetros nulos en TeleportToAnchor.");
            return;
        }

        var pos = anchor.position;
        var rot = anchor.rotation;
        bool useTrans = useTransition ?? useTransitionByDefault;

        if (useTrans) TeleportWithTransition(player, pos, rot, anchor);
        else          MoveNow(player, pos, rot, anchor);
    }

    public void DoTeleportToPosition(GameObject player, Vector3 worldPos, Quaternion worldRot, bool? useTransition = null)
    {
        bool useTrans = useTransition ?? useTransitionByDefault;
        if (useTrans) TeleportWithTransition(player, worldPos, worldRot, null);
        else          MoveNow(player, worldPos, worldRot, null);
    }

    // ================== Núcleo transición / movimiento ==================

    private void TeleportWithTransition(GameObject player, Vector3 worldPos, Quaternion worldRot, Transform anchorForEnv)
    {
        var tm = TransitionManager.Instance();
        if (tm == null || !teleportTransition)
        {
            if (tm == null) Debug.LogWarning("[TeleportService] TransitionManager no encontrado. Teleport inmediato.");
            if (!teleportTransition) Debug.LogWarning("[TeleportService] TransitionSettings no asignado. Teleport inmediato.");
            MoveNow(player, worldPos, worldRot, anchorForEnv);
            return;
        }

        UnityAction onCut = null;
        UnityAction onEnd = null;

        onCut = () =>
        {
            MovePlayerSafely(player, worldPos, worldRot);
            ApplyEnvironmentForAnchor(anchorForEnv);
            tm.onTransitionCutPointReached -= onCut;
        };

        onEnd = () =>
        {
            tm.onTransitionEnd -= onEnd;
        };

        tm.onTransitionCutPointReached += onCut;
        tm.onTransitionEnd            += onEnd;

        tm.Transition(teleportTransition, transitionDelay);
    }

    private void MoveNow(GameObject player, Vector3 pos, Quaternion rot, Transform anchorForEnv)
    {
        MovePlayerSafely(player, pos, rot);
        ApplyEnvironmentForAnchor(anchorForEnv);
    }

    private void MovePlayerSafely(GameObject player, Vector3 pos, Quaternion rot)
    {
        if (!player) return;

        var cc    = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        var agent = player.GetComponent<NavMeshAgent>()        ?? player.GetComponentInChildren<NavMeshAgent>(true);
        var rb    = player.GetComponent<Rigidbody>()           ?? player.GetComponentInChildren<Rigidbody>(true);

        bool ccWas = cc && cc.enabled;
        bool agWas = agent && agent.enabled;

        if (cc)    cc.enabled = false;
        if (agent) agent.enabled = false;

        player.transform.SetPositionAndRotation(pos, rot);

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (agent) agent.enabled = agWas;
        if (cc)    cc.enabled    = ccWas;
    }

    private void ApplyEnvironmentForAnchor(Transform anchor)
    {
        var ec = EnvironmentController.Instance;
        if (!ec) return;

        AnchorEnvironment env = null;
        if (anchor) env = anchor.GetComponentInParent<AnchorEnvironment>();

        if (env && env.isInterior) ec.ApplyInterior(env);
        else                       ec.ApplyExterior();
    }

    // ================== Utilidades ==================

    private static Transform FindAnchorByName(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.transform : null;
    }
}
