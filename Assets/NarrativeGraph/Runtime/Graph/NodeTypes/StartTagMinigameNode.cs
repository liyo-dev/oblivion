using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nodo que inicia el minijuego "Pilla Pilla" (Tag) y espera a que el jugador gane para avanzar.
/// - Busca el TagMinigameController por minigameId en las escenas cargadas.
/// - Se suscribe al evento de victoria para continuar el grafo narrativo.
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
    private INarrativeSignals _subscribedSignals;
    private string _eventKey;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        INarrativeSignals signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("[StartTagMinigameNode] No hay proveedor de señales. Avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Buscar el controlador del minijuego
        TagMinigameController controller = FindMinigameController();

        if (controller == null)
        {
            Debug.LogError($"[StartTagMinigameNode] No se encontró TagMinigameController con id='{minigameId}'.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Si el minijuego ya fue completado en una sesión anterior, avanzar sin bloquearse
        if (controller.IsAlreadyCompleted())
        {
            Debug.Log($"[StartTagMinigameNode] Minijuego '{minigameId}' ya completado → avanzando sin reiniciar.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Activar si es necesario
        if (activateOnStart && !controller.gameObject.activeInHierarchy)
        {
            controller.gameObject.SetActive(true);
        }

        // Configurar evento de victoria
        _eventKey = $"MINIGAME_{minigameId}_WON";
        _onWinCallback = () =>
        {
            Debug.Log($"[StartTagMinigameNode] Minijuego '{minigameId}' ganado. Avanzando en el grafo.");
            
            // Desactivar si se configuró así
            if (deactivateOnEnd && controller != null)
            {
                controller.gameObject.SetActive(false);
            }

            // Desuscribirse y avanzar
            SafeUnsubscribe();
            onReadyToAdvance?.Invoke();
        };

        // Suscribirse al evento de victoria
        try
        {
            signals.OnCustom(_eventKey, _onWinCallback);
            _subscribedSignals = signals;
            Debug.Log($"[StartTagMinigameNode] Suscrito a '{_eventKey}'. Iniciando minijuego...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StartTagMinigameNode] Error al suscribirse: {ex.Message}");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Iniciar el minijuego
        controller.StartMinigame();
    }

    public override void Exit(NarrativeContext ctx)
    {
        SafeUnsubscribe();
    }

    private void SafeUnsubscribe()
    {
        if (_subscribedSignals != null && _onWinCallback != null && !string.IsNullOrEmpty(_eventKey))
        {
            try
            {
                _subscribedSignals.OffCustom(_eventKey, _onWinCallback);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartTagMinigameNode] Error al desuscribirse: {ex.Message}");
            }
        }

        _subscribedSignals = null;
        _onWinCallback = null;
    }

    private TagMinigameController FindMinigameController()
    {
        // Buscar por ID en todas las escenas cargadas (incluye objetos inactivos)
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
                        {
                            return ctrl;
                        }
                    }
                }
            }
        }

        Debug.LogError($"[StartTagMinigameNode] No se encontró TagMinigameController con minigameId='{minigameId}' en ninguna escena cargada.");
        return null;
    }
}
