using System.Collections;
using UnityEngine;
#if UNITY_VISUAL_EFFECT_GRAPH
using UnityEngine.VFX;
#endif

public class AutoDestroyVFX : MonoBehaviour
{
    [Tooltip("Tiempo máximo de vida por seguridad, aunque algún PS quede 'colgado'.")]
    [SerializeField] private float maxLifetime = 6f;

    [Tooltip("Margen extra antes de destruir.")]
    [SerializeField] private float extraTime = 0.2f;

    private IEnumerator Start()
    {
        // 1) Particles (Shuriken)
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (main.loop) main.loop = false; // forzamos no-loop en esta instancia
            ps.Play(true);                     // asegúrate de que están jugando
        }

        // 2) VFX Graph (si lo usas)
#if UNITY_VISUAL_EFFECT_GRAPH
        var vfxs = GetComponentsInChildren<VisualEffect>(true);
        foreach (var v in vfxs) v.Play();
#endif

        float t = 0f;
        while (t < maxLifetime)
        {
            bool anyAlive = false;

            // ¿Sigue vivo algún PS?
            foreach (var ps in systems)
            {
                if (ps != null && ps.IsAlive(true)) { anyAlive = true; break; }
            }

#if UNITY_VISUAL_EFFECT_GRAPH
            if (!anyAlive)
            {
                // Si hay VFX Graph, mira si alguno sigue emitiendo
                foreach (var v in vfxs)
                {
                    if (v != null && (v.aliveParticleCount > 0 || v.aliveParticleCountLastFrame > 0))
                    {
                        anyAlive = true; break;
                    }
                }
            }
#endif

            if (!anyAlive) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(extraTime);
        Destroy(gameObject);
    }
}