using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;
using Game.NPC;
using Game.UI;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Combate - Actúa como puente entre la Máquina de Estados General y el Cerebro de Combate (NPCCombatBrain).
    /// </summary>
    public class CombatState : NPCStateBase
    {
        public override string StateName => "Combat";
        
        private NPCCombatBrain _combatBrain;
        private float _checkTimer;
        private const float EXIT_CHECK_INTERVAL = 1.0f;
        private const float MAX_COMBAT_DISTANCE = 30f; // Margen de seguridad para salir de combate
        
        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            // 🚫 1. Verificación de Seguridad: Si ya está muerto o derrotado
            if (context.WasDefeatedInCombat)
            {
                context.Log("[CombatState] ⛔ NPC derrotado previamente. Cancelando combate.");
                context.IsInCombat = false;
                return;
            }
            
            context.Log("[CombatState] ⚔️ INICIANDO COMBATE");
            context.IsInCombat = true;
            
            // ✅ Registrar NPC en el registro de combate activo
            if (context.Transform != null)
            {
                ActiveCombatRegistry.RegisterNPC(context.Transform.gameObject);
                context.Log("[CombatState] ✅ NPC registrado en ActiveCombatRegistry");
                
                // ✅ FIX: Si soy líder de un equipo, forzar a todos los miembros a entrar en combate
                var teamMember = context.Transform.GetComponent<NPCTeamMember>();
                if (teamMember != null && teamMember.IsLeader && teamMember.Team != null)
                {
                    context.Log("[CombatState] 👥 Soy líder - forzando equipo a combate");
                    teamMember.Team.ForceTeamCombat(context.Player);
                }
            }
            
            // 🎵 2. Audio
            TriggerBattleMusic(context);
            
            // 🛠️ 3. Configuración de Componentes (Targetable, Damageable, UI)
            InitializeCombatComponents(context);
            
            // 🧠 4. Inicializar el Cerebro de Combate
            _combatBrain = context.Transform.GetComponent<NPCCombatBrain>();
            if (_combatBrain == null) 
                _combatBrain = context.Transform.gameObject.AddComponent<NPCCombatBrain>();

            // Inicializar el Brain con el Manager
            var manager = context.Transform.GetComponent<NPCBehaviourManagerV2>();
            if (manager != null) _combatBrain.Initialize(manager);

            // ⚙️ 5. Configurar y Arrancar el Brain
            if (context.Config?.combatConfig != null)
            {
                var cc = context.Config.combatConfig;
                
                // Mapeamos la configuración del ScriptableObject a la Struct del Brain
                var brainSettings = new NPCCombatBrain.Settings
                {
                    // Distancias
                    minSafeDistance = cc.minAttackDistance,
                    optimalDistance = (cc.minAttackDistance + cc.maxAttackDistance) / 2f,
                    maxDistance = cc.maxAttackDistance,
                    runSpeed = 6f, // Velocidad base de correr
                    walkSpeed = 2.5f,
                    
                    // Ataques (Mapeo directo de slots)
                    upperBodyLayer = 1,
                    leftAttack = new NPCCombatBrain.AttackSlot { 
                        animationState = "MagicLeft", cooldown = cc.spell1Cooldown, slotIndex = 0 
                    },
                    rightAttack = new NPCCombatBrain.AttackSlot { 
                        animationState = "MagicRight", cooldown = cc.spell2Cooldown, slotIndex = 1 
                    },
                    specialAttack = new NPCCombatBrain.AttackSlot { 
                        animationState = "MagicSpecial", cooldown = cc.spell3Cooldown, slotIndex = 2 
                    },
                    
                    // Comportamiento
                    attackFrequencyMultiplier = cc.isAggressive ? 1.5f : 1.0f,
                    globalCooldown = 1.5f, // Pausa entre acciones para "ritmo humano"
                    spawnProjectileViaAnimEvent = false,
                    fireDelaySeconds = 0.4f, // Ajustar según animación
                    maxMana = cc.maxMana,
                    manaRegenPerSecond = cc.manaRegenPerSecond,
                    manaRegenDelayAfterSpend = cc.manaRegenDelayAfterSpend,
                    manaCostLeft = cc.spell1ManaCost,
                    manaCostRight = cc.spell2ManaCost,
                    manaCostSpecial = cc.spell3ManaCost,
                    lowManaRetreatThreshold = cc.lowManaRetreatThreshold,
                    
                    // Tácticas & Defensa
                    difficultyLevel = cc.difficultyLevel, // ✅ FIX #13: configurable por NPC (antes hardcodeado a 0.7f para todos)
                    useShield = cc.useShield,
                    shieldDuration = cc.shieldMaxDuration,
                    shieldCooldown = cc.shieldCooldown,
                    coverLayerMask = cc.coverLayerMask,
                    coverSearchRadius = cc.coverSearchRadius,
                    dodgeDistance = 3f,

                    // 🎭 FIX #5: completar el mapeo de la estrategia de engaño/emboscada — antes
                    // estos dos campos ni existían en el struct-initializer, así que quedaban en
                    // 0f/0 por defecto y la mecánica de fingir quedarse sin maná para emboscar
                    // (~150 líneas ya implementadas en NPCCombatBrain) nunca se activaba.
                    deceptionChance = cc.deceptionChance,
                    minAttacksToKeepForAmbush = cc.minAttacksToKeepForAmbush,
                    
                    // 🔍 Búsqueda del Jugador
                    activelySearchForPlayer = cc.activelySearchForPlayer,
                    searchDuration = cc.searchDuration,
                    passiveSearchDuration = cc.passiveSearchDuration,
                    searchMovementRadius = cc.searchMovementRadius,
                    returnToOriginAfterSearch = cc.returnToOriginAfterSearch
                };

                // ¡ARRANCAR LA FSM DE COMBATE!
                _combatBrain.BeginCombat(brainSettings, cc);
                context.Log("[CombatState] ✅ Brain activado con configuración exitosa.");
            }
            else
            {
                context.LogError("[CombatState] ❌ Falta CombatConfig en el NPC. El combate no funcionará bien.");
            }
        }

        public override void OnUpdate(NPCStateContext context)
        {
            // Verificación periódica para salir del combate si el jugador se aleja mucho
            _checkTimer += Time.deltaTime;
            if (_checkTimer > EXIT_CHECK_INTERVAL)
            {
                _checkTimer = 0;
                if (context.Player != null)
                {
                    float dist = Vector3.Distance(context.Transform.position, context.Player.position);
                    if (dist > MAX_COMBAT_DISTANCE)
                    {
                        context.Log($"[CombatState] Jugador fuera de rango ({dist:F1}m). Finalizando combate.");
                        HandleFleeByDistance(context);
                        context.IsInCombat = false; // Esto disparará la transición en CheckTransitions
                    }
                }
            }
        }

        /// <summary>
        /// Petición de Raúl (1 sep 2026): "huir" de un combate de equipo (p.ej. Lety+Vicky)
        /// alejándose lo suficiente. El corte por distancia (MAX_COMBAT_DISTANCE) ya existía y ya
        /// dejaba al NPC "no derrotado" (WasDefeatedInCombat sigue en false, la vida con la que se
        /// quedó se conserva para el próximo reenganche — ver InitializeCombatComponents más abajo,
        /// isFirstCombatEngagement) — pero no avisaba al jugador, no devolvía la música a la
        /// normalidad, y si el NPC formaba parte de un NPCCombatTeam (como Lety+Vicky) tampoco
        /// reseteaba el estado DEL EQUIPO (_isTeamInCombat seguía en true para siempre, aunque cada
        /// miembro hubiera salido de CombatState). Este método añade esas tres cosas, una sola vez,
        /// justo cuando el ÚLTIMO miembro con combate activo del grupo (el propio NPC si va solo, o
        /// todo el equipo si es un NPCCombatTeam) se aleja lo bastante. Enemigos menores (arañas,
        /// ImpDemon, Golem) no pasan por CombatState — tienen su propia IA — así que no les afecta
        /// este cambio, tal y como se pidió ("de las arañas no hace falta").
        /// </summary>
        private void HandleFleeByDistance(NPCStateContext context)
        {
            var npcGO = context.Transform.gameObject;
            var teamMember = npcGO.GetComponent<NPCTeamMember>();
            var team = teamMember != null ? teamMember.Team : null;
            string battleMusicId = context.Config?.combatConfig?.battleMusicId;

            if (team != null)
            {
                bool anyOtherMemberStillFighting = false;
                foreach (var member in team.AllMembers)
                {
                    if (member == null || member.gameObject == npcGO) continue;
                    if (ActiveCombatRegistry.IsInCombat(member.gameObject)) { anyOtherMemberStillFighting = true; break; }
                }

                // Si queda otro miembro del equipo todavía enganchado, no hay nada que resetear
                // todavía — el aviso/música/reset de equipo se dispara cuando se aleja el ÚLTIMO.
                if (anyOtherMemberStillFighting) return;
                if (!team.IsTeamInCombat) return; // ya se había reseteado (defensivo)

                team.ResetTeamState();
                RestoreMusicAfterFlee(battleMusicId);
                HudToastService.Instance?.Show("COMBAT_FLED_TEAM", 3f);
                context.Log("[CombatState] 🏃 Equipo completo fuera de rango — combate de equipo reseteado (no derrotados).");
            }
            else
            {
                RestoreMusicAfterFlee(battleMusicId);
                HudToastService.Instance?.Show("COMBAT_FLED_SOLO", 3f);
            }
        }

        private void RestoreMusicAfterFlee(string battleMusicId)
        {
            // Simétrico con TriggerBattleMusic (arriba): esa función SOLO cambia la música si el
            // NPC tiene battleMusicId configurado. Si está vacío, esta pelea nunca tocó la música
            // en primer lugar — no hay nada que restaurar, y llamar a RestoreAfterBattle() a ciegas
            // aquí podría cortar la música de OTRO combate simultáneo sin relación con este NPC.
            if (AudioService.Instance == null || string.IsNullOrEmpty(battleMusicId)) return;
            AudioService.Instance.EndBattleById(battleMusicId);
        }

        public override void OnExit(NPCStateContext context)
        {
            base.OnExit(context);
            context.IsInCombat = false;
            
            // ✅ Desregistrar NPC del registro de combate activo
            if (context.Transform != null)
            {
                ActiveCombatRegistry.UnregisterNPC(context.Transform.gameObject);
                context.Log("[CombatState] ✅ NPC desregistrado de ActiveCombatRegistry");
            }

            // Detener el cerebro de combate limpiamente
            if (_combatBrain != null)
            {
                _combatBrain.StopCombat();
            }
            
            // ⭐ Desactivar targeting para que el marker desaparezca
            var targetable = context.Transform.GetComponent<Targetable>();
            if (targetable != null)
            {
                targetable.isInActiveCombat = false;
            }

            // Reactivar interacciones si el NPC sigue vivo (si está muerto, DeadState se encarga)
            if (!context.WasDefeatedInCombat)
            {
                var interactable = context.Transform.GetComponent<Interactable>();
                if (interactable != null) interactable.enabled = true;
            }
            
            context.Log("[CombatState] Salida del estado de combate completada.");
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Muerte/Derrota (Prioridad Absoluta)
            if (context.WasDefeatedInCombat) return new DeadState();
            
            // 2. Cinemática
            if (context.IsInCinematic) return new CinematicState();
            
            // 3. Fin de combate voluntario (por distancia o script)
            if (!context.IsInCombat) return new IdleState();

            return null; // Seguir en combate
        }

        // ------------------------------------------------------------------------
        // Helpers de Inicialización
        // ------------------------------------------------------------------------

        private void InitializeCombatComponents(NPCStateContext context)
        {
            var cc = context.Config?.combatConfig;
            if (cc == null) return;

            // Targetable (Para auto-aim del jugador)
            var targetable = context.Transform.GetComponent<Targetable>();
            if (targetable == null) targetable = context.Transform.gameObject.AddComponent<Targetable>();
            targetable.enabled = true;
            targetable.isInActiveCombat = true; // ⭐ Activar para que el marker aparezca
            
            // EnemyMarker (Para sistemas de puertas/gates que esperan muerte de enemigos)
            var enemyMarker = context.Transform.GetComponent<EnemyMarker>();
            if (enemyMarker == null)
            {
                enemyMarker = context.Transform.gameObject.AddComponent<EnemyMarker>();
                context.Log("[CombatState] 🎯 EnemyMarker añadido al NPC");
            }

            // Damageable (Salud)
            var damageable = context.Transform.GetComponent<Damageable>();
            // FIX: si el NPC ya tenía este componente (re-enganche tras alejarse del combate sin
            // llegar a morir), NO se debe curar a tope más abajo — solo un combate nuevo de
            // verdad (primer Damageable de este NPC) debe arrancar con vida llena. Antes, cada
            // OnEnter volvía a poner damageable.SetMaxAndCurrent(cc.health, cc.health)
            // incondicionalmente, así que alejarse un poco de la pelea y volver a acercarse
            // "curaba" al NPC del todo y el combate empezaba de 0.
            bool isFirstCombatEngagement = damageable == null;
            if (damageable == null)
            {
                damageable = context.Transform.gameObject.AddComponent<Damageable>();
                // ✅ CRÍTICO: Establecer destroyOnDeath=false para que LifecycleHandler controle la muerte
                damageable.SetDestroyOnDeath(false);
            }
            
            // UI Barra de Vida
            var healthBar = context.Transform.GetComponent<NPCHealthBarSpawner>();
            if (healthBar == null) healthBar = context.Transform.gameObject.AddComponent<NPCHealthBarSpawner>();
            
            // Escudo (opcional según config)
            if (cc.useShield)
            {
                var shieldController = context.Transform.GetComponent<NPCShieldController>();
                if (shieldController == null)
                {
                    shieldController = context.Transform.gameObject.AddComponent<NPCShieldController>();
                }
                shieldController.ConfigureShield(cc.shieldPrefab, cc.shieldMinDuration, cc.shieldMaxDuration);
            }
            
            // Solo resetear vida y spawnear barra si NO ha sido derrotado previamente
            if (!context.WasDefeatedInCombat)
            {
                // Solo curar a tope en el primer enganche real (ver comentario arriba). Un
                // re-enganche conserva la vida con la que el NPC se quedó al salir de combate.
                if (isFirstCombatEngagement)
                    damageable.SetMaxAndCurrent(cc.health, cc.health);
                if (cc.healthBarPrefab != null) healthBar.SetHealthBarPrefab(cc.healthBarPrefab);
                healthBar.SpawnHealthBar();
                
                // UI Barra de Maná (opcional, recomendado solo debug)
                if (cc.showManaBarToPlayer && cc.manaBarPrefab != null)
                {
                    var manaBar = context.Transform.GetComponentInChildren<NPCManaBarUI>(true);
                    if (manaBar == null)
                    {
                        Object.Instantiate(cc.manaBarPrefab, context.Transform);
                    }
                }
            }

            // Desactivar interacción "Hablar" durante pelea
            var interactable = context.Transform.GetComponent<Interactable>();
            if (interactable != null) interactable.enabled = false;
        }

        private void TriggerBattleMusic(NPCStateContext context)
        {
            string musicId = context.Config?.combatConfig?.battleMusicId;
            if (!string.IsNullOrEmpty(musicId))
            {
                AudioService.Instance?.BeginBattleById(musicId);
            }
        }
    }
}
