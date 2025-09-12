using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerPresetService : MonoBehaviour
{
    [Header("Perfil de arranque (SO)")]
    [SerializeField] private GameBootProfile profile;

    [Header("Librería de hechizos (ID → SO)")]
    [SerializeField] private SpellLibrarySO spellLibrary;

    [Header("Opciones")]
    [SerializeField] private bool autoFillEmptySlotsFromUnlocked = false;
    [SerializeField] private GameObject instigatorOverride;

    MagicProjectileSpawner _spawner;

    void Awake()
    {
        _spawner = GetComponent<MagicProjectileSpawner>() ?? gameObject.AddComponent<MagicProjectileSpawner>();

        if (!profile)
        {
            var runner = FindFirstObjectByType<GameBootRunner>();
            var boot   = FindFirstObjectByType<WorldBootstrap>();
            profile = runner ? runner.profile : (boot ? boot.profile : null);
        }
        if (!profile || !spellLibrary) { enabled = false; Debug.LogError("[PlayerPresetService] Falta Boot o SpellLibrary."); return; }

        var preset = profile.GetActivePresetResolved();
        if (!preset) { Debug.LogWarning("[PlayerPresetService] Sin preset activo."); return; }

        // Resolver respetando None
        var leftId    = preset.leftSpellId;
        var rightId   = preset.rightSpellId;
        var specialId = preset.specialSpellId;

        var left    = leftId    == SpellId.None ? null : spellLibrary.Get(leftId);
        var right   = rightId   == SpellId.None ? null : spellLibrary.Get(rightId);
        var special = specialId == SpellId.None ? null : spellLibrary.Get(specialId);

        // Reglas: izq/dcha no SpecialOnly; arriba solo SpecialOnly
        if (left && left.slotType == SpellSlotType.SpecialOnly)   left = null;
        if (right && right.slotType == SpellSlotType.SpecialOnly) right = null;
        if (special && special.slotType != SpellSlotType.SpecialOnly) special = null;

        // Evitar duplicar el MISMO ID en izq/dcha
        if (left && right && leftId == rightId) right = null;

        // Autocompletar (opcional)
        if (autoFillEmptySlotsFromUnlocked && preset.unlockedSpells != null)
        {
            MagicSpellSO FindFirst(bool requireSpecial, SpellId avoid)
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

            if (!left)    left    = FindFirst(false, rightId);
            if (!right)   right   = FindFirst(false, leftId);
            if (!special) special = FindFirst(true,  SpellId.None);
        }

        _spawner.SetSpells(left, right, special);
        _spawner.SetInstigator(instigatorOverride ? instigatorOverride : gameObject);
    }
}
