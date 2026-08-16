using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cursor de hardware personalizado para todo el juego (menús, HUD, cualquier UI con ratón).
/// Sustituye el cursor del sistema operativo por dos texturas propias: una de reposo
/// (estrella, acorde al nombre del juego) y otra que aparece automáticamente al pasar
/// sobre cualquier <see cref="Selectable"/> interactable (botón, slider, dropdown, toggle...),
/// sin necesidad de tocar cada Controller de menú uno a uno.
///
/// Setup en el Editor (una sola vez, en Start.unity):
/// 1. Crear un GameObject vacío en Start.unity, ej. "CursorManager".
/// 2. Añadir este componente.
/// 3. Asignar <see cref="defaultCursorTexture"/> e <see cref="interactCursorTexture"/>
///    (Import Settings de cada textura: Texture Type = "Cursor", o "Default" con
///    "Read/Write Enabled" activado — si no, Cursor.SetCursor falla silenciosamente en build).
/// 4. Ajustar los hotspots si se sustituyen las texturas placeholder por arte definitivo.
///
/// Start.unity es la escena que siempre está cargada (ver AGENTS.md § 1), así que esto
/// cubre automáticamente MainMenu, Credits, HUD in-game, pausa, etc. — no hace falta
/// repetir el setup en cada escena.
/// </summary>
[DisallowMultipleComponent]
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Texturas (Import Settings: Read/Write Enabled = true)")]
    [SerializeField] private Texture2D defaultCursorTexture;
    [SerializeField] private Texture2D interactCursorTexture;

    [Header("Hotspot (punto activo del cursor dentro de la imagen, en píxeles)")]
    [SerializeField] private Vector2 defaultHotspot = new Vector2(32f, 32f);
    [SerializeField] private Vector2 interactHotspot = new Vector2(10f, 6f);

    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    [Header("Detección de hover")]
    [Tooltip("Cada cuántos frames se reevalúa el hover. 1 = cada frame (recomendado).")]
    [Min(1)] [SerializeField] private int hoverCheckEveryNFrames = 1;

    // Buffer reutilizado a propósito: un raycast de UI cambia de objetivo cada frame,
    // así que no se puede cachear en Awake como con las referencias normales del proyecto;
    // lo que sí evitamos es el alloc, reusando siempre la misma lista (regla TDD.md § 12).
    private static readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>(8);

    private PointerEventData _pointerEventData;
    private bool _isOverInteractable;
    private bool _cursorApplied;
    private int _frameCounter;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    void Start()
    {
        ApplyDefaultCursor();
    }

    void Update()
    {
        if (++_frameCounter < hoverCheckEveryNFrames) return;
        _frameCounter = 0;

        bool overInteractable = IsPointerOverInteractable();

        // Guard de estado previo (misma regla que SetActive): solo tocamos el cursor
        // del sistema cuando el estado de hover realmente cambia.
        if (!_cursorApplied || overInteractable != _isOverInteractable)
        {
            _isOverInteractable = overInteractable;
            _cursorApplied = true;

            if (overInteractable)
                ApplyInteractCursor();
            else
                ApplyDefaultCursor();
        }
    }

    bool IsPointerOverInteractable()
    {
        var es = EventSystem.current;
        if (es == null) return false;

        Vector2 pointerPos;
        if (!TryGetPointerPosition(out pointerPos))
            return false; // sin ratón conectado (solo mando) -> no forzamos hover

        if (_pointerEventData == null)
            _pointerEventData = new PointerEventData(es);

        _pointerEventData.position = pointerPos;

        _raycastBuffer.Clear();
        es.RaycastAll(_pointerEventData, _raycastBuffer);

        for (int i = 0; i < _raycastBuffer.Count; i++)
        {
            var go = _raycastBuffer[i].gameObject;
            if (go == null) continue;

            var selectable = go.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.interactable)
                return true;
        }

        return false;
    }

    static bool TryGetPointerPosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        position = default;
        return false;
#else
        position = Input.mousePosition;
        return true;
#endif
    }

    public void ApplyDefaultCursor()
    {
        if (defaultCursorTexture == null) return;
        Cursor.SetCursor(defaultCursorTexture, defaultHotspot, cursorMode);
    }

    public void ApplyInteractCursor()
    {
        if (interactCursorTexture == null) return;
        Cursor.SetCursor(interactCursorTexture, interactHotspot, cursorMode);
    }
}
