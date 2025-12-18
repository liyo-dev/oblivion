using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Sistema compatible con URP que cambia temporalmente las layers del player
/// solo durante el render de la cámara de retrato, para aislar al player del mundo.
/// </summary>
public class PortraitLayerSwapSRP : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera portraitCamera;
    [SerializeField] private Transform playerRoot;

    [Header("Configuración")]
    [Tooltip("Nombre de la layer usada para el retrato (debe existir en el proyecto)")]
    [SerializeField] private string portraitLayerName = "UI_Portrait";

    private int _portraitLayer = -1;
    private readonly Dictionary<GameObject, int> _originalLayers = new Dictionary<GameObject, int>();
    private bool _isSwapped;

    void OnEnable()
    {
        // Resolver la layer por nombre
        _portraitLayer = LayerMask.NameToLayer(portraitLayerName);
        if (_portraitLayer < 0)
        {
            Debug.LogError($"[PortraitLayerSwapSRP] Layer '{portraitLayerName}' no existe. Por favor créala en el proyecto.");
            enabled = false;
            return;
        }

        // Suscribirse a eventos de render (compatible con URP)
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        // Desuscribirse
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        // Restaurar layers si quedaron cambiadas
        if (_isSwapped)
        {
            RestoreLayers();
        }
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Solo actuar si es NUESTRA cámara de retrato
        if (camera != portraitCamera || portraitCamera == null)
            return;

        // Cambiar layers del player
        SwapToPortraitLayer();
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Solo actuar si es NUESTRA cámara de retrato
        if (camera != portraitCamera || portraitCamera == null)
            return;

        // Restaurar layers originales
        RestoreLayers();
    }

    private void SwapToPortraitLayer()
    {
        if (_isSwapped || playerRoot == null)
            return;

        _originalLayers.Clear();

        // Cambiar recursivamente todas las layers del player y sus hijos
        ChangeLayerRecursive(playerRoot, _portraitLayer);

        _isSwapped = true;
    }

    private void RestoreLayers()
    {
        if (!_isSwapped)
            return;

        // Restaurar todas las layers guardadas
        foreach (var kvp in _originalLayers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.layer = kvp.Value;
            }
        }

        _originalLayers.Clear();
        _isSwapped = false;
    }

    private void ChangeLayerRecursive(Transform root, int newLayer)
    {
        if (root == null)
            return;

        // Guardar layer original
        _originalLayers[root.gameObject] = root.gameObject.layer;

        // Aplicar nueva layer
        root.gameObject.layer = newLayer;

        // Aplicar recursivamente a todos los hijos
        foreach (Transform child in root)
        {
            ChangeLayerRecursive(child, newLayer);
        }
    }

    /// <summary>
    /// Configura las referencias en runtime
    /// </summary>
    public void Setup(Camera camera, Transform player)
    {
        portraitCamera = camera;
        playerRoot = player;

        // Validar que la layer existe
        if (_portraitLayer < 0)
        {
            _portraitLayer = LayerMask.NameToLayer(portraitLayerName);
        }
    }

    /// <summary>
    /// Limpia y restaura todo
    /// </summary>
    public void Cleanup()
    {
        if (_isSwapped)
        {
            RestoreLayers();
        }

        portraitCamera = null;
        playerRoot = null;
    }
}

