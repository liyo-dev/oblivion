﻿﻿using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de combate para NPCs
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Combat_Config", menuName = "NPC/Módulos/Combat Config", order = 2)]
    public class NPCCombatConfig : NPCModuleConfigBase
    {
        [Header("Combat Stats")]
        [Min(0f)]
        [Tooltip("Puntos de vida del NPC")]
        public float health = 100f;
        
        [Min(0f)]
        [Tooltip("Daño base de ataque")]
        public float attackDamage = 10f;
        
        [Min(0f)]
        [Tooltip("Tiempo entre ataques")]
        public float attackCooldown = 1.5f;
        
        [Header("Ranges")]
        [Min(0f)]
        [Tooltip("Rango de detección del jugador")]
        public float detectionRange = 10f;
        
        [Min(0f)]
        [Tooltip("Rango para ataques a distancia")]
        public float combatRange = 8f;
        
        [Min(0f)]
        [Tooltip("Rango para ataques cuerpo a cuerpo")]
        public float meleeRange = 2f;
        
        [Header("Behavior")]
        [Tooltip("¿El NPC es agresivo automáticamente o espera ser atacado?")]
        public bool isAggressive = true;
        
        [Tooltip("¿Puede el NPC perseguir al jugador fuera de su área inicial?")]
        public bool canChaseOutOfBounds = false;
        
        [Min(0f)]
        [Tooltip("Distancia máxima de persecución (si canChaseOutOfBounds = true)")]
        public float maxChaseDistance = 20f;
        
        [Header("Alert Visual")]
        [Tooltip("Prefab del icono de alerta (exclamación, etc.) que aparece al detectar al jugador")]
        public GameObject alertIconPrefab;
        
        [Tooltip("Duración del icono de alerta en segundos")]
        [Min(0.1f)]
        public float alertIconDuration = 2f;
        
        [Header("Health Bar UI")]
        [Tooltip("Prefab de la barra de vida del NPC (Canvas con NPCHealthBarUI)")]
        public GameObject healthBarPrefab;
        
        [Header("Diálogos")]
        [Tooltip("Diálogo que se muestra durante la fase de alerta (antes del combate)")]
        public DialogueAsset dialogueOnAlert;
        
        [Tooltip("Diálogo que se muestra si el NPC es derrotado")]
        public DialogueAsset dialogueOnDefeat;
        
        [Tooltip("Diálogo repetible después de haber sido derrotado")]
        public DialogueAsset dialogueAfterDefeat;
        
        [Tooltip("¿Esperar a que el diálogo de alerta termine antes de iniciar combate?")]
        public bool waitForAlertDialogue = true;
        
        [Header("Música y Eventos")]
        [Tooltip("Evento custom para la fase de alerta/persecución. Se emite al detectar al jugador.")]
        public string alertMusicEvent = "Npc_Battle_Alert";
        
        [Tooltip("ID de batalla para AudioGraphProfile (se usa en BATTLE_START:{id} y BattleWon)")]
        public string battleMusicId = "Npc_Battle";
        
        [Tooltip("Evento custom opcional para restaurar/ajustar la música cuando acaba la batalla.")]
        public string endMusicEvent = "Npc_Battle_Victory";
        
        [Header("Spells / Attacks (3 Slots)")]
        [Tooltip("Hechizo/ataque básico (slot 1) - Similar a Pokemon, cada NPC puede tener hasta 3 hechizos")]
        public GameObject spell1Prefab;
        
        [Tooltip("Hechizo/ataque intermedio (slot 2)")]
        public GameObject spell2Prefab;
        
        [Tooltip("Hechizo/ataque especial (slot 3) - Normalmente el más poderoso")]
        public GameObject spell3Prefab;
        
        [Header("Spell Cooldowns")]
        [Min(0.1f)]
        [Tooltip("Cooldown del hechizo 1 en segundos")]
        public float spell1Cooldown = 1.5f;
        
        [Min(0.1f)]
        [Tooltip("Cooldown del hechizo 2 en segundos")]
        public float spell2Cooldown = 2.5f;
        
        [Min(0.1f)]
        [Tooltip("Cooldown del hechizo 3 (especial) en segundos")]
        public float spell3Cooldown = 5f;
        
        [Header("Spell Usage Probability")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de usar hechizo 1 (0-1)")]
        public float spell1Chance = 0.5f;
        
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de usar hechizo 2 (0-1)")]
        public float spell2Chance = 0.3f;
        
        [Range(0f, 1f)]
        [Tooltip("Probabilidad de usar hechizo 3 especial (0-1)")]
        public float spell3Chance = 0.2f;
        
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            
            if (health <= 0f)
            {
                errorMessage = "Health debe ser mayor a 0";
                return false;
            }
            
            if (attackDamage < 0f)
            {
                errorMessage = "Attack damage no puede ser negativo";
                return false;
            }
            
            if (detectionRange < combatRange)
            {
                errorMessage = "Detection range debe ser mayor o igual a combat range";
                return false;
            }
            
            // Verificar que al menos un hechizo esté configurado
            if (spell1Prefab == null && spell2Prefab == null && spell3Prefab == null)
            {
                errorMessage = "⚠️ CRÍTICO: Al menos un hechizo (spell1, spell2 o spell3) debe estar configurado para que el NPC pueda atacar.\n" +
                              "Ve al NPCCombatConfig y asigna prefabs de hechizos en la sección 'Spells / Attacks (3 Slots)'";
                return false;
            }
            
            // Advertencia si no hay spell1 (hechizo básico)
            if (spell1Prefab == null)
            {
                Debug.LogWarning($"[NPCCombatConfig] ⚠️ Spell1 (básico) no configurado. Se recomienda siempre tener al menos el hechizo básico.");
            }
            
            // Normalizar probabilidades (advertencia si no suman 1.0)
            float totalChance = spell1Chance + spell2Chance + spell3Chance;
            if (Mathf.Abs(totalChance - 1f) > 0.01f && totalChance > 0f)
            {
                Debug.LogWarning($"[NPCCombatConfig] Las probabilidades de hechizos suman {totalChance:F2} en lugar de 1.0. " +
                    "Se normalizarán automáticamente en runtime, pero considera ajustarlas manualmente.");
            }
            
            return true;
        }
        
        #region Spell System Helpers
        
        /// <summary>
        /// Obtiene el prefab de un hechizo por su índice (0-2)
        /// </summary>
        public GameObject GetSpellPrefab(int spellIndex)
        {
            return spellIndex switch
            {
                0 => spell1Prefab,
                1 => spell2Prefab,
                2 => spell3Prefab,
                _ => null
            };
        }
        
        /// <summary>
        /// Obtiene el cooldown de un hechizo por su índice (0-2)
        /// </summary>
        public float GetSpellCooldown(int spellIndex)
        {
            return spellIndex switch
            {
                0 => spell1Cooldown,
                1 => spell2Cooldown,
                2 => spell3Cooldown,
                _ => 1f
            };
        }
        
        /// <summary>
        /// Obtiene la probabilidad de uso de un hechizo por su índice (0-2)
        /// </summary>
        public float GetSpellChance(int spellIndex)
        {
            return spellIndex switch
            {
                0 => spell1Chance,
                1 => spell2Chance,
                2 => spell3Chance,
                _ => 0f
            };
        }
        
        /// <summary>
        /// Selecciona un hechizo aleatorio basándose en las probabilidades configuradas
        /// </summary>
        /// <returns>Índice del hechizo seleccionado (0-2) o -1 si ninguno está disponible</returns>
        public int SelectRandomSpell()
        {
            // Recopilar hechizos disponibles con sus probabilidades
            var availableSpells = new System.Collections.Generic.List<(int index, float chance)>();
            
            if (spell1Prefab != null) availableSpells.Add((0, spell1Chance));
            if (spell2Prefab != null) availableSpells.Add((1, spell2Chance));
            if (spell3Prefab != null) availableSpells.Add((2, spell3Chance));
            
            if (availableSpells.Count == 0)
                return -1;
            
            // Normalizar probabilidades
            float totalChance = 0f;
            foreach (var spell in availableSpells)
                totalChance += spell.chance;
            
            if (totalChance <= 0f)
                return availableSpells[Random.Range(0, availableSpells.Count)].index;
            
            // Selección ponderada
            float randomValue = Random.Range(0f, totalChance);
            float cumulative = 0f;
            
            foreach (var spell in availableSpells)
            {
                cumulative += spell.chance;
                if (randomValue <= cumulative)
                    return spell.index;
            }
            
            // Fallback al último disponible
            return availableSpells[availableSpells.Count - 1].index;
        }
        
        /// <summary>
        /// Verifica si un hechizo específico está configurado
        /// </summary>
        public bool HasSpell(int spellIndex)
        {
            return GetSpellPrefab(spellIndex) != null;
        }
        
        /// <summary>
        /// Obtiene el número de hechizos configurados
        /// </summary>
        public int GetSpellCount()
        {
            int count = 0;
            if (spell1Prefab != null) count++;
            if (spell2Prefab != null) count++;
            if (spell3Prefab != null) count++;
            return count;
        }
        
        #endregion
    }
}

