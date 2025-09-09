using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Damageable))]
public class DamageableKnockback : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField] private float strength = 6.0f;   // “metros/seg” equivalentes
    [SerializeField] private float duration = 0.15f;  // tiempo del empujón
    [SerializeField] private AnimationCurve curve = null; // 1 → 0
    [SerializeField] private bool preferRigidbody = false;

    [Header("Opcional: desactivar estos behaviours durante el knockback")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    Damageable _damageable;
    Rigidbody _rb;
    CharacterController _cc;
    NavMeshAgent _agent;
    Animator _anim;
    Coroutine _co;

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _rb   = GetComponent<Rigidbody>();
        _cc   = GetComponent<CharacterController>();
        _agent= GetComponent<NavMeshAgent>();
        _anim = GetComponent<Animator>();
        if (curve == null) curve = AnimationCurve.EaseInOut(0,1, 1,0);
    }

    void OnEnable()  { _damageable.OnDamaged += OnDamaged; }
    void OnDisable() { _damageable.OnDamaged -= OnDamaged; }

    void OnDamaged(DamageInfo info)
    {
        Vector3 dir;
        if (info.normal.sqrMagnitude > 0.001f) dir = info.normal;
        else
        {
            Vector3 from = info.source ? info.source.transform.position : info.point;
            dir = (transform.position - from);
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        if (debugLogs) Debug.Log($"[Knockback:{name}] dir={dir} strength={strength} dur={duration}");

        if (preferRigidbody && _rb && !_rb.isKinematic)
        {
            _rb.AddForce(dir * strength, ForceMode.VelocityChange);
            return;
        }

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Knock(dir));
    }

    IEnumerator Co_Knock(Vector3 dir)
    {
        bool hadAgent = _agent && _agent.enabled;
        bool hadRoot  = _anim && _anim.applyRootMotion;

        if (hadAgent) _agent.isStopped = true;
        if (hadRoot)  _anim.applyRootMotion = false;
        foreach (var b in behavioursToDisable) if (b) b.enabled = false;

        float t = 0f;
        while (t < duration)
        {
            float k = curve.Evaluate(t / duration);
            Vector3 delta = dir * (strength * k) * Time.deltaTime;

            if (_cc) _cc.Move(delta);
            else     transform.position += delta;

            t += Time.deltaTime;
            yield return null;
        }

        if (hadAgent) _agent.isStopped = false;
        if (hadRoot)  _anim.applyRootMotion = true;
        foreach (var b in behavioursToDisable) if (b) b.enabled = true;
    }
}
