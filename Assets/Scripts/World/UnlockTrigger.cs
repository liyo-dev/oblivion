using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UnlockTrigger : MonoBehaviour
{
    [Header("Condición")]
    [Tooltip("Si se establece, este trigger solo se aplicará si el flag no existe aún en el preset. Tras aplicarse, añade el flag.")]
    public string oneShotFlag;

    [Header("Disparadores")]
    [Tooltip("Si está activo, aplicará los desbloqueos automáticamente al habilitar este GameObject (sin requerir trigger del Player)")]
    public bool unlockOnEnable;

    [Header("Desbloqueos")]
    public List<AbilityId> abilitiesToUnlock = new();
    public List<SpellId>   spellsToUnlock    = new();

    [Header("Asignación de slots")]
    [Tooltip("Intentar asignar los hechizos desbloqueados a slots vacíos automáticamente")]
    public bool assignSpellsToEmptySlots = true;

    [Header("Maná (opcional)")]
    public bool setManaMinimums;
    public float minMaxMana = 50f;
    public float minCurrentMana = 0f;

    [Header("Calibración MP (auto)")]
    [Tooltip("Asegura suficiente maná para lanzar al menos un hechizo tras el desbloqueo (usa el coste del hechizo asignado).")]
    public bool ensureFirstCast;
    [Tooltip("Margen adicional sobre el coste del hechizo (por ejemplo, 0-2).")]
    public float firstCastExtraBuffer = 0f;

    [Header("Feedback")]
    public bool disableAfterUse = true;
    public GameObject onUnlockedVfx;

    [Header("Guardado")]
    [Tooltip("Guardar automáticamente tras aplicar el desbloqueo/cambios de MP")] 
    public bool saveAfterUnlock = true;

    private bool _pendingApply;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;

        // Opción: desbloquear al habilitar el GO
        if (unlockOnEnable)
        {
            if (GameBootService.IsAvailable)
            {
                TryApplyUnlocks();
            }
            else
            {
                _pendingApply = true;
                Debug.Log("[UnlockTrigger] Perfil no listo. Desbloqueo (OnEnable) diferido");
            }
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_pendingApply)
        {
            TryApplyUnlocks();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameBootService.IsAvailable)
        {
            TryApplyUnlocks();
        }
        else
        {
            _pendingApply = true;
            Debug.Log("[UnlockTrigger] Perfil no listo. Desbloqueo diferido hasta OnProfileReady");
        }
    }

    // === Nuevo: método público para invocar por evento externo (UnityEvent, AnimationEvent, Timeline, etc.) ===
    public void ApplyUnlocksNow()
    {
        if (GameBootService.IsAvailable)
        {
            TryApplyUnlocks();
        }
        else
        {
            _pendingApply = true;
            Debug.Log("[UnlockTrigger] Perfil no listo. Desbloqueo (ApplyUnlocksNow) diferido");
        }
    }

    [ContextMenu("Apply Unlocks Now (Debug)")]
    private void CtxApplyNow()
    {
        ApplyUnlocksNow();
    }

    private void TryApplyUnlocks()
    {
        _pendingApply = false;
        var preset = UnlockService.GetActivePreset();
        if (!preset)
        {
            Debug.LogWarning("[UnlockTrigger] No hay preset activo");
            return;
        }

        // One-shot via flag
        if (!string.IsNullOrEmpty(oneShotFlag) && preset.flags != null && preset.flags.Contains(oneShotFlag))
        {
            if (disableAfterUse) gameObject.SetActive(false);
            return;
        }

        bool changed = false;

        // Desbloquear habilidades
        foreach (var ab in abilitiesToUnlock)
        {
            changed |= UnlockService.UnlockAbility(ab);
        }

        // Desbloquear hechizos
        foreach (var sp in spellsToUnlock)
        {
            changed |= UnlockService.UnlockSpell(sp, assignSpellsToEmptySlots);
        }

        // Asegurar maná si procede (valores proporcionados)
        if (setManaMinimums)
        {
            changed |= UnlockService.EnsureMana(minMaxMana, minCurrentMana);
        }

        // Añadir flag one-shot si configurado
        if (!string.IsNullOrEmpty(oneShotFlag))
        {
            changed |= UnlockService.AddFlag(oneShotFlag);
        }

        // Re-aplicar preset a sistemas (MagicCaster/Spawner) si hubo cambios
        PlayerPresetService presetService = null;
        if (changed)
        {
            presetService = FindFirstObjectByType<PlayerPresetService>();
            if (presetService) presetService.ApplyCurrentPreset();
        }

        // === Garantizar primer casteo si se pide ===
        bool mpChangedByEnsure = false;
        if (ensureFirstCast)
        {
            float needed = 0f;
            var caster = FindFirstObjectByType<MagicCaster>();
            if (caster != null)
            {
                // Coste mínimo de alguno de los slots asignados (asegura poder lanzar al menos uno)
                float cL = caster.GetSpellForSlot(MagicSlot.Left)    ? caster.GetSpellForSlot(MagicSlot.Left).manaCost    : float.PositiveInfinity;
                float cR = caster.GetSpellForSlot(MagicSlot.Right)   ? caster.GetSpellForSlot(MagicSlot.Right).manaCost   : float.PositiveInfinity;
                float cS = caster.GetSpellForSlot(MagicSlot.Special) ? caster.GetSpellForSlot(MagicSlot.Special).manaCost : float.PositiveInfinity;
                needed = Mathf.Min(cL, cR, cS);
                if (!float.IsFinite(needed)) needed = 0f; // por si no hay ningún spell asignado
            }
            else
            {
                // Fallback: si no hay caster, usa un valor conservador (5)
                needed = 5f;
            }

            needed = Mathf.Max(0f, needed + Mathf.Max(0f, firstCastExtraBuffer));
            if (needed > 0f)
            {
                mpChangedByEnsure = UnlockService.EnsureMana(Mathf.Max(preset.maxMP, needed), Mathf.Max(preset.currentMP, needed));
                if (mpChangedByEnsure)
                {
                    if (!presetService) presetService = FindFirstObjectByType<PlayerPresetService>();
                    if (presetService) presetService.ApplyCurrentPreset();
                }
            }
        }

        // === Fallback de MP mínimo si se han desbloqueado hechizos y no hay otra configuración de MP activa ===
        bool mpChangedByFallback = false;
        if (!ensureFirstCast && !setManaMinimums && spellsToUnlock != null && spellsToUnlock.Count > 0)
        {
            const float fallbackMin = 5f; // valor prudente para permitir probar un hechizo barato
            mpChangedByFallback = UnlockService.EnsureMana(
                Mathf.Max(preset.maxMP, fallbackMin),
                Mathf.Max(preset.currentMP, fallbackMin)
            );
            if (mpChangedByFallback)
            {
                if (!presetService) presetService = FindFirstObjectByType<PlayerPresetService>();
                if (presetService) presetService.ApplyCurrentPreset();
            }
        }

        // Forzar refresco de HUD si hubo cambios en MP o preset
        if (changed || mpChangedByEnsure || mpChangedByFallback)
        {
            var hud = FindFirstObjectByType<PlayerHUD>();
            if (hud != null)
            {
                hud.ForceRefresh();
            }
        }

        if (changed)
        {
            if (onUnlockedVfx)
            {
                Instantiate(onUnlockedVfx, transform.position, Quaternion.identity);
            }

            Debug.Log("[UnlockTrigger] Desbloqueos aplicados al preset activo");
        }

        // Guardar si procede (si hubo cambios o si se ajustó MP para primer cast o fallback)
        if (saveAfterUnlock && (changed || mpChangedByEnsure || mpChangedByFallback))
        {
            var profile = GameBootService.Profile;
            var saveSystem = FindFirstObjectByType<SaveSystem>();
            if (profile != null && saveSystem != null)
            {
                bool ok = profile.SaveCurrentGameState(saveSystem);
                if (!ok) Debug.LogError("[UnlockTrigger] Error al guardar tras desbloqueo");
            }
        }

        if (disableAfterUse)
        {
            gameObject.SetActive(false);
        }
    }
}
