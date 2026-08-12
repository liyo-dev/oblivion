using System.Collections.Generic;
using Game.Core.Pooling;
using UnityEngine;

/// <summary>
/// Servicio global de pooling para VFX de un solo uso (impactos, explosiones, despawns, etc.).
/// Sustituye el patrón "Instantiate + Destroy(fx, t)" repetido por todo el proyecto, que
/// generaba spikes de GC en combate (ver TDD.md § 13, familia de bugs C1-C6 y comentarios
/// "TODO: Usar un sistema de pooling" en GolemBossAI).
///
/// Se auto-crea al arrancar el juego (patrón igual a HudToastService); no requiere prefab
/// ni setup en escena. Un único pool por prefab, un único Update centralizado para las
/// devoluciones (nunca coroutines por instancia, ver CLAUDE.md § 2).
///
/// Uso:
///   VfxPoolService.Instance.Play(_cfg.impactVFX, hitPoint, Quaternion.identity, vfxLifetime);
/// </summary>
public class VfxPoolService : MonoBehaviour
{
    public static VfxPoolService Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        var root = new GameObject("[VfxPoolService]");
        DontDestroyOnLoad(root);
        root.AddComponent<VfxPoolService>();
    }

    private const float DefaultLifetime = 3f;
    private const int InitialPoolSize = 4;
    private const int MaxPoolSizePerPrefab = 64; // por prefab; suficiente para VFX de impacto simultáneos

    // Un pool por prefab. Se poolea el Transform porque todo GameObject lo tiene:
    // así reutilizamos ObjectPool<T> sin duplicar su lógica para GameObject.
    private readonly Dictionary<GameObject, ObjectPool<Transform>> _pools = new(32);

    // Instancia activa -> pool de origen, para devolverla sin volver a buscar por prefab.
    private readonly Dictionary<Transform, ObjectPool<Transform>> _instancePool = new(64);

    private struct ActiveVfx
    {
        public Transform instance;
        public float returnAt;
    }

    // Cola de expiración procesada en un único Update (evita una coroutine por VFX activo).
    private readonly List<ActiveVfx> _active = new(64);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        float now = Time.time;
        // Iterar hacia atrás para poder hacer swap-remove sin allocations ni desajustar índices.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveVfx entry = _active[i];
            if (entry.instance == null)
            {
                // FIX A3 (auditoría 2026-08-07): si el VFX murió con su "parent" externo (ver
                // Play(..., parent:)) en vez de expirar por lifetime, antes solo se quitaba de
                // _active — el ObjectPool<Transform> de origen seguía contando esta instancia
                // como "en uso" para siempre (nunca pasa por Return/ReturnInternal). Tras
                // MaxPoolSizePerPrefab (64) muertes así, ese prefab deja de poder Get() nuevas
                // instancias y ese VFX deja de verse el resto de la sesión. Purgar aquí también
                // _instancePool y liberar el hueco en el ObjectPool de origen (sin reactivar
                // nada, el GameObject ya no existe).
                if (_instancePool.TryGetValue(entry.instance, out ObjectPool<Transform> deadPool))
                {
                    deadPool.ForceRelease(entry.instance);
                    _instancePool.Remove(entry.instance);
                }
                _active.RemoveAt(i);
                continue;
            }
            if (now >= entry.returnAt)
            {
                ReturnInternal(entry.instance);
                _active.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Reproduce un VFX pooled en la posición/rotación indicadas y lo devuelve al pool
    /// automáticamente tras "lifetime" segundos (por defecto 3s, igual que el Destroy manual
    /// que sustituye). Devuelve el Transform de la instancia por si el llamador necesita
    /// ajustar escala u otros parámetros puntuales (ver GolemBossAI).
    /// </summary>
    public Transform Play(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = DefaultLifetime, Transform parent = null)
    {
        if (prefab == null) return null;

        if (!_pools.TryGetValue(prefab, out ObjectPool<Transform> pool))
        {
            pool = new ObjectPool<Transform>(prefab.transform, InitialPoolSize, MaxPoolSizePerPrefab, expandable: true, parent: transform);
            _pools[prefab] = pool;
        }

        Transform instance = pool.Get();
        if (instance == null) return null; // pool agotado (no debería pasar con expandable:true)

        Transform instanceParent = parent != null ? parent : transform;
        instance.SetParent(instanceParent, worldPositionStays: true);
        instance.SetPositionAndRotation(position, rotation);
        instance.localScale = prefab.transform.localScale;

        RestartParticles(instance);

        _instancePool[instance] = pool;
        _active.Add(new ActiveVfx { instance = instance, returnAt = Time.time + (lifetime > 0f ? lifetime : DefaultLifetime) });

        return instance;
    }

    private static void RestartParticles(Transform instance)
    {
        // Un ParticleSystem pooled queda "muerto" al desactivarse (ObjectPool.Return hace SetActive(false));
        // no se reproduce solo al reactivarse, hay que forzar Clear + Play.
        var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }

    private void ReturnInternal(Transform instance)
    {
        if (instance == null) return;
        if (_instancePool.TryGetValue(instance, out ObjectPool<Transform> pool))
        {
            _instancePool.Remove(instance);
            pool.Return(instance);
        }
        else
        {
            // No pertenece a ningún pool conocido (no debería pasar); evitar un leak silencioso.
            Destroy(instance.gameObject);
        }
    }
}
