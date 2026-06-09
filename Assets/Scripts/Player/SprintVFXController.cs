using UnityEngine;

/// <summary>
/// Activa un VFX de "hyperdrive" (sensación de velocidad) mientras el jugador está en sprint.
/// Colocar en el mismo GameObject que el Animator del jugador.
/// Asignar el prefab vfx_Hyperdrive_01 en el inspector.
///
/// Lee los parámetros del Animator de Invector (IsSprinting, InputMagnitude, IsGrounded)
/// para evitar depender de propiedades internal del motor.
/// </summary>
public class SprintVFXController : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("Prefab del efecto de velocidad (se instancia como hijo del jugador)")]
    [SerializeField] private GameObject hyperdriveVfxPrefab;

    [Tooltip("Offset local respecto al jugador donde se posiciona el VFX")]
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 1f, 0f);

    [Header("Timing")]
    [Tooltip("Retardo antes de activar el VFX para evitar parpadeos en sprints muy cortos")]
    [SerializeField] private float activationDelay = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Animator _animator;
    private Camera _cam;
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
            _vfxInstance = Instantiate(hyperdriveVfxPrefab, transform);
            _vfxInstance.transform.localPosition = vfxOffset;
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
        // Si solo se pulsa el botón de sprint sin moverse, puede valer 0.5f.
        // Requerimos > 1.1f para asegurar que realmente se está moviendo a velocidad de sprint.
        bool isGrounded = _animator.GetBool(HashIsGrounded);
        float inputMag  = _animator.GetFloat(HashInputMagnitude);
        // IsSprinting parpadea frame a frame en Invector — usar solo InputMagnitude
        bool shouldShow = isGrounded && inputMag > 1.05f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLog)
            Debug.Log($"[SprintVFX] IsGrounded={isGrounded} InputMagnitude={inputMag:F2} shouldShow={shouldShow}");
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

        if (_vfxActive && _vfxInstance != null && _cam != null)
            _vfxInstance.transform.rotation = _cam.transform.rotation;
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
