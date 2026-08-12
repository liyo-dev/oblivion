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
    [Tooltip("Puede incluir el token literal \"{BOTON}\" (ver Core.InputGlyphs.InputGlyphLabels), " +
             "que se sustituye en tiempo real por el nombre corto de la tecla/botón real según el " +
             "dispositivo activo (p.ej. \"E\" en teclado, \"A\" en Xbox). Solo funciona si " +
             "'buttonName' está relleno. NO escribir a mano el nombre de una tecla/botón concreta " +
             "aquí (p.ej. \"Pulsa A...\" o \"Usa el Joystick...\"): sería incorrecto en cuanto el " +
             "dispositivo activo no coincida con lo escrito — usar el token en su lugar.")]
    public string text;
    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;
    [Tooltip("Icono de botón que aparece a la izquierda del texto (opcional). Si 'buttonName' " +
             "está relleno, este campo solo se usa como respaldo por si el nombre no resuelve nada " +
             "para la familia activa.")]
    public Sprite icon;
    [Tooltip("Nombre simbólico del botón/acción (constantes en Core.InputGlyphs.InputGlyphNames). " +
             "Resuelve el icono Y, si 'text' usa el token {BOTON}, también el literal — ambos en " +
             "tiempo real según el mando/teclado activo. Usar InputGlyphNames.Confirm (no South) " +
             "para cualquier prompt que dependa de UI/Submit en vez de GamePlay/Interact (p.ej. " +
             "cualquier \"pulsa para continuar\" que aparezca con el mapa GamePlay deshabilitado, " +
             "como en ActionMode.Cinematic — ver PlayerLockService.ApplyHardLock): en mando ambos " +
             "botones son físicamente el mismo, pero en teclado Interactuar (E) y Confirmar " +
             "(Espacio/Enter) son teclas distintas, y usar South ahí mostraría 'E' para algo que " +
             "solo funciona con Espacio.")]
    public string buttonName;

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

        // La resolución de icono (y, si 'resolved' trae el token {BOTON}, también del literal) la
        // hace ahora TutorialPromptUI internamente, en caliente — así que si el jugador cambia de
        // mando/teclado con el prompt ya visible, texto e icono se actualizan solos en vez de
        // quedarse fijados al dispositivo que estaba activo cuando se llamó a Show().
        ui.Show(resolved, buttonName, icon);

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
