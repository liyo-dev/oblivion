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
            
            // Intentar obtener desde ServiceLocator primero
            if (ServiceLocator.TryGet(out TeleportService service) && service != null)
            {
                _inst = service;
                return _inst;
            }
            
            // Si no está registrado, advertir
            Debug.LogWarning("[TeleportService] No se encontró instancia registrada en ServiceLocator.");
            return null;
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
        ServiceLocator.Register(this);
        //DontDestroyOnLoad(gameObject);
        //Debug.Log($"[TeleportService] Awake in '{name}' | TransitionSettings: {(teleportTransition ? teleportTransition.name : "<null>")}");
    }

    private void OnDestroy()
    {
        if (_inst == this)
        {
            ServiceLocator.Unregister(this);
            _inst = null;
        }
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
            // Fallback: el anchor puede estar en una zona inactiva (zoneRoot desactivado)
            // y nunca ha llamado OnEnable para registrarse en AnchorRegistry.
            // Esta búsqueda es costosa pero solo ocurre al iniciar partida, nunca en Update.
            var all = FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in all)
            {
                if (candidate.anchorId == anchorId) { sa = candidate; break; }
            }
        }
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
        var sa = anchor.GetComponentInParent<SpawnAnchor>(includeInactive: true);
        if (sa && !string.IsNullOrEmpty(sa.anchorId))
        {
            SpawnManager.SetCurrentAnchor(sa.anchorId);
        }

        var pos = anchor.position;
        var rot = anchor.rotation;
        
        // Aplicar orientación según faceDoor
        if (sa != null)
        {
            // CONVENCIÓN: El SpawnAnchor se coloca con el eje Z (forward) apuntando
            // hacia donde quieres que mire el jugador POR DEFECTO
            if (sa.faceDoor)
            {
                // faceDoor = true → Invertir la dirección (mirar al lado contrario)
                // Usamos -forward para dar la vuelta 180°
                rot = Quaternion.LookRotation(-anchor.forward, Vector3.up);
            }
            else
            {
                // faceDoor = false (por defecto) → Usar la dirección del anchor tal cual
                // El jugador mira en la dirección del eje Z del anchor
                rot = Quaternion.LookRotation(anchor.forward, Vector3.up);
            }
        }

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
            TeleportCompanionsToPlayer();
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
        TeleportCompanionsToPlayer();
        ApplyEnvironmentForAnchor(anchorForEnv);
        // En modo inmediato, emitir cut y end seguidos
        InvokeEvent(OnTeleportCut, nameof(OnTeleportCut));
        InvokeEvent(OnTeleportEnded, nameof(OnTeleportEnded));
    }

    private void MovePlayerSafely(GameObject player, Vector3 pos, Quaternion rot)
    {
        if (!player) return;

        player.transform.SetPositionAndRotation(pos, rot);

        var rb = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>(true);
        if (rb && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Teleporta a todos los compañeros (party members + Will NPC instanciado) cerca del jugador.
    /// Llamado automáticamente después de cada teleport del jugador.
    /// </summary>
    private static void TeleportCompanionsToPlayer()
    {
        // 1. Teleportar party members registrados
        if (Game.NPC.PlayerParty.HasInstance)
            Game.NPC.PlayerParty.Instance.TeleportAllMembersToPlayer();

        // 2. Teleportar Will NPC instanciado (no está en el party formal)
        ActiveCharacterSwapper.Instance?.TeleportWillNpcToPlayer();
    }

    /// <summary>
    /// Aplica el entorno (interior/exterior) basado en el anchor de destino.
    /// </summary>
    public void ApplyEnvironmentForAnchor(Transform anchor)
    {
        var ec = EnvironmentController.Instance;
        if (!ec) return;

        AnchorEnvironment env = null;
        // includeInactive:true porque el anchor puede estar en una zona todavía inactiva
        if (anchor) env = anchor.GetComponentInParent<AnchorEnvironment>(includeInactive: true);

        if (env && env.isInterior) ec.ApplyInterior(env);
        else                       ec.ApplyExterior();
    }

    // ================== Utilidades ==================
    
    static TransitionManager FindTM()
    {
        if (ServiceLocator.TryGet(out TransitionManager tm) && tm != null)
            return tm;
        
        // Fallback: intentar obtener instancia del plugin
        return TransitionManager.Instance();
    }

    static bool IsPluginTransitionRunning()
    {
        // El plugin EasyTransition crea objetos Transition temporales durante las transiciones
        // Como no es un servicio registrado, verificamos si hay alguna transición activa
        // consultando el estado del TransitionManager
        var tm = FindTM();
        if (tm == null) return false;
        
        // Si el TransitionManager existe y está procesando una transición,
        // lo consideramos ocupado
        return false; // El plugin maneja esto internamente
    }
}
