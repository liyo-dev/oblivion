// Assets/Scripts/Audio/AudioService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Header("Perfil de reglas")]
    [SerializeField] private AudioGraphProfile profile;

    [Header("Mixer (grupos opcionales)")]
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;
    public AudioMixerGroup ambienceGroup;

    [Header("Music")]
    [Min(0f)] public float defaultFade = 0.75f;

    [Header("SFX Pooling")]
    [Min(1)] public int pool2DSize = 16;
    [Min(1)] public int pool3DSize = 16;

    // Motor interno
    AudioSource _musicA, _musicB;
    bool _musicATurn;
    readonly Queue<AudioSource> _pool2D = new();
    readonly Queue<AudioSource> _pool3D = new();

    // Señales y suscripciones SFX
    DefaultNarrativeSignals _signals;
    readonly List<string> _subscribedKeys = new();

    // ========= LIFECYCLE =========
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Crear fuentes
        _musicA = CreateChildSource("MusicA", musicGroup, spatial:false, loop:true);
        _musicB = CreateChildSource("MusicB", musicGroup, spatial:false, loop:true);
        for (int i = 0; i < pool2DSize; i++) _pool2D.Enqueue(CreateChildSource($"SFX2D_{i}", sfxGroup, spatial:false));
        for (int i = 0; i < pool3DSize; i++) _pool3D.Enqueue(CreateChildSource($"SFX3D_{i}", sfxGroup, spatial:true));

        // Escena → música
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Señales (incluye inactivos)
        _signals = DefaultNarrativeSignals.Instance
                   ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);

        WireEventSfx(); // suscribir eventos a SFX

        // Aplicar música de la escena actual
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_signals != null)
        {
            for (int i = 0; i < _subscribedKeys.Count; i++)
            {
                string k = _subscribedKeys[i];
                _signals.OffCustom(k, () => PlaySfxForKey(k));
            }
        }
        _subscribedKeys.Clear();
    }

    // ========= WIRING (perfil) =========
    void WireEventSfx()
    {
        if (_signals == null || profile == null || profile.eventSfx == null) return;
        if (_subscribedKeys.Count > 0) return; // ya suscrito

        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var rule = profile.eventSfx[i];
            if (rule == null || string.IsNullOrWhiteSpace(rule.eventKey) || rule.sfx == null) continue;
            string key = rule.eventKey;
            _signals.OnCustom(key, () => PlaySfxForKey(key));
            _subscribedKeys.Add(key);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (profile == null) return;

        // Busca primera coincidencia por nombre (contains)
        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var rule = profile.sceneMusic[i];
            if (rule != null &&
                !string.IsNullOrEmpty(rule.sceneName) &&
                scene.name.IndexOf(rule.sceneName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                rule.music != null)
            {
                PlayMusic(rule.music);
                break;
            }
        }
    }

    void PlaySfxForKey(string key)
    {
        if (profile == null || string.IsNullOrEmpty(key)) return;
        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r != null &&
                !string.IsNullOrWhiteSpace(r.eventKey) &&
                string.Equals(r.eventKey, key, StringComparison.OrdinalIgnoreCase) &&
                r.sfx != null)
            {
                PlaySFX(r.sfx);
                break;
            }
        }
    }

    // ========= MOTOR: MUSIC =========
    public void PlayMusic(AudioClip clip, float fadeSeconds = -1f)
    {
        if (!clip) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        var from = _musicATurn ? _musicA : _musicB;
        var to   = _musicATurn ? _musicB : _musicA;
        _musicATurn = !_musicATurn;

        to.clip = clip;
        to.volume = 0f;
        if (!to.isPlaying) to.Play();

        if (from.isPlaying) { StopAllCoroutines(); StartCoroutine(Crossfade(from, to, fadeSeconds)); }
        else                { to.volume = 1f; }
    }

    public void StopMusic(float fadeOut = -1f)
    {
        if (fadeOut < 0f) fadeOut = defaultFade;
        var current = _musicATurn ? _musicB : _musicA;
        if (!current.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndStop(current, fadeOut));
    }

    System.Collections.IEnumerator Crossfade(AudioSource from, AudioSource to, float seconds)
    {
        if (seconds <= 0f) { from.Stop(); to.volume = 1f; yield break; }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            from.volume = 1f - k;
            to.volume   = k;
            yield return null;
        }
        from.Stop();
        to.volume = 1f;
    }

    System.Collections.IEnumerator FadeOutAndStop(AudioSource src, float seconds)
    {
        if (seconds <= 0f) { src.Stop(); yield break; }
        float start = src.volume, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        src.Stop();
        src.volume = 1f;
    }

    // ========= MOTOR: SFX =========
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (!clip) return;
        var src = Rent2D();
        src.transform.localPosition = Vector3.zero;
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnWhenDone(src, _pool2D));
    }

    public void PlaySFXAt(AudioClip clip, Vector3 worldPos, float volume = 1f)
    {
        if (!clip) return;
        var src = Rent3D();
        src.transform.position = worldPos;
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnWhenDone(src, _pool3D));
    }

    AudioSource CreateChildSource(string name, AudioMixerGroup group, bool spatial, bool loop=false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.outputAudioMixerGroup = group ? group : null;
        src.spatialBlend = spatial ? 1f : 0f;
        if (spatial) { src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 2f; src.maxDistance = 30f; }
        return src;
    }

    AudioSource Rent2D() => _pool2D.Count > 0 ? _pool2D.Dequeue() : CreateChildSource("SFX2D_dyn", sfxGroup, spatial:false);
    AudioSource Rent3D() => _pool3D.Count > 0 ? _pool3D.Dequeue() : CreateChildSource("SFX3D_dyn", sfxGroup, spatial:true);

    System.Collections.IEnumerator ReturnWhenDone(AudioSource src, Queue<AudioSource> pool)
    {
        float wait = src.clip ? Mathf.Max(0.02f, src.clip.length / Mathf.Max(0.01f, src.pitch)) : 1f;
        yield return new WaitForSeconds(wait);
        src.Stop(); src.clip = null; pool.Enqueue(src);
    }

    // ========= MIXER =========
    public void SetExposedVolume(string exposedParam, float linear01)
    {
        if (!mixer || string.IsNullOrEmpty(exposedParam)) return;
        float dB = Mathf.Lerp(-80f, 0f, Mathf.Clamp01(linear01));
        mixer.SetFloat(exposedParam, dB);
    }

    public float GetExposedVolume01(string exposedParam, float def01 = 1f)
    {
        if (!mixer || string.IsNullOrEmpty(exposedParam)) return def01;
        return mixer.GetFloat(exposedParam, out float dB) ? Mathf.InverseLerp(-80f, 0f, dB) : def01;
    }
}
