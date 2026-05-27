using UnityEngine;
using System.Collections;

/// <summary>
/// Permite que un GameObject sea afectado por el hechizo de levitación.
/// Funciona tanto en NPCs/enemigos (con Animator, NavMeshAgent, Damageable)
/// como en objetos de puzle como cajas (solo Rigidbody necesario).
///
/// Comportamiento en NPCs:
///   - Se levita y mueve con física hacia el punto de agarre.
///   - Al chocar contra una pared tras el lanzamiento, recibe daño escalado con velocidad.
///   - Si recibe un proyectil durante la levitación, aplica daño y reproduce animación de impacto.
///
/// Comportamiento en objetos (cajas, etc.):
///   - Solo la física actúa; no hay animación ni daño por colisión.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class LevitationTarget : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Si está marcado, este objeto puede ser levitado.")]
    [SerializeField] private bool canBeLevitated = true;
    [Tooltip("Multiplicador de fuerza de atracción (para ajustar por objeto).")]
    [SerializeField] private float pullForceMultiplier = 1f;
    [Tooltip("Multiplicador de fuerza de repulsión (para ajustar por objeto).")]
    [SerializeField] private float pushForceMultiplier = 1f;

    [Header("Animación (solo NPCs)")]
    [Tooltip("Estado de animación para la levitación (elevarse).")]
    [SerializeField] private string levitationAnimState = "LevelUp_NoWeapon";
    [Tooltip("Tiempo normalizado (0-1) donde pausar la animación durante el hold.")]
    [SerializeField] private float holdPauseNormalizedTime = 0.5f;
    [Tooltip("Velocidad de elevación vertical.")]
    [SerializeField] private float liftSpeed = 3f;
    [Tooltip("Estado de animación de impacto al recibir daño durante la levitación.")]
    [SerializeField] private string hitAnimState = "Hit";
    [Tooltip("Duración mínima (s) de la animación de impacto antes de volver a la pose de levitación.")]
    [Min(0.1f)] [SerializeField] private float hitAnimDuration = 0.4f;

    [Header("Física")]
    [Tooltip("Si está marcado, desactivar NavMeshAgent durante la levitación.")]
    [SerializeField] private bool disableNavMeshDuringLevitation = true;
    [Tooltip("Drag del Rigidbody durante levitación para suavizar el movimiento.")]
    [SerializeField] private float levitationDrag = 2f;

    [Header("VFX")]
    [Tooltip("Prefab del efecto visual sobre el objeto mientras está levitando.")]
    [SerializeField] private GameObject levitationVFXPrefab;
    [Tooltip("Prefab del efecto visual en el punto de impacto al chocar tras ser lanzado.")]
    [SerializeField] private GameObject impactVFXPrefab;
    [Tooltip("Tiempo en segundos antes de destruir el VFX de impacto (0 = se autodestruye).")]
    [Min(0f)] [SerializeField] private float impactVFXLifetime = 2f;
    [Tooltip("Offset de posición para el VFX respecto al centro del objeto.")]
    [SerializeField] private Vector3 vfxOffset = Vector3.up;
    [Tooltip("Escala del VFX de levitación.")]
    [SerializeField] private Vector3 vfxScale = Vector3.one;
    [Tooltip("Si está marcado, el VFX seguirá al objeto durante la levitación.")]
    [SerializeField] private bool vfxFollowsTarget = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // ── Referencias ──────────────────────────────────────────────────────────
    private Rigidbody _rigidbody;
    private Animator _animator;
    private UnityEngine.AI.NavMeshAgent _navAgent;
    private Damageable _damageable;

    // ── Estado ───────────────────────────────────────────────────────────────
    private bool _isBeingLevitated;
    private bool _isInFlight;
    private bool _isPlayingHitReaction;
    private PlayerLevitationController _currentLevitator;
    private float _targetHeight;
    private float _originalDrag;
    private bool _wasNavAgentEnabled;
    private bool _wasKinematic;
    private bool _wasRootMotion;
    private MagicSpellSO _flightSpell;

    // ── VFX ─────────────────────────────────────────────────────────────────
    private GameObject _currentVFXInstance;

    // ── Corrutinas ───────────────────────────────────────────────────────────
    private Coroutine _pauseAnimCoroutine;
    private Coroutine _hitReactionCoroutine;

    // ── Hashes de Animator ───────────────────────────────────────────────────
    private int _levitationStateHash;
    private int _hitStateHash;

    // ── API pública ──────────────────────────────────────────────────────────
    public bool CanBeLevitated   => canBeLevitated && !_isBeingLevitated;
    public bool IsBeingLevitated => _isBeingLevitated;

    public static event System.Action OnAnyLevitationStarted;
    public static event System.Action OnAnyLevitationEnded;

    // ────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _rigidbody  = GetComponent<Rigidbody>();
        _animator   = GetComponentInChildren<Animator>();
        _navAgent   = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _damageable = GetComponent<Damageable>() ?? GetComponentInParent<Damageable>();

        if (!string.IsNullOrEmpty(levitationAnimState))
            _levitationStateHash = Animator.StringToHash(levitationAnimState);
        if (!string.IsNullOrEmpty(hitAnimState))
            _hitStateHash = Animator.StringToHash(hitAnimState);
    }

    // ── Inicio de levitación ─────────────────────────────────────────────────

    public void BeginLevitation(PlayerLevitationController levitator, MagicSpellSO spell)
    {
        if (_isBeingLevitated || !canBeLevitated) return;

        _isBeingLevitated = true;
        _currentLevitator = levitator;
        _targetHeight     = transform.position.y + spell.levitationHeight;

        // Configurar física
        _originalDrag          = _rigidbody.linearDamping;
        _wasKinematic          = _rigidbody.isKinematic;
        _rigidbody.linearDamping = levitationDrag;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity  = false;

        if (disableNavMeshDuringLevitation && _navAgent != null)
        {
            _wasNavAgentEnabled = _navAgent.enabled;
            _navAgent.enabled   = false;
        }

        if (_animator != null)
        {
            _wasRootMotion             = _animator.applyRootMotion;
            _animator.applyRootMotion  = false;

            if (_levitationStateHash != 0)
            {
                _animator.Play(_levitationStateHash, 0, 0f);
                _pauseAnimCoroutine = StartCoroutine(Co_PauseAnimationDuringHold());
            }
        }

        // Suscribirse a daño para poder reaccionar durante la levitación
        if (_damageable != null)
            _damageable.OnDamaged += OnDamagedWhileLevitated;

        SpawnLevitationVFX();
        OnAnyLevitationStarted?.Invoke();

        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} comenzando levitación");
    }

    // ── Actualización cada frame ─────────────────────────────────────────────

    public void UpdateLevitation(MagicSpellSO spell, Vector3 targetPosition, float followSpeed)
    {
        if (!_isBeingLevitated) return;

        Vector3 finalTarget = new Vector3(targetPosition.x, _targetHeight, targetPosition.z);
        Vector3 toTarget    = finalTarget - transform.position;
        float   distance    = toTarget.magnitude;

        float elasticSpeed = followSpeed * pullForceMultiplier * Mathf.Max(1f, distance * 0.5f);

        if (distance > 0.1f)
        {
            Vector3 targetVelocity = toTarget.normalized * elasticSpeed;
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, targetVelocity, Time.deltaTime * 8f);
        }
        else
        {
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
        }

        float currentY = transform.position.y;
        if (currentY < _targetHeight - 0.1f)
        {
            float liftForce = Mathf.Min(liftSpeed * spell.levitationLiftSpeed, (_targetHeight - currentY) * 5f);
            _rigidbody.AddForce(Vector3.up * liftForce, ForceMode.Acceleration);
        }

        UpdateVFXPosition();
    }

    // ── Fin de levitación (lanzamiento) ──────────────────────────────────────

    public void EndLevitation(MagicSpellSO spell, Vector3 pushDirection, float pushForce)
    {
        if (!_isBeingLevitated) return;

        UnsubscribeDamage();

        if (_pauseAnimCoroutine != null)
        {
            StopCoroutine(_pauseAnimCoroutine);
            _pauseAnimCoroutine = null;
        }
        if (_hitReactionCoroutine != null)
        {
            StopCoroutine(_hitReactionCoroutine);
            _hitReactionCoroutine = null;
        }

        DestroyLevitationVFX();

        if (_animator != null)
            _animator.Play(_levitationStateHash, 0, holdPauseNormalizedTime);

        _flightSpell    = spell;
        _isInFlight     = true;
        _isPlayingHitReaction = false;

        _rigidbody.isKinematic  = false;
        _rigidbody.linearVelocity   = Vector3.zero;
        _rigidbody.angularVelocity  = Vector3.zero;
        _rigidbody.linearDamping     = 0f;
        _rigidbody.useGravity       = true;

        Vector3 push = pushDirection * pushForce * pushForceMultiplier;
        push.y = pushForce * 0.8f;
        _rigidbody.AddForce(push, ForceMode.Impulse);

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, pushDirection);
        _rigidbody.AddTorque(torqueAxis * pushForce * 4f, ForceMode.Impulse);

        StartCoroutine(Co_FinishLevitation(spell));
    }

    // ── Cancelación inmediata ────────────────────────────────────────────────

    public void CancelLevitation()
    {
        if (!_isBeingLevitated) return;

        UnsubscribeDamage();
        StopAllCoroutines();
        _pauseAnimCoroutine   = null;
        _hitReactionCoroutine = null;
        _isPlayingHitReaction = false;

        DestroyLevitationVFX();
        RestoreNormalState();
    }

    // ── Daño y reacción de golpe durante la levitación ───────────────────────

    void OnDamagedWhileLevitated(float amount)
    {
        if (!_isBeingLevitated || _isPlayingHitReaction) return;
        if (_animator == null || _hitStateHash == 0) return;
        if (!_animator.HasState(0, _hitStateHash)) return;

        if (_hitReactionCoroutine != null) StopCoroutine(_hitReactionCoroutine);
        _hitReactionCoroutine = StartCoroutine(Co_PlayHitReaction());
    }

    IEnumerator Co_PlayHitReaction()
    {
        _isPlayingHitReaction = true;
        _animator.Play(_hitStateHash, 0, 0f);

        yield return new WaitForSeconds(hitAnimDuration);

        _isPlayingHitReaction = false;
        _hitReactionCoroutine = null;

        // Si todavía está levitado, volver a la pose de levitación
        if (_isBeingLevitated && _levitationStateHash != 0)
            _animator.Play(_levitationStateHash, 0, holdPauseNormalizedTime);
    }

    // ── Animación de levitación (pausa en hold) ──────────────────────────────

    IEnumerator Co_PauseAnimationDuringHold()
    {
        if (_animator == null) yield break;

        int waited = 0;
        while (_animator != null && waited < 10)
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _levitationStateHash) break;
            waited++;
            yield return null;
        }

        while (_animator != null && _isBeingLevitated)
        {
            // Ceder el control si hay una reacción de golpe en curso
            if (!_isPlayingHitReaction)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                if (info.normalizedTime >= holdPauseNormalizedTime)
                    _animator.Play(_levitationStateHash, 0, holdPauseNormalizedTime);
            }
            yield return null;
        }
    }

    // ── Aterrizaje y restauración ────────────────────────────────────────────

    IEnumerator Co_FinishLevitation(MagicSpellSO spell)
    {
        yield return new WaitForSeconds(0.3f);

        float timeout = 5f, elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.5f)) break;
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
        RestoreNormalState();
    }

    void RestoreNormalState()
    {
        _isBeingLevitated     = false;
        _isInFlight           = false;
        _isPlayingHitReaction = false;
        _flightSpell          = null;
        _currentLevitator     = null;

        if (_rigidbody != null)
        {
            _rigidbody.linearDamping    = _originalDrag;
            _rigidbody.isKinematic  = _wasKinematic;
            _rigidbody.useGravity   = true;
            _rigidbody.linearVelocity   = Vector3.zero;
            _rigidbody.angularVelocity  = Vector3.zero;

            Vector3 euler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, euler.y, 0f);
        }

        var damageable = GetComponent<IDamageable>() ?? GetComponentInParent<IDamageable>();
        bool isDead    = damageable != null && !damageable.IsAlive;

        if (_animator != null)
            _animator.applyRootMotion = _wasRootMotion;

        if (!isDead)
        {
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
                if (_animator.HasState(0, Animator.StringToHash("Idle")))
                    _animator.Play("Idle", 0, 0f);
                else if (_animator.HasState(0, Animator.StringToHash("Locomotion")))
                    _animator.Play("Locomotion", 0, 0f);
            }

            if (_navAgent != null && _wasNavAgentEnabled)
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    transform.position = hit.position;
                _navAgent.enabled = true;
            }
        }

        OnAnyLevitationEnded?.Invoke();
        if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} estado normal restaurado");
    }

    void UnsubscribeDamage()
    {
        if (_damageable != null)
            _damageable.OnDamaged -= OnDamagedWhileLevitated;
    }

    // ── Daño por impacto en pared (después del lanzamiento) ──────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (!_isInFlight || _flightSpell == null) return;
        if (collision.contactCount == 0) return;

        ContactPoint contact = collision.GetContact(0);
        if (contact.normal.y > 0.5f) return; // ignorar suelo

        float speed = collision.relativeVelocity.magnitude;
        if (speed < _flightSpell.levitationImpactMinSpeed) return;

        var shield = GetComponent<NPCShieldController>() ?? GetComponentInParent<NPCShieldController>();
        if (shield != null && shield.IsDefending)
        {
            if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} impacto bloqueado por escudo");
            return;
        }

        float damage   = _flightSpell.levitationImpactDamage * (speed / _flightSpell.levitationImpactMinSpeed);
        var   dmg      = GetComponent<IDamageable>() ?? GetComponentInParent<IDamageable>();
        if (dmg != null && dmg.IsAlive)
        {
            dmg.TakeDamage(damage);
            if (showDebugLogs) Debug.Log($"[LevitationTarget] {name} impacto pared → {speed:F1}m/s, daño={damage:F0}");
        }

        if (impactVFXPrefab != null)
        {
            var vfx = Instantiate(impactVFXPrefab, contact.point, Quaternion.LookRotation(contact.normal));
            if (impactVFXLifetime > 0f) Destroy(vfx, impactVFXLifetime);
        }
    }

    // ── VFX ─────────────────────────────────────────────────────────────────

    void SpawnLevitationVFX()
    {
        if (levitationVFXPrefab == null) return;

        _currentVFXInstance = Instantiate(levitationVFXPrefab, transform.position + vfxOffset, Quaternion.identity);
        _currentVFXInstance.transform.localScale = vfxScale;

        if (vfxFollowsTarget)
        {
            _currentVFXInstance.transform.SetParent(transform);
            _currentVFXInstance.transform.localPosition = vfxOffset;
            _currentVFXInstance.transform.localScale     = vfxScale;
        }
    }

    void UpdateVFXPosition()
    {
        if (_currentVFXInstance == null || vfxFollowsTarget) return;
        _currentVFXInstance.transform.position = transform.position + vfxOffset;
    }

    void DestroyLevitationVFX()
    {
        if (_currentVFXInstance != null)
        {
            Destroy(_currentVFXInstance);
            _currentVFXInstance = null;
        }
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void OnDisable()
    {
        if (_isBeingLevitated) CancelLevitation();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!_isBeingLevitated) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 1f);
        Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, _targetHeight, transform.position.z));
    }
#endif
}
