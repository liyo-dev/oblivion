using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;
using Game.NPC;

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
                    
                    // Tácticas & Defensa
                    difficultyLevel = 0.7f, // Valor por defecto equilibrado
                    useShield = cc.useShield,
                    shieldDuration = cc.shieldMaxDuration,
                    shieldCooldown = cc.shieldCooldown,
                    coverLayerMask = cc.coverLayerMask,
                    coverSearchRadius = cc.coverSearchRadius,
                    dodgeDistance = 3f,
                    
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
                        context.IsInCombat = false; // Esto disparará la transición en CheckTransitions
                    }
                }
            }
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
            if (damageable == null)
            {
                damageable = context.Transform.gameObject.AddComponent<Damageable>();
                // ✅ CRÍTICO: Establecer destroyOnDeath=false para que LifecycleHandler controle la muerte
                damageable.SetDestroyOnDeath(false);
            }
            
            // UI Barra de Vida
            var healthBar = context.Transform.GetComponent<NPCHealthBarSpawner>();
            if (healthBar == null) healthBar = context.Transform.gameObject.AddComponent<NPCHealthBarSpawner>();
            
            // Solo resetear vida y spawnear barra si NO ha sido derrotado previamente
            if (!context.WasDefeatedInCombat)
            {
                damageable.SetMaxAndCurrent(cc.health, cc.health);
                if (cc.healthBarPrefab != null) healthBar.SetHealthBarPrefab(cc.healthBarPrefab);
                healthBar.SpawnHealthBar();
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