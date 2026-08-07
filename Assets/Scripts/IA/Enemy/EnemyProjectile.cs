using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Proyectil disparado por el boss demonio
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Daño")]
    [Tooltip("Daño usado cuando Initialize() recibe un valor negativo (ej: -1). Permite que el prefab defina su propio daño por defecto.")]
    [SerializeField] private float baseDamage = 10f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al spawnearse el proyectil")]
    [SerializeField] private string spawnSFXKey;
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al impactar/explotar")]
    [SerializeField] private string impactSFXKey;
    
    private Vector3 direction;
    private float damage;
    private bool initialized = false;

    /// <summary>Daño configurado de este proyectil (usado p. ej. por ProjectileCollisionHandler al sumar daños en un choque de hechizos).</summary>
    public float Damage => damage;
    private bool hasHit = false;
    private float _spawnTime;
    private Rigidbody rb;
    private System.Collections.Generic.List<GameObject> _attachedVfx;
    
    private Collider[] _playerDetectionBuffer = new Collider[8];
    private RaycastHit[] _sweepHitBuffer = new RaycastHit[8];
    private int _playerLayerMask;
    private float _nextProximityCheck;
    // FIX INC-027: posición del último chequeo de proximidad, usada para barrer (sweep) todo el
    // tramo recorrido desde entonces. Antes se comprobaba solo un punto instantáneo cada 0.05s;
    // con rocas rápidas (rockSpeed=18, rockRainSpeed=12) el proyectil podía avanzar más que el
    // radio de detección entre dos chequeos y "pasar por encima" del jugador sin llegar a
    // solaparse con él en ningún muestreo puntual.
    private Vector3 _lastCheckedPosition;
    private bool _hasLastCheckedPosition;

    // Pool estático para VFX de impacto — evita GC spikes cuando muchos proyectiles mueren a la vez
    private static readonly Dictionary<EntityId, Stack<GameObject>> _hitFxPool = new Dictionary<EntityId, Stack<GameObject>>(4);

    // Layers cacheados en Awake para evitar string lookups en OnTriggerEnter
    private int _enemyLayer;
    private int _transparentFXLayer;
    private int _enemyProjectileLayer;
    private int _projectileEnemyLayer;
    private int _interactableLayer;
    private int _interactHintLayer;
    private int _minimapLayer;
    private int _uiLayer;
    private int _pauseUILayer;
    private int _playerLayer;
    private int _projectileLayer;
    private int _defaultLayer;

    // AoE al impactar (configurado en tiempo de ejecución para lluvia de rocas)
    private bool _dealAoEOnImpact;
    private float _aoeRadius;
    private float _aoeDamage;
    private bool _bypassInvulnerabilityOnHit;

    void Awake()
    {
        // ✅ Configurar el collider - NO forzamos isTrigger para permitir OnCollisionEnter con el player
        var col = GetComponent<SphereCollider>();
        if (col) col.radius = 0.5f;

        _playerLayerMask = LayerMask.GetMask("Player");

        // Cachear layers para evitar string lookups en OnTriggerEnter (por frame con muchos proyectiles)
        _enemyLayer          = LayerMask.NameToLayer("Enemy");
        _transparentFXLayer  = LayerMask.NameToLayer("TransparentFX");
        _enemyProjectileLayer = LayerMask.NameToLayer("EnemyProjectile");
        _projectileEnemyLayer = LayerMask.NameToLayer("ProjectileEnemy");
        _interactableLayer   = LayerMask.NameToLayer("Interactable");
        _interactHintLayer   = LayerMask.NameToLayer("InteractHint");
        _minimapLayer        = LayerMask.NameToLayer("Minimap");
        _uiLayer             = LayerMask.NameToLayer("UI");
        _pauseUILayer        = LayerMask.NameToLayer("PauseUI");
        _playerLayer         = LayerMask.NameToLayer("Player");
        _projectileLayer     = LayerMask.NameToLayer("Projectile");
        _defaultLayer        = LayerMask.NameToLayer("Default");

        rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true;
        }

    }

    public void Initialize(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        // FIX INC-024/027: un valor negativo (ej: -1) es el sentinel "usa el daño del prefab".
        // Antes no existía este fallback y el daño quedaba en -1, por lo que
        // PlayerHealthSystem.TakeDamage() lo descartaba (damageAmount <= 0f) y la roca no dañaba nunca.
        damage = dmg >= 0f ? dmg : baseDamage;
        initialized = true;
        _spawnTime = Time.time;
        _hasLastCheckedPosition = false; // reinicia el sweep de proximidad (INC-027)

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnemyProjectile] Inicializado — daño: {damage}, velocidad: {speed}");
#endif
        
        // 🔊 Reproducir SFX de spawn
        if (!string.IsNullOrEmpty(spawnSFXKey))
        {
            AudioService.Instance?.PlaySFX(spawnSFXKey, worldPosition: transform.position);
        }
        
        // ✅ Aplicar velocidad inicial usando física
        if (rb)
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Destruir después del tiempo de vida
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// Activa el daño de área al aterrizar. Usado para rocas de lluvia del Golem.
    /// El AoE ignora iframes pero respeta el escudo del jugador.
    /// </summary>
    public void ConfigureAoE(float radius, float aoeDmg)
    {
        _dealAoEOnImpact = true;
        _aoeRadius = radius;
        _aoeDamage = aoeDmg;
        _bypassInvulnerabilityOnHit = true;
    }

    private void ApplyAoEImpact()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _aoeRadius, _playerDetectionBuffer, _playerLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _playerDetectionBuffer[i];

            var shield = hit.GetComponentInParent<PlayerShieldController>();
            if (shield != null && shield.IsDefending)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[EnemyProjectile] 🛡️ AoE de lluvia bloqueado por escudo");
