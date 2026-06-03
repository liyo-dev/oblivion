using System;
using UnityEngine;
using Game.NPC;

/// <summary>
/// Gestor de estadísticas por personaje del equipo (HP y MP).
/// Al cambiar el personaje activo guarda las stats del saliente y carga las del entrante
/// en PlayerHealthSystem y ManaPool, actualizando la UI automáticamente.
/// Además gestiona la muerte en batalla de los miembros NPC y su resurrección al fin de batalla.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PartyStatsManager : MonoBehaviour
{
    public static PartyStatsManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnMemberDied = null;
        OnMemberRevived = null;
    }
#endif

    // ── Configuración ──────────────────────────────────────────────────────────
    [Header("Nombres de personaje (deben coincidir con NPCPartyConfig.displayName)")]
    [SerializeField] private string liamDisplayName  = "Liam";
    [SerializeField] private string estelaDisplayName = "Estela";

    // ── Datos internos ─────────────────────────────────────────────────────────
    private struct SlotStats
    {
        public float maxHP, currentHP, maxMP, currentMP;
        public bool  isDead;
    }

    private readonly SlotStats[]      _stats          = new SlotStats[3];
    private readonly NPCPartyMember[] _npcMembers     = new NPCPartyMember[3];
    private readonly Damageable[]     _npcDamageables = new Damageable[3];

    // Delegados almacenados para poder desuscribirse correctamente
    private readonly Action<float>[] _onDamagedHandlers = new Action<float>[3];
    private readonly Action[]        _onDiedHandlers    = new Action[3];

    private PlayerHealthSystem _playerHealth;
    private ManaPool           _manaPool;
    private int                _activeSlot = (int)PartyControlManager.CharacterSlot.Will;

    // ── Eventos públicos ────────────────────────────────────────────────────────
    public static event Action<int> OnMemberDied;    // slot (0=Liam, 1=Will, 2=Estela)
    public static event Action<int> OnMemberRevived; // slot

    // ── API pública ─────────────────────────────────────────────────────────────
    public bool IsDead(int slot)                                    => slot >= 0 && slot < 3 && _stats[slot].isDead;
    public bool IsSlotDead(PartyControlManager.CharacterSlot slot)  => IsDead((int)slot);

    // ── Ciclo de vida ───────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Liam y Estela: se inicializan en HandleMemberJoined desde NPCPartyConfig
        // Will: se inicializa en Start desde PlayerHealthSystem
    }

    private void Start()
    {
        _playerHealth = ServiceLocator.Get<PlayerHealthSystem>(false);
        _manaPool     = ServiceLocator.Get<ManaPool>(false);

        // Sincronizar Will si el sistema ya está inicializado
        if (_playerHealth != null && _playerHealth.MaxHealth > 0f)
            SyncWillFromSystems();

        GameBootService.OnProfileReady                += SyncWillFromSystems;
        PartyControlManager.OnActiveCharacterChanged  += HandleActiveCharacterChanged;
        BossArenaController.OnAnyBattleEnded          += HandleBattleEnded;
        ActiveCombatRegistry.OnNPCExitedCombat        += HandleEnemyExitedCombat;
        PlayerParty.OnMemberJoined                    += HandleMemberJoined;
        PlayerParty.OnMemberLeft                      += HandleMemberLeft;

        // Inicializar miembros que ya estaban en el party antes de suscribirnos
        if (PlayerParty.Instance != null)
        {
            foreach (var member in PlayerParty.Instance.Members)
                HandleMemberJoined(member);
        }

        if (_playerHealth != null)
            _playerHealth.OnDied += HandleActiveCharacterDied;
    }

    private void OnDestroy()
    {
        GameBootService.OnProfileReady                -= SyncWillFromSystems;
        PartyControlManager.OnActiveCharacterChanged  -= HandleActiveCharacterChanged;
        BossArenaController.OnAnyBattleEnded          -= HandleBattleEnded;
        ActiveCombatRegistry.OnNPCExitedCombat        -= HandleEnemyExitedCombat;
        PlayerParty.OnMemberJoined                    -= HandleMemberJoined;
        PlayerParty.OnMemberLeft                      -= HandleMemberLeft;

        if (_playerHealth != null)
            _playerHealth.OnDied -= HandleActiveCharacterDied;

        for (int i = 0; i < 3; i++)
            UnsubscribeNpc(i);

        if (Instance == this) Instance = null;
    }

    // ── API para ActiveCharacterSwapper ────────────────────────────────────────

    /// <summary>Llámalo ANTES del cambio visual. Guarda HP/MP del personaje saliente.</summary>
    public void SaveCurrentStats(int fromSlot)
    {
        if (_playerHealth == null) return;
        ref var s = ref _stats[fromSlot];
        s.maxHP     = _playerHealth.MaxHealth;
        s.currentHP = _playerHealth.CurrentHealth;
        if (_manaPool != null)
        {
            s.maxMP     = _manaPool.Max;
            s.currentMP = _manaPool.Current;
        }
    }

    /// <summary>
    /// Sincroniza el Damageable del NPC saliente con las stats guardadas.
    /// Llámalo tras SaveCurrentStats, antes de hacer visible al NPC.
    /// </summary>
    public void SyncNpcDamageable(int fromSlot)
    {
        var damageable = _npcDamageables[fromSlot];
        if (damageable == null) return;

        var s = _stats[fromSlot];
        if (s.isDead)
            damageable.Kill();                           // Dispara OnDied → EnterDeathState
        else
            damageable.SetMaxAndCurrent(s.maxHP, s.currentHP);
    }

    /// <summary>Llámalo DESPUÉS del cambio visual. Carga HP/MP del personaje entrante en PlayerHealthSystem/ManaPool.</summary>
    public void LoadStats(int toSlot)
    {
        _activeSlot = toSlot;
        var s = _stats[toSlot];

        // Solo Will causa game over al morir; Liam y Estela hacen switch al morir
        if (_playerHealth != null)
            _playerHealth.IsPartyCharacterMode = (toSlot != (int)PartyControlManager.CharacterSlot.Will);

        _playerHealth?.InitStats(s.maxHP, s.currentHP);
        _manaPool?.Init(s.maxMP, s.currentMP);
    }

    // ── Handlers de eventos ─────────────────────────────────────────────────────

    private void HandleEnemyExitedCombat(GameObject _)
    {
        // Si ya no quedan enemigos y hay miembros muertos, resucitarlos
        if (ActiveCombatRegistry.Count == 0 && HasDeadMembers())
            HandleBattleEnded();
    }

    private bool HasDeadMembers()
    {
        for (int i = 0; i < 3; i++)
            if (_stats[i].isDead) return true;
        return false;
    }

    private void SyncWillFromSystems()
    {
        if (_playerHealth == null || _playerHealth.MaxHealth <= 0f) return;
        int will = (int)PartyControlManager.CharacterSlot.Will;
        ref var s = ref _stats[will];
        s.maxHP     = _playerHealth.MaxHealth;
        s.currentHP = _playerHealth.CurrentHealth;
        if (_manaPool != null)
        {
            s.maxMP     = _manaPool.Max;
            s.currentMP = _manaPool.Current;
        }
    }

    private void HandleActiveCharacterChanged(int newSlot)
    {
        _activeSlot = newSlot;
    }

    private void HandleActiveCharacterDied()
    {
        if (_stats[_activeSlot].isDead) return;

        _stats[_activeSlot].isDead    = true;
        _stats[_activeSlot].currentHP = 0f;
        OnMemberDied?.Invoke(_activeSlot);

        // Si es Will, el game over ya se activó en PlayerHealthSystem
        int willSlot = (int)PartyControlManager.CharacterSlot.Will;
        if (_activeSlot == willSlot) return;

        // Cambiar a Will si está vivo
        if (!_stats[willSlot].isDead)
            PartyControlManager.Instance?.ForceSwitch(PartyControlManager.CharacterSlot.Will);
    }

    private void HandleMemberJoined(NPCPartyMember member)
    {
        int slot = SlotForMember(member);
        if (slot < 0) return;

        UnsubscribeNpc(slot);
        _npcMembers[slot]     = member;
        var damageable        = member.GetComponent<Damageable>();
        _npcDamageables[slot] = damageable;

        // Fuente de verdad: NPCPartyConfig
        var cfg = member.PartyConfig;
        if (cfg != null)
        {
            _stats[slot].maxHP     = cfg.maxHP;
            _stats[slot].currentHP = cfg.maxHP;
            _stats[slot].maxMP     = cfg.maxMP;
            _stats[slot].currentMP = cfg.maxMP;
            _stats[slot].isDead    = false;

            // Sincronizar el Damageable con las stats configuradas
            if (damageable != null && slot != _activeSlot)
                damageable.SetMaxAndCurrent(cfg.maxHP, cfg.maxHP);
        }

        if (damageable != null)
        {
            damageable.SetDestroyOnDeath(false);

            _onDamagedHandlers[slot] = _ => OnNpcDamaged(slot);
            _onDiedHandlers[slot]    = () => OnNpcDied(slot);
            damageable.OnDamaged += _onDamagedHandlers[slot];
            damageable.OnDied    += _onDiedHandlers[slot];
        }
    }

    private void HandleMemberLeft(NPCPartyMember member)
    {
        int slot = SlotForMember(member);
        if (slot < 0) return;
        UnsubscribeNpc(slot);
        _npcMembers[slot]     = null;
        _npcDamageables[slot] = null;
    }

    private void OnNpcDamaged(int slot)
    {
        if (slot == _activeSlot) return; // El activo se gestiona vía PlayerHealthSystem
        var d = _npcDamageables[slot];
        if (d != null) _stats[slot].currentHP = d.Current;
    }

    private void OnNpcDied(int slot)
    {
        if (_stats[slot].isDead) return; // Evitar doble procesamiento

        _stats[slot].currentHP = 0f;
        _stats[slot].isDead    = true;

        _npcMembers[slot]?.EnterDeathState();
        OnMemberDied?.Invoke(slot);
    }

    private void HandleBattleEnded()
    {
        for (int slot = 0; slot < 3; slot++)
        {
            if (!_stats[slot].isDead) continue;

            _stats[slot].isDead    = false;
            _stats[slot].currentHP = 1f;

            if (slot == _activeSlot)
            {
                // Resucitar al personaje activo en PlayerHealthSystem
                _playerHealth?.InitStats(_stats[slot].maxHP, 1f);
            }
            else
            {
                // Resucitar NPC
                _npcDamageables[slot]?.Revive(1f);
                _npcMembers[slot]?.ExitDeathState();
            }

            OnMemberRevived?.Invoke(slot);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PartyStatsManager] Slot {slot} resucitado con 1 PS al fin de batalla.");
#endif
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private int SlotForMember(NPCPartyMember member)
    {
        if (member == null) return -1;
        var name = member.DisplayName;
        if (name == liamDisplayName)   return (int)PartyControlManager.CharacterSlot.Liam;
        if (name == estelaDisplayName) return (int)PartyControlManager.CharacterSlot.Estela;
        return -1;
    }

    private void UnsubscribeNpc(int slot)
    {
        var d = _npcDamageables[slot];
        if (d == null) return;
        if (_onDamagedHandlers[slot] != null) { d.OnDamaged -= _onDamagedHandlers[slot]; _onDamagedHandlers[slot] = null; }
        if (_onDiedHandlers[slot]    != null) { d.OnDied    -= _onDiedHandlers[slot];    _onDiedHandlers[slot]    = null; }
    }
}
