using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-500)]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] string masterVolumeParam = "MasterVol";
    [SerializeField] string musicVolumeParam = "MusicVol";
    [SerializeField] string sfxVolumeParam = "SfxVol";
    [SerializeField] string dialogueVolumeParam = "DialogVol";

    [Header("Mixer Groups")]
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup dialogueGroup;

    [Header("Music")]
    [SerializeField] float defaultMusicFadeSeconds = 0.5f;
    [SerializeField] AnimationCurve crossFadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("SFX")]
    [SerializeField] int sfxPoolSize = 10;
    [SerializeField] bool expandSfxPool = true;

    [Header("Voice")]
    [SerializeField] bool stopVoiceOnPlay = true;

    readonly Dictionary<AudioBus, float> _volumeCache = new Dictionary<AudioBus, float>()
    {
        { AudioBus.Master, 1f },
        { AudioBus.Music, 1f },
        { AudioBus.Sfx, 1f },
        { AudioBus.Dialogue, 1f }
    };

    readonly Dictionary<AudioBus, bool> _muteStates = new Dictionary<AudioBus, bool>()
    {
        { AudioBus.Master, false },
        { AudioBus.Music, false },
        { AudioBus.Sfx, false },
        { AudioBus.Dialogue, false }
    };

    const float MinDb = -80f;
    const string PrefMaster = "vol_master";
    const string PrefMusic = "vol_music";
    const string PrefSfx = "vol_sfx";
    const string PrefDialogue = "vol_dialog";

    AudioSource _musicA;
    AudioSource _musicB;
    bool _isMusicAForeground = true;
    Coroutine _musicFadeRoutine;

    readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    AudioSource _voiceSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        LoadPersistedVolumes();
    }

    void EnsureAudioSources()
    {
        _musicA = CreateChildSource("Music Source A", musicGroup, loop: true);
        _musicB = CreateChildSource("Music Source B", musicGroup, loop: true);
        _musicA.playOnAwake = false;
        _musicB.playOnAwake = false;

        _voiceSource = CreateChildSource("Voice Source", dialogueGroup, loop: false);
        _voiceSource.playOnAwake = false;

        for (int i = 0; i < sfxPoolSize; i++)
            _sfxPool.Add(CreateChildSource($"SFX Source {i + 1}", sfxGroup, loop: false));
    }

    AudioSource CreateChildSource(string name, AudioMixerGroup group, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.outputAudioMixerGroup = group;
        return src;
    }

    #region Volume API

    public void SetVolume(AudioBus bus, float volume01)
    {
        SetVolumeInternal(bus, Mathf.Clamp01(volume01), true);
    }

    public void SetVolumeDb(AudioBus bus, float db)
    {
        var linear = Mathf.Clamp01(DbTo01(db));
        SetVolumeInternal(bus, linear, true);
    }

    void SetVolumeInternal(AudioBus bus, float volume01, bool persist)
    {
        _volumeCache[bus] = volume01;
        var param = GetParam(bus);
        if (mixer && !string.IsNullOrEmpty(param))
        {
            var db = _muteStates[bus] ? MinDb : Volume01ToDb(volume01);
            mixer.SetFloat(param, db);
        }

        if (persist)
            PlayerPrefs.SetFloat(GetPrefKey(bus), volume01);
    }

    public float GetVolume(AudioBus bus) => _volumeCache[bus];

    public void Mute(AudioBus bus, bool mute)
    {
        _muteStates[bus] = mute;
        var param = GetParam(bus);
        if (mixer && !string.IsNullOrEmpty(param))
        {
            float db = mute ? MinDb : Volume01ToDb(_volumeCache[bus]);
            mixer.SetFloat(param, db);
        }
    }

    void LoadPersistedVolumes()
    {
        SetVolumeInternal(AudioBus.Master, PlayerPrefs.GetFloat(PrefMaster, 1f), false);
        SetVolumeInternal(AudioBus.Music, PlayerPrefs.GetFloat(PrefMusic, 1f), false);
        SetVolumeInternal(AudioBus.Sfx, PlayerPrefs.GetFloat(PrefSfx, 1f), false);
        SetVolumeInternal(AudioBus.Dialogue, PlayerPrefs.GetFloat(PrefDialogue, 1f), false);
    }

    string GetParam(AudioBus bus) => bus switch
    {
        AudioBus.Master => masterVolumeParam,
        AudioBus.Music => musicVolumeParam,
        AudioBus.Sfx => sfxVolumeParam,
        AudioBus.Dialogue => dialogueVolumeParam,
        _ => null
    };

    string GetPrefKey(AudioBus bus) => bus switch
    {
        AudioBus.Master => PrefMaster,
        AudioBus.Music => PrefMusic,
        AudioBus.Sfx => PrefSfx,
        AudioBus.Dialogue => PrefDialogue,
        _ => PrefMaster
    };

    static float Volume01ToDb(float value)
    {
        if (value <= 0.0001f) return MinDb;
        return Mathf.Log10(value) * 20f;
    }

    static float DbTo01(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    #endregion

    #region Music

    public void PlayMusic(AudioClip clip, float fadeSeconds = -1f, bool loop = true)
    {
        if (clip == null) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultMusicFadeSeconds;

        var newSource = _isMusicAForeground ? _musicB : _musicA;
        newSource.clip = clip;
        newSource.loop = loop;
        newSource.volume = 0f;
        newSource.Play();

        if (_musicFadeRoutine != null)
            StopCoroutine(_musicFadeRoutine);

        _musicFadeRoutine = StartCoroutine(CrossFadeMusicRoutine(fadeSeconds, newSource));
        _isMusicAForeground = !_isMusicAForeground;
    }

    public void StopMusic(float fadeSeconds = -1f)
    {
        if (fadeSeconds < 0f) fadeSeconds = defaultMusicFadeSeconds;
        if (_musicFadeRoutine != null)
            StopCoroutine(_musicFadeRoutine);

        _musicFadeRoutine = StartCoroutine(FadeOutCurrentMusicRoutine(fadeSeconds));
    }

    IEnumerator CrossFadeMusicRoutine(float duration, AudioSource newForeground)
    {
        var current = GetCurrentMusicSource();
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
            float eased = crossFadeCurve != null ? crossFadeCurve.Evaluate(t) : t;
            newForeground.volume = eased;
            if (current != null)
                current.volume = 1f - eased;
            yield return null;
        }

        if (current != null)
        {
            current.volume = 0f;
            current.Stop();
            current.clip = null;
        }

        newForeground.volume = 1f;
        _musicFadeRoutine = null;
    }

    IEnumerator FadeOutCurrentMusicRoutine(float duration)
    {
        var current = GetCurrentMusicSource();
        if (current == null)
        {
            _musicFadeRoutine = null;
            yield break;
        }

        float startVolume = current.volume;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
            float eased = crossFadeCurve != null ? crossFadeCurve.Evaluate(1f - t) : 1f - t;
            current.volume = startVolume * eased;
            yield return null;
        }

        current.volume = 0f;
        current.Stop();
        current.clip = null;
        _musicFadeRoutine = null;
    }

    AudioSource GetCurrentMusicSource() => _isMusicAForeground ? _musicA : _musicB;

    #endregion

    #region SFX & Voice

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        var src = GetAvailableSfxSource();
        src.volume = Mathf.Clamp01(volume);
        src.clip = clip;
        src.loop = false;
        src.Play();
    }

    AudioSource GetAvailableSfxSource()
    {
        foreach (var source in _sfxPool)
        {
            if (!source.isPlaying)
                return source;
        }

        if (!expandSfxPool)
            return _sfxPool[0];

        var extra = CreateChildSource($"SFX Source {_sfxPool.Count + 1}", sfxGroup, loop: false);
        _sfxPool.Add(extra);
        return extra;
    }

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (stopVoiceOnPlay && _voiceSource.isPlaying)
            _voiceSource.Stop();

        _voiceSource.clip = clip;
        _voiceSource.volume = Mathf.Clamp01(volume);
        _voiceSource.loop = false;
        _voiceSource.Play();
    }

    public void StopVoice()
    {
        if (_voiceSource.isPlaying)
            _voiceSource.Stop();
    }

    #endregion
}
