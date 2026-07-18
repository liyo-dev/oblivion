using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona los iconos de quest sobre el minimapa como elementos de UI.
///
/// Comportamiento:
///   - Si el NPC está dentro del rango de la cámara del minimapa →
///     el icono aparece en la posición proporcional sobre el mapa.
///   - Si el NPC está fuera del rango →
///     el icono se ancla al borde del círculo del minimapa
///     para indicar la dirección hacia donde ir.
///
/// Setup en la escena:
///   1. Añadir este componente al mismo GameObject que MinimapController
///      (o a cualquier GameObject activo en la escena).
///   2. Asignar minimapRect: el RectTransform del círculo del minimapa (la RawImage).
///   3. Crear un hijo vacío dentro del minimapRect llamado "IconsParent",
///      con anchorMin/Max = (0.5, 0.5), pivot = (0.5, 0.5), sizeDelta = (0, 0).
///      Asignarlo al campo iconsParent.
///   4. Asignar un Sprite por defecto para los marcadores sin icono.
/// </summary>
[DisallowMultipleComponent]
public class MinimapUIController : MonoBehaviour
{
    public static MinimapUIController Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [Header("Referencias UI")]
    [Tooltip("RectTransform del círculo del minimapa (la RawImage)")]
    [SerializeField] RectTransform minimapRect;

    [Tooltip("Hijo vacío centrado dentro del minimapRect donde se crean los iconos")]
    [SerializeField] RectTransform iconsParent;

    [Header("Iconos")]
    [Tooltip("Sprite usado cuando el marcador no tiene icono asignado")]
    [SerializeField] Sprite defaultMarkerSprite;

    [Tooltip("Tamaño del icono cuando está dentro del minimapa (px)")]
    [SerializeField] float markerSizeInside = 24f;

    [Tooltip("Tamaño del icono cuando está fijado al borde (px)")]
    [SerializeField] float markerSizeEdge = 18f;

    [Tooltip("Distancia mínima al borde interior del círculo para los iconos de borde (px)")]
    [SerializeField] float edgePadding = 12f;

    [Tooltip("Opacidad de los iconos fijados al borde (0-1)")]
    [SerializeField] float edgeAlpha = 0.85f;

    // ── Datos internos ───────────────────────────────────────────────────────

    class Entry
    {
        public MinimapMarker marker;
        public Image          icon;
    }

    readonly List<Entry> _entries = new();
    float _radius;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RefreshRadius();
    }

    void LateUpdate()
    {
        var ctrl = MinimapController.Instance;
        if (ctrl == null) return;

        Vector3 playerPos = ctrl.PlayerPosition;
        float   orthoSize = ctrl.OrthoSize;
        if (orthoSize <= 0f) return;

        RefreshRadius();

        foreach (var entry in _entries)
        {
            if (entry.marker == null || entry.icon == null) continue;

            bool visible = entry.marker.IsVisible;
            entry.icon.gameObject.SetActive(visible);
            if (!visible) continue;

            // Posición relativa normalizada ([-1, 1] si está dentro del rango de cámara)
            Vector3 wp = entry.marker.WorldPosition;
            float nx = (wp.x - playerPos.x) / orthoSize;
            float nz = (wp.z - playerPos.z) / orthoSize;
            var normalized = new Vector2(nx, nz);
            float dist = normalized.magnitude;

            Vector2 pixelPos;
            bool isEdge = dist > 1f;

            if (!isEdge)
            {
                // Dentro del mapa: posición proporcional
                pixelPos = normalized * _radius;
                entry.icon.rectTransform.sizeDelta = Vector2.one * markerSizeInside;
                entry.icon.color = new Color(entry.marker.MarkerColor.r,
                                             entry.marker.MarkerColor.g,
                                             entry.marker.MarkerColor.b,
                                             1f);
            }
            else
            {
                // Fuera del mapa: fijar al borde del círculo
                pixelPos = normalized.normalized * (_radius - edgePadding);
                entry.icon.rectTransform.sizeDelta = Vector2.one * markerSizeEdge;
                entry.icon.color = new Color(entry.marker.MarkerColor.r,
                                             entry.marker.MarkerColor.g,
                                             entry.marker.MarkerColor.b,
                                             edgeAlpha);
            }

            entry.icon.rectTransform.anchoredPosition = pixelPos;
        }
    }

    // ── API pública ──────────────────────────────────────────────────────────

    public void Register(MinimapMarker marker)
    {
        // Evitar duplicados
        foreach (var e in _entries)
            if (e.marker == marker) return;

        if (iconsParent == null)
        {
            Debug.LogWarning("[MinimapUIController] iconsParent no asignado. Asígnalo en el Inspector.");
            return;
        }

        var go  = new GameObject($"MapIcon_{marker.name}");
        go.transform.SetParent(iconsParent, false);

        var img = go.AddComponent<Image>();
        img.sprite = marker.Icon != null ? marker.Icon : defaultMarkerSprite;
        img.color  = marker.MarkerColor;
        img.rectTransform.anchorMin  = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchorMax  = new Vector2(0.5f, 0.5f);
        img.rectTransform.pivot      = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta  = Vector2.one * markerSizeInside;
        img.rectTransform.anchoredPosition = Vector2.zero;
        go.SetActive(false);

        _entries.Add(new Entry { marker = marker, icon = img });
    }

    public void Unregister(MinimapMarker marker)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].marker != marker) continue;
            if (_entries[i].icon != null)
                Destroy(_entries[i].icon.gameObject);
            _entries.RemoveAt(i);
        }
    }

    public void RefreshIcon(MinimapMarker marker)
    {
        // Si todavía no está registrado (SetIcon llamado antes de Start), registrarlo ahora
        Register(marker);

        foreach (var e in _entries)
        {
            if (e.marker != marker || e.icon == null) continue;
            e.icon.sprite = marker.Icon != null ? marker.Icon : defaultMarkerSprite;
            e.icon.color  = marker.MarkerColor;
            break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void RefreshRadius()
    {
        if (minimapRect != null)
            _radius = minimapRect.rect.width * 0.5f;
    }

#if UNITY_EDITOR
    public struct DebugEntry
    {
        public MinimapMarker marker;
        public bool          hasUIIcon;
    }

    public System.Collections.Generic.List<DebugEntry> Editor_GetEntries()
    {
        var result = new System.Collections.Generic.List<DebugEntry>(_entries.Count);
        foreach (var e in _entries)
            result.Add(new DebugEntry { marker = e.marker, hasUIIcon = e.icon != null });
        return result;
    }
#endif
}
