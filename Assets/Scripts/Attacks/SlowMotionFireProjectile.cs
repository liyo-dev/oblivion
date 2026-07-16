using System;
using UnityEngine;

/// Proyectil enemigo para secuencias scripted.
/// Se mueve con Time.deltaTime, respetando Time.timeScale, para sincronizarse
/// con el fireball del jugador durante el slow-motion.
[DisallowMultipleComponent]
public class SlowMotionFireProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float turnSpeedDegPerSec = 60f;
    [SerializeField] private GameObject collisionVFX;
    [SerializeField] private float maxLifetimeSeconds = 12f;

    // El sequencer escucha este evento para orquestar la explosión
    public event Action OnHitByPlayerFireball;

    private Transform _target;
    private bool _ended;
    private bool _paused;
    private float _spawnUnscaledTime;
    private Collider _col;
    private readonly Collider[] _overlapBuffer = new Collider[8];

    [Tooltip("Offset vertical sobre la posición del target (ajustar si el proyectil apunta demasiado bajo)")]
    [SerializeField] private float aimHeightOffset = 0.9f;

    void Awake()
    {
        _col = GetComponent<Collider>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    public void Launch(Transform target)
    {
        _target = target;
        _spawnUnscaledTime = Time.unscaledTime;

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * aimHeightOffset;
            transform.LookAt(aimPos);
        }
    }

    /// Congela el proyectil en el aire; útil durante el panic input.
    public void Pause()
    {
        _paused = true;
        // Deshabilitar collider para que el fireball de Will no lo detone al spawnear cerca
        if (_col) _col.enabled = false;
    }

    /// Reanuda el movimiento tras un Pause(). Reactiva el collider un frame después
    /// para dar tiempo a que los dos proyectiles se separen antes de poder colisionar.
    public void Resume()
    {
        _paused = false;
        StartCoroutine(ReenableCollider());
    }

    private System.Collections.IEnumerator ReenableCollider()
    {
        yield return null;
        if (!_ended && _col) _col.enabled = true;
    }

    /// Llamado por el sequencer cuando quiere forzar la colisión desde código
    public void ForceCollide()
    {
        if (_ended) return;
        HandleCollision(transform.position);
    }

    void Update()
    {
        if (_ended || _paused) return;

        if (Time.unscaledTime - _spawnUnscaledTime > maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * aimHeightOffset;
            Vector3 toTarget = aimPos - transform.position;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector3 dir = toTarget.normalized;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    turnSpeedDegPerSec * Time.deltaTime);
                transform.position += dir * speed * Time.deltaTime;
            }
        }
        else
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (_ended) return;
        if (other.TryGetComponent<MagicProjectile>(out _))
            HandleCollision(other.ClosestPoint(transform.position));
    }

    void OnCollisionEnter(Collision col)
    {
        if (_ended) return;
        if (col.gameObject.TryGetComponent<MagicProjectile>(out _))
            HandleCollision(col.GetContact(0).point);
    }

    private void HandleCollision(Vector3 point)
    {
        _ended = true;

        if (collisionVFX != null)
            Destroy(Instantiate(collisionVFX, point, Quaternion.identity), 3f);

        OnHitByPlayerFireball?.Invoke();
        Destroy(gameObject);
    }
}