#endif
                continue;
            }

            var playerHealth = hit.GetComponent<PlayerHealthSystem>() ?? hit.GetComponentInParent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EnemyProjectile] 💥 AoE lluvia: {_aoeDamage} daño (ignora iframes)");
#endif
                playerHealth.TakeDamage(_aoeDamage, ignoreInvulnerability: true);
                return;
            }
        }
    }

    // Permite registrar VFX asociados al proyectil para que se destruyan cuando este desaparezca
    public void RegisterAttachedVFX(GameObject vfx, bool parentToProjectile = true)
    {
        if (vfx == null) return;
        _attachedVfx ??= new System.Collections.Generic.List<GameObject>();
        _attachedVfx.Add(vfx);
        if (parentToProjectile)
        {
            try { vfx.transform.SetParent(transform, true); } catch { }
        }
    }

    void FixedUpdate()
    {
        // ✅ Usar SOLO física para movimiento (detección de colisiones garantizada)
        if (!initialized || hasHit) return;

        // Mantener la velocidad constante
        if (rb)
        {
            rb.linearVelocity = direction * speed;
        }

        // ✅ Detección activa del player throttleada a 20/seg para reducir OverlapSphere por proyectil.
        // CharacterController no dispara OnTriggerEnter de forma confiable, de ahí el fallback.
        if (Time.time >= _nextProximityCheck)
        {
            _nextProximityCheck = Time.time + 0.05f;
            CheckPlayerProximity();
        }
    }
    
    /// <summary>
    /// Detecta si el proyectil está cerca del player o aliados por distancia directa.
    /// Necesario porque CharacterController no dispara OnTriggerEnter de forma confiable.
    /// También respeta el escudo del jugador y detecta compañeros de party.
    /// </summary>
    private void CheckPlayerProximity()
    {
        if (hasHit) return;

        const float detectionRadius = 0.8f;
        Vector3 currentPos = transform.position;

        // FIX INC-027: en lugar de comprobar solo el punto actual, barremos (SphereCast) desde la
        // última posición chequeada hasta la actual. Así, aunque el proyectil avance más que el
        // radio de detección entre dos chequeos (rocas rápidas), no puede "saltarse" al jugador.
        int hitCount;
        if (_hasLastCheckedPosition)
        {
            Vector3 segment = currentPos - _lastCheckedPosition;
            float segmentLength = segment.magnitude;

            if (segmentLength > 0.001f)
            {
                hitCount = Physics.SphereCastNonAlloc(_lastCheckedPosition, detectionRadius, segment / segmentLength,
                    _sweepHitBuffer, segmentLength, _playerLayerMask);

                for (int i = 0; i < hitCount; i++)
                {
                    if (TryResolvePlayerHit(_sweepHitBuffer[i].collider))
                        return;
                }
            }
            else
            {
                hitCount = Physics.OverlapSphereNonAlloc(currentPos, detectionRadius, _playerDetectionBuffer, _playerLayerMask);
                for (int i = 0; i < hitCount; i++)
                {
                    if (TryResolvePlayerHit(_playerDetectionBuffer[i]))
                        return;
                }
            }
        }
        else
        {
            hitCount = Physics.OverlapSphereNonAlloc(currentPos, detectionRadius, _playerDetectionBuffer, _playerLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                if (TryResolvePlayerHit(_playerDetectionBuffer[i]))
                    return;
            }
        }

        _lastCheckedPosition = currentPos;
        _hasLastCheckedPosition = true;
    }

    /// <summary>
    /// Evalúa un collider detectado (por proximidad o por sweep) y aplica daño/bloqueo si es el jugador.
    /// Devuelve true si el proyectil impactó y ya fue destruido.
    /// </summary>
    private bool TryResolvePlayerHit(Collider hit)
    {
        if (hit == null) return false;

        var playerHealth = hit.GetComponent<PlayerHealthSystem>() ?? hit.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            // Respetar escudo: si el jugador está defendiendo, bloquear sin dañar
            var shield = hit.GetComponentInParent<PlayerShieldController>();
            if (shield != null && shield.IsDefending)
            {
                hasHit = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[EnemyProjectile] 🛡️ Bloqueado por escudo (proximity)");
#endif
                DestroyProjectile();
                return true;
            }

            hasHit = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnemyProjectile] 🎯 Impacto JUGADOR por proximidad: {damage} daño");
