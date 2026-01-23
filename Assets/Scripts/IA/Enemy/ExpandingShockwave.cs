using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Onda expansiva que se expande desde un punto central y daña a los objetivos que toca.
/// Típica "onda de choque" de impacto contra el suelo.
/// 
/// El objeto puede tener:
/// - Un mesh visual (anillo/cilindro) que se escalará
/// - Un Particle System que se configurará para expandirse
/// - Opcionalmente un collider trigger para detectar hits
/// 
/// El script expande desde 0 hasta maxRadius y detecta enemigos en su paso.
/// </summary>
public class ExpandingShockwave : MonoBehaviour
{
    [Header("Configuración Visual")]
    [Tooltip("Transform que se escalará (si es diferente al principal y no es particle system)")]
    [SerializeField] private Transform visualTransform;
    [Tooltip("Altura del anillo de la onda")]
    [SerializeField] private float ringHeight = 0.5f;
    [Tooltip("Grosor del anillo (diferencia entre radio externo e interno)")]
    [SerializeField] private float ringThickness = 1.5f;
    
    [Header("Configuración de Daño")]
    [SerializeField] private float damage = 35f;
    [SerializeField] private float maxRadius = 12f;
    [SerializeField] private float expandDuration = 0.8f;
    [SerializeField] private float knockbackForce = 15f;
    [SerializeField] private LayerMask targetLayers;
    
    [Header("Audio/VFX")]
    [SerializeField] private string impactSFXKey;
    
    // Estado interno
    private float _currentRadius;
    private float _previousRadius;
    private bool _initialized;
    private bool _isExpanding;
    private HashSet<GameObject> _alreadyDamaged = new HashSet<GameObject>();
    
    // Particle System (si existe)
    private ParticleSystem _particleSystem;
    private ParticleSystem.ShapeModule _shapeModule;
    private bool _useParticleSystem;
    
    // Buffer para detección
    private static readonly Collider[] HitBuffer = new Collider[32];

    void Awake()
    {
        if (!visualTransform) visualTransform = transform;
        
        // Detectar si hay un Particle System
        _particleSystem = GetComponentInChildren<ParticleSystem>();
        if (_particleSystem != null)
        {
            _useParticleSystem = true;
            _shapeModule = _particleSystem.shape;
            Debug.Log($"[ExpandingShockwave] 🌀 Usando Particle System para visual");
        }
        else
        {
            _useParticleSystem = false;
        }
    }

    void Start()
    {
        // Si no se inicializó externamente, usar valores del inspector
        if (!_initialized)
        {
            Initialize(maxRadius, expandDuration, damage, targetLayers);
        }
    }

    /// <summary>
    /// Inicializa y comienza la expansión de la onda.
    /// </summary>
    public void Initialize(float radius, float duration, float dmg, LayerMask layers)
    {
        maxRadius = radius;
        expandDuration = duration;
        damage = dmg;
        targetLayers = layers;
        _initialized = true;
        
        // Configurar visual
        if (_useParticleSystem && _particleSystem != null)
        {
            // Configurar el Particle System para que emita en forma de anillo
            _shapeModule.enabled = true;
            _shapeModule.shapeType = ParticleSystemShapeType.Circle;
            _shapeModule.radius = 0.1f; // Empezar pequeño
            
            // Asegurar que está reproduciendo
            if (!_particleSystem.isPlaying)
            {
                _particleSystem.Play();
            }
            
            Debug.Log($"[ExpandingShockwave] 🌀 Particle System configurado (radio inicial: {_shapeModule.radius})");
        }
        else
        {
            // Empezar con escala 0 para meshes
            visualTransform.localScale = Vector3.zero;
        }
        
        // Reproducir sonido
        if (!string.IsNullOrEmpty(impactSFXKey))
        {
            AudioService.Instance?.PlaySFX(impactSFXKey, worldPosition: transform.position);
        }
        
        StartCoroutine(ExpandCoroutine());
    }

