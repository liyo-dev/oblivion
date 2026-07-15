using System;
using UnityEngine;

/// Proyectil enemigo para secuencias scripted que ignora Time.timeScale.
/// Se mueve con unscaledDeltaTime para mantener su velocidad durante slow-motion.
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
    private float _spawnUnscaledTime;

    // Offset vertical para apuntar al centro del personaje en lugar de los pies
    private const float AimHeightOffset = 0.9f;

    public void Launch(Transform target)
    {
        _target = target;
        _spawnUnscaledTime = Time.unscaledTime;

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * AimHeightOffset;
            transform.LookAt(aimPos);
        }
    }

    /// Llamado por el sequencer cuando quiere forzar la colisión desde código
    public void ForceCollide()
    {
        if (_ended) return;
        HandleCollision(transform.position);
    }

    void Update()
    {
        if (_ended) return;

        if (Time.unscaledTime - _spawnUnscaledTime > maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * AimHeightOffset;
            Vector3 dir = (aimPos - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, turnSpeedDegPerSec * Time.unscaledDeltaTime);
            }
        }

        transform.position += transform.forward * speed * Time.unscaledDeltaTime;
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
