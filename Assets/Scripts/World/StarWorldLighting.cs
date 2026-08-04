using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Aplica iluminación fija de mundo estelar y pausa el DayNightCycle mientras esta escena esté cargada.
/// Colocar en un GameObject de la escena de batalla final.
/// Al descargarse la escena, OnDisable reactiva automáticamente el ciclo día/noche.
/// </summary>
public class StarWorldLighting : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material _skybox;

    [Header("Luz direccional")]
    [SerializeField] private Light _directionalLight;
    [SerializeField] private Color _lightColor = new Color(0.55f, 0.65f, 1f);
    [Range(0f, 2f)]
    [SerializeField] private float _lightIntensity = 0.25f;
    [SerializeField] private Vector3 _lightRotation = new Vector3(35f, 170f, 0f);

    [Header("Luz ambiental")]
    [SerializeField] private Color _ambientColor = new Color(0.06f, 0.06f, 0.18f);
    [Range(0f, 2f)]
    [SerializeField] private float _ambientIntensity = 0.6f;

    [Header("Niebla")]
    [SerializeField] private bool _enableFog = false;

    private DayNightCycle _dayNightCycle;

    private void Start()
    {
        _dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        if (_dayNightCycle != null)
            _dayNightCycle.enabled = false;

        ApplySettings();
    }

    private void OnDisable()
    {
        if (_dayNightCycle != null)
            _dayNightCycle.enabled = true;
    }

    private void ApplySettings()
    {
        if (_skybox != null)
        {
            RenderSettings.skybox = _skybox;
            DynamicGI.UpdateEnvironment();
        }

        if (_directionalLight != null)
        {
            _directionalLight.color = _lightColor;
            _directionalLight.intensity = _lightIntensity;
            _directionalLight.transform.eulerAngles = _lightRotation;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = _ambientColor * _ambientIntensity;
        RenderSettings.fog = _enableFog;
    }
}
