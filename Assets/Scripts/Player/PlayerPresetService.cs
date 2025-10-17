using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerPresetService : MonoBehaviour
{
    [Header("Librería de hechizos (ID → SO)")]
    [SerializeField] private SpellLibrarySO spellLibrary;

    [Header("Opciones")]
    [SerializeField] private bool autoFillEmptySlotsFromUnlocked; // default false por defecto
    [SerializeField] private GameObject instigatorOverride;

    MagicProjectileSpawner _spawner;
    MagicCaster _magicCaster; // ← NUEVO: referencia al MagicCaster
    ManaPool _manaPool;       // ← NUEVO: referencia al ManaPool
    PlayerActionManager _actionManager; // ← NUEVO: referencia al PlayerActionManager
    
    // Evitar inicialización doble si el evento llega más de una vez o ya está listo al habilitar
    bool _initialized;

    void Awake()
    {
        // Buscar spawner y magicCaster también en los padres del GameObject.
        // Esto evita crear un MagicProjectileSpawner LOCAL cuando ya existe uno en el root del jugador.
        _spawner = GetComponent<MagicProjectileSpawner>() ?? GetComponentInParent<MagicProjectileSpawner>();
        if (_spawner == null)
        {
            // Como último recurso, añadimos uno local (solo si realmente no existe en la jerarquía)
            _spawner = gameObject.AddComponent<MagicProjectileSpawner>();
        }

        _magicCaster = GetComponent<MagicCaster>() ?? GetComponentInParent<MagicCaster>();
        _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>(); // ← NUEVO: obtener ManaPool
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>(); // ← NUEVO: obtener PlayerActionManager
    }

    // Suscribirnos al evento y cubrir el caso de que ya esté disponible
    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        InitializePresetService();
        _initialized = true;
        // Ya no necesitamos el evento tras inicializar
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void InitializePresetService()
    {
        var profile = GameBootService.Profile;
        
        if (!profile)
        {
            enabled = false;
            Debug.LogError("[PlayerPresetService] Falta GameBootProfile.");
            return;
        }
        if (!spellLibrary)
        {
            Debug.LogWarning("[PlayerPresetService] SpellLibrary no asignada en el inspector — algunas funcionalidades (resolución de SpellId) no estarán disponibles.");
        }

        // Log diagnóstico: comprobar qué componentes hemos encontrado para evitar configuraciones en el objeto equivocado
        Debug.Log($"[PlayerPresetService] Componentes encontrados -> spawner={( _spawner != null ? _spawner.GetType().Name : "null" )}, magicCaster={( _magicCaster != null ? _magicCaster.GetType().Name : "null" )}, manaPool={( _manaPool != null ? _manaPool.GetType().Name : "null" )}, actionManager={( _actionManager != null ? _actionManager.GetType().Name : "null" )}");

        var preset = profile.GetActivePresetResolved();
        if (!preset) 
        { 
            Debug.LogWarning("[PlayerPresetService] Sin preset activo."); 
            return; 
        }

        // Log inicial de diagnóstico
        var entriesCount = spellLibrary.Entries != null ? spellLibrary.Entries.Count : -1;
        int unlockedCount = preset.unlockedSpells != null ? preset.unlockedSpells.Count : 0;
        Debug.Log($"[PlayerPresetService] SpellLibrary entries: {entriesCount} | Unlocked: {unlockedCount} | Slots IDs → L:{preset.leftSpellId} R:{preset.rightSpellId} S:{preset.specialSpellId}");

        // USAR EL ANCHOR DEL PRESET SO
        if (!string.IsNullOrEmpty(preset.spawnAnchorId))
        {
            SpawnManager.SetCurrentAnchor(preset.spawnAnchorId);
        }

        // Sincronizar maná del preset → ManaPool (evita arrancar 50/50 si el preset está en 0/0)
        ApplyManaFromPreset(preset);

        // Configurar hechizos del preset
        ConfigureSpells(preset);

        // === NUEVO: Aplicar abilities del preset al PlayerActionManager si existe ===
        if (_actionManager == null)
            _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();

        if (_actionManager != null && preset != null)
        {
            _actionManager.ApplyAbilities(preset.abilities);
            Debug.Log("[PlayerPresetService] Abilities del preset aplicadas al PlayerActionManager");
            Debug.Log($"[PlayerPresetService] PlayerActionManager.AllowMagic = {_actionManager.AllowMagic}");
        }
        else if (_actionManager == null)
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró PlayerActionManager para aplicar abilities del preset");
        }

        // Notificar a subscriptores (HUD, UI u otros) que el preset ha sido aplicado
        OnPresetApplied?.Invoke();
    }

    // === NUEVO: Aplica valores de maná desde el preset al ManaPool del jugador ===
    private void ApplyManaFromPreset(PlayerPresetSO preset)
    {
        if (!preset) return;
        if (_manaPool == null)
        {
            // Intentar obtener el ManaPool del jugador (preferente) para evitar apuntar a otras instancias en escena
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _manaPool = playerGO.GetComponentInChildren<ManaPool>() ?? playerGO.GetComponent<ManaPool>();
            }
            // Si no se encontró en el Player, seguir con la lógica previa
            if (_manaPool == null)
            {
                _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>();
            }
#if UNITY_2022_3_OR_NEWER
            if (_manaPool == null) _manaPool = FindFirstObjectByType<ManaPool>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            if (_manaPool == null) _manaPool = FindObjectOfType<ManaPool>(true);
#pragma warning restore 618
#endif
        }
        if (_manaPool != null)
        {
            Debug.Log($"[PlayerPresetService] Using ManaPool on GameObject: {_manaPool.gameObject.name}");
        }
        if (_manaPool != null)
        {
            // Si el preset NO tiene la ability de magia, no queremos mostrar una barra de maná llena.
            if (preset.abilities != null && !preset.abilities.magic)
            {
                _manaPool.Init(0f, 0f);
                Debug.Log("[PlayerPresetService] Preset indica que no tiene magia -> maxMP y currentMP seteados a 0");
                return;
            }

            // Si el preset explícitamente no define maná, intentar inferir un valor mínimo a partir
            // de los hechizos asignados o desbloqueados para evitar que el jugador tenga 0 MP y no pueda lanzar.
            float maxMP = Mathf.Max(0f, preset.maxMP);
            float currentMP = Mathf.Clamp(preset.currentMP, 0f, maxMP > 0f ? maxMP : float.MaxValue);

            if (maxMP <= 0f)
            {
                float highestCost = 0f;
                // Revisar slots asignados
                SpellId[] slotIds = new[] { preset.leftSpellId, preset.rightSpellId, preset.specialSpellId };
                foreach (var id in slotIds)
                {
                    if (id == SpellId.None) continue;
                    var s = spellLibrary != null ? spellLibrary.Get(id) : null;
                    if (s != null) highestCost = Mathf.Max(highestCost, s.manaCost);
                }
                // Revisar desbloqueados
                if (preset.unlockedSpells != null && spellLibrary != null)
                {
                    foreach (var id in preset.unlockedSpells)
                    {
                        if (id == SpellId.None) continue;
                        var s = spellLibrary.Get(id);
                        if (s != null) highestCost = Mathf.Max(highestCost, s.manaCost);
                    }
                }

                if (highestCost > 0f)
                {
                    // Dar suficiente maná para lanzar al menos 1-2 veces el hechizo más caro
                    maxMP = Mathf.Max(20f, highestCost * 2f);
                    currentMP = Mathf.Max(currentMP, maxMP);
                    Debug.Log($"[PlayerPresetService] Preset maxMP was 0 — inferred maxMP={maxMP} from highest spell cost {highestCost}");
                }
                else
                {
                    // Fallback genérico si no hay hechizos conocidos
                    maxMP = 50f;
                    if (currentMP <= 0f) currentMP = maxMP;
                    Debug.Log("[PlayerPresetService] Preset maxMP was 0 and no spells found; using default maxMP=50");
                }
            }

            _manaPool.Init(maxMP, currentMP);
        }
        else
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró ManaPool en escena para sincronizar MP");
        }
    }

    private void ConfigureSpells(PlayerPresetSO preset)
    {
        // Resolver IDs respetando None
        var leftId = preset.leftSpellId;
        var rightId = preset.rightSpellId;
        var specialId = preset.specialSpellId;

        // Fallback: si no hay ningún slot asignado pero sí hay desbloqueados, autocompletamos aunque la opción esté desactivada
        bool allNone = leftId == SpellId.None && rightId == SpellId.None && specialId == SpellId.None;

        var left = leftId == SpellId.None ? null : spellLibrary.Get(leftId);
        var right = rightId == SpellId.None ? null : spellLibrary.Get(rightId);
        var special = specialId == SpellId.None ? null : spellLibrary.Get(specialId);

        if (leftId != SpellId.None && left == null) Debug.LogWarning($"[PlayerPresetService] Left ID {leftId} no está en SpellLibrary");
        if (rightId != SpellId.None && right == null) Debug.LogWarning($"[PlayerPresetService] Right ID {rightId} no está en SpellLibrary");
        if (specialId != SpellId.None && special == null) Debug.LogWarning($"[PlayerPresetService] Special ID {specialId} no está en SpellLibrary");

        // Validar tipos de slot
        if (left && left.slotType == SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Left es SpecialOnly, se descarta"); left = null; }
        if (right && right.slotType == SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Right es SpecialOnly, se descarta"); right = null; }
        if (special && special.slotType != SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Special no es SpecialOnly, se descarta"); special = null; }

        // Evitar duplicados en slots izq/der
        if (left && right && leftId == rightId) { Debug.LogWarning("[PlayerPresetService] Left y Right tienen el mismo ID, se borra Right"); right = null; }

        // Auto-completar slots vacíos (opcional o por fallback si todos estaban None)
        if ((autoFillEmptySlotsFromUnlocked || allNone) && preset.unlockedSpells != null)
        {
            MagicSpellSO FindFirstAvailable(bool requireSpecial, SpellId avoid)
            {
                foreach (var id in preset.unlockedSpells)
                {
                    if (id == SpellId.None || id == avoid) continue;
                    var s = spellLibrary.Get(id);
                    if (!s) continue;
                    if (requireSpecial && s.slotType != SpellSlotType.SpecialOnly) continue;
                    if (!requireSpecial && s.slotType == SpellSlotType.SpecialOnly) continue;
                    return s;
                }
                return null;
            }

            if (!left) left = FindFirstAvailable(false, rightId);
            if (!right) right = FindFirstAvailable(false, leftId);
            if (!special) special = FindFirstAvailable(true, SpellId.None);
        }

        // Aplicar configuración al spawner
        if (_spawner != null)
        {
            _spawner.SetSpells(left, right, special);
            _spawner.SetInstigator(instigatorOverride ? instigatorOverride : gameObject);
        }

        // ← NUEVO: Aplicar configuración al MagicCaster también
        if (_magicCaster)
        {
            _magicCaster.SetSpells(left, right, special);
            Debug.Log($"[PlayerPresetService] MagicCaster configurado con hechizos del preset");
        }
        else
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró MagicCaster en el GameObject");
        }

        Debug.Log($"[PlayerPresetService] Hechizos configurados - L:{left?.name} R:{right?.name} S:{special?.name}");
    }

    // === NUEVO: API pública para re-aplicar el preset activo en runtime (incluye mana) ===
    public void ApplyCurrentPreset()
    {
        if (!GameBootService.IsAvailable)
        {
            Debug.LogWarning("[PlayerPresetService] GameBootService aún no está disponible");
            return;
        }
        var preset = GameBootService.Profile.GetActivePresetResolved();
        if (!preset)
        {
            Debug.LogWarning("[PlayerPresetService] No hay preset activo para aplicar");
            return;
        }
        // Aplicar stats (maná) y luego los hechizos
        ApplyManaFromPreset(preset);
        ConfigureSpells(preset);

        // NUEVO: aplicar abilities al action manager
        if (_actionManager == null)
            _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();

        if (_actionManager != null)
        {
            _actionManager.ApplyAbilities(preset.abilities);
            Debug.Log("[PlayerPresetService] Re-aplicado preset: abilities actualizadas");
        }

        // Notificar a subscriptores que el preset ha sido aplicado (p.ej. UI)
        OnPresetApplied?.Invoke();
    }

    // Evento público para notificar re-aplicación de preset (subscriptores UI o sistemas)
    public static System.Action OnPresetApplied;
}
