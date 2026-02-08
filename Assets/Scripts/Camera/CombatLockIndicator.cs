using UnityEngine;

/// <summary>
/// Indicador visual simple para el sistema de lock de cámara.
/// Dibuja un círculo/ring alrededor del objetivo bloqueado.
/// </summary>
public class CombatLockIndicator : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color ringColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private float ringRadius = 1.5f;
    [SerializeField] private float ringThickness = 0.1f;
    [SerializeField] private int segments = 32;
    [SerializeField] private float rotationSpeed = 90f;
    
    [Header("Pulse Effect")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.2f;
    
    private LineRenderer lineRenderer;
    private float basedRadius;
    
    private void Awake()
    {
        SetupLineRenderer();
        basedRadius = ringRadius;
    }
    
    private void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Configurar LineRenderer
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = segments + 1;
        lineRenderer.startWidth = ringThickness;
        lineRenderer.endWidth = ringThickness;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = ringColor;
        lineRenderer.endColor = ringColor;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        
        // Dibujar el círculo
        DrawCircle();
    }
    
    private void Update()
    {
        // Rotación constante
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        
        // Efecto de pulso
        if (enablePulse)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            ringRadius = basedRadius + pulse;
            DrawCircle();
        }
    }
    
    private void DrawCircle()
    {
        if (lineRenderer == null) return;
        
        float angleStep = 360f / segments;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * ringRadius;
            float z = Mathf.Sin(angle) * ringRadius;
            
            lineRenderer.SetPosition(i, new Vector3(x, 0, z));
        }
    }
    
    private void OnValidate()
    {
        if (lineRenderer != null)
        {
            lineRenderer.startColor = ringColor;
            lineRenderer.endColor = ringColor;
            lineRenderer.startWidth = ringThickness;
            lineRenderer.endWidth = ringThickness;
            DrawCircle();
        }
    }
}
