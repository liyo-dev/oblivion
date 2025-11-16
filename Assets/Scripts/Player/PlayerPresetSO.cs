using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Game/Player Preset", fileName="PlayerPreset_Default")]
public class PlayerPresetSO : ScriptableObject
{
    [Header("Spawn")]
    [Tooltip("ID del anchor donde debe aparecer el jugador con este preset")]
    public string spawnAnchorId = "Bedroom";

    [Header("Stats")]
    public int   level = 1;
    public float maxHP = 100, currentHP = 100;
    public float maxMP = 50,  currentMP = 50;

    [Header("Desbloqueos")]
    [HideInInspector] public List<AbilityId> unlockedAbilities = new();
    public List<SpellId>   unlockedSpells    = new();

    [Header("Slots de hechizo (por ID)")]
    public SpellId leftSpellId;
    public SpellId rightSpellId;
    public SpellId specialSpellId;

    [Header("Flags (misiones/estados simples)")]
    public List<string> flags = new();

    // Usar la clase separada PlayerAbilities para evitar problemas de resolución entre archivos
    [Header("Abilities (Swim, Jump, Climb)")]
    public PlayerAbilities abilities = new PlayerAbilities();

    [Header("Apariencia")]
    public List<AppearanceEntry> appearance = new();

    [Header("Inventario")]
    public List<InventoryItemSave> inventoryItems = new();

    [Header("Progreso de bosses")]
    public List<string> defeatedBossIds = new();

    [Header("Estado de grafos narrativos")]
    public List<PlayerSaveData.NarrativeBlackboardSnapshot> narrativeBlackboards = new();

    [System.Serializable]
    public struct NpcPosEntry
    {
        public string npcId;        // normalizado: nombre del GameObject
        public Vector3 position;    // última posición persistida
    }

    [Header("NPCs (persistencia opcional)")]
    [Tooltip("Lista de posiciones persistidas por NPC. El id es el nombre único del GameObject del NPC.")]
    public List<NpcPosEntry> npcPositions = new();
}
