using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName = "AmbientPreset_New", menuName = "El Sendero/Entorno/Ambient Preset", order = 1)]
public class AmbientPreset : ScriptableObject
{
    [Header("Niebla de Nubes Bajas (sistema de clima — 1 sep 2026)")]
    [Tooltip("Checkbox de 'nubes bajas': si está activo, mientras el jugador esté dentro de esta zona se fuerza la niebla ocasional de DayNightCycle (la misma que el sorteo global de clima, ver DayNightCycle.SetZoneMistOverride) — y se bloquea que empiece a llover/hacer tormenta/viento nuevos mientras siga activa. Si ya estaba lloviendo al entrar, la lluvia sigue como estaba (no se apila niebla encima) y la niebla de zona toma el relevo en cuanto esa lluvia termine sola. DayNightCycle es la ÚNICA fuente de verdad de RenderSettings.fog* — este checkbox solo la activa/desactiva por zona, no dibuja niebla propia. 1 sep 2026: este mismo checkbox también sube la cadencia de nubes sueltas de AmbientCloudDirector (ver AmbientCloudDirector.SetZoneCloudBoost) mientras se está en la zona, para que se vean nubes reales cruzando el cielo además de la niebla de distancia — no dispara tormenta por sí solo. Distinto de la niebla de pies (footFogObjects en AmbientZone), que es un objeto fijo con material propio, no depende del clima. No afecta a interiores: la visibilidad de la niebla la sigue controlando IsSkyboxLockedByEnvironment igual que siempre.")]
    public bool forcesMist = false;

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
        if (controlAmbientLight)
        {
            RenderSettings.ambientLight = ambientLightColor;
            RenderSettings.ambientIntensity = ambientLightIntensity;
        }
    }

    public Tween ApplyWithTransition()
    {
        Color startAmbient = RenderSettings.ambientLight;
        float startAmbientI = RenderSettings.ambientIntensity;

        return DOTween.To(
            () => 0f,
            t => {
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
