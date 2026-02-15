using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [Header("Perfil de reglas")]
    [SerializeField] public AudioGraphProfile profile;

    [Header("Mixer (grupos opcionales)")]
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;
    public AudioMixerGroup ambienceGroup;
    public AudioMixerGroup dialogueGroup;

    [Header("Parámetros del Mixer")]
    [SerializeField] private string masterVolumeParam = "MasterVol";
    [SerializeField] private string musicVolumeParam = "MusicVol";
    [SerializeField] private string sfxVolumeParam = "SfxVol";
    [SerializeField] private string dialogueVolumeParam = "DialogVol";

    [Header("Música")]
    [Min(0f)] public float defaultFade = 0.75f;

    [Header("Pool SFX")]
    [Min(1)] public int pool2DSize = 16;
    [Min(1)] public int pool3DSize = 16;

    // --- motor interno ---
    AudioSource _musicA, _musicB;
    AudioSource _voiceSource;
    bool _musicATurn; // false => current=_musicA, true => current=_musicB
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
    bool _isCinematicMode;

    // --- control de música de victoria ---
    Coroutine _victoryRestoreCoroutine;
    float _duckTarget = 1f;
    int _duckCount = 0;
    Coroutine _duckRoutine;
    bool _battleActive;

    // Recuerdo qué clip pidió la última escena base (no aditiva)
    AudioClip _lastRequestedSceneClip;
    
    // --- Footstep alternation ---
    int _footstepIndex = 0;

    // --- reintentos de wiring de señales ---
    bool _signalsWired = false;
    Coroutine _ensureSignalsCoro;

    // ===========================================================
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fuentes
        _musicA = CreateChildSource("MusicA", musicGroup, spatial:false, loop:true);
        _musicB = CreateChildSource("MusicB", musicGroup, spatial:false, loop:true);
        _voiceSource = CreateChildSource("Voice", dialogueGroup, spatial:false, loop:false);
        for (int i = 0; i < pool2DSize; i++) _pool2D.Enqueue(CreateChildSource($"SFX2D_{i}", sfxGroup, spatial:false));
        for (int i = 0; i < pool3DSize; i++) _pool3D.Enqueue(CreateChildSource($"SFX3D_{i}", sfxGroup, spatial:true));

        // Escenas
        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        // Señales (incluye inactivos)
        _signals = DefaultNarrativeSignals.Instance
                   ?? ServiceLocator.Get<DefaultNarrativeSignals>(false)
                   ?? DefaultNarrativeSignals.EnsureInstance();

        EnsureSignalsAndWireNow();
        if (!_signalsWired && _ensureSignalsCoro == null)
            _ensureSignalsCoro = StartCoroutine(EnsureSignalsRoutine());

        // Música para la escena actual
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        // Aplicar preferencias persistidas
        PlayerSettings.ApplyAudioToService(this);
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
    // Señales (wiring robusto)
    void EnsureSignalsAndWireNow()
    {
        if (_signals == null)
        {
            _signals = DefaultNarrativeSignals.Instance
                       ?? ServiceLocator.Get<DefaultNarrativeSignals>(false)
                       ?? DefaultNarrativeSignals.EnsureInstance();
            if (_signals == null) return; // aún no disponible
        }
        if (_signalsWired) return;

        WireEventSfx();
        WireBattleStarts();
        WireBattleWins();
        _signalsWired = true;
        // Debug.Log("[AudioService] Señales conectadas (SFX, BattleStarts, BattleWins).");
    }

    IEnumerator EnsureSignalsRoutine()
    {
        const float timeout = 5f;
        float t = 0f;
        while (!_signalsWired && t < timeout)
        {
            EnsureSignalsAndWireNow();
            if (_signalsWired) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

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

            object key = r.battleId; // coincide con RaiseBattleWon(battleId)
            Action h = () => OnBattleWonRestoreMusic(r);
            _signals.OnBattleWon(key, h);
            _battleWinHandlers[key] = h;
        }
    }

    // ===========================================================
    // Escenas (normales y aditivas/cinemáticas)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSignalsAndWireNow();
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
                    // REPLACE: apila la música actual y sustituye por la de la cinemática
                    var current = GetCurrentMusicClip();
                    _musicStack.Push(new MusicStackItem { clip = current });
                    if (rule.music != null && current != rule.music)
                        PlayMusic(rule.music, rule.fade);
                    else if (rule.music != null && current == rule.music)
                        PlayMusic(rule.music, rule.fade); // fast-path evita reinicio
                }
            }
            return;
        }

        // Escena base (no aditiva): elige la primera coincidencia
        _lastRequestedSceneClip = null;
        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var r = profile.sceneMusic[i];
            if (r != null &&
                !string.IsNullOrEmpty(r.sceneName) &&
                r.music != null &&
                scene.name.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _lastRequestedSceneClip = r.music;
                if (GetCurrentMusicClip() != r.music) PlayMusic(r.music);
                // si ya suena el mismo, fast-path evita reinicio
                return;
            }
        }
        // si ninguna regla coincide, _lastRequestedSceneClip se queda null
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
            // REPLACE: difiere la restauración 1 frame para ver qué música pide la nueva escena base
            StartCoroutine(HandleAdditiveUnloadRestore(rule.fade));
        }
    }

    IEnumerator HandleAdditiveUnloadRestore(float fade)
    {
        // Dar un frame para que la nueva escena base haga su OnSceneLoaded y fije _lastRequestedSceneClip
        yield return null;

        // Si hay batalla activa, no restauramos ni forzamos música de base.
        if (_battleActive)
            yield break;

        var current = GetCurrentMusicClip();
        // Si la nueva escena base quiere continuar EXACTAMENTE con el clip actual, no restaures
        if (_lastRequestedSceneClip != null && current == _lastRequestedSceneClip)
        {
            // Consumimos la entrada del stack asociada a la cinemática para no dejar basura
            if (_musicStack.Count > 0) _musicStack.Pop();
            yield break;
        }

        // Si no hay continuidad, restaurar lo que sonaba antes de la cinemática
        if (_musicStack.Count > 0)
        {
            var prev = _musicStack.Pop();
            if (prev.clip != null) PlayMusic(prev.clip, fade);
            else StopMusic(fade);
        }

        // Salvaguarda adicional (opcional): si la cinemática define una escena base a restaurar
        // y no se ha restaurado nada (o la base no pidió música), forzar la música de esa escena.
        // Buscamos la última regla aditiva activa en las escenas actualmente cargadas.
        if (profile != null && profile.additiveCinematics != null)
        {
            // Buscar alguna regla cuya restoreBaseSceneName esté configurada
            for (int i = 0; i < profile.additiveCinematics.Count; i++)
            {
                var r = profile.additiveCinematics[i];
                if (r == null) continue;
                if (string.IsNullOrWhiteSpace(r.restoreBaseSceneName)) continue;

                // Encontrar en SceneMusic el clip asociado a esa escena base por substring
                var targetClip = FindSceneMusicClipByName(r.restoreBaseSceneName);
                if (targetClip != null)
                {
                    // Si ya está sonando, no reiniciamos; si no, lo forzamos
                    if (GetCurrentMusicClip() != targetClip)
                        PlayMusic(targetClip, fade);
                    break;
                }
            }
        }
    }

    AudioClip FindSceneMusicClipByName(string sceneName)
    {
        if (profile == null || profile.sceneMusic == null || string.IsNullOrWhiteSpace(sceneName))
            return null;
        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var r = profile.sceneMusic[i];
            if (r == null || string.IsNullOrWhiteSpace(r.sceneName) || r.music == null) continue;
            if (sceneName.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.sceneName.IndexOf(sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
                return r.music;
        }
        return null;
    }

    AudioGraphProfile.AdditiveCinematicRule FindAdditiveRuleFor(string sceneName)
    {
        if (profile == null || profile.additiveCinematics == null) return null;
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
        _battleActive = true;
        if (current != r.music) PlayMusic(r.music, r.fade);
        else PlayMusic(r.music, r.fade); // fast-path evita reinicio si ya suena esa
    }

    void OnBattleWonRestoreMusic(AudioGraphProfile.BattleRule r)
    {
        // ✅ IMPORTANTE: Restaurar el loop en los AudioSource (después de música de victoria)
        _musicA.loop = true;
        _musicB.loop = true;
        Debug.Log($"[AudioService] 🔄 Loop restaurado en AudioSources de música");
        
        // PRIORIDAD 1: Si hay una FogZone activa, restaurar su música (no usar el stack)
        var activeFogZone = FogZone.CurrentActiveZone;
        if (activeFogZone != null && !string.IsNullOrEmpty(activeFogZone.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(activeFogZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
                Debug.Log($"[AudioService] Restaurando música de FogZone '{activeFogZone.MusicZoneId}' después de batalla");
                PlayMusic(zoneRule.music, r.fade);
                // Limpiar el stack porque la FogZone maneja su propia música
                if (_musicStack.Count > 0) _musicStack.Pop();
                _battleActive = false;
                return;
            }
        }
        
        // PRIORIDAD 2: Restaurar música de la escena (Gameplay)
        // Descartamos lo que hubiera en el stack para esta batalla
        if (_musicStack.Count > 0) _musicStack.Pop();
        
        if (!RestoreSceneMusic(r.fade))
        {
            StopMusic(r.fade);
        }
        _battleActive = false;
    }
    
    // --- Helpers para lookup de batallas por id (exacto y fallback substring) ---
    AudioGraphProfile.BattleRule FindBattleRuleForId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || profile == null || profile.battles == null) return null;
        string key = id.Trim();

        // 1) match exacto (case-insensitive)
        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;
            if (string.Equals(r.battleId.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return r;
        }

        // 2) fallback: substring por si el id viene con prefijos/sufijos
        for (int i = 0; i < profile.battles.Count; i++)
        {
            var r = profile.battles[i];
            if (r == null || string.IsNullOrWhiteSpace(r.battleId)) continue;
            if (key.IndexOf(r.battleId.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                return r;
        }

        return null;
    }

// --- API opcional llamada desde BossArenaController ---
    public void BeginBattleById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null)
        {
            BeginBattleMusic(rule); // apila la música actual y pone la del boss
        }
        else
        {
            Debug.LogWarning($"[AudioService] BeginBattleById: no hay BattleRule para id='{id}'.");
        }
    }

    public void EndBattleById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null)
        {
            OnBattleWonRestoreMusic(rule); // restaura la música previa a la batalla
        }
        else
        {
            Debug.LogWarning($"[AudioService] EndBattleById: no hay BattleRule para id='{id}'.");
        }
    }

    // ===========================================================
    // Alerta (no inicia estado de batalla; solo cambia música)
    public void BeginAlertById(string id)
    {
        var rule = FindBattleRuleForId(id);
        if (rule != null && rule.music != null)
        {
            // No alterar _battleActive ni apilar stack: es solo una alerta temporal
            PlayMusic(rule.music, rule.fade);
        }
        else
        {
            Debug.LogWarning($"[AudioService] BeginAlertById: no hay BattleRule/music para id='{id}'.");
        }
    }

    // Inicia música de victoria y tras un tiempo restaura a la escena actual
    // Si holdSeconds <= 0, NO restaura automáticamente (control manual)
    public void PlayVictoryForBattle(string battleId, string victoryId, float holdSeconds = 2f)
    {
        Debug.Log($"[AudioService] 🎵 PlayVictoryForBattle LLAMADO - battleId: '{battleId}', victoryId: '{victoryId}', holdSeconds: {holdSeconds}");
        
        // ✅ Cancelar cualquier corrutina de restauración anterior
        if (_victoryRestoreCoroutine != null)
        {
            Debug.LogWarning($"[AudioService] ⚠️ Cancelando corrutina de restauración anterior - evitando doble restauración");
            StopCoroutine(_victoryRestoreCoroutine);
            _victoryRestoreCoroutine = null;
        }
        
        var battleRule = FindBattleRuleForId(battleId);
        var victoryRule = FindBattleRuleForId(victoryId);
        if (victoryRule != null && victoryRule.music != null)
        {
            Debug.Log($"[AudioService] ✅ Reproduciendo música de victoria: {victoryRule.music.name}");
            
            // ✅ CRÍTICO: Desactivar loop para música de victoria
            // La música de victoria debe reproducirse UNA SOLA VEZ
            _musicA.loop = false;
            _musicB.loop = false;
            
            PlayMusic(victoryRule.music, victoryRule.fade);
        }
        else
        {
            Debug.LogWarning($"[AudioService] PlayVictoryForBattle: no hay música de victoria para id='{victoryId}'.");
        }
        
        // ✅ Solo programar restauración automática si holdSeconds > 0
        if (holdSeconds > 0f && battleRule != null)
        {
            Debug.Log($"[AudioService] 🔄 Programando restauración automática de música después de {holdSeconds}s");
            _victoryRestoreCoroutine = StartCoroutine(RestoreAfterVictoryDelay(battleRule, holdSeconds));
        }
        else if (holdSeconds <= 0f)
        {
            Debug.Log($"[AudioService] ⏸️ Restauración automática deshabilitada (holdSeconds={holdSeconds}) - se requiere llamada manual a RestoreBattleMusic()");
        }
        else
        {
            Debug.LogWarning($"[AudioService] ⚠️ No se encontró battleRule para '{battleId}' - no se restaurará música");
        }
    }

    IEnumerator RestoreAfterVictoryDelay(AudioGraphProfile.BattleRule battleRule, float holdSeconds)
    {
        Debug.Log($"[AudioService] ⏱️ RestoreAfterVictoryDelay iniciado - esperando {holdSeconds}s");
        
        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);
        
        Debug.Log($"[AudioService] 🔄 Restaurando música después de victoria");
        OnBattleWonRestoreMusic(battleRule);
        
        // ✅ Limpiar referencia de corrutina
        _victoryRestoreCoroutine = null;
    }


    // ===========================================================
    // Música
    public void PlayMusic(AudioClip clip, float fadeSeconds = -1f)
    {
        if (!clip) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        // Fuente "actual": la que está sonando ahora mismo
        var current = _musicATurn ? _musicB : _musicA;
        var other   = _musicATurn ? _musicA : _musicB;

        // 1) Si YA está sonando este mismo clip, no reiniciamos.
        //    Solo aseguramos volumen (con ducking aplicado) y salimos.
        if (current.clip == clip)
        {
            float target = GetDuckedVolume(1f);

            // si por lo que sea está parado (pausa/crossfade previo), reanudar sin resetear tiempo
            if (!current.isPlaying)
                current.Play();

            // llevar al volumen objetivo suavemente (sin cambiar de fuente)
            StopAllCoroutines();
            StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
            return;
        }

        // 2) Si estaba en la otra fuente el mismo clip (por un crossfade previo a medias),
        //    también evitamos reiniciar y nos quedamos con esa.
        if (other.clip == clip && other.isPlaying)
        {
            float target = GetDuckedVolume(1f);
            StopAllCoroutines();
            StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
            return;
        }

        // 3) Clip distinto → crossfade normal alternando fuentes
        var from = current;
        var to   = other;

        to.clip = clip;
        to.volume = GetDuckedVolume(0f);
        to.timeSamples = 0;             // nuevo clip, empieza de inicio
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

        // Alternamos el turno después de preparar el crossfade
        _musicATurn = !_musicATurn;
    }

    public void StopMusic(float fadeOut = -1f)
    {
        if (fadeOut < 0f) fadeOut = defaultFade;
        var current = _musicATurn ? _musicB : _musicA;
        if (!current.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndStop(current, fadeOut));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float seconds)
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

    IEnumerator FadeOutAndStop(AudioSource src, float seconds)
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

    // Ducking simple (para cinemáticas aditivas duckInsteadOfReplace)
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

    IEnumerator SetMusicVolumeTo(float target, float fade)
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
    // SFX - API pública para reproducir efectos de sonido
    
    /// <summary>
    /// Reproduce un SFX por clave de evento configurada en el AudioGraphProfile.
    /// Ejemplo: PlaySFX("Ambience_Cave"), PlaySFX("Spell01"), PlaySFX("FootStep00")
    /// </summary>
    public void PlaySFX(string eventKey, float volume = 1f, Vector3? worldPosition = null)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        
        AudioClip clip = FindSfxClipByKey(eventKey);
        if (clip != null)
        {
            if (worldPosition.HasValue)
                PlaySFXAt(clip, worldPosition.Value, volume);
            else
                PlaySFX(clip, volume);
        }
        else
        {
            Debug.LogWarning($"[AudioService] SFX no encontrado para clave '{eventKey}'.");
        }
    }
    
    /// <summary>
    /// Reproduce un footstep alternando automáticamente entre FootStep00-04.
    /// Llama desde el controlador de movimiento cada vez que el pie toca el suelo.
    /// </summary>
    public void PlayFootstep(float volume = 1f, Vector3? worldPosition = null)
    {
        string key = $"FootStep0{_footstepIndex}";
        _footstepIndex = (_footstepIndex + 1) % 5; // Alterna entre 0-4
        PlaySFX(key, volume, worldPosition);
    }
    
    /// <summary>
    /// Reproduce un spell SFX por número.
    /// Ejemplo: PlaySpell(2) → reproduce Spell_02
    /// </summary>
    public void PlaySpell(int spellNumber, float volume = 1f, Vector3? worldPosition = null)
    {
        string key = $"Spell0{spellNumber}";
        PlaySFX(key, volume, worldPosition);
    }
    
    /// <summary>
    /// Reproduce un SFX de ambiente por clave.
    /// Ejemplo: PlayAmbience("Ambience_Cave")
    /// </summary>
    public void PlayAmbience(string ambienceKey, float volume = 1f, Vector3? worldPosition = null)
    {
        PlaySFX(ambienceKey, volume, worldPosition);
    }
    
    /// <summary>
    /// Busca un AudioClip en el profile por clave de evento.
    /// </summary>
    AudioClip FindSfxClipByKey(string key)
    {
        if (profile == null || string.IsNullOrWhiteSpace(key)) return null;
        
        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            var r = profile.eventSfx[i];
            if (r != null &&
                !string.IsNullOrWhiteSpace(r.eventKey) &&
                string.Equals(r.eventKey, key, StringComparison.OrdinalIgnoreCase) &&
                r.sfx != null)
            {
                return r.sfx;
            }
        }
        return null;
    }
    
    // ===========================================================
    // SFX (métodos internos y legacy)
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

    IEnumerator ReturnWhenDone(AudioSource src, Queue<AudioSource> pool)
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
    
    /// <summary>
    /// Propiedad pública para obtener el clip de música actual
    /// </summary>
    public AudioClip CurrentMusicClip => GetCurrentMusicClip();
    
    /// <summary>
    /// Restaura la música de la escena actual (según las reglas del profile)
    /// </summary>
    public bool RestoreSceneMusic(float fadeDuration = -1f)
    {
        if (fadeDuration < 0f) fadeDuration = defaultFade;
        
        // Intentar restaurar la última música de escena solicitada
        if (_lastRequestedSceneClip != null)
        {
            PlayMusic(_lastRequestedSceneClip, fadeDuration);
            return true;
        }
        
        // Si no hay, buscar en las reglas del profile para la escena actual
        if (profile != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            foreach (var rule in profile.sceneMusic)
            {
                if (!string.IsNullOrEmpty(rule.sceneName) && currentScene.Contains(rule.sceneName))
                {
                    if (rule.music != null)
                    {
                        PlayMusic(rule.music, fadeDuration);
                        return true;
                    }
                }
            }
        }
        
        Debug.Log("[AudioService] No se encontró música para restaurar en la escena actual");
        return false;
    }

    // ===========================================================
    // API compatible con AudioManager (para narrative nodes)

    public void SetVolume(AudioBus bus, float volume01)
    {
        if (mixer == null) return;
        string param = bus switch
        {
            AudioBus.Master => masterVolumeParam,
            AudioBus.Music => musicVolumeParam,
            AudioBus.Sfx => sfxVolumeParam,
            AudioBus.Dialogue => dialogueVolumeParam,
            _ => null
        };
        if (!string.IsNullOrEmpty(param))
            SetExposedVolume(param, volume01);
    }

    public float GetVolume(AudioBus bus)
    {
        if (mixer == null) return 1f;
        string param = bus switch
        {
            AudioBus.Master => masterVolumeParam,
            AudioBus.Music => musicVolumeParam,
            AudioBus.Sfx => sfxVolumeParam,
            AudioBus.Dialogue => dialogueVolumeParam,
            _ => null
        };
        return !string.IsNullOrEmpty(param) ? GetExposedVolume01(param) : 1f;
    }

    public void Mute(AudioBus bus, bool mute)
    {
        SetVolume(bus, mute ? 0f : 1f);
    }

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (_voiceSource == null || clip == null) return;
        _voiceSource.Stop();
        _voiceSource.clip = clip;
        _voiceSource.volume = Mathf.Clamp01(volume);
        _voiceSource.Play();
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        PlaySFX(clip, volume);
    }
}
