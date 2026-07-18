using UnityEngine;

/// <summary>
/// Registra este objeto como marcador en el minimapa.
/// Se añade automáticamente a los NPCs con quest activa vía NPCQuestIconManager.
/// El MinimapUIController lee esta información y dibuja el icono en la UI.
/// </summary>
[DisallowMultipleComponent]
public class MinimapMarker : MonoBehaviour
{
    [SerializeField] Sprite icon;
    [SerializeField] Color color = Color.yellow;

    bool _visible;

    public Sprite Icon        => icon;
    public Color MarkerColor  => color;
    public bool IsVisible     => _visible && isActiveAndEnabled;
    public Vector3 WorldPosition => transform.position;

    void OnEnable()
    {
        MinimapUIController.Instance?.Register(this);
    }

    void Start()
    {
        // Segundo intento por si OnEnable disparó antes de que Instance existiera
        MinimapUIController.Instance?.Register(this);
    }

    void OnDisable()
    {
        // El icono de UI se ocultará automáticamente porque IsVisible devuelve false
        // cuando isActiveAndEnabled es false. No hace falta unregister.
    }

    void OnDestroy()
    {
        MinimapUIController.Instance?.Unregister(this);
    }

    // ── API pública ──────────────────────────────────────────────────────────

    public void SetIcon(Sprite newIcon, Color newColor)
    {
        icon  = newIcon;
        color = newColor;
        MinimapUIController.Instance?.RefreshIcon(this);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        // Garantizar registro si SetVisible se llama antes de Start
        if (visible)
            MinimapUIController.Instance?.Register(this);
    }

#if UNITY_EDITOR
    // Solo para herramientas de editor — distingue el flag interno del estado del GO
    public bool Editor_InternalVisible => _visible;
#endif
}
