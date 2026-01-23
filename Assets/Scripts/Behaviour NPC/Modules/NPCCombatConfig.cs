﻿using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Comportamiento del NPC después de ser derrotado
    /// </summary>
    public enum PostDeathBehavior
    {
        [Tooltip("El NPC desaparece con VFX después de la muerte")]
        Disappear,
        
        [Tooltip("El NPC se levanta mareado y muestra un diálogo")]
        GetUpDizzy
    }

    /// <summary>
    /// Acción a ejecutar después de la derrota (y diálogo opcional)
    /// </summary>
    public enum PostDefeatAction
    {
        [Tooltip("Quedarse en estado dizzy/idle")]
        None,
        
        [Tooltip("Huir del jugador y desaparecer")]
        FleeAndDisappear,
        
        [Tooltip("Volver a patrullar/idle")]
        ReturnToIdle,
        
        [Tooltip("Moverse a un punto específico (anchor) después de la derrota")]
        MoveToAnchor
    }
    
    /// <summary>
    /// Configuración de combate para NPCs
    /// Última actualización: 28/12/2024 - Agregado fieldOfView
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Combat_Config", menuName = "NPC/Módulos/Combat Config", order = 2)]
    public class NPCCombatConfig : NPCModuleConfigBase
    {
        [Header("Combat Stats")]
        [Min(0f)]
        [Tooltip("Puntos de vida del NPC")]
        public float health = 100f;
        
        [Header("🎯 Rangos de Combate (CRÍTICO)")]
        [Min(0f)]
        [Tooltip("📡 DETECTION RANGE: Radio de detección para iniciar combate (metros)\n• Debe ser MAYOR que Max Attack Distance\n• Recomendado: 10-15m para magos, 8-10m para melee")]
        public float detectionRange = 10f;
        
        [Range(30f, 360f)]
        [Tooltip("👁️ FIELD OF VIEW: Ángulo de visión del NPC en grados\n• 180° = visión frontal amplia\n• 90° = visión frontal estrecha\n• 360° = visión completa (ojos en la nuca)")]
        public float fieldOfView = 160f;
        
        [Min(0f)]
        [Tooltip("🔴 MIN ATTACK DISTANCE (minDistance en logs)\nDistancia MÍNIMA ideal para atacar. Si el jugador está MÁS CERCA, el NPC RETROCEDE.\n\n📊 Valores Recomendados:\n• Magos: 4-6m (mantener distancia)\n• Melee: 1.5-2.5m (rango de espada)\n\n⚠️ IMPORTANTE:\n• DEBE ser MENOR que Max Attack Distance\n• Si minDistance >= maxDistance, el NPC NUNCA atacará")]
        public float minAttackDistance = 2f;
        
        [Min(0f)]
        [Tooltip("🟢 MAX ATTACK DISTANCE (maxDistance en logs)\nDistancia MÁXIMA ideal para atacar. Si el jugador está MÁS LEJOS, el NPC SE ACERCA.\n\n📊 Valores Recomendados:\n• Magos: 8-12m (rango de hechizos)\n• Melee: 3-5m (persecución)\n\n⚠️ IMPORTANTE:\n• DEBE ser MAYOR que Min Attack Distance\n• DEBE ser MENOR que Detection Range\n• El rango IDEAL de ataque es: [minAttackDistance, maxAttackDistance]")]
        public float maxAttackDistance = 8f;
        
        // ✅ DEPRECATED: Propiedades obsoletas para compatibilidad con assets antiguos
        // ⚠️ NO USAR: Solo existen para migración automática
        [System.Obsolete("❌ OBSOLETO: Usar minAttackDistance en su lugar. Esta propiedad será eliminada.", true)]
        public float meleeRange { get => minAttackDistance; set => minAttackDistance = value; }
        
        [System.Obsolete("❌ OBSOLETO: Usar maxAttackDistance en su lugar. Esta propiedad será eliminada.", true)]
        public float combatRange { get => maxAttackDistance; set => maxAttackDistance = value; }
        
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
        
        [Tooltip("Prefab del icono de interrogación (❓) que aparece cuando el NPC está buscando al jugador")]
        public GameObject questionIconPrefab;
        
        [Tooltip("Prefab del icono de admiración (❗) que aparece cuando el NPC encuentra al jugador")]
        public GameObject exclamationIconPrefab;
        
        [Tooltip("Duración del icono de alerta en segundos")]
        [Min(0.1f)]
        public float alertIconDuration = 2f;
        
        [Tooltip("Altura del icono de alerta sobre el NPC (Y offset). Ajustar según el tamaño del NPC.\n• NPCs pequeños: 1.5-2.0\n• NPCs normales: 2.5\n• NPCs grandes: 3.0-4.0")]
        [Range(0.5f, 5f)]
        public float alertIconHeight = 2.5f;
        
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
        
        [Header("🎭 Comportamiento Post-Muerte")]
        [Tooltip("¿Qué sucede cuando el NPC muere?\n• DESAPARECER: El NPC desaparece con VFX después de la animación de muerte\n• MAREARSE: El NPC se levanta mareado y muestra un diálogo")]
        public PostDeathBehavior postDeathBehavior = PostDeathBehavior.Disappear;
        
        [Tooltip("Acción a ejecutar después de la derrota (y diálogo opcional)")]
        public PostDefeatAction postDefeatAction = PostDefeatAction.None;
        
        [Header("Movimiento Post-Derrota (si postDefeatAction = MoveToAnchor)")]
        [Tooltip("Nombre del anchor de destino para el movimiento post-derrota")]
        public string postDefeatMoveAnchor;
        
        [Tooltip("¿Desaparecer al llegar al destino?")]
        public bool disappearOnArrival = false;
        
        [Tooltip("VFX a reproducir al desaparecer (opcional)")]
        public GameObject disappearOnArrivalVFX;
        
        [Tooltip("Si es líder de equipo, ¿mover también a los compañeros?")]
        public bool moveTeamMembersOnDefeat = true;

        [Tooltip("Diálogo que se muestra cuando el NPC se levanta mareado (solo si postDeathBehavior = Marearse)")]
        public DialogueAsset dialogueOnDizzy;
        
        [Tooltip("Prefab del VFX de desaparición (solo si postDeathBehavior = Desaparecer)")]
        public GameObject disappearVFXPrefab;
        
        [Tooltip("TransitionSettings para la desaparición con fade (ej: Fade.asset)")]
        public EasyTransition.TransitionSettings disappearTransition;
        
        [Tooltip("Duración del efecto de desaparición en segundos")]
        [Min(0.5f)]
        public float disappearDuration = 2f;
        
        [Header("Música y Eventos")]
        [Tooltip("Evento custom para la fase de alerta/persecución. Se emite al detectar al jugador.")]
        public string alertMusicEvent = "Npc_Battle_Alert";
        
        [Tooltip("ID de batalla para AudioGraphProfile (se usa en BATTLE_START:{id} y BattleWon)")]
        public string battleMusicId = "Npc_Battle";
        
        [Tooltip("Evento custom opcional para restaurar/ajustar la música cuando acaba la batalla.")]
        public string endMusicEvent = "Npc_Battle_Victory";
        
        [Header("Eventos al Grafo Narrativo")]
        [Tooltip("¿Enviar evento al grafo narrativo cuando el NPC sea derrotado?")]
        public bool sendEventOnDefeat = false;
        
        [Tooltip("Clave del evento a enviar cuando el NPC es derrotado (ej: 'Boss_Defeated', 'Enemy_Killed')\nEste evento se puede usar para completar misiones, desbloquear áreas, etc.")]
        public string defeatEventKey = "";
        
        [Tooltip("¿Enviar el evento ANTES de la animación de muerte? (por defecto se envía después de la secuencia completa)")]
        public bool sendDefeatEventBeforeDeath = false;
        
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
        
        [Header("🛡️ Escudo Defensivo")]
        [Tooltip("¿El NPC puede usar escudo para defenderse cuando no puede atacar?")]
        public bool useShield = false;
        
        [Tooltip("Prefab del escudo visual (esfera mágica, etc.)")]
        public GameObject shieldPrefab;
        
        [Min(0.5f)]
        [Tooltip("Duración mínima del escudo en segundos")]
        public float shieldMinDuration = 2f;
        
        [Min(0.5f)]
        [Tooltip("Duración máxima del escudo en segundos")]
        public float shieldMaxDuration = 5f;
        
        [Min(0f)]
        [Tooltip("Cooldown del escudo después de usarlo")]
        public float shieldCooldown = 10f;
        
        [Header("🏃 Huida Táctica y Cobertura")]
        [Tooltip("¿El NPC puede buscar cobertura cuando está en desventaja?")]
        public bool useTacticalRetreat = false;
        
        [Range(0.1f, 0.5f)]
        [Tooltip("% de salud para activar huida táctica (0.3 = 30% de salud)")]
        public float retreatHealthThreshold = 0.3f;
        
        [Min(5f)]
        [Tooltip("Cooldown entre intentos de huida en segundos")]
        public float retreatCooldown = 15f;
        
        [Min(5f)]
        [Tooltip("Radio de búsqueda de cobertura en metros")]
        public float coverSearchRadius = 15f;
        
        [Tooltip("Capas que se consideran cobertura (Default, Environment, Props)")]
        public LayerMask coverLayerMask = -1;
        
        [Min(2f)]
        [Tooltip("Distancia mínima de la cobertura al NPC")]
        public float minCoverDistance = 3f;
        
        [Min(5f)]
        [Tooltip("Distancia máxima de la cobertura al NPC")]
        public float maxCoverDistance = 15f;
        
        [Min(2f)]
        [Tooltip("Tiempo que permanece en cobertura en segundos")]
        public float coverStayDuration = 4f;
        
        [Tooltip("Si true, prioriza usar escudo sobre buscar cobertura")]
        public bool preferShieldOverCover = false;
        
        [Header("🔍 Sistema de Búsqueda del Jugador")]
        [Tooltip("Si TRUE, el NPC se mueve activamente buscando al jugador (5 intentos de movimiento)\nSi FALSE, se queda quieto durante 'passiveSearchDuration' segundos antes de abandonar")]
        public bool activelySearchForPlayer = true;
        
        [Min(0f)]
        [Tooltip("⏱️ BÚSQUEDA ACTIVA: Duración máxima de búsqueda en segundos (si activelySearchForPlayer = TRUE)\n• Recomendado: 15-20s para búsqueda persistente\n• El NPC hará hasta 5 intentos de movimiento dentro de este tiempo")]
        public float searchDuration = 15f;
        
        [Min(0f)]
        [Tooltip("⏱️ BÚSQUEDA PASIVA: Tiempo que espera quieto antes de abandonar (si activelySearchForPlayer = FALSE)\n• Recomendado: 5-10s para espera breve\n• El NPC solo mostrará ❓ sin moverse")]
        public float passiveSearchDuration = 8f;
        
        [Min(2f)]
        [Tooltip("📍 Radio de movimiento durante búsqueda activa en metros\n• El NPC se moverá aleatoriamente dentro de este radio\n• Recomendado: 5-8m para búsqueda natural")]
        public float searchMovementRadius = 6f;
        
        [Tooltip("Si TRUE, el NPC vuelve a su posición inicial después de agotar la búsqueda\nSi FALSE, se queda donde está y simplemente abandona el combate")]
        public bool returnToOriginAfterSearch = false;
        
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            
            if (health <= 0f)
            {
                errorMessage = "Health debe ser mayor a 0";
                return false;
            }
            
            if (detectionRange < maxAttackDistance)
            {
                errorMessage = "Detection Range debe ser mayor o igual a Max Attack Distance";
                return false;
            }
            
            if (minAttackDistance >= maxAttackDistance)
            {
                errorMessage = "⚠️ CRÍTICO: Max Attack Distance debe ser MAYOR que Min Attack Distance, o el NPC NUNCA atacará. " +
                              $"Actual: min={minAttackDistance}, max={maxAttackDistance}";
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
