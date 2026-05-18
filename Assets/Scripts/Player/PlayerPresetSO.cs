using System;
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
    [Header("Abilities (Swim, Jump, Climb, Fly)")]
    public PlayerAbilities abilities = new PlayerAbilities();

    [Header("Apariencia")]
    [Tooltip("Aspecto visual del jugador. En defaultPreset define el look inicial, en runtime contiene el aspecto actual.")]
    public List<AppearanceEntry> appearance = new();

    [Header("Vestuario desbloqueado")]
    public List<string> unlockedWardrobeIds = new();

    [Header("Inventario")]
    public List<InventoryItemSave> inventoryItems = new();

    [Header("Progreso de bosses")]
    public List<string> defeatedBossIds = new();

    [Header("Estado de grafos narrativos")]
    public List<PlayerSaveData.NarrativeBlackboardSnapshot> narrativeBlackboards = new();

    [Serializable]
    public struct NpcPosEntry
    {
        public string npcId;        // normalizado: nombre del GameObject
        public Vector3 position;    // última posición persistida
        public bool hasActiveState; // si se ha guardado explícitamente el estado activo
        public bool isActive;       // si el NPC debe estar activo o no (solo válido si hasActiveState=true)
    }

    [Header("NPCs (persistencia opcional)")]
    [Tooltip("Lista de posiciones persistidas por NPC. El id es el nombre único del GameObject del NPC.")]
    public List<NpcPosEntry> npcPositions = new();

    [Header("Interactuables consumidos (single-use)")]
    [Tooltip("IDs de Interactable marcados como singleUse que ya han sido consumidos.")]
    public List<string> consumedInteractableIds = new();

    [Header("Narrativas interactivas completadas")]
    [Tooltip("IDs de persistencia de narrativas interactivas de NPCs que ya se han ejecutado.")]
    public List<string> completedInteractiveNarratives = new();

    [Header("Popups de lore vistos (single-use)")]
    [Tooltip("IDs de persistencia de LorePopupEntry que ya se han mostrado.")]
    public List<string> seenLorePopupIds = new();

    [Header("Equipo (Party)")]
    [Tooltip("IDs narrativos de los NPCs que están en el equipo del jugador.")]
    public List<string> partyMemberIds = new();

    [Tooltip("ID del personaje activo actual (int) para CharacterSlot. Por defecto es 1 (Will).")]
    public int activeCharacterSlot = 1;

    [Header("Sistema de Teletransporte")]
    [Tooltip("IDs de los puntos de teletransporte desbloqueados por el jugador.")]
    public List<string> unlockedTeleportPoints = new();
}