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
/// El WillNpcProxy (opcional) es un GameObject con apariencia de Will que aparece
/// cuando Will no es el líder activo.
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

    [Header("NPCs del equipo")]
    [SerializeField] private NPCPartyMember liamNpc;
    [SerializeField] private NPCPartyMember estelaaNpc;

    [Header("Proxy de Will como NPC (opcional)")]
    [Tooltip("GameObject con aspecto de Will que aparece cuando Will no es el líder.")]
    [SerializeField] private GameObject willNpcProxy;

    // Hechizos originales de Will, capturados en Start()
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

        // Snapshot de los hechizos iniciales de Will para poder restaurarlos
        if (magicCaster != null)
        {
            _willLeft    = magicCaster.GetSpellForSlot(MagicSlot.Left);
            _willRight   = magicCaster.GetSpellForSlot(MagicSlot.Right);
            _willSpecial = magicCaster.GetSpellForSlot(MagicSlot.Special);
        }

        if (willNpcProxy != null)
            willNpcProxy.SetActive(false);

        _ready = true;
    }

    private void OnDestroy()
    {
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

        // 1. Guardar cambios de vestuario del personaje que se abandona
        registry?.CaptureCurrentAppearance(from);

        // 2. Teleportar el controller a la posición del NPC objetivo (solo al ir a Liam/Estela)
        var toNpc = GetNpc(to);
        if (to != PartyControlManager.CharacterSlot.Will && toNpc != null)
            TeleportPlayer(toNpc.transform.position, toNpc.transform.rotation);

        // 3. Cambiar apariencia visual
        registry?.ApplyAppearance(to);

        // 4. Cambiar hechizos del player
        ApplySpells(to);

        // 5. Gestionar visibilidad de NPCs
        // Reactiva al NPC anterior (dejamos de ser ese personaje)
        SetNpcVisible(_hiddenNpc, true);

        // Oculta al NPC objetivo (el controller ahora ES ese personaje)
        _hiddenNpc = toNpc;
        SetNpcVisible(_hiddenNpc, false);

        // 6. Proxy de Will
        if (willNpcProxy != null)
            willNpcProxy.SetActive(to != PartyControlManager.CharacterSlot.Will);
    }

    /// <summary>
    /// Devuelve el NPC actualmente oculto (el que el controller representa).
    /// Útil para que PartyControlManager lo excluya del modo Libre/Siguiendo.
    /// </summary>
    public NPCPartyMember HiddenNpc => _hiddenNpc;
    #endregion

    #region Internals
    private NPCPartyMember GetNpc(PartyControlManager.CharacterSlot slot) => slot switch
    {
        PartyControlManager.CharacterSlot.Liam   => liamNpc,
        PartyControlManager.CharacterSlot.Estela => estelaaNpc,
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
