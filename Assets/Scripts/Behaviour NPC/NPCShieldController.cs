using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador de escudo para NPCs - Similar al PlayerShieldController
/// Permite a los NPCs defenderse de proyectiles del jugador
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCShieldController : MonoBehaviour
{
    [Header("Escudo")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private Vector3 shieldOffset = Vector3.zero;

    [Header("Animaciones")]
    [SerializeField] private string defendAnimation = "Defend_NoWeapon";
    [SerializeField] private string defendHitAnimation = "DefendHit_NoWeapon";
    [SerializeField] private int upperBodyLayer = 1;
    [SerializeField] private float upperBodyTransitionDuration = 0.1f;
    [SerializeField, Min(0f)] private float hitFeedbackDuration = 0.3f;

    [Header("Colisiones a bloquear")]
    [SerializeField] private string[] blockLayerNames = { "Projectile", "ProjectilePlayer", "MagicProjectile" };

    [Header("Duración")]
    [SerializeField, Min(0f)] private float minDefendDuration = 2f;  // Mínimo 2s defendiendo
    [SerializeField, Min(0f)] private float maxDefendDuration = 5f;  // Máximo 5s defendiendo

    private Animator _animator;
    private GameObject _shieldInstance;
    private readonly HashSet<int> _blockedLayers = new();
    private bool _isDefending;
    private float _defendStartTime;
    private float _targetDefendDuration;
    private float _originalUpperBodyWeight;

    public bool IsDefending => _isDefending;

    /// <summary>
    /// Permite configurar el escudo desde NPCCombatConfig en runtime.
    /// </summary>
    public void ConfigureShield(GameObject prefab, float minDuration, float maxDuration)
    {
        shieldPrefab = prefab != null ? prefab : shieldPrefab;
        minDefendDuration = Mathf.Max(0f, minDuration);
        maxDefendDuration = Mathf.Max(minDefendDuration, maxDuration);
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        CacheUpperBodyWeight();
        CacheBlockedLayers();
    }

    void OnDisable()
    {
        StopDefending();
    }

    void Update()
    {
        // Auto-desactivar defensa después de la duración
        if (_isDefending && Time.time >= _defendStartTime + _targetDefendDuration)
        {
            Debug.Log($"[NPCShieldController] 🛡️ Duración de defensa completada ({_targetDefendDuration:F1}s)");
            StopDefending();
        }
    }

    /// <summary>
    /// Inicia la defensa por una duración aleatoria
    /// </summary>
    public void StartDefending()
    {
        StartDefending(Random.Range(minDefendDuration, maxDefendDuration));
    }

    /// <summary>
    /// Inicia la defensa por una duración específica
    /// </summary>
    public void StartDefending(float duration)
    {
        if (_isDefending)
        {
            Debug.Log($"[NPCShieldController] ⚠️ Ya está defendiendo");
            return;
        }

        _isDefending = true;
        _defendStartTime = Time.time;
        _targetDefendDuration = Mathf.Max(0f, duration);

        ActivateShield();
        SetUpperBodyWeight(1f);
        PlayUpperBodyAnimation(defendAnimation);

        Debug.Log($"[NPCShieldController] 🛡️ DEFENSA ACTIVADA - Duración: {_targetDefendDuration:F1}s");
    }

    /// <summary>
    /// Detiene la defensa inmediatamente
    /// </summary>
    public void StopDefending()
    {
        if (!_isDefending)
            return;

        _isDefending = false;
        DeactivateShield();
        ResetUpperBodyWeight();

        Debug.Log($"[NPCShieldController] 🛡️ DEFENSA DESACTIVADA");
    }

    private void ActivateShield()
    {
        if (_shieldInstance == null)
        {
            GameObject resolvedShieldPrefab = ResolveShieldPrefab(shieldPrefab);
            if (resolvedShieldPrefab == null)
            {
                Debug.LogWarning("[NPCShieldController] ⚠️ shieldPrefab no asignado, no se puede instanciar el escudo.");
                return;
            }

            Transform parent = shieldAnchor != null ? shieldAnchor : transform;
            _shieldInstance = Instantiate(resolvedShieldPrefab, parent);
            _shieldInstance.transform.localPosition = shieldOffset;
            _shieldInstance.transform.localRotation = Quaternion.identity;

            ConfigureShieldDetector(_shieldInstance);

            Debug.Log($"[NPCShieldController] ✅ Escudo instanciado: '{resolvedShieldPrefab.name}' en {_shieldInstance.transform.position}");
        }
    }

    private void DeactivateShield()
    {
        if (_shieldInstance != null)
        {
            Destroy(_shieldInstance);
            _shieldInstance = null;
            Debug.Log($"[NPCShieldController] ✅ Escudo destruido");
        }
    }

    private void PlayUpperBodyAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName)) return;
        if (_animator.runtimeAnimatorController == null) return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
        {
            _animator.CrossFade(animationName, upperBodyTransitionDuration, upperBodyLayer);
        }
        else
        {
            _animator.Play(animationName);
        }
    }

    private void CacheUpperBodyWeight()
    {
        _originalUpperBodyWeight = 0f;

        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _originalUpperBodyWeight = _animator.GetLayerWeight(upperBodyLayer);
    }

    private void SetUpperBodyWeight(float weight)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _animator.SetLayerWeight(upperBodyLayer, weight);
    }

    private void ResetUpperBodyWeight()
    {
        SetUpperBodyWeight(_originalUpperBodyWeight);
    }

    private void CacheBlockedLayers()
    {
        _blockedLayers.Clear();
        foreach (var name in blockLayerNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0)
            {
                _blockedLayers.Add(layer);
                Debug.Log($"[NPCShieldController] ✅ Bloqueando capa: {name} (layer {layer})");
            }
            else
            {
                Debug.Log($"[NPCShieldController] ℹ️ Capa '{name}' no existe en este proyecto, se omite.");
            }
        }
    }

    private GameObject ResolveShieldPrefab(GameObject configuredPrefab)
    {
        if (configuredPrefab == null)
            return null;

        // Compatibilidad: algunos assets asignaron un prefab "wrapper" que contiene otro NPCShieldController.
        // En ese caso usamos directamente el prefab visual interno para que el escudo se vea.
        var nestedController = configuredPrefab.GetComponent<NPCShieldController>();
        if (nestedController != null && nestedController != this && nestedController.shieldPrefab != null)
        {
            return nestedController.shieldPrefab;
        }

        return configuredPrefab;
    }

    private void ConfigureShieldDetector(GameObject shield)
    {
        if (!shield.TryGetComponent<Collider>(out var collider))
        {
            collider = shield.AddComponent<SphereCollider>();
            collider.isTrigger = true;
        }
        else
        {
            collider.isTrigger = true;
        }

        // Marcador público para identificar el escudo
        if (!shield.TryGetComponent<NPCShieldMarker>(out var _))
            shield.AddComponent<NPCShieldMarker>();

        var detector = shield.GetComponent<NPCShieldHitDetector>() ?? shield.AddComponent<NPCShieldHitDetector>();
        detector.Initialize(this, _blockedLayers);
    }

    internal void OnShieldHit()
    {
        // Reproduce animación de impacto breve en la capa superior y vuelve a defensa
        if (string.IsNullOrEmpty(defendHitAnimation)) return;
        
        PlayUpperBodyAnimation(defendHitAnimation);
        
        if (hitFeedbackDuration > 0f)
        {
            // Reinicia el temporizador ante impactos consecutivos
            CancelInvoke(nameof(ReturnToDefendAnimation));
            Invoke(nameof(ReturnToDefendAnimation), hitFeedbackDuration);
        }

        Debug.Log($"[NPCShieldController] 💥 Escudo impactado!");
    }

    private void ReturnToDefendAnimation()
    {
        if (_isDefending)
            PlayUpperBodyAnimation(defendAnimation);
    }

    // ========== NESTED CLASSES ==========

    /// <summary>
    /// Detector de impactos en el escudo del NPC
    /// </summary>
    private class NPCShieldHitDetector : MonoBehaviour
    {
        private NPCShieldController _owner;
        private HashSet<int> _blockedLayers;
        private int _projectileLayer;
        private int _playerProjectileLayer;

        public void Initialize(NPCShieldController owner, HashSet<int> blockedLayers)
        {
            _owner = owner;
            _blockedLayers = blockedLayers;
            
            // Resolver capas en runtime
            _projectileLayer = LayerMask.NameToLayer("Projectile");
            _playerProjectileLayer = LayerMask.NameToLayer("PlayerProjectile");
            
            Debug.Log($"[NPCShieldHitDetector] ✅ Inicializado - Bloqueando {_blockedLayers.Count} capas");
        }

        void OnTriggerEnter(Collider other)
        {
            HandleHit(other);
        }

        void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.collider);
        }

        private void HandleHit(Collider col)
        {
            if (_owner == null || _blockedLayers == null)
                return;

            var go = GetRootRigidbodyOrSelf(col);
            int layer = go.layer;

            if (_blockedLayers.Contains(layer))
            {
                Debug.Log($"[NPCShieldHitDetector] 🛡️ Bloqueado proyectil: {go.name} (layer {layer})");
                
                _owner.OnShieldHit();

                // Destruir proyectiles del player
                if (IsPlayerProjectile(go))
                {
                    SafeDestroy(go);
                }
            }
        }

        private static GameObject GetRootRigidbodyOrSelf(Collider col)
        {
            return col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
        }

        private bool IsPlayerProjectile(GameObject go)
        {
            if (go == null) return false;

            // Verificar component conocido de proyectiles del player
            if (go.GetComponentInParent<MagicProjectile>() != null) return true;

            // Verificar capas comunes de proyectiles del player
            if (go.layer == _projectileLayer || go.layer == _playerProjectileLayer) return true;

            return false;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (!go) return;
            Debug.Log($"[NPCShieldHitDetector] 💥 Destruyendo proyectil: {go.name}");
            Object.Destroy(go);
        }
    }

    /// <summary>
    /// Marcador público para identificar el escudo del NPC
    /// </summary>
    public class NPCShieldMarker : MonoBehaviour { }
}
