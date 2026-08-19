using UnityEngine;
using UnityEngine.AI;
using Game.NPC;

/// <summary>
/// Orquesta el cambio de personaje activo en el sistema de equipo.
///
/// Cuando el jugador cambia a Liam o Estela:
///   1. Teleporta el controller de Will a la posición del NPC objetivo.
///   2. Aplica la apariencia del personaje vía CharacterAppearanceRegistry.
///   3. Actualiza los hechizos del MagicCaster con los del NPCPartyConfig.
///   4. Oculta el NPC objetivo (el controller ES ese personaje ahora).
///   5. Reactiva el NPC anterior (Will vuelve a ser un compañero IA, o Liam/Estela reanudan seguimiento).
///
/// Will nunca desaparece del mundo: cuando no es el personaje activo su NPC
/// permanece visible y sigue al jugador como IA.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ActiveCharacterSwapper : MonoBehaviour
{
    #region Singleton
    public static ActiveCharacterSwapper Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Componentes del player")]
    [Tooltip("El MagicCaster del jugador. Se auto-busca si no se asigna.")]
    [SerializeField] private MagicCaster magicCaster;

    [Header("Nombres en el party (deben coincidir con NPCPartyConfig.displayName)")]
    [SerializeField] private string liamDisplayName = "Liam";
    [SerializeField] private string estelaDisplayName = "Estela";

    [Header("NPC de Will (prefab, se instancia cuando Will no es el activo)")]
    [Tooltip("Prefab de Will como NPC. Se instancia al cambiar a Liam/Estela y se destruye al volver a Will.")]
    [SerializeField] private GameObject willNpcPrefab;

    private NPCPartyMember _willNpcInstance;

    /// <summary>
    /// Referencia al NPC instanciado de Will (cuando no es el personaje activo).
    /// Usado por PlayerParty para notificarle eventos de combate.
    /// </summary>
    public NPCPartyMember WillNpcInstance => _willNpcInstance;

    // Hechizos de Will, actualizados cada vez que se abandona su slot
    private MagicSpellSO _willLeft, _willRight, _willSpecial;

    // NPC actualmente oculto porque el controller lo está representando
    private NPCPartyMember _hiddenNpc;

    private bool _ready;
    public bool IsReady => _ready;

    // Temporizador para verificar periódicamente si el Will NPC sigue al jugador
    private float _willFollowCheckTimer;

    // Temporizador para verificar periódicamente que el NPC oculto (representado ahora mismo
    // por el controller) sigue oculto. Ver EnsureHiddenNpcSuppressed().
    private float _hiddenCheckTimer;

    // Cámara principal cacheada (regla: nunca Camera.main por frame). Se refresca si muere.
    private Camera _mainCamCached;

    // Ticks consecutivos (de 0.5s) en los que Will está dentro del encuadre de la cámara pero
    // ninguno de sus renderers se está renderizando. 2+ = estado anómalo confirmado.
    private int _willInvisibleStrikes;

    // Contador de ticks para el latido de diagnóstico 🫀 (cada 5 ticks de 0.5s = ~2.5s).
    private int _willDiagTick;

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (magicCaster == null)
            magicCaster = GetComponentInParent<MagicCaster>()
                ?? UnityEngine.Object.FindAnyObjectByType<MagicCaster>();

        CaptureWillSpells();

        PartyControlManager.OnFollowModeChanged += OnFollowModeChanged;

        _ready = true;
        // Notificar al PartyControlManager para que reintente cualquier restauración
        // diferida que se bloqueó porque Start() aún no había corrido.
        PartyControlManager.Instance?.OnSwapperReady();
    }

    private void OnDestroy()
    {
        PartyControlManager.OnFollowModeChanged -= OnFollowModeChanged;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Red de seguridad periódica contra el bug conocido "dos Estelas y un Will" (ver
        // comentario en ResetState()): si el NPC actualmente oculto recupera renderers activos
        // por culpa de otro sistema (ModularAutoBuilder reconstruyendo apariencia, un rejoin al
        // party, el chequeo periódico de NPCPartyMember.Update()...), se ve DUPLICADO — el NPC
        // "oculto" reaparece mientras el controller ya luce esa misma apariencia. Antes solo
        // existía protección contra esta carrera al REVELAR (ReassertVisibilityNextFrames); esta
        // comprobación cubre el caso simétrico de OCULTAR, igual que EnsureWillNpcVisible cubre a
        // Will. Gateado a 0.5s, no por frame, así que no incumple la regla de
        // GetComponentInChildren en Update. Corre siempre (no solo cuando _willNpcInstance existe)
        // porque el NPC oculto puede ser Liam o Estela sin que Will esté instanciado como NPC.
        if (_hiddenNpc != null)
        {
            _hiddenCheckTimer += Time.deltaTime;
            if (_hiddenCheckTimer >= 0.5f)
            {
                _hiddenCheckTimer = 0f;
                EnsureHiddenNpcSuppressed();
            }
        }

        if (_willNpcInstance == null) return;

        _willFollowCheckTimer += Time.deltaTime;
        if (_willFollowCheckTimer < 0.5f) return;
        _willFollowCheckTimer = 0f;

        // Red de seguridad periódica contra la condición de carrera de FIX INC-050 (ver
        // SetNpcVisible/ReassertWillVisibilityNextFrames): si algo tocó los renderers del Will
        // NPC instanciado más tarde de lo que cubre esa ventana (p.ej. el Brain tardó más de lo
        // normal en inicializar, o hubo un pico de carga), lo detectamos aquí. Invariante: si
        // _willNpcInstance existe, SIEMPRE debe estar visible (Will nunca se instancia como NPC
        // mientras él sea el personaje activo). Gateado a 0.5s, no por frame, así que no incumple
        // la regla de GetComponentInChildren en Update.
        EnsureWillNpcVisible();

        var brain = _willNpcInstance.NPCManager?.Brain;
        if (brain == null) return;
        if (_willNpcInstance.NPCManager.Context.IsInCinematic) return;

        // Red de seguridad: si hay un enemigo activo (ej. el Golem) pero el Will NPC instanciado
        // no está en combate, forzarlo a entrar. Cubre el caso en que ApplyFollowModeToWillNpc
        // comprobó el combate antes de que el Brain estuviera listo (o el enemigo se registró un
        // frame más tarde) y Will se quedaba parado en vez de atacar tras cambiar a otro personaje.
        var activeEnemy = GetActiveCombatEnemy();
        if (activeEnemy != null && !_willNpcInstance.NPCManager.Context.IsInCombat)
        {
            _willNpcInstance.OnPlayerEnteredCombat(activeEnemy);
            return;
        }

        if (_willNpcInstance.NPCManager.Context.IsInCombat) return;
        if (!(PartyControlManager.Instance?.IsPartyFollowing ?? true)) return;

        // Si cayó en Idle y NO está anclado, reiniciar seguimiento
        if (brain.CurrentState?.StateName == "Idle"
            && !(_willNpcInstance.NPCManager?.Context?.IsPinnedByParty ?? false))
            _willNpcInstance.StartFollowingIgnorePartyCheck();
    }

    /// <summary>
    /// Reafirma que el Will NPC instanciado tiene todos sus renderers activos. Ver comentario
    /// en el llamador (Update) sobre la condición de carrera que esto blinda.
    /// </summary>
    private void EnsureWillNpcVisible()
    {
        if (_willNpcInstance == null) return;

        bool hadDisabled = false;
        foreach (var r in _willNpcInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && !r.enabled)
            {
                r.enabled = true;
                hadDisabled = true;
            }

            // Ver comentario en SpawnWillNpc(): partes activadas más tarde por ModularAutoBuilder
            // (equipamiento, pelo, etc.) traen su propio SkinnedMeshRenderer con
            // updateWhenOffscreen=false por defecto — reafirmarlo aquí también cubre esos casos.
            // Además forzamos la lectura de .bounds: es lo que el editor hace al seleccionar el
            // objeto en la Hierarchy y lo que realmente "curaba" el AABB de culling atascado —
            // red de seguridad extra por si algún recálculo se pierde entre spawn y este tick.
            if (r is SkinnedMeshRenderer smr)
            {
                if (!smr.updateWhenOffscreen)
                    smr.updateWhenOffscreen = true;
                if (smr.gameObject.activeInHierarchy)
                    _ = smr.bounds;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (hadDisabled)
            Debug.LogWarning("[ActiveCharacterSwapper] Will NPC tenía renderers desactivados fuera de la ventana de ReassertWillVisibilityNextFrames — reactivados por la red de seguridad periódica (0.5s).");
#endif

        DiagnoseAndHealWillVisibility();
    }

    /// <summary>
    /// Simétrico de EnsureWillNpcVisible pero para el caso general: reafirma que el NPC
    /// actualmente oculto (_hiddenNpc, el que el controller está representando) sigue con sus
    /// renderers apagados. Si algo los reactivó fuera de la ventana de ReassertHiddenNextFrames,
    /// los vuelve a apagar aquí. Evita el bug "dos Estelas y un Will" (ver ResetState()) cuando
    /// el NPC afectado es Liam o Estela en vez de Will.
    /// </summary>
    private void EnsureHiddenNpcSuppressed()
    {
        if (_hiddenNpc == null) return;

        bool hadEnabled = false;
        foreach (var r in _hiddenNpc.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.enabled)
            {
                r.enabled = false;
                hadEnabled = true;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (hadEnabled)
            Debug.LogWarning($"[ActiveCharacterSwapper] NPC oculto '{_hiddenNpc.name}' tenía renderers reactivados fuera de la ventana de ReassertHiddenNextFrames — vuelto a ocultar por la red de seguridad periódica (0.5s). Bug 'dos Estelas y un Will' evitado.");
#endif
    }

    /// <summary>
    /// Detector + autocuración del "Will invisible" residual (secuela de INC-050 que las medidas
    /// de SpawnWillNpc no llegaban a cubrir en todos los casos, p.ej. dejar a Will anclado en un
    /// botón en modo Libre y volver con otro personaje).
    ///
    /// Condición anómala: el punto de pecho de Will está dentro del encuadre de la cámara, a
    /// distancia razonable, con renderers activos y enabled... y aun así NINGUNO se está
    /// renderizando (Renderer.isVisible == false en todos) durante 2 ticks seguidos (~1s).
    /// Un objeto en pantalla que lleva 1s sin renderizarse no es culling legítimo: es
    /// exactamente el estado "atascado" que se curaba seleccionando el GO en la Hierarchy.
    ///
    /// Curación según la clase de fallo detectada:
    ///  - Renderers activos pero nunca visibles → apagar y reencender Renderer.enabled. A
    ///    diferencia de leer .bounds (lo que ya hacíamos y no bastaba), el toggle fuerza el
    ///    des-registro y re-registro del renderer en el sistema de culling, que es lo mismo que
    ///    consigue la selección en la Hierarchy. Se acompaña de Animator.Update(0f) para
    ///    garantizar pose real antes del siguiente test de visibilidad.
    ///  - Ningún renderer activo+enabled (partes del modelo desactivadas por alguien) →
    ///    reaplicar la apariencia de Will vía su ModularAutoBuilder, como hace SpawnWillNpc.
    ///
    /// Siempre deja un log [🩺] con el estado detectado: si el bug reaparece, ese log nos dice
    /// exactamente qué clase de fallo fue y con qué números.
    /// Gateado por el tick de 0.5s de Update — no corre por frame.
    /// </summary>
    private void DiagnoseAndHealWillVisibility()
    {
        if (_willNpcInstance == null) return;

        if (_mainCamCached == null || !_mainCamCached.isActiveAndEnabled)
            _mainCamCached = Camera.main;
        var cam = _mainCamCached;
        if (cam == null) return;

        Vector3 chest = _willNpcInstance.transform.position + Vector3.up * 1.2f;
        Vector3 vp = cam.WorldToViewportPoint(chest);
        float dist = Vector3.Distance(cam.transform.position, chest);
        bool enEncuadre = vp.z > 0f && vp.x > 0.08f && vp.x < 0.92f && vp.y > 0.08f && vp.y < 0.92f;

        bool algunoVisible = false;
        bool algunoActivoYEncendido = false;
        int activos = 0;
        int visibles = 0;
        Renderer firstActive = null;
        var renderers = _willNpcInstance.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
            algunoActivoYEncendido = true;
            activos++;
            if (firstActive == null) firstActive = r;
            // Nota editor: isVisible también cuenta la Scene View. Si la Scene View está
            // renderizando a Will, este detector no salta — probar con la Scene View cerrada
            // (Game view maximizado) o en build para una lectura fiable.
            if (r.isVisible) { algunoVisible = true; visibles++; }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 🫀 Latido de diagnóstico (cada ~2.5s mientras exista el clon): foto del estado real
        // de Will aunque no salte ninguna anomalía. Cuando el jugador reporte "Will invisible",
        // estas líneas dicen DÓNDE está Will de verdad (¿bajo el suelo? ¿desplazado del botón?),
        // a qué distancia de la cámara, y cuántos renderers se están renderizando — distingue
        // "no se renderiza" de "está en otro sitio" sin depender de reproducirlo con el inspector.
        //
        // AMPLIADO tras descartar Occlusion Culling (2026-08-16, segunda repro: renderers
        // activos=6/218 visibles=0 IDÉNTICO con allowOcclusionWhenDynamic=false ya aplicado):
        // se añaden bounds reales, escala del objeto, layer y la cámara+cullingMask que Unity
        // usa de verdad, para no tener que adivinar una tercera causa a ciegas la próxima vez.
        _willDiagTick++;
        if (_willDiagTick >= 5)
        {
            _willDiagTick = 0;
            var diagAgent = _willNpcInstance.GetComponent<NavMeshAgent>();
            string boundsInfo = "sinRenderers";
            string layerInfo = "?";
            if (firstActive != null)
            {
                var b = firstActive.bounds;
                boundsInfo = $"center={b.center} size={b.size}";
                layerInfo = $"{LayerMask.LayerToName(firstActive.gameObject.layer)}({firstActive.gameObject.layer})";
            }
            Vector3 scale = _willNpcInstance.transform.lossyScale;
            bool inCullingMask = firstActive != null && (cam.cullingMask & (1 << firstActive.gameObject.layer)) != 0;
            Debug.Log($"[ActiveCharacterSwapper] 🫀 WillNPC pos={_willNpcInstance.transform.position} " +
                $"distCam={dist:F1}m enEncuadre={enEncuadre} vp=({vp.x:F2},{vp.y:F2},{vp.z:F2}) " +
                $"renderers activos={activos}/{renderers.Length} visibles={visibles} " +
                $"pinned={_willNpcInstance.NPCManager?.Context?.IsPinnedByParty.ToString() ?? "?"} " +
                $"estado={_willNpcInstance.NPCManager?.Brain?.CurrentState?.StateName ?? "?"} " +
                $"onNavMesh={(diagAgent != null && diagAgent.isOnNavMesh)} " +
                $"cam={cam.name}(cullingMask={cam.cullingMask:X}) rendererLayer={layerInfo} " +
                $"inCullingMask={inCullingMask} lossyScale={scale} rendererBounds=[{boundsInfo}] " +
                $"useOcclusionCulling={cam.useOcclusionCulling} allowOcclusionWhenDynamic={(firstActive != null ? firstActive.allowOcclusionWhenDynamic.ToString() : "?")} " +
                $"materialNull={(firstActive != null && firstActive.sharedMaterial == null)} " +
                $"shader={(firstActive != null ? firstActive.sharedMaterial?.shader?.name ?? "NULL" : "?")}");
        }
#endif

        // Solo evaluar la anomalía cuando Will debería verse sí o sí: bien dentro del encuadre
        // y a <60m. Fuera de encuadre, isVisible=false es culling legítimo y no significa nada.
        if (!enEncuadre || dist > 60f) { _willInvisibleStrikes = 0; return; }
        if (algunoVisible) { _willInvisibleStrikes = 0; return; }

        _willInvisibleStrikes++;
        if (_willInvisibleStrikes < 2) return;
        _willInvisibleStrikes = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var agent = _willNpcInstance.GetComponent<NavMeshAgent>();
        Debug.LogWarning($"[ActiveCharacterSwapper] 🩺 Will NPC en encuadre pero SIN renderizar durante ~1s — " +
            $"clase={(algunoActivoYEncendido ? "renderers activos atascados en culling" : "partes del modelo desactivadas")}, " +
            $"renderersActivos={activos}/{renderers.Length}, pos={_willNpcInstance.transform.position}, dist={dist:F1}m, " +
            $"viewport=({vp.x:F2},{vp.y:F2},{vp.z:F2}), onNavMesh={(agent != null && agent.isOnNavMesh)}. Aplicando autocuración.");
#endif

        if (!algunoActivoYEncendido)
        {
            // Clase B: alguien desactivó las partes del modelo. Reaplicar apariencia de Will.
            var registryHeal = CharacterAppearanceRegistry.Instance;
            var npcBuilder = _willNpcInstance.GetComponentInChildren<ModularAutoBuilder>(true);
            var app = registryHeal != null ? registryHeal.GetAppearance(PartyControlManager.CharacterSlot.Will) : null;
            if (npcBuilder != null && app != null && app.Count > 0)
            {
                npcBuilder.DeactivateAllCategories();
                npcBuilder.ApplySelection(app);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ActiveCharacterSwapper] 🩺 Apariencia de Will reaplicada al NPC ({app.Count} partes).");
#endif
            }
        }
        else
        {
            // Clase A: renderers correctos pero el sistema de culling nunca los da por visibles.
            // Toggle enabled = des-registro + re-registro en culling (equivale a la "cura" de
            // seleccionar el GO en la Hierarchy, pero desde código).
            foreach (var r in renderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
                r.enabled = false;
                r.enabled = true;
                if (r is SkinnedMeshRenderer smr)
                    smr.updateWhenOffscreen = true;
            }
            var anim = _willNpcInstance.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                // FIX Will invisible — mismo AlwaysAnimate que SpawnWillNpc()/WarpNpcToPosition()
                // (ver comentario detallado en SpawnWillNpc): si este heal periódico llega a
                // dispararse es que el interbloqueo de CullUpdateTransforms ya se rearmó una vez
                // pese al fix de spawn/warp — reafirmar AlwaysAnimate aquí es la red de seguridad
                // por si algo (ModularAutoBuilder reconstruyendo el rig, un Animator distinto tras
                // reaplicar apariencia, etc.) lo hubiera revertido al valor por defecto.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Update(0f);
            }
        }
    }
    #endregion

    #region API pública
    /// <summary>
    /// Ejecuta el cambio de personaje activo.
    /// Llamado por PartyControlManager cuando el jugador pulsa DPad Izquierda/Derecha.
    /// </summary>
    public void SwitchCharacter(PartyControlManager.CharacterSlot from, PartyControlManager.CharacterSlot to)
    {
        if (!_ready || from == to) return;

        var registry = CharacterAppearanceRegistry.Instance;
        Debug.Log($"[ActiveCharacterSwapper] SwitchCharacter {from}→{to} | registry={(object)registry ?? "NULL"} | _ready={_ready}");

        // Capturar posición actual del controller ANTES de teleportar, para anclar NPCs
        PlayerService.TryGetPlayer(out var playerGO);
        Vector3 fromPos = playerGO != null ? playerGO.transform.position : Vector3.zero;
        Quaternion fromRot = playerGO != null ? playerGO.transform.rotation : Quaternion.identity;

        // 1. Guardar estado del personaje que se abandona
        registry?.CaptureCurrentAppearance(from);
        if (from == PartyControlManager.CharacterSlot.Will)
        {
            CaptureWillSpells();
            _willNpcInstance?.SetRuntimeSpells(_willLeft, _willRight, _willSpecial);
        }

        // 2. Teleportar el controller a la posición del NPC objetivo (solo al ir a Liam/Estela)
        var toNpc = GetNpc(to);
        if (to != PartyControlManager.CharacterSlot.Will && toNpc != null)
            TeleportPlayer(toNpc.transform.position, toNpc.transform.rotation);

        // 3. Cambiar apariencia visual
        registry?.ApplyAppearance(to);

        // 4. Cambiar hechizos del player
        ApplySpells(to);

        // 5. Gestionar visibilidad de NPCs de Liam/Estela
        // IMPORTANT: actualizar _hiddenNpc antes de llamar SetNpcVisible para que el guard
        // en OnPlayerEnteredCombat no bloquee al NPC que acaba de ser liberado del control del jugador.
        var prevHidden = _hiddenNpc;
        _hiddenNpc = toNpc;
        // Devolver el NPC previo exactamente a donde estaba el controller (ej: encima de un botón)
        if (prevHidden != null)
            WarpNpcToPosition(prevHidden, fromPos, fromRot);
        SetNpcVisible(prevHidden, true);
        SetNpcVisible(_hiddenNpc, false);

        // 5b. Desvincular compañeros del personaje que se abandona.
        // Un NPC que se unió mientras jugábamos como 'from' (Liam/Estela, no Will) es
        // compañero de ese personaje concreto. Al cambiar de personaje debe quedarse
        // junto al NPC prevHidden (están en la misma posición), no seguir al nuevo.
        // Excepción: si ese NPC es el toNpc (pasa a ser el personaje activo), se mantiene.
        {
            var party = Game.NPC.PlayerParty.Instance;
            if (party != null && from != PartyControlManager.CharacterSlot.Will)
            {
                var toDetach = new System.Collections.Generic.List<Game.NPC.NPCPartyMember>();
                foreach (var member in party.Members)
                {
                    if (member == null || member == toNpc) continue;
                    if (member._joinedForSlot == from) toDetach.Add(member);
                }
                foreach (var member in toDetach)
                    party.RemoveMember(member);
            }
        }

        // 6. NPC de Will: instanciar al alejarse de Will, destruir al volver
        bool willIsActive = to == PartyControlManager.CharacterSlot.Will;
        if (willIsActive)
        {
            // Teleportar el controller a donde está el NPC de Will antes de destruirlo
            if (_willNpcInstance != null)
                TeleportPlayer(_willNpcInstance.transform.position, _willNpcInstance.transform.rotation);
            DestroyWillNpc();
        }
        else if (_willNpcInstance == null)
        {
            // Will spawna exactamente donde estaba el controller (ej: encima de un botón)
            SpawnWillNpc(fromPos, fromRot);
        }
        else
        {
            // Will ya estaba instanciado (cambio Liam↔Estela): colocar a Will donde estaba
            // el personaje que se abandona. En combate activo se respeta la posición actual
            // para no interrumpir la IA de combate.
            //
            // FIX: si Will está anclado (IsPinnedByParty, modo Libre) o en cinemática, NO debe
            // teletransportarse aquí. Antes este warp era incondicional y arrancaba a Will de
            // donde el jugador lo hubiera colocado a propósito (p.ej. sobre una placa de presión
            // para un puzle), moviéndolo junto al otro personaje sin aviso — para el jugador
            // esto se percibía como que "Will desaparecía" justo en momentos críticos de puzles.
            var willContext = _willNpcInstance.NPCManager?.Context;
            bool willPinnedOrInCinematic = (willContext?.IsPinnedByParty ?? false) || (willContext?.IsInCinematic ?? false);

            if (willPinnedOrInCinematic)
            {
                Debug.Log("[ActiveCharacterSwapper] Will NPC anclado (modo Libre) o en cinemática — no se reposiciona al cambiar de personaje.");
            }
            else
            {
                var activeCombatEnemy = GetActiveCombatEnemy();
                if (activeCombatEnemy == null)
                {
                    // Buscar posición válida en NavMesh (radio amplio para cubrir vuelo/natación)
                    Vector3 willPos = fromPos;
                    if (NavMesh.SamplePosition(fromPos, out NavMeshHit willHit, 30f, NavMesh.AllAreas))
                        willPos = willHit.position;
                    WarpNpcToPosition(_willNpcInstance, willPos, fromRot);
                }
                else if (!(_willNpcInstance.NPCManager?.Context?.IsInCombat ?? false))
                {
                    // Hay combate activo pero Will no está participando (perdió el target, etc.)
                    // Re-notificarle para que entre en AllyCombatState.
                    _willNpcInstance.OnPlayerEnteredCombat(activeCombatEnemy);
                }
            }
        }
    }

    /// <summary>
    /// Resetea el estado del swapper, útil al cargar una nueva partida o volver del menú.
    /// </summary>
    public void ResetState()
    {
        Debug.Log("[ActiveCharacterSwapper] 🔄 Reseteando estado.");
        DestroyWillNpc();

        // FIX: red de seguridad para el bug "dos Estelas y un Will" tras Game Over. En el camino
        // normal (recarga completa de escena) _hiddenNpc ya apunta a un objeto destruido y esto
        // es un no-op. Pero si por cualquier motivo ResetState() se llama SIN que la escena se
        // haya recargado de verdad (p. ej. un camino futuro que reutilice la escena actual), el
        // NPC que el controller estaba representando se quedaba oculto para siempre — y
        // WorldBootstrap podía instanciar un NPC nuevo y totalmente visible para ese mismo
        // personaje encima, dando la sensación de "personaje duplicado". Restaurar su visibilidad
        // antes de soltar la referencia evita ese escenario sin coste en el camino normal.
        if (_hiddenNpc != null)
            SetNpcVisible(_hiddenNpc, true);
        _hiddenNpc = null;

        // Asegurarse de que los hechizos de Will se capturen de nuevo si es necesario
        CaptureWillSpells();
        _ready = true; // Asegurarse de que esté listo para operar
    }

    /// <summary>
    /// Devuelve el NPC actualmente oculto (el que el controller representa).
    /// Útil para que PartyControlManager lo excluya del modo Libre/Siguiendo.
    /// </summary>
    public NPCPartyMember HiddenNpc => _hiddenNpc;

    /// <summary>
    /// Teleporta el NPC instanciado de Will cerca del jugador.
    /// Llamado por TeleportService tras teleportar al jugador.
    /// </summary>
    public void TeleportWillNpcToPlayer()
    {
        if (_willNpcInstance == null) return;
        // No teletransportar si el NPC de Will está anclado (equipo disuelto o modo libre)
        if (_willNpcInstance.NPCManager?.Context?.IsPinnedByParty == true) return;
        if (!PlayerService.TryGetPlayer(out var playerGO)) return;

        Vector3 behind = playerGO.transform.position - playerGO.transform.forward * 1.5f;
        if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            behind = hit.position;

        var agent = _willNpcInstance.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
            agent.Warp(behind);
        else
            _willNpcInstance.transform.position = behind;

        // Reanudar seguimiento si corresponde
        if (PartyControlManager.Instance?.IsPartyFollowing == true)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
    }
    #endregion

    #region Internals
    private void OnFollowModeChanged(bool isFollowing)
    {
        if (_willNpcInstance == null) return;

        if (isFollowing)
        {
            if (_willNpcInstance.NPCManager?.Context != null)
                _willNpcInstance.NPCManager.Context.IsPinnedByParty = false;
            _willNpcInstance.StartFollowingIgnorePartyCheck();
        }
        else
        {
            _willNpcInstance.StopFollowing();
            if (_willNpcInstance.NPCManager?.Context != null)
                _willNpcInstance.NPCManager.Context.IsPinnedByParty = true;
        }
    }

    private void SpawnWillNpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (willNpcPrefab == null) return;

        // En combate activo: spawnear junto al jugador para que Will pueda atacar de inmediato
        // en vez de desde la posición previa del controller (puede estar a > 30m, lo que fuerza
        // AllyCombatState a modo FollowingPlayer en lugar de atacar).
        if (GetActiveCombatEnemy() != null && PlayerService.TryGetPlayer(out var playerGO))
        {
            Vector3 behind = playerGO.transform.position - playerGO.transform.forward * 1.5f;
            if (NavMesh.SamplePosition(behind, out NavMeshHit combatHit, 3f, NavMesh.AllAreas))
                spawnPos = combatHit.position;
            else
                spawnPos = playerGO.transform.position;
            spawnRot = playerGO.transform.rotation;
        }

        // Buscar posición válida en NavMesh lo más cerca posible de donde estaba el controller
        Vector3 pos = NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : spawnPos;

        var go = Instantiate(willNpcPrefab, pos, spawnRot);
        _willNpcInstance = go.GetComponent<NPCPartyMember>();
        _willNpcInstance?.SetRuntimeSpells(_willLeft, _willRight, _willSpecial);
        _willFollowCheckTimer = 0f;

        // FIX Will invisible al separar el equipo: a diferencia de Liam/Estela (NPCs que ya
        // llevan varios frames renderizándose desde que cargó la escena), el NPC de Will se
        // Instantiate() de cero aquí. Si en su primer frame la cámara está mirando al personaje
        // recién activado (no a Will, que puede quedar fuera de cuadro o incluso detrás),
        // Unity nunca llega a considerarlo "visible" una primera vez. Con el Animator en modo
        // Cull Update Transforms (el que usa este rig) y el SkinnedMeshRenderer con
        // updateWhenOffscreen=false (valor por defecto en el prefab), eso deja los bounds del
        // renderer sin inicializar correctamente y en un punto muerto: al no considerarse visible
        // nunca se actualizan animación/bounds, y al no actualizarse nunca pasa a considerarse
        // visible. El resultado es un Will "invisible pero presente" que no se cura solo — encaja
        // con que solo le pase a él y justo al dejarlo quieto en un puzle (nunca se mueve lo
        // suficiente para forzar un recálculo de bounds). Forzamos updateWhenOffscreen=true para
        // que el renderer recalcule bounds cada frame sin depender de haber sido visible antes.
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        // FIX Will invisible — CAUSA RAÍZ real (confirmada con log 2026-08-16 en MainWorld, ver
        // renderers activos=6/218 visibles=0 sostenido varios segundos SIN que el heal de
        // DiagnoseAndHealWillVisibility lo resolviera): MainWorld.unity es la única escena del
        // proyecto con Occlusion Culling horneado (Assets/Scenes/Worlds/MainWorld/
        // OcclusionCullingData.asset). Todos los fixes anteriores (updateWhenOffscreen, forzar
        // .bounds, el toggle de Renderer.enabled) atacan el FRUSTUM culling — un sistema
        // completamente distinto del Occlusion Culling, que decide visibilidad contra un mapa
        // horneado de antemano de qué zonas tapan a cuáles. El clon de Will se instancia en
        // cualquier posición runtime que el bake nunca vio; si esa celda queda marcada como
        // "tapada" por geometría cercana (una pared, un pilar), Unity lo da por no-visible de
        // forma legítima y PERMANENTE para ese sistema — ningún toggle de Renderer.enabled ni
        // recálculo de bounds lo revierte, porque no es a lo que esos fixes apuntan. Cada
        // Renderer nace con allowOcclusionWhenDynamic=true (m_DynamicOccludee=1 en el prefab):
        // aquí lo desactivamos para que el clon de Will nunca sea occlusion-culled dinámicamente,
        // ya que su posición no formó parte del bake y no debería probarse contra él.
        foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            rend.allowOcclusionWhenDynamic = false;

        // ✅ Aplicar la apariencia actual de Will al NPC instanciado
        if (CharacterAppearanceRegistry.Instance != null)
        {
            var willAppearance = CharacterAppearanceRegistry.Instance.GetAppearance(PartyControlManager.CharacterSlot.Will);
            var npcBuilder = go.GetComponentInChildren<ModularAutoBuilder>(true);
            if (npcBuilder == null)
            {
                Debug.LogWarning($"[ActiveCharacterSwapper] willNpcPrefab '{go.name}' sin ModularAutoBuilder — activando partes por nombre como fallback.");
                ActivateWillPartsByName(go, willAppearance);
            }
            else if (willAppearance != null)
            {
                npcBuilder.DeactivateAllCategories();
                npcBuilder.ApplySelection(willAppearance);
                Debug.Log($"[ActiveCharacterSwapper] SpawnWillNpc — apariencia aplicada al NPC ({willAppearance.Count} partes).");
            }
        }

        // FIX Will invisible (parte 2 — la de verdad): updateWhenOffscreen=true no basta si el
        // AABB de culling de cada SkinnedMeshRenderer se calculó UNA vez a partir del rootBone en
        // su pose inicial (antes de que el Animator corriera ni un solo frame, o antes de que
        // ModularAutoBuilder activara las partes correctas). Si ese primer cálculo cae fuera del
        // frustum de la cámara real, Unity lo marca "no visible" y ahí se queda: nada dispara un
        // recálculo mientras se considera no-visible, así que nunca vuelve a considerarse visible.
        // Confirmado en el editor: seleccionar el NPC en la Hierarchy (que fuerza a leer
        // Renderer.bounds para dibujar el gizmo) lo hacía "reaparecer" al instante en el Game View
        // — exactamente la firma de un AABB de culling nunca refrescado. Forzamos aquí lo mismo
        // que hace la selección del editor: una pasada de Animator ANTES del primer frame de
        // render (para que las bones ya estén en su pose/posición real) y una lectura de
        // Renderer.bounds por cada SkinnedMeshRenderer para forzar el recálculo del AABB ya con
        // la pose correcta, antes de que la cámara real haga su primer test de culling.
        var willAnimator = go.GetComponentInChildren<Animator>(true);
        if (willAnimator != null)
        {
            // FIX Will invisible — causa raíz del interbloqueo (2026-08-19, reaparición pese a
            // los tres fixes de arriba: log real con renderers activos=7/218 visibles=0 sostenido
            // varios segundos, y rendererBounds.center clavado en un punto a ~650 unidades de la
            // posición real de Will, sin moverse un ápice entre latidos de 2,5s del 🫀 de
            // DiagnoseAndHealWillVisibility). Este rig usa Animator.CullingMode.CullUpdateTransforms
            // (valor por defecto del componente, ver comentario de arriba): con ese modo, mientras
            // el renderer no se considera "visible" el Animator deja de actualizar los huesos, así
            // que el SkinnedMeshRenderer nunca recalcula sus bounds — y al no recalcular bounds
            // nunca pasa a considerarse visible. Es el mismo círculo que ya diagnosticaba el
            // comentario de arriba, pero willAnimator.Update(0f) solo fuerza UN frame puntual: si
            // ese único frame no basta para que Unity marque el renderer como visible antes del
            // siguiente test de culling, el interbloqueo se rearma de inmediato — de ahí que
            // updateWhenOffscreen + leer .bounds + el toggle de Renderer.enabled del heal
            // periódico (DiagnoseAndHealWillVisibility) no lo cerraran del todo. AlwaysAnimate
            // saca al Animator de ese modo por completo: los huesos (y por tanto los bounds) se
            // actualizan todos los frames pase lo que pase, así que no hay estado "nunca visible"
            // del que depender para salir del bucle. Coste real: 3-4 NPCs de equipo, no cientos —
            // irrelevante en el profiler. Pendiente de confirmar en Play Mode (no se puede abrir
            // el Editor desde esta sesión) que esto cierra el bug de verdad y no solo lo reduce.
            willAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            willAnimator.Update(0f);
        }
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.gameObject.activeInHierarchy)
                _ = smr.bounds;
        }

        // FIX INC-050: mismo problema de carrera que documenta SetNpcVisible() más abajo —
        // ModularAutoBuilder (o ActivateWillPartsByName como fallback) puede tocar los Renderer
        // hijos un frame después de aplicarse, dejando al Will NPC recién instanciado invisible.
        // Ahí sí se reafirmaba la visibilidad para Liam/Estela (ReassertVisibilityNextFrames),
        // pero no para Will, así que al cambiar de personaje Will podía "desaparecer".
        if (_willNpcInstance != null)
            StartCoroutine(ReassertWillVisibilityNextFrames(_willNpcInstance));

        // Aplicar modo de seguimiento actual una vez que el NPC esté inicializado.
        // Capturar si había combate AL SPAWNEAR; el corrutina puede correr frames después,
        // momento en que el registro ya puede haberse vaciado por timing.
        bool hadCombatEnemyAtSpawn = GetActiveCombatEnemy() != null;
        if (_willNpcInstance != null)
            StartCoroutine(ApplyFollowModeToWillNpc(hadCombatEnemyAtSpawn));
    }

    private System.Collections.IEnumerator ApplyFollowModeToWillNpc(bool spawnedDuringCombat = false)
    {
        // Esperar hasta que el Brain esté inicializado (con timeout de seguridad)
        float waited = 0f;
        while (_willNpcInstance != null && _willNpcInstance.NPCManager?.Brain == null)
        {
            waited += Time.deltaTime;
            if (waited > 3f) yield break;
            yield return null;
        }

        if (_willNpcInstance == null) yield break;

        _willFollowCheckTimer = 0f; // Reiniciar timer para no duplicar la primera verificación

        var enemy = GetActiveCombatEnemy();
        bool partyFollowing = PartyControlManager.Instance?.IsPartyFollowing ?? true;
        if (enemy != null)
        {
            _willNpcInstance.OnPlayerEnteredCombat(enemy);
        }
        else if (spawnedDuringCombat)
        {
            // Había combate al spawnear pero el registro quedó vacío durante la espera del Brain
            // (el enemigo perdió al jugador al teletransportarse o timing de registro).
            // Entrar en AllyCombatState con target nulo: el estado encontrará enemigos cercanos
            // vía FindNearestEnemy o saldrá por timeout si ya no quedan enemigos.
            _willNpcInstance.OnPlayerEnteredCombat(null);
        }
        else if (partyFollowing)
        {
            _willNpcInstance.StartFollowingIgnorePartyCheck();
        }
        else
        {
            // Modo libre: Will se queda anclado donde está
            _willNpcInstance.StopFollowing();
            if (_willNpcInstance.NPCManager?.Context != null)
                _willNpcInstance.NPCManager.Context.IsPinnedByParty = true;
        }
    }

    private void DestroyWillNpc()
    {
        if (_willNpcInstance == null) return;
        Destroy(_willNpcInstance.gameObject);
        _willNpcInstance = null;
    }

    private NPCPartyMember GetNpc(PartyControlManager.CharacterSlot slot) => slot switch
    {
        PartyControlManager.CharacterSlot.Liam   => PlayerParty.Instance?.GetMemberByName(liamDisplayName),
        PartyControlManager.CharacterSlot.Estela => PlayerParty.Instance?.GetMemberByName(estelaDisplayName),
        _                                         => null
    };

    private void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        if (!PlayerService.TryGetPlayer(out var playerGO)) return;

        var cc = playerGO.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerGO.transform.SetPositionAndRotation(position, rotation);

        if (cc != null) cc.enabled = true;
    }

    private void CaptureWillSpells()
    {
        if (magicCaster == null) return;
        _willLeft    = magicCaster.GetSpellForSlot(MagicSlot.Left);
        _willRight   = magicCaster.GetSpellForSlot(MagicSlot.Right);
        _willSpecial = magicCaster.GetSpellForSlot(MagicSlot.Special);
    }

    private void ApplySpells(PartyControlManager.CharacterSlot slot)
    {
        if (magicCaster == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ActiveCharacterSwapper] ApplySpells({slot}): magicCaster es NULL");
#endif
            return;
        }

        if (slot == PartyControlManager.CharacterSlot.Will)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActiveCharacterSwapper] ApplySpells(Will): L={_willLeft?.displayName} R={_willRight?.displayName} S={_willSpecial?.displayName}");