#endif
            playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
            DestroyProjectile();
            return true;
        }

        Transform checkTransform = hit.transform;
        for (int j = 0; j < 5 && checkTransform != null; j++)
        {
            if (checkTransform.CompareTag("Player"))
            {
                var shield = checkTransform.GetComponentInChildren<PlayerShieldController>();
                if (shield != null && shield.IsDefending)
                {
                    hasHit = true;
                    DestroyProjectile();
                    return true;
                }
                hasHit = true;
                ApplyDamage(checkTransform.gameObject);
                DestroyProjectile();
                return true;
            }
            checkTransform = checkTransform.parent;
        }

        return false;
    }

    // ✅ OnTriggerEnter: PRINCIPAL - Para colisiones con triggers (jugador, obstáculos)
    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        // Grace period: ignorar colisiones inmediatas al spawnear (evita explotar dentro del lanzador)
        if (Time.time - _spawnTime < 0.15f) return;
        
        // Ignorar enemigos y el boss (proyectil de enemy no debe dañar a otros enemies)
        // Nota: "Boss" es un Tag, no un Layer → usar CompareTag, no LayerMask
        int otherLayer = other.gameObject.layer;
        if (other.CompareTag("Enemy") || other.CompareTag("Boss") || otherLayer == _enemyLayer)
            return;

        // Ignorar el arena del boss (layer TransparentFX)
        if (otherLayer == _transparentFXLayer)
            return;

        // Ignorar layers que no deben detener el proyectil
        if (otherLayer == _enemyProjectileLayer || otherLayer == _projectileEnemyLayer ||
            otherLayer == _interactableLayer    || otherLayer == _interactHintLayer    ||
            otherLayer == _minimapLayer         || otherLayer == _uiLayer              ||
            otherLayer == _pauseUILayer)
        {
            return;
        }
        
        // ✅ PRIORIDAD 1: Si impacta contra el escudo del jugador
        if (other.GetComponent<PlayerShieldController.ShieldMarker>() != null)
        {
            hasHit = true;
            DestroyProjectile();
            return;
        }

        if (otherLayer == _playerLayer)
        {
            hasHit = true;
            ApplyDamage(other.gameObject);
            DestroyProjectile();
            return;
        }

        Transform checkTransform = other.transform;
        for (int i = 0; i < 5; i++)
        {
            if (checkTransform.CompareTag("Player"))
            {
                hasHit = true;
                ApplyDamage(checkTransform.gameObject);
                DestroyProjectile();
                return;
            }

            if (checkTransform.parent != null)
                checkTransform = checkTransform.parent;
            else
                break;
        }

        var playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            DestroyProjectile();
            return;
        }

        // Aliados NPC del party: el proyectil los atraviesa sin dañarlos (solo el player recibe daño)
        var partyMember = other.GetComponentInParent<Game.NPC.NPCPartyMember>();
        if (partyMember != null && partyMember.IsInParty)
            return;

        // ✅ Colisión con proyectiles del jugador (layer "Projectile")
        if (otherLayer == _projectileLayer)
        {
            hasHit = true;
            ProjectileCollisionHandler.HandleCollision(other.gameObject, gameObject, other.ClosestPoint(transform.position));
            return;
        }

        if (otherLayer == _defaultLayer)
        {
            if (other.isTrigger) return;
            hasHit = true;
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
            return;
        }

        if (!other.isTrigger)
        {
            hasHit = true;
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
        }
    }
    
    // OnCollisionEnter: Fallback para colisiones físicas (si el collider NO es trigger)
    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        if (Time.time - _spawnTime < 0.15f) return;

        Collider other = collision.collider;
        OnTriggerEnter(other);
    }

    private void ApplyDamage(GameObject target)
    {
        // GetComponentInParent como fallback por si el collider que recibió el hit es un hijo del root
        var playerHealth = target.GetComponent<PlayerHealthSystem>()
                           ?? target.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
            return;
        }

        var damageable = target.GetComponent<IDamageable>()
                         ?? target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            return;
        }

        Debug.LogWarning($"[EnemyProjectile] No se pudo aplicar daño a {target.name}");
    }

    public void DestroyProjectile()
    {
        // Detener movimiento antes de destruir
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Destruir cualquier VFX registrado/adjunto
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }

        // Efecto visual de impacto (pooled para evitar GC spikes con múltiples proyectiles)
        SpawnHitEffect();
        
        // 🔊 Reproducir SFX de impacto/explosión
        if (!string.IsNullOrEmpty(impactSFXKey))
        {
            AudioService.Instance?.PlaySFX(impactSFXKey, worldPosition: transform.position);
        }

        // Destruir el proyectil
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Salvaguarda: si se destruye por lifetime o desde fuera, limpiar VFX registrados
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }
    }

    private void SpawnHitEffect()
    {
        if (!hitEffectPrefab) return;

        EntityId id = hitEffectPrefab.GetEntityId();
        if (!_hitFxPool.TryGetValue(id, out var stack))
            _hitFxPool[id] = stack = new Stack<GameObject>(8);

        GameObject fx;
        if (stack.Count > 0 && (fx = stack.Pop()) != null)
        {
            fx.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            fx.SetActive(true);
        }
        else
        {
            fx = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        var returner = fx.GetComponent<HitFxAutoReturn>();
        if (returner == null) returner = fx.AddComponent<HitFxAutoReturn>();
        returner.Init(id, 2f);
    }

    // Devuelve el VFX de impacto al pool estático tras reproducirse
    private sealed class HitFxAutoReturn : MonoBehaviour
    {
        private EntityId _poolKey;

        public void Init(EntityId key, float delay)
        {
            _poolKey = key;
            CancelInvoke(nameof(ReturnToPool));
            Invoke(nameof(ReturnToPool), delay);
        }

        private void ReturnToPool()
        {
            if (!_hitFxPool.TryGetValue(_poolKey, out var stack))
            {
                Destroy(gameObject);
                return;
            }
            if (stack.Count < 8)
            {
                gameObject.SetActive(false);
                stack.Push(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
