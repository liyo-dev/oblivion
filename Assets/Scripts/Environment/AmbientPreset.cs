using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName = "AmbientPreset_New", menuName = "El Sendero/Entorno/Ambient Preset", order = 1)]
public class AmbientPreset : ScriptableObject
{
    [Header("Fog Settings")]
    [Tooltip("¿Activar fog con este preset?")]
    public bool enableFog = true;

    [Tooltip("Color del fog")]
    public Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Tooltip("Densidad del fog (0-1). Mayor = más denso. Para niebla cercana usa Linear con fogEnd bajo.")]
    [Range(0f, 1f)]
    public float fogDensity = 0.02f;

    [Tooltip("Modo del fog")]
    public FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Linear Fog (solo si fogMode = Linear)")]
    [Tooltip("Distancia de inicio del fog")]
    public float fogStartDistance = 0f;

    [Tooltip("Distancia final del fog")]
    public float fogEndDistance = 100f;

    [Header("Luz Ambiente")]
    [Tooltip("¿Controlar la luz ambiente con este preset?")]
    public bool controlAmbientLight = false;

    [Tooltip("Color de la luz ambiente")]
    public Color ambientLightColor = Color.white;

    [Tooltip("Intensidad de la luz ambiente")]
    [Range(0f, 2f)]
    public float ambientLightIntensity = 1f;

    [Header("Niebla de Cámara (Overlay)")]
    [Tooltip("Overlay de pantalla completa. Úsalo para simular estar dentro de niebla densa: " +
             "no depende de la distancia a cámara, tapa el entorno inmediato del jugador.")]
    public bool enableCameraOverlay = false;

    [Tooltip("Color del overlay de niebla")]
    public Color overlayColor = new Color(0.85f, 0.88f, 0.92f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Opacidad máxima del overlay (0 = transparente, 1 = completamente opaco).")]
    public float overlayMaxAlpha = 0.85f;

    [Header("Música")]
    [Tooltip("Si está activo, cambia la música al entrar en la zona.")]
    public bool changeMusic = false;

    [Tooltip("Debe coincidir con el 'Zone Id' en AudioGraphProfile → Ambient Zones.")]
    public string musicZoneId = "";

    [Header("Cámara")]
    [Tooltip("Si está activo, la zona anula la configuración de la cámara del jugador al entrar.")]
    public bool controlCamera = false;

    [Tooltip("Preset de cámara. Cada modo ajusta ángulo, altura y restricciones de rotación.")]
    public ZoneCameraMode cameraMode = ZoneCameraMode.SoloDistancia;

    [Range(0f, 360f)]
    [Tooltip("Ángulo horizontal (grados) al que se fija la cámara en modos bloqueados: Plataformas2D, TopDown, Isométrico. " +
             "0=Norte, 90=Este, 180=Sur, 270=Oeste.")]
    public float cameraHorizontalAngle = 90f;

    [Range(0.5f, 12f)]
    [Tooltip("Distancia cámara-jugador. 2.5 = distancia normal de juego.")]
    public float cameraDistance = 2.5f;

    [Header("Transition")]
    [Tooltip("Duración de la transición (segundos)")]
    public float transitionDuration = 1.5f;

    [Tooltip("Ease de la transición")]
    public Ease transitionEase = Ease.InOutSine;

    [Header("Metadata")]
    [TextArea(2, 4)]
    [Tooltip("Descripción del preset para referencia")]
    public string description = "";

    public void ApplyImmediate()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = fogEndDistance;

        if (controlAmbientLight)
        {
            RenderSettings.ambientLight = ambientLightColor;
            RenderSettings.ambientIntensity = ambientLightIntensity;
        }
    }

    public Tween ApplyWithTransition()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = fogMode;

        float startDensity = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd = RenderSettings.fogEndDistance;
        Color startAmbient = RenderSettings.ambientLight;
        float startAmbientI = RenderSettings.ambientIntensity;

        return DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogDensity, t);
                RenderSettings.fogColor = Color.Lerp(startFogColor, fogColor, t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, fogStartDistance, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, fogEndDistance, t);

                if (controlAmbientLight)
                {
                    RenderSettings.ambientLight = Color.Lerp(startAmbient, ambientLightColor, t);
                    RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientI, ambientLightIntensity, t);
                }
            },
            1f,
            transitionDuration
        ).SetEase(transitionEase).SetUpdate(true);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview in Editor")]
    private void PreviewInEditor()
    {
        ApplyImmediate();
        Debug.Log($"[AmbientPreset] Preview aplicado: {name}");
    }
#endif
}
