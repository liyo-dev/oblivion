// AdditiveSceneCinematicNode.cs
using System;
using UnityEngine;

[Obsolete("AdditiveSceneCinematicNode está obsoleto. Usa PlayCinematicNode o el sistema AdditiveSceneCinematic directamente.")]
[Serializable]
public sealed class AdditiveSceneCinematicNode : NarrativeNode
{
    public string cinematicSceneName;
    public bool useInternalCinematic = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        AdditiveSceneCinematic c = null;
        if (!string.IsNullOrEmpty(cinematicSceneName))
        {
            var all = UnityEngine.Object.FindObjectsByType<AdditiveSceneCinematic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var a in all)
            {
                try { if (a.CinematicSceneName == cinematicSceneName) { c = a; break; } }
                catch (Exception ex) { Debug.LogWarning($"[AdditiveSceneCinematicNode] Error checking cinematic name: {ex.Message}"); }
            }
        }

        if (c != null)
        {
            ctx.Runner.StartCoroutine(PlayAndAdvance(c, onReadyToAdvance));
            return;
        }

        if (!string.IsNullOrEmpty(cinematicSceneName) && useInternalCinematic)
        {
            var go = new GameObject("_Runtime_AdditiveSceneCinematic");
            var comp = go.AddComponent<AdditiveSceneCinematic>();
            comp.CinematicSceneName = cinematicSceneName;
            ctx.Runner.StartCoroutine(PlayTempAndAdvance(comp, onReadyToAdvance, go));
            return;
        }

        onReadyToAdvance?.Invoke();
    }

    System.Collections.IEnumerator PlayAndAdvance(AdditiveSceneCinematic c, Action done)
    {
        var e = c.PlayAndBlock();
        if (e != null) yield return e;
        done?.Invoke();
    }

    System.Collections.IEnumerator PlayTempAndAdvance(AdditiveSceneCinematic c, Action done, GameObject owner)
    {
        var e = c.PlayAndBlock();
        if (e != null) yield return e;
        done?.Invoke();
        if (owner != null) UnityEngine.Object.Destroy(owner);
    }
}

// Este nodo se ha deshabilitado porque actualmente no hay referencias a este tipo en los assets del grafo.
// Puedes eliminar el archivo si confirmas que no lo necesitas.