#endif
            magicCaster.SetSpells(_willLeft, _willRight, _willSpecial);
            return;
        }

        var npc = GetNpc(slot);
        var config = npc?.PartyConfig;
        if (config != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActiveCharacterSwapper] ApplySpells({slot}): L={config.GetSpell(0)?.displayName} R={config.GetSpell(1)?.displayName} S={config.GetSpell(2)?.displayName} — en magicCaster={magicCaster.name} (instanceID={magicCaster.GetEntityId()})");
#endif
            magicCaster.SetSpells(config.GetSpell(0), config.GetSpell(1), config.GetSpell(2));
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ActiveCharacterSwapper] ApplySpells({slot}): NPC={npc?.name ?? "null"}, config={config?.name ?? "null"} — hechizos no actualizados");
#endif
        }
    }

    private void SetNpcVisible(NPCPartyMember npc, bool visible)
    {
        if (npc == null) return;

        ApplyRendererVisibility(npc, visible);

        // Reafirmar un frame (y de nuevo un poco más tarde) después del swap. Algunos sistemas
        // (reconstrucción de apariencia con ModularAutoBuilder, spawners de partes, etc.) pueden
        // tocar los Renderer hijos justo después de este cambio y dejar al NPC invisible aunque
        // ya esté "visible" a efectos de lógica — el interactuable seguía funcionando (por eso se
        // veía la "A" sin el modelo) y el NPC solo reaparecía cuando algo más volvía a tocar sus
        // renderers (p.ej. al acercarse el jugador). Forzamos el estado correcto un par de veces
        // más para blindarnos de esa condición de carrera.
        if (visible)
            StartCoroutine(ReassertVisibilityNextFrames(npc));
        else
            StartCoroutine(ReassertHiddenNextFrames(npc));

        // Cuando el NPC está oculto, desactivar sus colliders para que los proyectiles enemigos
        // no choquen físicamente con el NPC y puedan alcanzar el CharacterController del jugador.
        foreach (var col in npc.GetComponentsInChildren<Collider>(true))
            col.enabled = visible;

        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (visible)
            {
                agent.isStopped = false;
                var enemy = GetActiveCombatEnemy();
                bool partyFollowing = PartyControlManager.Instance?.IsPartyFollowing ?? true;
                if (enemy != null)
                    npc.OnPlayerEnteredCombat(enemy);
                else if (partyFollowing)
                    npc.StartFollowingIgnorePartyCheck();
                else
                {
                    // Modo libre: el NPC se queda anclado donde fue posicionado (p. ej., sobre un botón)
                    npc.StopFollowing();
                    if (npc.NPCManager?.Context != null)
                        npc.NPCManager.Context.IsPinnedByParty = true;
                }
            }
            else
            {
                agent.ResetPath();
                agent.isStopped = true;
                // AllyCombatState.OnUpdate llama a agent.isStopped = false en cada frame
                // de movimiento y sigue disparando aunque el NPC sea invisible. Salir del
                // estado de combate aquí evita el "atacante invisible".
                var npcMgr = npc.NPCManager;
                if (npcMgr != null && npcMgr.Context.IsInCombat)
                {
                    npcMgr.ExitCombat();
                    npcMgr.ForceIdle();
                }
            }
        }
    }

    private static void ApplyRendererVisibility(NPCPartyMember npc, bool visible)
    {
        if (npc == null) return;
        foreach (var r in npc.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
            // Mismo FIX de causa raíz que SpawnWillNpc (ver comentario allí): Liam/Estela también
            // son NPCs dinámicos que se muestran/ocultan en cualquier posición de MainWorld, la
            // única escena con Occlusion Culling horneado — sin este flag pueden sufrir el mismo
            // "invisible pero presente" que Will al revelarse cerca de geometría no contemplada
            // en el bake.
            r.allowOcclusionWhenDynamic = false;
        }
    }

    private System.Collections.IEnumerator ReassertVisibilityNextFrames(NPCPartyMember npc)
    {
        yield return null;
        // Si mientras tanto el jugador volvió a cambiar de personaje y este NPC pasó a ser
        // el controlado (oculto), no lo reactivemos: respetar el estado más reciente.
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);

        yield return new WaitForSeconds(0.25f);
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);

        // Tercer pase más allá de los 0.25s originales: en frames con carga pesada (streaming,
        // ModularAutoBuilder reconstruyendo partes, etc.) ese margen no siempre alcanzaba y el
        // NPC quedaba invisible de forma intermitente ("solo a veces"). Cubrimos una ventana más
        // amplia sin pasar a reafirmar por frame indefinidamente (evita pisar ocultamientos
        // legítimos de sistemas como DialogueCinematicController más allá de este margen corto).
        yield return new WaitForSeconds(0.5f);
        if (npc == null || npc == _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, true);
    }

    /// <summary>
    /// FIX "dos Estelas y un Will" (ver ResetState() y EnsureHiddenNpcSuppressed): simétrico de
    /// ReassertVisibilityNextFrames pero para OCULTAR. SetNpcVisible(npc, false) apaga los
    /// renderers al cambiar de personaje, pero si algo los reactiva un frame después
    /// (ModularAutoBuilder reconstruyendo apariencia, OnJoinedParty, el chequeo periódico de
    /// NPCPartyMember.Update()...) el NPC "oculto" reaparecía junto al controller, que ya luce esa
    /// misma apariencia — se ven dos copias del mismo personaje a la vez. Antes solo el camino de
    /// REVELAR tenía esta protección; este es el mismo patrón de reintentos (frame siguiente,
    /// +0.25s, +0.75s) para el camino de OCULTAR. Aborta si mientras tanto el NPC dejó de ser el
    /// oculto actual (respeta el estado más reciente, igual que ReassertVisibilityNextFrames
    /// respeta si volvió a ser el oculto).
    /// </summary>
    private System.Collections.IEnumerator ReassertHiddenNextFrames(NPCPartyMember npc)
    {
        yield return null;
        if (npc == null || npc != _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, false);

        yield return new WaitForSeconds(0.25f);
        if (npc == null || npc != _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, false);

        yield return new WaitForSeconds(0.5f);
        if (npc == null || npc != _hiddenNpc) yield break;
        ApplyRendererVisibility(npc, false);
    }

    /// <summary>
    /// FIX INC-050: reafirma la visibilidad del Will NPC recién spawneado, igual que
    /// ReassertVisibilityNextFrames hace para Liam/Estela. Necesario porque ModularAutoBuilder
    /// (o su fallback ActivateWillPartsByName) puede tocar los Renderer hijos un frame después
    /// de SpawnWillNpc(), dejando a Will invisible tras cambiar de personaje.
    /// </summary>
    private System.Collections.IEnumerator ReassertWillVisibilityNextFrames(NPCPartyMember willNpc)
    {
        yield return null;
        if (willNpc == null || willNpc != _willNpcInstance) yield break;
        ApplyRendererVisibility(willNpc, true);

        yield return new WaitForSeconds(0.25f);
        if (willNpc == null || willNpc != _willNpcInstance) yield break;
        ApplyRendererVisibility(willNpc, true);

        // Tercer pase (ver mismo comentario en ReassertVisibilityNextFrames): amplía la ventana
        // de protección más allá de los 0.25s originales. A partir de aquí, EnsureWillNpcVisible()
        // en Update() (cada 0.5s mientras _willNpcInstance exista) actúa como red de seguridad
        // continua, así que no hace falta seguir encadenando pases aquí.
        yield return new WaitForSeconds(0.5f);
        if (willNpc == null || willNpc != _willNpcInstance) yield break;
        ApplyRendererVisibility(willNpc, true);
    }

    /// Fallback para cuando el willNpcPrefab no tiene ModularAutoBuilder:
    /// activa los GOs cuyos nombres coincidan con las partes de la apariencia de Will.
    /// Desactiva todos los demás GOs con Renderer para evitar mezclas visuales.
    private void ActivateWillPartsByName(GameObject npcRoot, System.Collections.Generic.Dictionary<PartCategory, string> willAppearance)
    {
        if (willAppearance == null || willAppearance.Count == 0) return;

        var partNames = new System.Collections.Generic.HashSet<string>(willAppearance.Values);
        int activated = 0;

        foreach (var t in npcRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject == npcRoot) continue;
            var hasRenderer = t.GetComponent<Renderer>() != null;
            if (!hasRenderer) continue;

            bool shouldBeActive = partNames.Contains(t.gameObject.name);
            t.gameObject.SetActive(shouldBeActive);
            if (shouldBeActive) activated++;
        }

        Debug.Log($"[ActiveCharacterSwapper] ActivateWillPartsByName — {activated}/{partNames.Count} partes activadas en '{npcRoot.name}'.");
    }

    private void WarpNpcToPosition(NPCPartyMember npc, Vector3 pos, Quaternion rot)
    {
        if (npc == null) return;
        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.Warp(pos);
        else
            npc.transform.position = pos;
        npc.transform.rotation = rot;

        // FIX Will invisible — mismo AABB de culling atascado que documentan SpawnWillNpc() y
        // EnsureWillNpcVisible() más arriba, pero este call site se quedó sin el fix (gap conocido:
        // Will podía quedar invisible tras un warp cerca de un puzle). Un teleport brusco puede
        // dejar el bounds de culling calculado con la posición ANTERIOR hasta que algo lo refresque.
        var anim = npc.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            // FIX Will invisible — mismo AlwaysAnimate que SpawnWillNpc() (ver comentario
            // detallado allí): este call site reposiciona a Will (cambio Liam↔Estela con Will de
            // fondo) y también a Liam/Estela residentes en escena que nunca pasaron por
            // SpawnWillNpc(), así que necesitan el mismo fix de raíz contra el interbloqueo de
            // CullUpdateTransforms, no solo el Update(0f) puntual que ya había aquí.
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Update(0f);
        }
        foreach (var smr in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.updateWhenOffscreen) smr.updateWhenOffscreen = true;
            if (smr.gameObject.activeInHierarchy) _ = smr.bounds;
        }

        // FIX (16/08/2026): "Will desaparece cerca de un punto de guardado al cambiar de personaje".
        // El fix de Occlusion Culling horneado (ver SpawnWillNpc(), líneas de arriba —
        // allowOcclusionWhenDynamic=false) SOLO se aplicaba al instanciar el NPC de Will desde
        // cero. Este método (WarpNpcToPosition) también reposiciona a Will cuando ya estaba
        // instanciado (cambio Liam↔Estela con Will de fondo, línea ~488) Y reposiciona al NPC de
        // Liam/Estela que se abandona (línea ~423) — un NPC residente en escena desde el arranque
        // que JAMÁS pasó por SpawnWillNpc() y por tanto nunca recibió este fix. Los puntos de
        // guardado suelen estar en hornacinas con pilares/paredes para enmarcarlos visualmente —
        // justo el tipo de geometría que el bake de Occlusion Culling marca como "tapada". Sin
        // este flag, el NPC puede caer en una celda oculta del bake nada más llegar ahí y
        // quedarse invisible de forma permanente para ese sistema, sin que ningún recálculo de
        // bounds (arriba) lo revierta — esa parte solo cubre el frustum culling, un sistema
        // distinto.
        foreach (var rend in npc.GetComponentsInChildren<Renderer>(true))
            rend.allowOcclusionWhenDynamic = false;
    }

    /// <summary>
    /// Devuelve el Transform del primer enemigo activo en combate (excluye compañeros de party).
    /// </summary>
    private Transform GetActiveCombatEnemy()
    {
        foreach (var go in ActiveCombatRegistry.GetAllInCombat())
        {
            if (go == null) continue;
            if (go.GetComponent<NPCPartyMember>() != null) continue;
            return go.transform;
        }
        return null;
    }
    #endregion
}
