// Scripts/World/TeleportService.cs
using UnityEngine;
using UnityEngine.AI;
using EasyTransition;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class TeleportService : MonoBehaviour
{
    // ===== Singleton mínimo =====
    private static TeleportService _inst;
    public static TeleportService Inst
    {
        get
        {
            if (_inst != null) return _inst;
#if UNITY_2022_3_OR_NEWER
            // Intentar seleccionar la mejor instancia disponible (preferir la que tenga settings asignados)
            var list = Object.FindObjectsByType<TeleportService>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            TeleportService best = null;
            foreach (var it in list)
            {
                if (best == null) best = it;
                if (it && it.teleportTransition != null) { best = it; break; }
            }
            _inst = best;
#else
#pragma warning disable 618
            var list = FindObjectsOfType<TeleportService>(true);
            TeleportService best = null;
            foreach (var it in list)
            {
                if (best == null) best = it;
                if (it && it.teleportTransition != null) { best = it; break; }
            }
            _inst = best;
#pragma warning restore 618
#endif
            return _inst;
        }
    }

    // Nuevos eventos para notificar estado de teleport
    public static event System.Action OnTeleportStarted;
    public static event System.Action OnTeleportCut;     // Momento del movimiento real
    public static event System.Action OnTeleportEnded;   // Fin de transición (o inmediato si no hay transición)

    // Helper para invocar eventos de forma segura y con logging por suscriptor
    private static void InvokeEvent(System.Action evt, string eventName)
    {
        if (evt == null) return;
        foreach (var d in evt.GetInvocationList())
        {
            try { ((System.Action)d).Invoke(); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TeleportService] Excepción en suscriptor de {eventName}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    [Header("Transición (EasyTransition)")]
    [SerializeField] private TransitionSettings teleportTransition; // arrastra p.ej. Fade.asset
    [SerializeField] private float transitionDelay; // 0 por defecto implícito
    [SerializeField] private bool useTransitionByDefault = true;

    // Flag propio para no invocar Transition() cuando ya hay una en curso (el plugin usa el mismo mensaje de error)
    private static bool _sTransitionInProgress;

    private void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        //DontDestroyOnLoad(gameObject);
        Debug.Log($"[TeleportService] Awake in '{name}' | TransitionSettings: {(teleportTransition ? teleportTransition.name : "<null>")}");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!teleportTransition)
        {
            Debug.LogWarning("[TeleportService] No hay TransitionSettings asignado. El teletransporte usará modo inmediato.", this);
        }
    }
#endif

    // ================== API ESTÁTICA mínima ==================

    /// <summary>Teleporta a un anchor por id.</summary>
    public static void TeleportToAnchor(GameObject player, string anchorId, bool? useTransition = null)
    {
        if (!Inst) return;
        var sa = SpawnManager.GetAnchor(anchorId);
        if (!sa)
        {
            Debug.LogWarning($"[TeleportService] Anchor '{anchorId}' no encontrado.");
            return;
        }
        Inst.DoTeleportToAnchor(player, sa.transform, useTransition);
    }

    // ================== API de instancia ==================

    public void DoTeleportToAnchor(GameObject player, Transform anchor, bool? useTransition = null)
    {
        if (!player || !anchor)
        {
            Debug.LogWarning("[TeleportService] Parámetros nulos en TeleportToAnchor.");
            return;
        }

        // Sincronizar anchor actual (SpawnManager y runtimePreset) si conocemos su id
        var sa = anchor.GetComponentInParent<SpawnAnchor>();
        if (sa && !string.IsNullOrEmpty(sa.anchorId))
        {
            SpawnManager.SetCurrentAnchor(sa.anchorId);
        }

        var pos = anchor.position;
        var rot = anchor.rotation;

        // Notificar inicio de teleport
        InvokeEvent(OnTeleportStarted, nameof(OnTeleportStarted));

        // Decidir si podemos hacer transición de forma segura
        bool wantTransition = useTransition ?? useTransitionByDefault;
        var tm = wantTransition ? FindTM() : null;
        bool hasSettings = teleportTransition != null;
        bool pluginBusy = IsPluginTransitionRunning();
        bool canTransition = wantTransition && hasSettings && tm != null && !_sTransitionInProgress && !pluginBusy;

        if (!canTransition && wantTransition)
        {
            if (_sTransitionInProgress)
                Debug.LogWarning("[TeleportService] Una transición ya está en curso (local). Se hace teletransporte inmediato para evitar el error del plugin.");
            else if (pluginBusy)
                Debug.LogWarning("[TeleportService] TransitionManager está ocupado con otra transición. Teletransporte inmediato.");
            else if (teleportTransition == null)
                Debug.LogWarning("[TeleportService] No hay TransitionSettings asignado. Se hace teletransporte inmediato.");
            else if (tm == null)
                Debug.LogWarning("[TeleportService] No se encontró TransitionManager. Se hace teletransporte inmediato.");
        }

        if (canTransition) TeleportWithTransition(player, pos, rot, anchor);
        else               MoveNow(player, pos, rot, anchor);
    }

    // ================== Núcleo transición / movimiento ==================
    
    private void TeleportWithTransition(GameObject player, Vector3 worldPos, Quaternion worldRot, Transform anchorForEnv)
    {
        var tm = FindTM(); // ← seguro, no usa Instance()
        if (tm == null || teleportTransition == null)
        {
            // Si el manager aún no está o no tienes settings, teleporta sin fade (no rompe)
            MoveNow(player, worldPos, worldRot, anchorForEnv);
            return;
        }

        if (_sTransitionInProgress)
        {
            Debug.LogWarning("[TeleportService] Se intentó iniciar una transición mientras otra sigue activa. Ejecutando teletransporte inmediato.");
            MoveNow(player, worldPos, worldRot, anchorForEnv);
            return;
        }

        Debug.Log($"[TeleportService] Transition OK → Settings='{teleportTransition.name}', Delay={transitionDelay:0.00}, Manager='{tm.name}'");

        _sTransitionInProgress = true;

        void OnCut()
        {
            MovePlayerSafely(player, worldPos, worldRot);
            ApplyEnvironmentForAnchor(anchorForEnv);
            // Notificar corte (momento del movimiento)
            InvokeEvent(OnTeleportCut, nameof(OnTeleportCut));
            tm.onTransitionCutPointReached -= OnCut;
        }

        void OnEnd()
        {
            _sTransitionInProgress = false;
            // Notificar fin
            InvokeEvent(OnTeleportEnded, nameof(OnTeleportEnded));
            tm.onTransitionEnd -= OnEnd;
        }

        tm.onTransitionCutPointReached += OnCut;
        tm.onTransitionEnd            += OnEnd;

        // OJO: usamos la versión SIN cambio de escena del plugin (la estable)
        tm.Transition(teleportTransition, transitionDelay);
    }

    private void MoveNow(GameObject player, Vector3 pos, Quaternion rot, Transform anchorForEnv)
    {
        MovePlayerSafely(player, pos, rot);
        ApplyEnvironmentForAnchor(anchorForEnv);
        // En modo inmediato, emitir cut y end seguidos
        InvokeEvent(OnTeleportCut, nameof(OnTeleportCut));
        InvokeEvent(OnTeleportEnded, nameof(OnTeleportEnded));
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
    
    static TransitionManager FindTM()
    {
#if UNITY_2022_3_OR_NEWER
        return Object.FindFirstObjectByType<TransitionManager>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<TransitionManager>(true);
#endif
    }

    static bool IsPluginTransitionRunning()
    {
#if UNITY_2022_3_OR_NEWER
        var t = Object.FindFirstObjectByType<Transition>(FindObjectsInactive.Include);
        return t != null;
#else
        return Object.FindObjectOfType<Transition>(true) != null;
#endif
    }
}
