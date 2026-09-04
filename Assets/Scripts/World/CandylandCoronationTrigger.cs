using System;
using UnityEngine;

/// <summary>
/// Dispara la escena de la coronación de Estela en Chuchelandia (GDD escena 17, prueba de Estela)
/// la primera vez que el jugador entra en el trigger. Deliberadamente NO usa
/// NPCInteractiveNarrativeExecutor (motor narrativo "congelado", ver AGENTS.md §5) ni el grafo
/// narrativo real (TDD.md §10 avisa de que tocarlo a ciegas es caro de depurar, y las escenas 15-22
/// no tienen todavía ningún nodo) — es un disparador autocontenido, igual de sencillo que
/// LevelExit.cs, pensado para el primer pase de prueba que pidió Raúl ("meter cualquier cosilla,
/// diálogos y ya"). Cuando se decida cómo enganchar esto a la narrativa real, sustituir este
/// componente es un cambio aislado, no toca nada del resto de la escena.
///
/// Al terminar el diálogo dispara el beat de liberación: apaga la jaula (el Gate de Sweet_Land
/// delante de la criatura) y recolorea al Duque con sus materiales "después" (pasados aquí ya
/// hechos por CandylandClimaxBuilder — este script no crea materiales, solo los intercambia,
/// renderer a renderer, respetando el número real de slots de cada uno).
/// </summary>
[RequireComponent(typeof(Collider))]
public class CandylandCoronationTrigger : MonoBehaviour
{
    [Serializable]
    public class RendererMaterialSwap
    {
        public Renderer renderer;
        public Material[] afterMaterials;
    }

    [Tooltip("Diálogo de la coronación/negativa/liberación (DG_CANDYLAND_CORONACION).")]
    [SerializeField] private DialogueAsset dialogue;

    [SerializeField] private string playerTag = "Player";

    [Header("Beat de liberación (al terminar el diálogo)")]
    [Tooltip("El Gate de Sweet_Land colocado delante de la criatura a modo de jaula. Se desactiva al liberarla.")]
    [SerializeField] private GameObject cageObject;

    [Tooltip("Un elemento por Renderer del Duque, con su set completo de materiales 'después' (mismo número de slots que sharedMaterials).")]
    [SerializeField] private RendererMaterialSwap[] duqueAfterSwaps;

    private bool _hasPlayed;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasPlayed) return;
        if (!other.CompareTag(playerTag)) return;
        if (dialogue == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CandylandCoronationTrigger] No hay DialogueAsset asignado, no se dispara nada.");
#endif
            return;
        }
        if (DialogueManager.Instance == null) return;

        _hasPlayed = true;
        DialogueManager.Instance.StartDialogue(dialogue, onFinished: OnCoronationDialogueFinished);
    }

    private void OnCoronationDialogueFinished()
    {
        if (cageObject != null) cageObject.SetActive(false);

        if (duqueAfterSwaps != null)
        {
            foreach (var swap in duqueAfterSwaps)
            {
                if (swap == null || swap.renderer == null || swap.afterMaterials == null || swap.afterMaterials.Length == 0) continue;
                swap.renderer.sharedMaterials = swap.afterMaterials;
            }
        }

        // Enganche pendiente: aquí es donde Raúl decide la resolución real de la prueba de Estela
        // (avance de quest, señal narrativa, transición de escena) cuando conecte esto a la
        // narrativa real — de momento el beat es solo visual (jaula fuera, Duque agrietado).
    }
}
