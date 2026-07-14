using System;
using Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra u oculta el TutorialPromptUI.
/// Show: el grafo ESPERA hasta que el jugador pulse la acción indicada (A/Submit/Interact) y
/// entonces oculta el prompt antes de avanzar.
/// Hide: oculta inmediatamente y avanza.
/// </summary>
[Serializable]
public sealed class TutorialPromptNode : NarrativeNode
{
    public enum PromptAction { Show, Hide }

    public PromptAction action = PromptAction.Show;

    [TextArea(1, 2)]
    public string text;
    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;
    [Tooltip("Icono de botón que aparece a la izquierda del texto (opcional).")]
    public Sprite icon;

    [System.NonSerialized]
    private Action<GamepadInputReader.InputEvent> _waitHandler;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = TutorialPromptUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[TutorialPromptNode] ❌ TutorialPromptUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (action == PromptAction.Hide)
        {
            ui.Hide();
            onReadyToAdvance?.Invoke();
            return;
        }

        string resolved = string.IsNullOrEmpty(textId)
            ? text
            : LocalizationManager.Instance?.Get(textId, text) ?? text;
        ui.Show(resolved, icon);

        // Esperar a que el jugador pulse A (Interact del GamePlay map, o Submit como fallback
        // cuando el mapa GamePlay está deshabilitado, p.ej. en modo Cinematic).
        _waitHandler = (GamepadInputReader.InputEvent evt) =>
        {
            if (evt.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
            bool isConfirm = evt.Type == GamepadInputReader.InputEventType.Interact
                          || evt.Type == GamepadInputReader.InputEventType.Submit;
            if (!isConfirm) return;

            GamepadInputReader.OnInput -= _waitHandler;
            _waitHandler = null;
            TutorialPromptUI.Instance?.Hide();
            onReadyToAdvance?.Invoke();
        };

        GamepadInputReader.OnInput += _waitHandler;
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_waitHandler != null)
        {
            GamepadInputReader.OnInput -= _waitHandler;
            _waitHandler = null;
        }
        TutorialPromptUI.Instance?.Hide();
    }
}