    private IEnumerator ExpandCoroutine()
    {
        _isExpanding = true;
        _currentRadius = 0f;
        _previousRadius = 0f;
        
        float elapsed = 0f;
        
        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / expandDuration;
            
            // Easing: rápido al principio, desacelera al final
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            
            _previousRadius = _currentRadius;
            _currentRadius = easedT * maxRadius;
            
            // Actualizar visual según el tipo
            if (_useParticleSystem && _particleSystem != null)
            {
                // Actualizar el radio del círculo emisor de partículas
                _shapeModule.radius = _currentRadius;
                
                // Opcional: ajustar la velocidad inicial de las partículas para que se expandan
                var mainModule = _particleSystem.main;
                mainModule.startSpeed = 0.5f; // Partículas se mueven lentamente desde el anillo
            }
            else
            {
                // Escalar el visual mesh
                float scaleXZ = _currentRadius * 2f; // Diámetro
                visualTransform.localScale = new Vector3(scaleXZ, ringHeight, scaleXZ);
            }
            
            // Detectar objetivos en la zona del anillo (entre previousRadius y currentRadius)
            DetectAndDamageTargets();
            
            yield return null;
        }
        
        // Asegurar estado final
        if (_useParticleSystem && _particleSystem != null)
        {
            _shapeModule.radius = maxRadius;
        }
        else
        {
            float finalScale = maxRadius * 2f;
            visualTransform.localScale = new Vector3(finalScale, ringHeight, finalScale);
        }
        
        _isExpanding = false;
        
        // Detener particle system y destruir después de un momento
        if (_useParticleSystem && _particleSystem != null)
        {
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(_particleSystem.main.startLifetime.constantMax + 0.3f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
        
        Destroy(gameObject);
    }

    /// <summary>
    /// Detecta objetivos en la zona del anillo de expansión y les aplica daño.
    /// Solo daña cada objetivo una vez.
    /// </summary>
    private void DetectAndDamageTargets()
    {
        // Detectar todos los colliders en el radio actual
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _currentRadius, HitBuffer, targetLayers);
        
        for (int i = 0; i < hitCount; i++)
        {
            var col = HitBuffer[i];
            if (col == null) continue;
            
            GameObject target = col.gameObject;
            
            // Evitar dañar dos veces
            if (_alreadyDamaged.Contains(target)) continue;
            
            // Verificar que está dentro del anillo (entre previousRadius y currentRadius)
            float distance = Vector3.Distance(transform.position, col.transform.position);
            
            // Si está dentro del anillo actual (con algo de tolerancia)
            if (distance >= _previousRadius - ringThickness && distance <= _currentRadius + ringThickness)
            {
                _alreadyDamaged.Add(target);
                
                // Aplicar daño
                var damageable = col.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);
                    Debug.Log($"[ExpandingShockwave] 💥 Daño a {target.name}: {damage}");
                    
                    // Knockback
                    ApplyKnockback(col);
                }
                else
                {
                    // Intentar en el padre
                    damageable = col.GetComponentInParent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.TakeDamage(damage);
                        Debug.Log($"[ExpandingShockwave] 💥 Daño a padre de {target.name}: {damage}");
                        ApplyKnockback(col);
                    }
                }
            }
        }
    }

    private void ApplyKnockback(Collider col)
    {
        if (knockbackForce <= 0) return;
        
        var rb = col.GetComponent<Rigidbody>();
        if (!rb) rb = col.GetComponentInParent<Rigidbody>();
        
        if (rb)
        {
            Vector3 knockDir = (col.transform.position - transform.position).normalized;
            knockDir.y = 0.3f; // Algo de elevación
            knockDir.Normalize();
            rb.AddForce(knockDir * knockbackForce, ForceMode.Impulse);
        }
    }

    // Para debugging visual
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxRadius);
        
        if (_isExpanding)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _currentRadius);
        }
    }
}

