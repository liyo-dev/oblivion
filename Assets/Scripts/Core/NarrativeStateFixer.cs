using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente auxiliar que se auto-crea para corregir inconsistencias del grafo narrativo.
/// Se destruye automáticamente después de completar su trabajo.
/// </summary>
public class NarrativeStateFixer : MonoBehaviour
{
    private static NarrativeStateFixer _instance;

    public static void EmitMissingEvents(List<string> events)
    {
        if (events == null || events.Count == 0)
            return;

        // Crear instancia si no existe
        if (_instance == null)
        {
            var go = new GameObject("[NarrativeStateFixer]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NarrativeStateFixer>();
        }

        _instance.StartCoroutine(_instance.EmitEventsDelayed(events));
    }

    private IEnumerator EmitEventsDelayed(List<string> events)
    {
        Debug.Log($"[NarrativeStateFixer] 🔧 Esperando a que el sistema narrativo se inicialice...");

        // Esperar a que NarrativeGraphHub esté disponible
        while (NarrativeGraphHub.Instance == null)
        {
            yield return null;
        }

        // Esperar un poco más para que el grafo termine de restaurarse
        yield return new WaitForSeconds(0.5f);

        // Buscar DefaultNarrativeSignals
        var signals = FindFirstObjectByType<DefaultNarrativeSignals>();
        if (signals == null)
        {
            Debug.LogError("[NarrativeStateFixer] ❌ No se encontró DefaultNarrativeSignals. Abortando corrección.");
            Destroy(gameObject);
            yield break;
        }

        Debug.Log($"[NarrativeStateFixer] 📤 Emitiendo {events.Count} eventos narrativos faltantes...");

        foreach (var eventKey in events)
        {
            Debug.Log($"[NarrativeStateFixer]   → {eventKey}");
            signals.RaiseCustom(eventKey);
            yield return new WaitForSeconds(0.2f); // Pequeña pausa entre eventos
        }

        Debug.Log("[NarrativeStateFixer] ✅ Corrección completada - Destruyendo componente auxiliar");
        
        // Auto-destruirse después de completar el trabajo
        Destroy(gameObject);
        _instance = null;
    }
}
