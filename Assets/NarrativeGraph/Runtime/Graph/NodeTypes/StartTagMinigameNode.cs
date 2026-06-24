using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nodo que inicia el minijuego "Pilla Pilla" (Tag) y espera a que el jugador gane para avanzar.
/// El rollback narrativo (nodo al que volver si se aborta) se configura en el TagMinigameController.
/// </summary>
[Serializable]
public sealed class StartTagMinigameNode : NarrativeNode
{
    [Tooltip("ID del minijuego (coincide con TagMinigameController.MinigameId).")]
    public string minigameId = "TAG_MINIGAME_01";

    [Header("Opciones")]
    [Tooltip("Si true, activa el GameObject del minijuego antes de iniciarlo.")]
    public bool activateOnStart = true;

    [Tooltip("Si true, desactiva el GameObject del minijuego al terminar.")]
    public bool deactivateOnEnd = false;

    // Estado interno
    private Action _onWinCallback;
    private Action _onAbortCallback;
    private INarrativeSignals _subscribedSignals;
    private string _eventKey;
    private string _abortKey;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        INarrativeSignals signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("[StartTagMinigameNode] No hay proveedor de señales. Avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        TagMinigameController controller = FindMinigameController();

        if (controller == null)
        {
            Debug.LogError($"[StartTagMinigameNode] No se encontró TagMinigameController con id='{minigameId}'.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (controller.IsAlreadyCompleted())
        {
            Debug.Log($"[StartTagMinigameNode] Minijuego '{minigameId}' ya completado → avanzando.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (activateOnStart && !controller.gameObject.activeInHierarchy)
            controller.gameObject.SetActive(true);

        _eventKey = $"MINIGAME_{minigameId}_WON";
        _onWinCallback = () =>
        {
            Debug.Log($"[StartTagMinigameNode] Minijuego '{minigameId}' ganado. Avanzando en el grafo.");
            if (deactivateOnEnd && controller != null)
                controller.gameObject.SetActive(false);
            SafeUnsubscribe();
            onReadyToAdvance?.Invoke();
        };

        // El abort lo gestiona el controller (rollback + señal). Aquí solo limpiamos suscripciones.
        _abortKey = $"MINIGAME_ABORTED:{minigameId}";
        _onAbortCallback = () =>
        {
            Debug.Log($"[StartTagMinigameNode] Minijuego '{minigameId}' abortado.");
            SafeUnsubscribe();
        };

        try
        {
            signals.OnCustom(_eventKey, _onWinCallback);
            signals.OnCustom(_abortKey, _onAbortCallback);
            _subscribedSignals = signals;
            Debug.Log($"[StartTagMinigameNode] Suscrito a '{_eventKey}' y '{_abortKey}'. Iniciando minijuego...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StartTagMinigameNode] Error al suscribirse: {ex.Message}");
            onReadyToAdvance?.Invoke();
            return;
        }

        controller.StartMinigame();
    }

    public override void Exit(NarrativeContext ctx)
    {
        SafeUnsubscribe();
    }

    private void SafeUnsubscribe()
    {
        if (_subscribedSignals != null)
        {
            try
            {
                if (_onWinCallback != null && !string.IsNullOrEmpty(_eventKey))
                    _subscribedSignals.OffCustom(_eventKey, _onWinCallback);
                if (_onAbortCallback != null && !string.IsNullOrEmpty(_abortKey))
                    _subscribedSignals.OffCustom(_abortKey, _onAbortCallback);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartTagMinigameNode] Error al desuscribirse: {ex.Message}");
            }
        }

        _subscribedSignals = null;
        _onWinCallback     = null;
        _onAbortCallback   = null;
    }

    private TagMinigameController FindMinigameController()
    {
        if (!string.IsNullOrEmpty(minigameId))
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var controllers = root.GetComponentsInChildren<TagMinigameController>(true);
                    foreach (var ctrl in controllers)
                    {
                        if (ctrl.MinigameId == minigameId)
                            return ctrl;
                    }
                }
            }
        }

        Debug.LogError($"[StartTagMinigameNode] No se encontró TagMinigameController con minigameId='{minigameId}'.");
        return null;
    }
}
