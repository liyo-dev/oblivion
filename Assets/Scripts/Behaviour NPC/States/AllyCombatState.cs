using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;
using System.Collections;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de combate para NPCs aliados/compañeros.
    /// Se queda atacando al target del jugador mientras esté dentro de un radio.
    /// Solo se mueve si se aleja demasiado del jugador.
    /// </summary>
    public class AllyCombatState : NPCStateBase
    {
        public override string StateName => "AllyCombat";

        private Transform _currentTarget;
        private NPCPartyMember _partyMember;
        private Transform _player;
        private PlayerTargeting _playerTargeting;
        
        // Cache de layers para daño
        private LayerMask _damageLayers;
        private bool _damageLayersInitialized;
        
        // Cooldowns individuales para cada hechizo
        private float _spellLeftTimer;
        private float _spellRightTimer;
        private float _spellSpecialTimer;
        
        // Flag para evitar lanzar mientras hay un cast en progreso
        private bool _isCasting;
        
        // Radio de combate - si está dentro de este radio del jugador, ataca libremente
        private const float COMBAT_RADIUS = 12f;
        // Si se aleja más de esto, DEBE moverse hacia el jugador
        private const float MAX_DISTANCE_FROM_PLAYER = 15f;
        
        private const float LOSE_TARGET_TIME = 2f;
        private float _timeSinceLastHadTarget;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            context.Log("[AllyCombatState] ⚔️ Compañero entrando en combate aliado");
            context.IsInCombat = true;
            
            _partyMember = context.Transform.GetComponent<NPCPartyMember>();
            _spellLeftTimer = 0f;
            _spellRightTimer = 0f;
            _spellSpecialTimer = 0f;
            _timeSinceLastHadTarget = 0f;
            _isCasting = false;
            
            // Obtener referencia al jugador y su sistema de targeting
            FindPlayerAndTargeting();
            
            // Obtener las layers de daño
            InitializeDamageLayers();
            
            // Activar modo batalla en el animator
            context.Animator?.SetBattleMode(true);
            
            // Parar movimiento al entrar - atacar desde donde está
            StopMovement(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            // Asegurar referencias al jugador
            if (_player == null || _playerTargeting == null)
            {
                FindPlayerAndTargeting();
            }
            
            // Sincronizar target con el jugador
            SyncTargetWithPlayer(context);
            
            // Calcular distancia al jugador
            float distanceToPlayer = _player != null 
                ? Vector3.Distance(context.Transform.position, _player.position) 
                : 0f;
            
            // ✅ LÓGICA SIMPLE:
            // - Si está MUY lejos del jugador → moverse hacia él
            // - Si está dentro del radio → quedarse quieto y atacar
            
            if (distanceToPlayer > MAX_DISTANCE_FROM_PLAYER)
            {
                // Demasiado lejos, correr hacia el jugador
                MoveTowardsPlayer(context);
                UpdateMovementAnimation(context);
                return;
            }
            
            // Dentro del radio de combate - QUEDARSE QUIETO y atacar
            StopMovement(context);
            
            // Si no hay target, esperar
            if (_currentTarget == null)
            {
                _timeSinceLastHadTarget += Time.deltaTime;
                UpdateMovementAnimation(context);
                return;
            }
            
            _timeSinceLastHadTarget = 0f;
            
            // Rotar hacia el enemigo
            RotateTowardsTarget(context);
            
            // Intentar atacar
            TryAttack(context);
            
            // Actualizar animación (idle de batalla)
            UpdateMovementAnimation(context);
        }

        public override void OnExit(NPCStateContext context)
        {
            context.IsInCombat = false;
            context.Animator?.SetBattleMode(false);
            StopMovement(context);
            
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Cinemática tiene prioridad
            if (context.IsInCinematic) return new CinematicState();
            
            // 2. Si fue derrotado
            if (context.WasDefeatedInCombat) return new DeadState();
            
            // 3. Si ya no está en combate (forzado externamente)
            if (!context.IsInCombat) return new FollowPlayerState(_partyMember);
            
            // 4. Si no hay target por mucho tiempo Y no hay enemigos en combate
            if (_currentTarget == null && _timeSinceLastHadTarget > LOSE_TARGET_TIME)
            {
                if (ActiveCombatRegistry.Count == 0)
                {
                    context.Log("[AllyCombatState] 🏳️ Sin enemigos, fin del combate");
                    return new FollowPlayerState(_partyMember);
                }
            }
            
            return null;
        }

        #region Private Methods
        
        private void FindPlayerAndTargeting()
        {
            if (_player == null)
            {
                var playerGO = PlayerService.Player;
                if (playerGO != null)
                {
                    _player = playerGO.transform;
                    _playerTargeting = playerGO.GetComponent<PlayerTargeting>();
                }
            }
        }
        
        private void InitializeDamageLayers()
        {
            if (_damageLayersInitialized) return;
            
            // Intentar obtener del MagicProjectileSpawner del jugador
            if (_player != null)
            {
                var spawner = _player.GetComponent<MagicProjectileSpawner>();
                if (spawner != null)
                {
                    var settingsField = spawner.GetType().GetField("projectileSettings", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (settingsField != null)
                    {
                        var settings = settingsField.GetValue(spawner) as ProjectileSettingsSO;
                        if (settings != null)
                        {
                            _damageLayers = settings.damageableLayers;
                            _damageLayersInitialized = true;
                            Debug.Log($"[AllyCombatState] ✅ Layers de daño obtenidas: {_damageLayers.value}");
                            return;
                        }
                    }
                }
            }
            
            // Fallback
            _damageLayers = LayerMask.GetMask("Enemy", "Boss");
            _damageLayersInitialized = true;
            Debug.Log($"[AllyCombatState] ⚠️ Usando layers fallback: {_damageLayers.value}");
        }
        
        private void SyncTargetWithPlayer(NPCStateContext context)
        {
            Transform playerTarget = _playerTargeting?.CurrentTarget;
            
            // Si el jugador tiene un target, usarlo
            if (playerTarget != null)
            {
                if (_currentTarget != playerTarget)
                {
                    _currentTarget = playerTarget;
                    context.Log($"[AllyCombatState] 🎯 Target: {playerTarget.name}");
                }
                return;
            }
            
            // El jugador NO tiene target, pero Estela puede mantener el suyo si sigue siendo válido
            if (_currentTarget != null)
            {
                // Verificar que el target siga vivo
                var damageable = _currentTarget.GetComponent<Damageable>();
                if (damageable != null && damageable.Current <= 0)
                {
                    context.Log("[AllyCombatState] 🎯 Target murió");
                    _currentTarget = null;
                    return;
                }
                
                // Verificar que el target siga en rango
                float dist = Vector3.Distance(context.Transform.position, _currentTarget.position);
                if (dist > 25f) // Rango máximo de combate
                {
                    context.Log("[AllyCombatState] 🎯 Target fuera de rango");
                    _currentTarget = null;
                    return;
                }
                
                // Target sigue siendo válido, mantenerlo
            }
        }
        
        private void MoveTowardsPlayer(NPCStateContext context)
        {
            if (_player == null || !IsAgentValid(context)) return;
            
            Vector3 targetPos = _player.position;
            context.Agent.speed = context.Config?.runSpeed ?? 4f;
            SetDestination(context, targetPos);
        }

        private void RotateTowardsTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            
            Vector3 direction = (_currentTarget.position - context.Transform.position).normalized;
            direction.y = 0;
            
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float rotSpeed = context.Config?.rotationSpeed ?? 360f; // Rotación rápida
                context.Transform.rotation = Quaternion.RotateTowards(
                    context.Transform.rotation,
                    targetRotation,
                    rotSpeed * Time.deltaTime
                );
            }
        }

        private void TryAttack(NPCStateContext context)
        {
            // Si ya está casteando, no hacer nada
            if (_isCasting) return;
            
            // Actualizar cooldowns SIEMPRE (incluso si no ataca)
            _spellLeftTimer += Time.deltaTime;
            _spellRightTimer += Time.deltaTime;
            _spellSpecialTimer += Time.deltaTime;
            
            var partyConfig = _partyMember?.PartyConfig;
            if (partyConfig == null) return;
            
            // Verificar que está mirando al target (ángulo < 45 grados - más permisivo)
            if (!IsFacingTarget(context, 45f)) return;
            
            // Seleccionar hechizo disponible
            int spellIndex = SelectAvailableSpell(partyConfig);
            if (spellIndex == -1) return; // Todos en cooldown
            
            MagicSpellSO spell = partyConfig.GetSpell(spellIndex);
            if (spell == null) return;
            
            // Verificar distancia de ataque
            if (_currentTarget == null) return;
            float distToTarget = Vector3.Distance(context.Transform.position, _currentTarget.position);
            if (distToTarget > (partyConfig.maxAttackDistance > 0 ? partyConfig.maxAttackDistance : 20f)) return;
            
            // Resetear cooldown del hechizo usado
            switch (spellIndex)
            {
                case 0: _spellLeftTimer = 0f; break;
                case 1: _spellRightTimer = 0f; break;
                case 2: _spellSpecialTimer = 0f; break;
            }
            
            CastSpell(context, spell, spellIndex);
        }
        
        private bool IsFacingTarget(NPCStateContext context, float maxAngle)
        {
            if (_currentTarget == null) return false;
            
            Vector3 toTarget = (_currentTarget.position - context.Transform.position);
            toTarget.y = 0;
            
            Vector3 forward = context.Transform.forward;
            forward.y = 0;
            
            float angle = Vector3.Angle(forward, toTarget);
            return angle < maxAngle;
        }
        
        private int SelectAvailableSpell(NPCPartyConfig config)
        {
            // Verificar qué hechizos están disponibles (fuera de cooldown)
            bool leftOk = config.spellLeft != null && _spellLeftTimer >= config.spellLeft.cooldown;
            bool rightOk = config.spellRight != null && _spellRightTimer >= config.spellRight.cooldown;
            bool specialOk = config.spellSpecial != null && _spellSpecialTimer >= config.spellSpecial.cooldown;
            
            // Construir lista de disponibles
            var available = new System.Collections.Generic.List<int>();
            if (leftOk) available.Add(0);
            if (rightOk) available.Add(1);
            if (specialOk) available.Add(2);
            
            if (available.Count == 0) return -1;
            
            // Seleccionar uno aleatorio de los disponibles
            return available[Random.Range(0, available.Count)];
        }
        
        private void CastSpell(NPCStateContext context, MagicSpellSO spell, int spellIndex)
        {
            if (spell == null || spell.prefab == null || _currentTarget == null) return;
            
            // Marcar que estamos casteando
            _isCasting = true;
            
            // Animación INMEDIATAMENTE
            switch (spellIndex)
            {
                case 0: context.Animator?.PlaySpellCastLeft(); break;
                case 1: context.Animator?.PlaySpellCastRight(); break;
                case 2: context.Animator?.PlaySpellCastSpecial(); break;
            }
            
            // ⭐ SFX INMEDIATAMENTE (antes del delay, igual que el jugador)
            if (!string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
            {
                AudioService.Instance.PlaySFX(spell.castSFXKey);
            }
            
            // Iniciar corrutina para el delay y spawn
            var mono = context.Transform.GetComponent<MonoBehaviour>();
            if (mono != null)
            {
                mono.StartCoroutine(Co_SpawnProjectileAfterDelay(context, spell));
            }
            else
            {
                // Fallback sin delay si no hay MonoBehaviour
                SpawnProjectile(context, spell);
                _isCasting = false;
            }
        }
        
        /// <summary>
        /// Corrutina que espera el castDelaySeconds antes de instanciar el proyectil.
        /// </summary>
        private IEnumerator Co_SpawnProjectileAfterDelay(NPCStateContext context, MagicSpellSO spell)
        {
            // Esperar el delay de animación
            float delay = Mathf.Max(0f, spell.castDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            
            // Verificar que el target siga siendo válido después del delay
            if (_currentTarget != null)
            {
                // Instanciar el proyectil
                SpawnProjectile(context, spell);
            }
            
            // Terminar el cast
            _isCasting = false;
        }
        
        /// <summary>
        /// Instancia el proyectil (igual que LaunchProjectile del jugador).
        /// </summary>
        private void SpawnProjectile(NPCStateContext context, MagicSpellSO spell)
        {
            if (spell == null || spell.prefab == null || _currentTarget == null) return;
            
            // === Dirección hacia el target ===
            Transform origin = context.Transform;
            Vector3 targetPos = _currentTarget.position + Vector3.up * 1f;
            Vector3 dir = (targetPos - origin.position).normalized;
            
            // Respetar flattenDirection del spell
            if (spell.flattenDirection)
            {
                dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            }
            if (dir.sqrMagnitude < 0.001f) dir = origin.forward;
            
            // === Posición de spawn (igual que el jugador) ===
            Vector3 spawnPos = origin.position + dir * spell.forwardOffset;
            
            // Aplicar offset de posición
            if (spell.positionOffset != Vector3.zero)
            {
                // Y es siempre arriba/abajo (espacio mundial)
                spawnPos.y += spell.positionOffset.y;
                
                // X (derecha) y Z (adelante) en espacio local del caster
                if (spell.positionOffset.x != 0f || spell.positionOffset.z != 0f)
                {
                    Vector3 localOffset = new Vector3(spell.positionOffset.x, 0f, spell.positionOffset.z);
                    spawnPos += origin.TransformDirection(localOffset);
                }
            }
            
            Quaternion spawnRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);
            
            // === Spawn VFX ===
            if (spell.spawnVFX != null)
            {
                var fx = Object.Instantiate(spell.spawnVFX, spawnPos, spawnRot);
                if (spell.useScaleOverride) fx.transform.localScale = spell.scaleOverride;
                float destroyTime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f;
                Object.Destroy(fx, destroyTime);
            }
            
            // === Instanciar proyectil ===
            GameObject go = Object.Instantiate(spell.prefab, spawnPos, spawnRot);
            if (spell.useScaleOverride) go.transform.localScale = spell.scaleOverride;
            
            // === Ignorar colisiones con el caster (Estela) y el jugador ===
            IgnoreCollisionsBetween(go, context.Transform.gameObject);
            
            // === Configurar MagicProjectile (igual que el jugador) ===
            if (go.TryGetComponent<MagicProjectile>(out var mp))
            {
                var cfg = new MagicProjectile.ProjectileConfig
                {
                    damage = spell.damage,
                    aoeRadius = spell.aoeRadius,
                    knockbackForce = spell.knockbackForce,
                    hitLayers = _damageLayers,
                    collisionLayers = _damageLayers,
                    destroyOnHit = spell.destroyOnHit,
                    lifeTime = spell.lifeTime,
                    maxRange = spell.maxRange,
                    initialSpeed = spell.initialSpeed,
                    useGravity = spell.useGravity,
                    impactVFX = spell.impactVFX,
                    despawnVFX = spell.despawnVFX,
                    vfxLifetime = spell.vfxLifetime,
                    impactSFXKey = spell.impactSFXKey,
                    element = spell.element
                };
                mp.Configure(cfg, context.Transform.gameObject);
            }
            
            // === Configurar Rigidbody (EXACTAMENTE como el jugador) ===
            if (go.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.useGravity = spell.useGravity;
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.angularVelocity = Vector3.zero;
                rb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed);
            }
            
            Debug.Log($"[AllyCombatState] 🔥 {spell.displayName} → {_currentTarget.name} | dmg:{spell.damage} speed:{spell.initialSpeed}");
        }
        
        private void IgnoreCollisionsBetween(GameObject projectile, GameObject caster)
        {
            if (projectile == null || caster == null) return;
            
            var projCols = projectile.GetComponentsInChildren<Collider>(true);
            var casterCols = caster.GetComponentsInChildren<Collider>(true);
            
            foreach (var pc in projCols)
            {
                if (pc == null) continue;
                foreach (var cc in casterCols)
                {
                    if (cc != null)
                        Physics.IgnoreCollision(pc, cc, true);
                }
            }
            
            // También ignorar colisiones con el jugador para que no bloquee los proyectiles aliados
            if (_player != null)
            {
                var playerCols = _player.GetComponentsInChildren<Collider>(true);
                foreach (var pc in projCols)
                {
                    if (pc == null) continue;
                    foreach (var playerCol in playerCols)
                    {
                        if (playerCol != null)
                            Physics.IgnoreCollision(pc, playerCol, true);
                    }
                }
            }
        }
        
        #endregion
    }
}
