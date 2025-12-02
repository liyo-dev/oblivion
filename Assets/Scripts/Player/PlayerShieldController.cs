using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerShieldController : MonoBehaviour
{
    [Header("Escudo")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private Vector3 shieldOffset = new(0f, 0.9f, 0.6f);
    [SerializeField, Range(0f, 1f)] private float triggerThreshold = 0.5f;

    [Header("Animaciones")]
    [SerializeField] private string defendAnimation = "Defend_NoWeapon";
    [SerializeField] private string defendHitAnimation = "DefendHit_NoWeapon";

    [Header("Colisiones a bloquear")]
    [SerializeField] private string[] blockLayerNames = { "Enemy", "ProjectileEnemy" };

    private PlayerControls _controls;
    private bool _ownsControls;
    private Animator _animator;
    private GameObject _shieldInstance;
    private readonly HashSet<int> _blockedLayers = new();
    private bool _isDefending;
    private int _playerLayer;

    void Awake()
    {
        _controls = PlayerInputManager.GetSharedOrNew(out _ownsControls);
        _animator = GetComponent<Animator>();
        _playerLayer = gameObject.layer;
        CacheBlockedLayers();
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

        _isDefending = true;
        ActivateShield();
        PlayAnimation(defendAnimation);
    }

    private void StopDefending()
    {
        if (!_isDefending)
            return;

        _isDefending = false;
        DeactivateShield();
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

            Transform anchor = shieldAnchor ? shieldAnchor : transform;
            _shieldInstance = Instantiate(shieldPrefab, anchor);
            _shieldInstance.transform.localPosition = shieldOffset;
            _shieldInstance.transform.localRotation = Quaternion.identity;

            ConfigureShieldDetector(_shieldInstance);
        }

        SetLayerIgnores(true);
    }

    private void DeactivateShield()
    {
        if (_shieldInstance != null)
        {
            Destroy(_shieldInstance);
            _shieldInstance = null;
        }

        SetLayerIgnores(false);
    }

    private void PlayAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName)) return;
        _animator.Play(animationName);
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

    private void SetLayerIgnores(bool ignore)
    {
        foreach (int layer in _blockedLayers)
        {
            Physics.IgnoreLayerCollision(_playerLayer, layer, ignore);
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

        var rb = shield.GetComponent<Rigidbody>() ?? shield.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var detector = shield.GetComponent<ShieldHitDetector>() ?? shield.AddComponent<ShieldHitDetector>();
        detector.Initialize(this, _blockedLayers);
    }

    internal void OnShieldHit()
    {
        PlayAnimation(defendHitAnimation);
    }

    private class ShieldHitDetector : MonoBehaviour
    {
        private PlayerShieldController _owner;
        private HashSet<int> _blockedLayers;

        public void Initialize(PlayerShieldController owner, HashSet<int> blockedLayers)
        {
            _owner = owner;
            _blockedLayers = blockedLayers;
        }

        void OnTriggerEnter(Collider other)
        {
            CheckLayer(other.gameObject.layer);
        }

        void OnCollisionEnter(Collision collision)
        {
            CheckLayer(collision.gameObject.layer);
        }

        private void CheckLayer(int layer)
        {
            if (_owner == null || _blockedLayers == null)
                return;

            if (_blockedLayers.Contains(layer))
                _owner.OnShieldHit();
        }
    }
}
