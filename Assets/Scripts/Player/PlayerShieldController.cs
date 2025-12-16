using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

[RequireComponent(typeof(Animator))]
public class PlayerShieldController : MonoBehaviour
{
    [Header("Escudo")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private Vector3 shieldOffset = Vector3.zero;
    [SerializeField, Range(0f, 1f)] private float triggerThreshold = 0.5f;

    [Header("Animaciones")]
    [SerializeField] private string defendAnimation = "Defend_NoWeapon";
    [SerializeField] private string defendHitAnimation = "DefendHit_NoWeapon";
    [SerializeField] private string locomotionAnimation = "Free Locomotion";
    [SerializeField] private int upperBodyLayer = 1;
    [SerializeField] private float upperBodyTransitionDuration = 0.1f;
    [SerializeField, Min(0f)] private float hitFeedbackDuration = 0.3f; // tiempo de feedback antes de volver a defensa

    [Header("Colisiones a bloquear")]
    [SerializeField] private string[] blockLayerNames = { "Enemy", "ProjectileEnemy" };

    private PlayerControls _controls;
    private bool _ownsControls;
    private Animator _animator;
    private GameObject _shieldInstance;
    private readonly HashSet<int> _blockedLayers = new();
    private bool _isDefending;
    private int _playerLayer;
    private float _originalUpperBodyWeight;
    private MagicCaster _magicCaster;

    public bool IsDefending => _isDefending;

    void Awake()
    {
        _controls = Core.PlayerInputManager.GetSharedOrNew(out _ownsControls);
        _animator = GetComponent<Animator>();
        _playerLayer = gameObject.layer;
        CacheUpperBodyWeight();
        CacheBlockedLayers();
        _magicCaster = GetComponentInParent<MagicCaster>();
    }

    void OnEnable()
    {
        if (_controls == null) return;

        if (_ownsControls)
            _controls.Enable();

        _controls.GamePlay.LT.performed += OnTriggerChanged;
        _controls.GamePlay.LT.canceled += OnTriggerChanged;
        _controls.GamePlay.RT.performed += OnTriggerChanged;
        _controls.GamePlay.RT.canceled += OnTriggerChanged;
    }

    void OnDisable()
    {
        if (_controls == null) return;

        _controls.GamePlay.LT.performed -= OnTriggerChanged;
        _controls.GamePlay.LT.canceled -= OnTriggerChanged;
        _controls.GamePlay.RT.performed -= OnTriggerChanged;
        _controls.GamePlay.RT.canceled -= OnTriggerChanged;

        if (_ownsControls)
            _controls.Disable();

        StopDefending();
    }

    void Update()
    {
        if (_controls == null)
            return;

        if (!GameState.CanProcessGameplayInput)
        {
            StopDefending();
            return;
        }

        if (_magicCaster != null && _magicCaster.IsCasting)
        {
            StopDefending();
            return;
        }

        EvaluateDefenseState();
    }

    private void EvaluateDefenseState()
    {
        float lt = _controls.GamePlay.LT.ReadValue<float>();
        float rt = _controls.GamePlay.RT.ReadValue<float>();
        bool wantsDefense = lt >= triggerThreshold && rt >= triggerThreshold;

        if (wantsDefense)
            StartDefending();
        else
            StopDefending();
    }

    private void OnTriggerChanged(InputAction.CallbackContext _)
    {
        EvaluateDefenseState();
    }

    private void StartDefending()
    {
        if (_isDefending)
            return;

        if (_magicCaster != null && _magicCaster.IsCasting)
            return;

        _isDefending = true;
        ActivateShield();
        SetUpperBodyWeight(1f);
        PlayUpperBodyAnimation(defendAnimation);
    }

    private void StopDefending()
    {
        if (!_isDefending)
            return;

        _isDefending = false;
        DeactivateShield();
        ResetUpperBodyWeight();
        PlayAnimation(locomotionAnimation);
    }

    private void ActivateShield()
    {
        if (_shieldInstance == null)
        {
            if (shieldPrefab == null)
            {
                Debug.LogWarning("[PlayerShieldController] shieldPrefab no asignado, no se puede instanciar el escudo.");
                return;
            }

            Transform parent = transform;
            _shieldInstance = Instantiate(shieldPrefab, parent);
            _shieldInstance.transform.localPosition = shieldOffset;
            _shieldInstance.transform.localRotation = Quaternion.identity;

            ConfigureShieldDetector(_shieldInstance);
        }

    }

    private void DeactivateShield()
    {
        if (_shieldInstance != null)
        {
            Destroy(_shieldInstance);
            _shieldInstance = null;
        }
    }

    private void PlayAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName)) return;
        _animator.Play(animationName);
    }

    private void PlayUpperBodyAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName)) return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _animator.CrossFade(animationName, upperBodyTransitionDuration, upperBodyLayer);
        else
            _animator.Play(animationName);
    }

    private void CacheUpperBodyWeight()
    {
        _originalUpperBodyWeight = 0f;

        if (_animator == null)
            return;

        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
            _originalUpperBodyWeight = _animator.GetLayerWeight(upperBodyLayer);
    }

    private void SetUpperBodyWeight(float weight)
    {
        if (_animator == null)
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
                _blockedLayers.Add(layer);
            else
                Debug.LogWarning($"[PlayerShieldController] No se encontró la capa '{name}'.");
        }
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

        // No se requiere Rigidbody para el escudo

        // Marcador público para que proyectiles identifiquen el escudo explícitamente
        if (!shield.TryGetComponent<ShieldMarker>(out var _))
            shield.AddComponent<ShieldMarker>();

        var detector = shield.GetComponent<ShieldHitDetector>() ?? shield.AddComponent<ShieldHitDetector>();
        detector.Initialize(this, _blockedLayers);
    }

    internal void OnShieldHit()
    {
        // Reproduce animación de impacto breve en la capa superior y vuelve a defensa.
        if (string.IsNullOrEmpty(defendHitAnimation)) return;
        PlayUpperBodyAnimation(defendHitAnimation);
        if (hitFeedbackDuration > 0f)
        {
            // Reinicia el temporizador ante impactos consecutivos
            CancelInvoke(nameof(ReturnToDefendAnimation));
            Invoke(nameof(ReturnToDefendAnimation), hitFeedbackDuration);
        }
    }

    private void ReturnToDefendAnimation()
    {
        if (_isDefending)
            PlayUpperBodyAnimation(defendAnimation);
    }

    private class ShieldHitDetector : MonoBehaviour
    {
        private PlayerShieldController _owner;
        private HashSet<int> _blockedLayers;
        private int _projectileEnemyLayer;
        private int _projectileLayer;

        public void Initialize(PlayerShieldController owner, HashSet<int> blockedLayers)
        {
            _owner = owner;
            _blockedLayers = blockedLayers;
            // Resolver capas en runtime (no en constructor/initializer)
            _projectileEnemyLayer = LayerMask.NameToLayer("ProjectileEnemy");
            _projectileLayer = LayerMask.NameToLayer("Projectile");
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
                _owner.OnShieldHit();

                if (IsProjectile(go))
                {
                    SafeDestroy(go);
                }
            }
        }

        private static GameObject GetRootRigidbodyOrSelf(Collider col)
        {
            return col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
        }

        private bool IsProjectile(GameObject go)
        {
            if (go == null) return false;

            // Component known for enemy projectiles
            if (go.GetComponentInParent<EnemyProjectile>() != null) return true;

            // Common layers for projectiles
            if (go.layer == _projectileEnemyLayer || go.layer == _projectileLayer) return true;

            return false;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (!go) return;
            Object.Destroy(go);
        }
    }

    // Componente público y ligero para identificar el escudo en colisiones externas
    public class ShieldMarker : MonoBehaviour {}
}
