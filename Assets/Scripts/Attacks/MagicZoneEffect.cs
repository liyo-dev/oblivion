using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de efecto mágica: a diferencia de <see cref="MagicProjectile"/>, no viaja — se instancia
/// ya en su posición final y aplica daño periódico a todo lo que esté dentro de su radio mientras
/// dura. Pensada para MagicKind.Zone (ver Identifiers.cs): el VFX de casteo sigue saliendo de la
/// mano del lanzador, con el mismo timing que un hechizo Projectile normal (para aprovechar la
/// animación de casteo ya existente), pero en vez de instanciarse en la mano y volar, este
/// prefab se instancia directamente en el punto de impacto calculado por
/// MagicProjectileSpawner.SpawnZoneNow() — "sale de la mano pero se materializa al instante como
/// zona", que es el pedido de diseño original (30 ago 2026).
///
/// No requiere Collider: usa Physics.OverlapSphereNonAlloc en vez de triggers físicos, mismo
/// criterio que la rama AOE de MagicProjectile.ResolveHit(), para no depender de que los
/// colliders de enemigos en movimiento entren/salgan limpiamente de un trigger.
/// </summary>
[DisallowMultipleComponent]
public class MagicZoneEffect : MonoBehaviour
{
    [System.Serializable]
    public struct ZoneConfig
    {
        public float damagePerTick;
        public float tickInterval;
        public float radius;
        public float duration;
        public float knockbackForce;      // 0 = sin empuje
        public LayerMask hitLayers;       // Capas que reciben daño (Enemy, Boss, etc.)
        public string tickSFXKey;         // opcional: SFX en cada tick que golpea a alguien
        public GameObject despawnVFX;     // VFX al terminar la duración
        public float vfxLifetime;         // tiempo antes de destruir despawnVFX (0 = 3s por defecto)
    }

    ZoneConfig _cfg;
    GameObject _instigator;
    bool _configured;

    // Buffer reutilizable para no generar basura en cada tick (mismo criterio que MagicProjectile).
    readonly Collider[] _hitBuffer = new Collider[32];

    /// <summary>Inyecta la configuración de la zona y quién la lanzó. Llamar justo tras instanciar.</summary>
    public void Configure(in ZoneConfig cfg, GameObject instigator)
    {
        _cfg = cfg;
        _instigator = instigator;
        _configured = true;
    }

    void OnEnable()
    {
        if (!_configured)
        {
            // Fallback de seguridad: si se activa sin Configure() (p.ej. probado suelto en
            // escena desde el Editor), usar valores razonables para no romper nada.
            if (_cfg.radius <= 0f) _cfg.radius = 3f;
            if (_cfg.tickInterval <= 0f) _cfg.tickInterval = 0.5f;
            if (_cfg.duration <= 0f) _cfg.duration = 3f;
        }
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        float elapsed = 0f;
        // Primer tick inmediato al aparecer (sensación de "trampa que ya está mordiendo"),
        // luego uno cada tickInterval hasta agotar la duración.
        while (elapsed < _cfg.duration)
        {
            DoTick();
            float wait = Mathf.Max(0.05f, _cfg.tickInterval);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }
        End();
    }

    void DoTick()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _cfg.radius, _hitBuffer, ~0, QueryTriggerInteraction.Collide);
        bool hitSomething = false;
        HashSet<Damageable> alreadyHit = null;

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (!col) continue;

            // Mismo filtro que la rama AOE de MagicProjectile: solo se aplica si hitLayers está
            // explícitamente configurado (!= 0); si se deja vacío no se filtra por capa.
            if (_cfg.hitLayers.value != 0 && ((1 << col.gameObject.layer) & _cfg.hitLayers.value) == 0)
                continue;

            var d = col.GetComponent<Damageable>() ?? col.GetComponentInParent<Damageable>();
            if (d == null) continue;
            if (_instigator != null && d.gameObject == _instigator) continue; // no dañarse a sí mismo

            alreadyHit ??= new HashSet<Damageable>();
            if (!alreadyHit.Add(d)) continue; // evita doble tick si el enemigo tiene varios colliders

            d.TakeDamage(_cfg.damagePerTick, _instigator);
            hitSomething = true;

            if (_cfg.knockbackForce > 0f)
            {
                var rb = col.attachedRigidbody ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 dir = rb.worldCenterOfMass - transform.position;
                    dir.y = 0f;
                    dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
                    rb.AddForce(dir * _cfg.knockbackForce, ForceMode.Impulse);
                }
            }
        }

        if (hitSomething && !string.IsNullOrEmpty(_cfg.tickSFXKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(_cfg.tickSFXKey, worldPosition: transform.position);
        }
    }

    void End()
    {
        if (_cfg.despawnVFX)
        {
            float lifetime = _cfg.vfxLifetime > 0f ? _cfg.vfxLifetime : 3f;
            VfxPoolService.Instance.Play(_cfg.despawnVFX, transform.position, Quaternion.identity, lifetime);
        }
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.25f);
        float r = _configured && _cfg.radius > 0f ? _cfg.radius : 4f;
        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}
