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
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (hadDisabled)
            Debug.LogWarning("[ActiveCharacterSwapper] Will NPC tenía renderers desactivados fuera de la ventana de ReassertWillVisibilityNextFrames — reactivados por la red de seguridad periódica (0.5s).");
#endif
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
            r.enabled = visible;
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
