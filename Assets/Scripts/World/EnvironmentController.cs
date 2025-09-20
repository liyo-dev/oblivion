using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnvironmentController : MonoBehaviour
{
    public static EnvironmentController Instance { get; private set; }

    [Header("Opcional")]
    public Material exteriorSkyboxOverride;   // si lo pones, se usará al volver a exterior
    public Camera targetCamera;               // si lo dejas vacío, se resuelve solo

    // snapshot del “exterior”
    Material _savedRenderSettingsSkybox;
    Material _savedCameraSkyboxMat;
    CameraClearFlags _savedClearFlags = CameraClearFlags.Skybox;
    bool _savedHadCamSkybox;
    bool _hasSnapshot;
    Camera _snapshotCam;  // cámara usada al capturar (por si cambia)

    Camera _cam;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += (_, __) => ResetCache();
        SceneManager.sceneLoaded       += (_, __) => ResetCache();
    }

    void ResetCache()
    {
        _cam = null;
        _hasSnapshot = false;
        _snapshotCam = null;
        _savedCameraSkyboxMat = null;
        _savedRenderSettingsSkybox = null;
        _savedHadCamSkybox = false;
    }

    public void RefreshCameraNow()
    {
        _cam = ResolveCamera();
        // no capturamos aún; la captura se hace al primer ApplyInterior()
        _hasSnapshot = false;
    }

    public void ApplyInterior(AnchorEnvironment env)
    {
        EnsureSnapshot(); // capturamos si aún no lo hicimos

        var cam = ResolveCamera();
        if (env && env.useSolidColorBackground)
        {
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = env.interiorBgColor; }
            RenderSettings.skybox = null;
        }
        else
        {
            if (cam) cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
        }

        // apagar direccionales de exterior; dejar sólo las del interior si existen
        foreach (var l in FindObjectsOfType<Light>(true))
        {
            if (!l || l.type != LightType.Directional) continue;
            bool inside = env && IsChildOf(l.transform, env.transform);
            l.gameObject.SetActive(inside);
        }

        // encender luces locales del interior (aunque no estén en arrays)
        if (env)
        {
            SetActive(env.lightsDisableOnEnter, false);
            SetActive(env.lightsEnableOnEnter, true);
            foreach (var l in env.GetComponentsInChildren<Light>(true)) if (l) l.gameObject.SetActive(true);
        }

        DynamicGI.UpdateEnvironment();
    }

    public void ApplyExterior()
    {
        var cam = ResolveCamera();

        // 1) clear flags
        if (cam) cam.clearFlags = _hasSnapshot ? _savedClearFlags : CameraClearFlags.Skybox;

        // 2) skybox
        if (exteriorSkyboxOverride)
        {
            // fuerza un skybox global concreto
            RenderSettings.skybox = exteriorSkyboxOverride;
            var csb = EnsureCameraSkybox(cam);
            if (csb) csb.material = exteriorSkyboxOverride;
        }
        else if (_hasSnapshot)
        {
            // restaura lo que había antes: preferir material de la cámara si lo tenía
            if (_savedHadCamSkybox)
            {
                var csb = EnsureCameraSkybox(cam);
                if (csb) csb.material = _savedCameraSkyboxMat;
            }
            else
            {
                // no había skybox en la cámara: usa el RenderSettings antiguo
                RenderSettings.skybox = _savedRenderSettingsSkybox;
                // si la cámara tiene un componente Skybox colgado por cualquier motivo, lo vaciamos
                var csb = cam ? cam.GetComponent<Skybox>() : null;
                if (csb) csb.material = null;
            }
        }
        // si no hay snapshot y tampoco override, dejamos lo que estuviera

        // 3) reactivar direccionales
        foreach (var l in FindObjectsOfType<Light>(true))
            if (l && l.type == LightType.Directional) l.gameObject.SetActive(true);

        DynamicGI.UpdateEnvironment();
    }

    // -------- internos --------

    void EnsureSnapshot()
    {
        if (_hasSnapshot) return;

        var cam = ResolveCamera();

        _savedClearFlags = cam ? cam.clearFlags : CameraClearFlags.Skybox;
        _savedRenderSettingsSkybox = RenderSettings.skybox;

        _savedHadCamSkybox = false;
        _savedCameraSkyboxMat = null;

        if (cam)
        {
            var csb = cam.GetComponent<Skybox>();
            if (csb && csb.material)
            {
                _savedHadCamSkybox = true;
                _savedCameraSkyboxMat = csb.material;
            }
        }

        _snapshotCam = cam;
        _hasSnapshot = true;
    }

    Camera ResolveCamera()
    {
        if (targetCamera) return targetCamera;
        if (_cam && _cam) return _cam;

        // 1) MainCamera si existe y está activa
        var m = Camera.main;
        if (m && m.enabled && m.gameObject.activeInHierarchy) return _cam = m;

        // 2) la “mejor” cámara disponible (incluye inactivas)
#if UNITY_2022_3_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var cams = Resources.FindObjectsOfTypeAll<Camera>();
#endif
        Camera best = null; float bestScore = float.NegativeInfinity;
        foreach (var c in cams)
        {
            if (!c) continue;
            float score = 0f;
            if (c.enabled && c.gameObject.activeInHierarchy) score += 1000f;
            if (c.targetDisplay == 0) score += 100f;
            score += c.depth;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return _cam = best;
    }

    static Skybox EnsureCameraSkybox(Camera cam)
    {
        if (!cam) return null;
        var csb = cam.GetComponent<Skybox>();
        if (!csb) csb = cam.gameObject.AddComponent<Skybox>();
        return csb;
    }

    static void SetActive(Light[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var l in arr) if (l) l.gameObject.SetActive(active);
    }

    static bool IsChildOf(Transform t, Transform root)
    {
        if (!t || !root) return false;
        for (var cur = t; cur != null; cur = cur.parent)
            if (cur == root) return true;
        return false;
    }
}
