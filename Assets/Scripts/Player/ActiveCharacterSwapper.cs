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

    // Hechizos de Will, actualizados cada vez que se abandona su slot
    private MagicSpellSO _willLeft, _willRight, _willSpecial;

    // NPC actualmente oculto porque el controller lo está representando
    private NPCPartyMember _hiddenNpc;

    private bool _ready;

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
    }

    private void OnDestroy()
    {
        PartyControlManager.OnFollowModeChanged -= OnFollowModeChanged;
        if (Instance == this) Instance = null;
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
        SetNpcVisible(_hiddenNpc, true);
        _hiddenNpc = toNpc;
        SetNpcVisible(_hiddenNpc, false);

        // 6. NPC de Will: instanciar al alejarse de Will, destruir al volver
        bool willIsActive = to == PartyControlManager.CharacterSlot.Will;
        if (willIsActive)
        {
            DestroyWillNpc();
        }
        else if (_willNpcInstance == null)
        {
            SpawnWillNpc();
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
    #endregion

    #region Internals
    private void OnFollowModeChanged(bool isFollowing)
    {
        if (_willNpcInstance == null) return;

        if (isFollowing)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
        else
            _willNpcInstance.StopFollowing();
    }

    private void SpawnWillNpc()
    {
        if (willNpcPrefab == null) return;

        PlayerService.TryGetPlayer(out var playerGO);

        // Calcular posición detrás del jugador para que Will no aparezca encima del personaje activo
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (playerGO != null)
        {
            rot = playerGO.transform.rotation;
            Vector3 behind = playerGO.transform.position - playerGO.transform.forward * 1.5f;
            pos = NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : playerGO.transform.position;
        }

        var go = Instantiate(willNpcPrefab, pos, rot);
        _willNpcInstance = go.GetComponent<NPCPartyMember>();
        _willNpcInstance?.SetRuntimeSpells(_willLeft, _willRight, _willSpecial);

        // Aplicar modo de seguimiento actual una vez que el NPC esté inicializado
        if (_willNpcInstance != null)
            StartCoroutine(ApplyFollowModeToWillNpc());
    }

    private System.Collections.IEnumerator ApplyFollowModeToWillNpc()
    {
        yield return null; // Esperar un frame para que el Brain esté listo
        if (_willNpcInstance != null && PartyControlManager.Instance?.IsPartyFollowing == true)
            _willNpcInstance.StartFollowingIgnorePartyCheck();
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
        if (magicCaster == null) return;

        if (slot == PartyControlManager.CharacterSlot.Will)
        {
            magicCaster.SetSpells(_willLeft, _willRight, _willSpecial);
            return;
        }

        var config = GetNpc(slot)?.PartyConfig;
        if (config != null)
            magicCaster.SetSpells(config.GetSpell(0), config.GetSpell(1), config.GetSpell(2));
    }

    private void SetNpcVisible(NPCPartyMember npc, bool visible)
    {
        if (npc == null) return;

        foreach (var r in npc.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        var agent = npc.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (visible)
            {
                agent.isStopped = false;
                npc.StartFollowing();
            }
            else
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }
    }
    #endregion
}
