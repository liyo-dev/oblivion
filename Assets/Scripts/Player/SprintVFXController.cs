using UnityEngine;

/// <summary>
/// Activa un VFX de "hyperdrive" (sensación de velocidad) mientras el jugador corre en sprint
/// o vuela acelerando. El VFX se instancia como hijo de la cámara para que el efecto se vea
/// correctamente en espacio de pantalla.
/// </summary>
public class SprintVFXController : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("Prefab del efecto de velocidad (se instancia como hijo de la cámara)")]
    [SerializeField] private GameObject hyperdriveVfxPrefab;

    [Tooltip("Offset local respecto a la cámara donde se posiciona el VFX")]
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0f, 0f);

    [Header("Timing")]
    [Tooltip("Retardo antes de activar el VFX para evitar parpadeos en sprints muy cortos")]
    [SerializeField] private float activationDelay = 0.15f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
#endif

    private Animator _animator;
    private Camera _cam;
    private PlayerFlyingController _flyingController;
    private GameObject _vfxInstance;
    private ParticleSystem[] _particles;
    private bool _vfxActive;
    private float _sprintTimer;

    // Hashes cacheados de los parámetros del Animator de Invector
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

    void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _cam = Camera.main;
        _flyingController = GetComponent<PlayerFlyingController>();
    }

    void OnEnable()
    {
        if (_animator == null)
        {
            Debug.LogWarning("[SprintVFXController] No se encontró Animator. Desactivando.");
            enabled = false;
            return;
        }

        if (hyperdriveVfxPrefab != null && _vfxInstance == null)
        {
            var parent = _cam != null ? _cam.transform : transform;
            _vfxInstance = Instantiate(hyperdriveVfxPrefab, parent);
            _vfxInstance.transform.localPosition = vfxOffset;
            _vfxInstance.transform.localRotation = Quaternion.identity;
            _vfxInstance.transform.localScale = Vector3.one;
            _particles = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            _vfxInstance.SetActive(false);
            _vfxActive = false;
        }
    }

    void OnDisable()
    {
        if (_vfxActive)
            StopVFX();
    }

    void Update()
    {
        if (_animator == null) return;

        // InputMagnitude en Invector llega a 1.0f corriendo y a 1.5f en sprint.
        bool isGrounded = _animator.GetBool(HashIsGrounded);
        float inputMag  = _animator.GetFloat(HashInputMagnitude);
        bool isSprintRunning = isGrounded && inputMag > 1.05f;
        bool isFlyingBoost = _flyingController != null && _flyingController.IsFlying && _flyingController.IsBoosting;
        bool shouldShow = isSprintRunning || isFlyingBoost;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLog)
            Debug.Log($"[SprintVFX] IsGrounded={isGrounded} InputMag={inputMag:F2} FlyBoost={isFlyingBoost} shouldShow={shouldShow}");
#endif

        if (shouldShow && !_vfxActive)
        {
            _sprintTimer += Time.deltaTime;
            if (_sprintTimer >= activationDelay)
                PlayVFX();
        }
        else if (!shouldShow && _vfxActive)
        {
            StopVFX();
        }
        else if (!shouldShow)
        {
            _sprintTimer = 0f;
        }
    }

    private void PlayVFX()
    {
        if (_vfxInstance == null) return;

        _vfxInstance.SetActive(true);
        if (_particles == null)
            _particles = _vfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var ps in _particles)
        {
            ps.Clear();
            ps.Play();
        }

        _vfxActive = true;
    }

    private void StopVFX()
    {
        _sprintTimer = 0f;
        _vfxActive = false;

        if (_vfxInstance == null) return;

        if (_particles != null)
        {
            foreach (var ps in _particles)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        _vfxInstance.SetActive(false);
    }
}
