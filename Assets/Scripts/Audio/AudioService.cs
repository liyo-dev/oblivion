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

    [Header("Música")]
    [Min(0f)] public float defaultFade = 0.75f;

    [Header("Pool SFX")]
    [Min(1)] public int pool2DSize = 16;
    [Min(1)] public int pool3DSize = 16;

    // --- motor interno ---
    AudioSource _musicA, _musicB;
    bool _musicATurn;
    readonly Queue<AudioSource> _pool2D = new();
    readonly Queue<AudioSource> _pool3D = new();

    // --- señales / handlers ---
    DefaultNarrativeSignals _signals;
    readonly Dictionary<string, Action> _sfxHandlers = new();          // key → handler (OnCustom)
    readonly Dictionary<string, Action> _battleStartHandlers = new();  // $"BATTLE_START:{id}" → handler (OnCustom)
    readonly Dictionary<object, Action> _battleWinHandlers = new();    // battleId → handler (OnBattleWon)

    // --- estado cinemáticas / ducking / stack de música ---
    struct MusicStackItem { public AudioClip clip; }
    readonly Stack<MusicStackItem> _musicStack = new();
    int _duckCount = 0;
    float _duckTarget = 1f;
    Coroutine _duckRoutine;

    // ===========================================================
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fuentes
        _musicA = CreateChildSource("MusicA", musicGroup, spatial:false, loop:true);
        _musicB = CreateChildSource("MusicB", musicGroup, spatial:false, loop:true);
        for (int i = 0; i < pool2DSize; i++) _pool2D.Enqueue(CreateChildSource($"SFX2D_{i}", sfxGroup, spatial:false));
        for (int i = 0; i < pool3DSize; i++) _pool3D.Enqueue(CreateChildSource($"SFX3D_{i}", sfxGroup, spatial:true));

        // Escenas
        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        // Señales (incluye inactivos)
        _signals = DefaultNarrativeSignals.Instance
                   ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);

        WireEventSfx();
        WireBattleStarts();
        WireBattleWins();

        // Música para la escena actual
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded   -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        if (_signals != null)
        {
            foreach (var kv in _sfxHandlers)         _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _battleStartHandlers) _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _battleWinHandlers)   _signals.OffBattleWon(kv.Key, kv.Value);
        }

        _sfxHandlers.Clear();
        _battleStartHandlers.Clear();
        _battleWinHandlers.Clear();
    }

    // ===========================================================
    // Suscripciones según perfil
    void WireEventSfx()
    {
        if (_signals == null || profile == null || profile.eventSfx == null) return;
        if (_sfxHandlers.Count > 0) return;

        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r == null || string.IsNullOrWhiteSpace(r.eventKey) || r.sfx == null) continue;

            string key = r.eventKey;
            Action h = () => PlaySfxForKey(key);
            _signals.OnCustom(key, h);
            _sfxHandlers[key] = h;
        }
    }

    void WireBattleStarts()
    {
        if (_signals == null || profile == null || profile.battles == null) return;
        if (_battleStartHandlers.Count > 0) return;

        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId) || r.music == null) continue;

            string key = $"BATTLE_START:{r.battleId}";
            Action h = () => BeginBattleMusic(r);
            _signals.OnCustom(key, h);
            _battleStartHandlers[key] = h;
        }
    }

    void WireBattleWins()
    {
        if (_signals == null || profile == null || profile.battles == null) return;
        if (_battleWinHandlers.Count > 0) return;

        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;

            object key = r.battleId; // usamos el mismo id que levanta RaiseBattleWon(battleId)
            Action h = () => OnBattleWonRestoreMusic(r);
            _signals.OnBattleWon(key, h);
            _battleWinHandlers[key] = h;
        }
    }

    // ===========================================================
    // Escenas (normales y aditivas/cinemáticas)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (profile == null) return;

        if (mode == LoadSceneMode.Additive)
        {
            var rule = FindAdditiveRuleFor(scene.name);
            if (rule != null)
            {
                if (rule.duckInsteadOfReplace)
                {
                    StartDuck(rule.duckTo, rule.fade);
                }
                else
                {
                    var current = GetCurrentMusicClip();
                    _musicStack.Push(new MusicStackItem { clip = current });
                    if (rule.music != null) PlayMusic(rule.music, rule.fade);
                }
            }
            return;
        }

        // escena normal → buscar primera coincidencia
        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var r = profile.sceneMusic[i];
            if (r != null &&
                !string.IsNullOrEmpty(r.sceneName) &&
                scene.name.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                r.music != null)
            {
                PlayMusic(r.music);
                break;
            }
        }
    }

    void OnSceneUnloaded(Scene scene)
    {
        if (profile == null) return;
        var rule = FindAdditiveRuleFor(scene.name);
        if (rule == null) return;

        if (rule.duckInsteadOfReplace)
        {
            StopDuck(rule.fade);
        }
        else
        {
            if (_musicStack.Count > 0)
            {
                var prev = _musicStack.Pop();
                if (prev.clip != null) PlayMusic(prev.clip, rule.fade);
                else StopMusic(rule.fade);
            }
        }
    }

    AudioGraphProfile.AdditiveCinematicRule FindAdditiveRuleFor(string sceneName)
    {
        if (profile.additiveCinematics == null) return null;
        for (int i = 0; i < profile.additiveCinematics.Count; i++)
        {
            var r = profile.additiveCinematics[i];
            if (r != null &&
                !string.IsNullOrEmpty(r.sceneName) &&
                sceneName.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
                return r;
        }
        return null;
    }

    // ===========================================================
    // Batallas
    void BeginBattleMusic(AudioGraphProfile.BattleRule r)
    {
        var current = GetCurrentMusicClip();
        _musicStack.Push(new MusicStackItem { clip = current });
        PlayMusic(r.music, r.fade);
    }

    void OnBattleWonRestoreMusic(AudioGraphProfile.BattleRule r)
    {
        if (_musicStack.Count > 0)
        {
            var prev = _musicStack.Pop();
            if (prev.clip != null) PlayMusic(prev.clip, r.fade);
            else StopMusic(r.fade);
        }
    }

    // ===========================================================
    // Música
    public void PlayMusic(AudioClip clip, float fadeSeconds = -1f)
    {
        if (!clip) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        var from = _musicATurn ? _musicA : _musicB;
        var to   = _musicATurn ? _musicB : _musicA;
        _musicATurn = !_musicATurn;

        to.clip = clip;
        to.volume = GetDuckedVolume(1f);
        if (!to.isPlaying) to.Play();

        if (from.isPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(Crossfade(from, to, fadeSeconds));
        }
        else
        {
            to.volume = GetDuckedVolume(1f);
        }
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
        if (seconds <= 0f) { from.Stop(); to.volume = GetDuckedVolume(1f); yield break; }
        float t = 0f;
        float startFrom = from.volume;
        float targetTo  = GetDuckedVolume(1f);
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            to.volume   = Mathf.Lerp(0f, targetTo, k);
            yield return null;
        }
        from.Stop();
        to.volume = targetTo;
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
        src.volume = GetDuckedVolume(1f);
    }

    // Ducking simple (para cinemáticas aditivas)
    void StartDuck(float duckTo, float fade)
    {
        _duckCount++;
        _duckTarget = Mathf.Clamp01(duckTo);
        if (_duckRoutine != null) StopCoroutine(_duckRoutine);
        _duckRoutine = StartCoroutine(SetMusicVolumeTo(_duckTarget, fade));
    }

    void StopDuck(float fade)
    {
        _duckCount = Mathf.Max(0, _duckCount - 1);
        float target = (_duckCount == 0) ? 1f : _duckTarget;
        if (_duckRoutine != null) StopCoroutine(_duckRoutine);
        _duckRoutine = StartCoroutine(SetMusicVolumeTo(target, fade));
    }

    float GetDuckedVolume(float baseVol) => baseVol * (_duckCount > 0 ? _duckTarget : 1f);

    System.Collections.IEnumerator SetMusicVolumeTo(float target, float fade)
    {
        var current = _musicATurn ? _musicB : _musicA;
        var other   = _musicATurn ? _musicA : _musicB;
        float t = 0f;
        float a0 = current.volume;
        float b0 = other.volume;
        if (fade <= 0f) fade = 0.0001f;

        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fade);
            float v = Mathf.Lerp(a0, target, k);
            current.volume = v;
            other.volume   = v;
            yield return null;
        }
        current.volume = target;
        other.volume   = target;
        _duckRoutine = null;
    }

    // ===========================================================
    // SFX
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

    // ===========================================================
    // Mixer + utilidades
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

    AudioClip GetCurrentMusicClip()
    {
        var c = _musicATurn ? _musicB : _musicA;
        return c ? c.clip : null;
    }
}
