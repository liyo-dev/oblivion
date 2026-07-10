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
                ?? UnityEngine.Object.FindFirstObjectByType<MagicCaster>();

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

        var brain = _willNpcInstance.NPCManager?.Brain;
        if (brain == null) return;
        if (_willNpcInstance.NPCManager.Context.IsInCombat || _willNpcInstance.NPCManager.Context.IsInCinematic) return;
        if (!(PartyControlManager.Instance?.IsPartyFollowing ?? true)) return;

        // Si cayó en Idle y NO está anclado, reiniciar seguimiento
        if (brain.CurrentState?.StateName == "Idle"
            && !(_willNpcInstance.NPCManager?.Context?.IsPinnedByParty ?? false))
            _willNpcInstance.StartFollowingIgnorePartyCheck();
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
            bool hasNonHiddenMember = false;
            if (PlayerParty.HasInstance)
            {
                foreach (var m in PlayerParty.Instance.Members)
                {
                    if (m != null && m != _hiddenNpc) { hasNonHiddenMember = true; break; }
                }
            }
            if (hasNonHiddenMember)
            {
                if (_willNpcInstance.NPCManager?.Context != null)
                    _willNpcInstance.NPCManager.Context.IsPinnedByParty = false;
                _willNpcInstance.StartFollowingIgnorePartyCheck();
            }
            // Si no hay compañeros activos, Will se queda anclado aunque se active el modo seguir
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

        // Aplicar modo de seguimiento actual una vez que el NPC esté inicializado
        if (_willNpcInstance != null)
            StartCoroutine(ApplyFollowModeToWillNpc());
    }

    private System.Collections.IEnumerator ApplyFollowModeToWillNpc()
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
        // Will sigue solo si hay compañeros activos distintos al NPC oculto (personaje activo).
        // Si el único miembro del party es el propio personaje que el jugador controla, Will se ancla.
        bool hasNonHiddenMember = false;
        if (PlayerParty.HasInstance)
        {
            foreach (var m in PlayerParty.Instance.Members)
            {
                if (m != null && m != _hiddenNpc) { hasNonHiddenMember = true; break; }
            }
        }
        if (enemy != null)
            _willNpcInstance.OnPlayerEnteredCombat(enemy);
        else if (partyFollowing && hasNonHiddenMember)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
        else
        {
            // Modo libre, equipo disuelto o jugando en solitario: Will se queda anclado
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
            Debug.Log($"[ActiveCharacterSwapper] ApplySpells({slot}): L={config.GetSpell(0)?.displayName} R={config.GetSpell(1)?.displayName} S={config.GetSpell(2)?.displayName} — en magicCaster={magicCaster.name} (instanceID={magicCaster.GetInstanceID()})");
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

        foreach (var r in npc.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

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
