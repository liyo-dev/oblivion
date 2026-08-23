using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Capturador de rendimiento en tiempo de juego: pulsa F9 para empezar a grabar, F9 otra vez para
/// parar y volcar un .json con fps/ms-por-frame, tiempos de CPU/GPU (si la plataforma los soporta
/// vía FrameTimingManager), asignaciones de memoria (GC) y una lista de "hitches" (frames
/// anormalmente lentos) con el segundo exacto en que ocurrieron.
///
/// Pensado para jugar normal (sin el Editor de Unity abierto en pantalla) y luego pasar el .json
/// resultante para analizarlo — no requiere tener el Profiler abierto ni hacer nada manual aparte
/// de F9/F9.
///
/// No incluye número de draw calls/batches: esa métrica solo está expuesta por el Frame Debugger
/// del Editor (UnityStats es una API interna de UnityEditor, no existe en builds). Todo lo demás
/// (frame time, CPU/GPU, memoria) sí funciona tanto en Play Mode del Editor como en una build de
/// desarrollo.
///
/// Vive en MainMenu (ver PerformanceCaptureBuilder.cs) y usa DontDestroyOnLoad para sobrevivir a
/// los cambios de escena durante toda la sesión de juego.
///
/// Dónde se guarda el .json:
///  - Jugando desde el Editor: en ProfilerCaptures/ dentro del repo (ya existe, vacía).
///  - En una build: en la carpeta de datos persistente del juego (Application.persistentDataPath),
///    bajo PerformanceCaptures/ — la ruta completa se imprime en consola y en el aviso en pantalla.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class PerformanceCapture : MonoBehaviour
{
    [Header("Activación")]
    [Tooltip("Tecla para empezar/parar una grabación manualmente.")]
    [SerializeField] private Key toggleKey = Key.F9;

    [Tooltip("Duración máxima de una grabación, por si se te olvida pararla con F9 — protección " +
             "para no dejar el juego grabando horas y generar un archivo enorme.")]
    [SerializeField] private float maxDurationSeconds = 300f;

    [Tooltip("Un frame por encima de este umbral (en ms) se registra como posible \"hitch\" en la " +
             "lista de picos del informe. 50ms ≈ por debajo de 20 fps en ese frame.")]
    [SerializeField] private float hitchThresholdMs = 50f;

    [Header("Overlay en pantalla")]
    [SerializeField] private bool showOnScreenIndicator = true;

    const int MaxSpikesRecorded = 40;
    const float BucketDurationSeconds = 1f;

    static PerformanceCapture _instance;

    bool _recording;
    float _recordingStartRealtime;

    // Acumuladores del "bucket" (ventana de ~1s) en curso.
    float _bucketStartRealtime;
    int _bucketFrameCount;
    float _bucketFrameMsSum;
    float _bucketFrameMsMax;
    float _bucketGpuMsSum; int _bucketGpuSamples;
    float _bucketCpuMsSum; int _bucketCpuSamples;
    long _bucketAllocStart;
    int _bucketGcCollectionsStart;

    List<float> _allFrameMsSamples;
    List<PerfBucket> _buckets;
    List<string> _spikes;
    bool _spikesCapped;

    float _memStartMB, _memPeakMB;

    static readonly FrameTiming[] _frameTimingBuffer = new FrameTiming[1];

    string _lastSavedPath = "";
    bool _lastSaveFailed;
    float _lastSaveMessageUntil = -1f;

    public bool IsRecording => _recording;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // Hay que llamar a esto todos los frames (grabando o no) para que el buffer interno de
        // FrameTimingManager tenga historial cuando empecemos a leerlo.
        FrameTimingManager.CaptureFrameTimings();

        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            if (_recording) StopAndSave("parada manual (F9)");
            else StartRecording();
        }

        if (_recording)
        {
            SampleFrame();

            if (Time.unscaledTime - _recordingStartRealtime >= maxDurationSeconds)
                StopAndSave($"duración máxima alcanzada ({maxDurationSeconds:0}s)");
        }
    }

    void StartRecording()
    {
        _recording = true;
        _recordingStartRealtime = Time.unscaledTime;
        _bucketStartRealtime = _recordingStartRealtime;
        _bucketFrameCount = 0;
        _bucketFrameMsSum = 0f;
        _bucketFrameMsMax = 0f;
        _bucketGpuMsSum = 0f; _bucketGpuSamples = 0;
        _bucketCpuMsSum = 0f; _bucketCpuSamples = 0;
        _bucketAllocStart = Profiler.GetTotalAllocatedMemoryLong();
        _bucketGcCollectionsStart = GC.CollectionCount(0);

        _allFrameMsSamples = new List<float>(Mathf.CeilToInt(maxDurationSeconds * 90f));
        _buckets = new List<PerfBucket>(Mathf.CeilToInt(maxDurationSeconds));
        _spikes = new List<string>();
        _spikesCapped = false;

        _memStartMB = _bucketAllocStart / (1024f * 1024f);
        _memPeakMB = _memStartMB;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[PerformanceCapture] ▶ Grabación de rendimiento iniciada.");
#endif
    }

    void SampleFrame()
    {
        float dtMs = Time.unscaledDeltaTime * 1000f;
        _bucketFrameMsSum += dtMs;
        if (dtMs > _bucketFrameMsMax) _bucketFrameMsMax = dtMs;
        _bucketFrameCount++;
        _allFrameMsSamples.Add(dtMs);

        if (FrameTimingManager.GetLatestTimings(1, _frameTimingBuffer) > 0)
        {
            var ft = _frameTimingBuffer[0];
            if (ft.gpuFrameTime > 0) { _bucketGpuMsSum += (float)ft.gpuFrameTime; _bucketGpuSamples++; }
            if (ft.cpuFrameTime > 0) { _bucketCpuMsSum += (float)ft.cpuFrameTime; _bucketCpuSamples++; }
        }

        long allocNow = Profiler.GetTotalAllocatedMemoryLong();
        float memNowMB = allocNow / (1024f * 1024f);
        if (memNowMB > _memPeakMB) _memPeakMB = memNowMB;

        if (dtMs >= hitchThresholdMs)
        {
            if (_spikes.Count < MaxSpikesRecorded)
            {
                float t = Time.unscaledTime - _recordingStartRealtime;
                _spikes.Add($"{t:0.0}s → frame de {dtMs:0}ms");
            }
            else
            {
                _spikesCapped = true;
            }
        }

        if (Time.unscaledTime - _bucketStartRealtime >= BucketDurationSeconds)
            FlushBucket(allocNow);
    }

    void FlushBucket(long allocNow)
    {
        if (_bucketFrameCount == 0) return;

        float elapsed = Time.unscaledTime - _bucketStartRealtime;
        _buckets.Add(new PerfBucket
        {
            segundo = Mathf.Round(Time.unscaledTime - _recordingStartRealtime),
            fpsPromedio = elapsed > 0f ? _bucketFrameCount / elapsed : 0f,
            frameMsPromedio = _bucketFrameMsSum / _bucketFrameCount,
            frameMsMax = _bucketFrameMsMax,
            gpuMsPromedio = _bucketGpuSamples > 0 ? _bucketGpuMsSum / _bucketGpuSamples : -1f,
            cpuMsPromedio = _bucketCpuSamples > 0 ? _bucketCpuMsSum / _bucketCpuSamples : -1f,
            bytesAsignados = allocNow - _bucketAllocStart,
            coleccionesGC = GC.CollectionCount(0) - _bucketGcCollectionsStart,
        });

        _bucketStartRealtime = Time.unscaledTime;
        _bucketFrameCount = 0;
        _bucketFrameMsSum = 0f;
        _bucketFrameMsMax = 0f;
        _bucketGpuMsSum = 0f; _bucketGpuSamples = 0;
        _bucketCpuMsSum = 0f; _bucketCpuSamples = 0;
        _bucketAllocStart = allocNow;
        _bucketGcCollectionsStart = GC.CollectionCount(0);
    }

    void StopAndSave(string motivo)
    {
        _recording = false;
        FlushBucket(Profiler.GetTotalAllocatedMemoryLong());

        var data = BuildData();
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        try
        {
            _lastSavedPath = SaveToDisk(json);
            _lastSaveFailed = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PerformanceCapture] ⏹ Grabación detenida ({motivo}). Guardada en: {_lastSavedPath}");
#endif
        }
        catch (Exception ex)
        {
            _lastSaveFailed = true;
            _lastSavedPath = ex.Message;
            Debug.LogError($"[PerformanceCapture] ❌ No se pudo guardar la captura: {ex}");
        }

        _lastSaveMessageUntil = Time.unscaledTime + 8f;
    }

    PerformanceCaptureData BuildData()
    {
        var sortedMs = new List<float>(_allFrameMsSamples);
        sortedMs.Sort();

        float frameMsAvg = 0f;
        foreach (var v in _allFrameMsSamples) frameMsAvg += v;
        frameMsAvg = _allFrameMsSamples.Count > 0 ? frameMsAvg / _allFrameMsSamples.Count : 0f;

        float frameMsMax = sortedMs.Count > 0 ? sortedMs[sortedMs.Count - 1] : 0f;
        float p95 = Percentile(sortedMs, 95f);
        float p99 = Percentile(sortedMs, 99f);

        float fpsAvg = frameMsAvg > 0f ? 1000f / frameMsAvg : 0f;
        float fpsMinInstantaneo = frameMsMax > 0f ? 1000f / frameMsMax : 0f;
        float fpsPeorUnoPorCiento = p99 > 0f ? 1000f / p99 : 0f;

        float gpuSum = 0f; int gpuN = 0;
        float cpuSum = 0f; int cpuN = 0;
        long allocTotal = 0; int gcTotal = 0;
        foreach (var b in _buckets)
        {
            if (b.gpuMsPromedio >= 0f) { gpuSum += b.gpuMsPromedio; gpuN++; }
            if (b.cpuMsPromedio >= 0f) { cpuSum += b.cpuMsPromedio; cpuN++; }
            allocTotal += b.bytesAsignados;
            gcTotal += b.coleccionesGC;
        }

        string modo;
        if (Application.isEditor) modo = "Editor (Play Mode)";
        else if (UnityEngine.Debug.isDebugBuild) modo = "Build de desarrollo";
        else modo = "Build final";

        var meta = new PerfMeta
        {
            fechaHoraLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            escena = SceneManager.GetActiveScene().name,
            modo = modo,
            duracionSegundos = Time.unscaledTime - _recordingStartRealtime,
            resolucionAncho = Screen.width,
            resolucionAlto = Screen.height,
            calidad = QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?",
            targetFrameRate = Application.targetFrameRate,
            vSyncCount = QualitySettings.vSyncCount,
            gpuTimingDisponible = gpuN > 0,
            unityVersion = Application.unityVersion,
            picosRecortados = _spikesCapped,
            notas = "No incluye draw calls/batches (solo disponibles vía el Frame Debugger del Editor). " +
                    "bytesAsignados/coleccionesGC son deltas por segundo de memoria managed total " +
                    "(Profiler.GetTotalAllocatedMemoryLong / GC.CollectionCount(0)), no solo de este componente. " +
                    "gpuMsPromedio/cpuMsPromedio salen a -1 en los segundos en que FrameTimingManager no devolvió datos " +
                    "(frecuente en Play Mode del Editor en algunas plataformas; más fiable en una build).",
        };

        var resumen = new PerfResumen
        {
            fpsPromedio = fpsAvg,
            fpsMinimoInstantaneo = fpsMinInstantaneo,
            fpsPeorUnoPorCiento = fpsPeorUnoPorCiento,
            frameMsPromedio = frameMsAvg,
            frameMsMax = frameMsMax,
            frameMsP95 = p95,
            frameMsP99 = p99,
            gpuMsPromedio = gpuN > 0 ? gpuSum / gpuN : -1f,
            cpuMsPromedio = cpuN > 0 ? cpuSum / cpuN : -1f,
            bytesAsignadosTotal = allocTotal,
            coleccionesGCTotal = gcTotal,
            memoriaInicioMB = _memStartMB,
            memoriaFinMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
            memoriaPicoMB = _memPeakMB,
        };

        return new PerformanceCaptureData
        {
            meta = meta,
            resumen = resumen,
            muestrasPorSegundo = _buckets,
            picosDetectados = _spikes,
        };
    }

    static float Percentile(List<float> sortedAscending, float p)
    {
        if (sortedAscending.Count == 0) return 0f;
        float idx = (p / 100f) * (sortedAscending.Count - 1);
        int lo = Mathf.FloorToInt(idx);
        int hi = Mathf.CeilToInt(idx);
        if (lo == hi) return sortedAscending[lo];
        float frac = idx - lo;
        return Mathf.Lerp(sortedAscending[lo], sortedAscending[hi], frac);
    }

    string SaveToDisk(string json)
    {
        string dir;
#if UNITY_EDITOR
        dir = Path.Combine(Application.dataPath, "..", "ProfilerCaptures");
#else
        dir = Path.Combine(Application.persistentDataPath, "PerformanceCaptures");
#endif
        Directory.CreateDirectory(dir);

        string fileName = $"perf_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string fullPath = Path.Combine(dir, fileName);
        File.WriteAllText(fullPath, json);
        return Path.GetFullPath(fullPath);
    }

    void OnGUI()
    {
        if (!showOnScreenIndicator) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        if (_recording)
        {
            float t = Time.unscaledTime - _recordingStartRealtime;
            DrawWithShadow($"🔴 Grabando rendimiento... {t:0}s — F9 para parar", 16, 16, style);
        }
        else if (Time.unscaledTime < _lastSaveMessageUntil)
        {
            string msg = _lastSaveFailed
                ? $"❌ No se pudo guardar la captura de rendimiento: {_lastSavedPath}"
                : $"✅ Captura de rendimiento guardada en: {_lastSavedPath}";
            DrawWithShadow(msg, 16, 16, style);
        }
    }

    static void DrawWithShadow(string text, float x, float y, GUIStyle style)
    {
        var shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(x + 1, y + 1, 900, 24), text, shadowStyle);
        GUI.Label(new Rect(x, y, 900, 24), text, style);
    }

    [Serializable]
    public class PerfBucket
    {
        public float segundo;
        public float fpsPromedio;
        public float frameMsPromedio;
        public float frameMsMax;
        public float gpuMsPromedio;
        public float cpuMsPromedio;
        public long bytesAsignados;
        public int coleccionesGC;
    }

    [Serializable]
    public class PerfResumen
    {
        public float fpsPromedio;
        public float fpsMinimoInstantaneo;
        public float fpsPeorUnoPorCiento;
        public float frameMsPromedio;
        public float frameMsMax;
        public float frameMsP95;
        public float frameMsP99;
        public float gpuMsPromedio;
        public float cpuMsPromedio;
        public long bytesAsignadosTotal;
        public int coleccionesGCTotal;
        public float memoriaInicioMB;
        public float memoriaFinMB;
        public float memoriaPicoMB;
    }

    [Serializable]
    public class PerfMeta
    {
        public string fechaHoraLocal;
        public string escena;
        public string modo;
        public float duracionSegundos;
        public int resolucionAncho;
        public int resolucionAlto;
        public string calidad;
        public int targetFrameRate;
        public int vSyncCount;
        public bool gpuTimingDisponible;
        public string unityVersion;
        public bool picosRecortados;
        public string notas;
    }

    [Serializable]
    public class PerformanceCaptureData
    {
        public PerfMeta meta;
        public PerfResumen resumen;
        public List<PerfBucket> muestrasPorSegundo;
        public List<string> picosDetectados;
    }
}
