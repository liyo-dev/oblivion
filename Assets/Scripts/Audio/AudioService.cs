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
    readonly Dictionary<string, Action> _minigameStartHandlers = new(); // $"MINIGAME_START:{id}" → handler (OnCustom)
    readonly Dictionary<string, Action> _minigameEndHandlers = new();   // $"MINIGAME_{id}_WON" → handler (OnCustom)

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
    bool _minigameActive;

    // Coroutines de música rastreadas individualmente para no matar el pool SFX
    Coroutine _crossfadeRoutine;
    Coroutine _fadeOutRoutine;
    // FIX INC-056: durante un crossfade ambas fuentes (_musicA y _musicB) pueden estar sonando a
    // la vez. _fadeOutRoutine solo cubría una; esta segunda referencia permite parar la otra
    // también (ver StopMusic).
    Coroutine _fadeOutRoutineB;
    Coroutine _setVolumeRoutine;

    // Recuerdo qué clip pidió la última escena base (no aditiva)
    AudioClip _lastRequestedSceneClip;

    // Cuando se sabe que una escena aditiva con música propia va a cargarse justo después,
    // se activa este flag para que OnSceneLoaded(Single) registre el clip pero no lo reproduzca.
    public static bool MuteNextBaseSceneMusic;
    
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
            foreach (var kv in _minigameStartHandlers) _signals.OffCustom(kv.Key, kv.Value);
            foreach (var kv in _minigameEndHandlers)   _signals.OffCustom(kv.Key, kv.Value);
        }

        _sfxHandlers.Clear();
        _battleStartHandlers.Clear();
        _battleWinHandlers.Clear();
        _minigameStartHandlers.Clear();
        _minigameEndHandlers.Clear();
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
        WireMinigames();
        _signalsWired = true;
        // Debug.Log("[AudioService] Señales conectadas (SFX, BattleStarts, BattleWins, Minigames).");
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

    void WireMinigames()
    {
        if (_signals == null || profile == null || profile.minigames == null) return;
        if (_minigameStartHandlers.Count > 0) return;

        for (int i = 0; i < profile.minigames.Count; i++)
        {
            var r = profile.minigames[i];
            if (r == null || string.IsNullOrWhiteSpace(r.minigameId)) continue;

            // Evento de inicio: MINIGAME_START:{id}
            string startKey = $"MINIGAME_START:{r.minigameId}";
            Action startHandler = () => BeginMinigameMusic(r);
            _signals.OnCustom(startKey, startHandler);
            _minigameStartHandlers[startKey] = startHandler;

            // Evento de victoria/fin: MINIGAME_{id}_WON (el TagMinigameController emite esto)
            string endKey = $"MINIGAME_{r.minigameId}_WON";
            Action endHandler = () => OnMinigameEndRestoreMusic(r);
            _signals.OnCustom(endKey, endHandler);
            _minigameEndHandlers[endKey] = endHandler;
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
        bool suppressMusic = MuteNextBaseSceneMusic;
        MuteNextBaseSceneMusic = false; // consumir siempre, haya o no coincidencia

        for (int i = 0; i < profile.sceneMusic.Count; i++)
        {
            var r = profile.sceneMusic[i];
            if (r != null &&
                !string.IsNullOrEmpty(r.sceneName) &&
                r.music != null &&
                scene.name.IndexOf(r.sceneName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _lastRequestedSceneClip = r.music;
                if (!suppressMusic && GetCurrentMusicClip() != r.music) PlayMusic(r.music);
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

        // Si hay batalla o minijuego activo, no restauramos música de base.
        if (_battleActive || _minigameActive)
            yield break;

        var current = GetCurrentMusicClip();
        // Si la nueva escena base quiere continuar EXACTAMENTE con el clip actual, no restaures
        if (_lastRequestedSceneClip != null && current == _lastRequestedSceneClip)
        {
            // Consumimos la entrada del stack asociada a la cinemática para no dejar basura
            if (_musicStack.Count > 0) _musicStack.Pop();
            yield break;
        }

        // Si no hay continuidad, restaurar lo que sonaba antes de la cinemática.
        // Si el stack guardó null (nada sonaba cuando cargó la escena aditiva), usar _lastRequestedSceneClip.
        if (_musicStack.Count > 0)
        {
            var prev = _musicStack.Pop();
            if (prev.clip != null)
                PlayMusic(prev.clip, fade);
            else if (_lastRequestedSceneClip != null)
                PlayMusic(_lastRequestedSceneClip, fade);
            else
                StopMusic(fade);
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
        
        // PRIORIDAD 1: Si hay una AmbientZone activa, restaurar su música (no usar el stack)
        var activeAmbientZone = AmbientZone.CurrentActiveZone;
        if (activeAmbientZone != null && !string.IsNullOrEmpty(activeAmbientZone.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(activeAmbientZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
                Debug.Log($"[AudioService] Restaurando música de AmbientZone '{activeAmbientZone.MusicZoneId}' después de batalla");
                PlayMusic(zoneRule.music, r.fade);
                // Limpiar el stack porque la AmbientZone maneja su propia música
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
    
    // ===========================================================
    // Minijuegos
    void BeginMinigameMusic(AudioGraphProfile.MinigameRule r)
    {
        if (r.music == null)
        {
            Debug.LogWarning($"[AudioService] Minigame '{r.minigameId}' no tiene música configurada");
            return;
        }

        // Si el minijuego ya está activo (reinicio de ronda), no apilar de nuevo:
        // simplemente reiniciar la pista desde el inicio.
        if (_minigameActive)
        {
            _musicA.loop = r.loop;
            _musicB.loop = r.loop;
            RestartMusicClipFromBeginning(r.music, r.fade);
            Debug.Log($"[AudioService] 🔁 Música de minijuego '{r.minigameId}' reiniciada desde el inicio");
            return;
        }
        
        var current = GetCurrentMusicClip();
        _musicStack.Push(new MusicStackItem { clip = current });
        _minigameActive = true;
        
        // Configurar loop según la regla
        _musicA.loop = r.loop;
        _musicB.loop = r.loop;
        
        PlayMusic(r.music, r.fade);
        Debug.Log($"[AudioService] 🎮 Música de minijuego '{r.minigameId}' iniciada");
    }

    // Para las corrutinas de música se usan referencias explícitas y nunca StopAllCoroutines,
    // porque éste mataría las corrutinas ReturnWhenDone del pool SFX.
    void StopMusicCoroutines()
    {
        if (_crossfadeRoutine != null) { StopCoroutine(_crossfadeRoutine); _crossfadeRoutine = null; }
        if (_fadeOutRoutine   != null) { StopCoroutine(_fadeOutRoutine);   _fadeOutRoutine   = null; }
        if (_fadeOutRoutineB  != null) { StopCoroutine(_fadeOutRoutineB);  _fadeOutRoutineB  = null; }
        if (_setVolumeRoutine != null) { StopCoroutine(_setVolumeRoutine); _setVolumeRoutine = null; }
    }

    void RestartMusicClipFromBeginning(AudioClip clip, float fadeSeconds)
    {
        if (clip == null) return;
        if (fadeSeconds < 0f) fadeSeconds = defaultFade;

        var current = _musicATurn ? _musicB : _musicA;
        var other = _musicATurn ? _musicA : _musicB;

        // Seleccionar la fuente que está realmente sonando ahora.
        AudioSource active = current.isPlaying ? current : (other.isPlaying ? other : current);

        // Si por algún motivo no sonaba el clip esperado, delegar al flujo estándar.
        if (active.clip != clip)
        {
            PlayMusic(clip, fadeSeconds);
            return;
        }

        StopMusicCoroutines();
        active.Stop();
        active.timeSamples = 0;
        active.volume = GetDuckedVolume(1f);
        active.Play();

        // Evitar duplicados en la otra fuente.
        if (other != active && other.isPlaying && other.clip == clip)
        {
            other.Stop();
            other.timeSamples = 0;
        }
    }

    void OnMinigameEndRestoreMusic(AudioGraphProfile.MinigameRule r)
    {
        if (!_minigameActive)
        {
            Debug.Log($"[AudioService] Minijuego '{r.minigameId}' no estaba activo, ignorando restauración");
            return;
        }
        
        // Restaurar loop
        _musicA.loop = true;
        _musicB.loop = true;
        Debug.Log($"[AudioService] 🔄 Loop restaurado en AudioSources de música después de minijuego");
        
        // PRIORIDAD 1: Si hay una AmbientZone activa, restaurar su música
        var activeAmbientZone = AmbientZone.CurrentActiveZone;
        if (activeAmbientZone != null && !string.IsNullOrEmpty(activeAmbientZone.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(activeAmbientZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
                Debug.Log($"[AudioService] Restaurando música de AmbientZone '{activeAmbientZone.MusicZoneId}' después de minijuego");
                PlayMusic(zoneRule.music, r.fade);
                if (_musicStack.Count > 0) _musicStack.Pop();
                _minigameActive = false;
                return;
            }
        }
        
        // PRIORIDAD 2: Restaurar música del stack o de la escena
        if (_musicStack.Count > 0) _musicStack.Pop();
        
        if (!RestoreSceneMusic(r.fade))
        {
            StopMusic(r.fade);
        }
        _minigameActive = false;
        Debug.Log($"[AudioService] 🎮 Música de minijuego '{r.minigameId}' finalizada, música restaurada");
    }
    
    /// <summary>
    /// Inicia la música de un minijuego por ID (llamado manualmente si no se usa señales)
    /// </summary>
    public void BeginMinigameById(string minigameId)
    {
        var rule = profile?.GetMinigameRule(minigameId);
        if (rule != null)
        {
            BeginMinigameMusic(rule);
        }
        else
        {
            Debug.LogWarning($"[AudioService] No se encontró regla de música para minijuego '{minigameId}'");
        }
    }
    
    /// <summary>
    /// Finaliza la música de un minijuego por ID (llamado manualmente si no se usa señales)
    /// </summary>
    public void EndMinigameById(string minigameId)
    {
        var rule = profile?.GetMinigameRule(minigameId);
        if (rule != null)
        {
            OnMinigameEndRestoreMusic(rule);
        }
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

    /// <summary>
    /// Restaura la música después de una batalla sin necesitar una BattleRule específica.
    /// Úsalo cuando el NPC no tiene battleMusicId configurado pero sí reproduce música de victoria.
    /// </summary>
    public void RestoreAfterBattle(float fade = -1f)
    {
        if (fade < 0f) fade = defaultFade;

        _musicA.loop = true;
        _musicB.loop = true;

        var activeAmbientZone = AmbientZone.CurrentActiveZone;
        if (activeAmbientZone != null && !string.IsNullOrEmpty(activeAmbientZone.MusicZoneId))
        {
            var zoneRule = profile?.GetAmbientZoneRule(activeAmbientZone.MusicZoneId);
            if (zoneRule?.music != null)
            {
                Debug.Log($"[AudioService] RestoreAfterBattle: restaurando música de AmbientZone '{activeAmbientZone.MusicZoneId}'");
                PlayMusic(zoneRule.music, fade);
                // BUGFIX: solo tocar la pila si ESTE combate llegó a apilar algo. Los enemigos
                // sin battleMusicId nunca pasan por BeginBattleMusic (no cambian de música al
                // empezar), así que _battleActive sigue en false aquí; hacer Pop() igualmente
                // robaba la entrada de otro sistema (cinemática aditiva, minijuego) que sí la
                // había apilado legítimamente y aún no le tocaba restaurarse.
                if (_battleActive && _musicStack.Count > 0) _musicStack.Pop();
                _battleActive = false;
                return;
            }
        }

        if (_battleActive && _musicStack.Count > 0) _musicStack.Pop();
        if (_battleActive && !RestoreSceneMusic(fade))
            StopMusic(fade);
        _battleActive = false;
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

            // BUGFIX: si holdSeconds <= 0 la restauración es MANUAL (la hace el lifecycle
            // handler del NPC, normalmente al cerrar el diálogo post-derrota). Si en ese caso
            // desactivamos el loop, el AudioSource termina el clip por su cuenta y se para SOLO
            // en cuanto acaba el jingle — si el diálogo tarda más que el clip (caso muy común),
            // se queda un silencio real hasta que la restauración manual llega. Con holdSeconds
            // > 0 sí queremos que suene una sola vez, porque la propia corrutina de restauración
            // ya está temporizada para llegar a tiempo.
            bool holdUntilManualRestore = holdSeconds <= 0f;
            _musicA.loop = holdUntilManualRestore;
            _musicB.loop = holdUntilManualRestore;

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
            StopMusicCoroutines();
            _duckRoutine = null;

            // BUGFIX: StopMusicCoroutines() solo mata la corrutina de crossfade/fade-out en
            // curso, no la fuente en sí. Si 'other' venía de un crossfade interrumpido a medias
            // se queda sonando su clip anterior indefinidamente, mezclado con 'current' (esto es
            // lo que producía "suena la música de gameplay Y la de la zona a la vez"). Si 'other'
            // no comparte el clip que queremos, hay que silenciarla explícitamente aquí.
            if (other.isPlaying && other.clip != clip)
            {
                other.Stop();
                other.volume = 0f;
            }

            _setVolumeRoutine = StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
            return;
        }

        // 2) Si estaba en la otra fuente el mismo clip (por un crossfade previo a medias),
        //    también evitamos reiniciar y nos quedamos con esa.
        if (other.clip == clip && other.isPlaying)
        {
            float target = GetDuckedVolume(1f);
            StopMusicCoroutines();
            _duckRoutine = null;

            // BUGFIX: 'other' es la fuente que de verdad queremos activa a partir de ahora.
            // Antes no se actualizaba _musicATurn, así que SetMusicVolumeTo seguía tratando a
            // 'current' (el clip viejo) como la fuente "actual" y la subía al volumen objetivo
            // en vez de pararla — dejando el clip viejo y el nuevo sonando a la vez. Alineamos
            // el turno con la realidad y silenciamos 'current' si quedó con un clip distinto.
            if (current.isPlaying && current.clip != clip)
            {
                current.Stop();
                current.volume = 0f;
            }
            _musicATurn = !_musicATurn;

            _setVolumeRoutine = StartCoroutine(SetMusicVolumeTo(target, fadeSeconds));
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
            StopMusicCoroutines();
            _duckRoutine = null;
            _crossfadeRoutine = StartCoroutine(Crossfade(from, to, fadeSeconds));
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

        // FIX INC-056: antes solo se paraba la fuente "current" según el flag _musicATurn. Si
        // había un crossfade en curso (ej: música de batalla del Golem empezando a sonar mientras
        // la anterior aún no había terminado de apagarse) las DOS fuentes (_musicA y _musicB)
        // podían estar sonando a la vez, y esta función dejaba la otra sonando de fondo — al
        // morir, la música principal seguía escuchándose mezclada con la de Game Over. Ahora se
        // paran ambas fuentes si están sonando, en vez de asumir que solo una lo está.
        if (!_musicA.isPlaying && !_musicB.isPlaying) return;

        StopMusicCoroutines();
        _duckRoutine = null;

        if (_musicA.isPlaying) _fadeOutRoutine  = StartCoroutine(FadeOutAndStop(_musicA, fadeOut, isSecondary: false));
        if (_musicB.isPlaying) _fadeOutRoutineB = StartCoroutine(FadeOutAndStop(_musicB, fadeOut, isSecondary: true));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float seconds)
    {
        if (seconds <= 0f) { from.Stop(); to.volume = GetDuckedVolume(1f); _crossfadeRoutine = null; yield break; }
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
        _crossfadeRoutine = null;
    }

    IEnumerator FadeOutAndStop(AudioSource src, float seconds, bool isSecondary = false)
    {
        if (seconds <= 0f)
        {
            src.Stop();
            if (isSecondary) _fadeOutRoutineB = null; else _fadeOutRoutine = null;
            yield break;
        }
        float start = src.volume, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        src.Stop();
        src.volume = GetDuckedVolume(1f);
        if (isSecondary) _fadeOutRoutineB = null; else _fadeOutRoutine = null;
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

        // BUGFIX: antes 'other' se interpolaba desde a0 (volumen de 'current') hacia el mismo
        // target que 'current', sin usar b0 nunca. Si 'other' tenía un clip distinto sonando
        // (p.ej. un crossfade interrumpido) esto la subía al mismo volumen que la música actual
        // en vez de apagarla — dos pistas distintas sonando a la vez indefinidamente. Ahora
        // 'other' solo comparte el target si de verdad es el mismo clip (ducking normal);
        // si no, se apaga hacia 0 desde su propio volumen real (b0) y se para al terminar.
        bool sameClip = other.clip == current.clip;
        float otherTarget = sameClip ? target : 0f;

        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fade);
            current.volume = Mathf.Lerp(a0, target, k);
            other.volume   = Mathf.Lerp(b0, otherTarget, k);
            yield return null;
        }
        current.volume = target;
        other.volume   = otherTarget;
        if (!sameClip && other.isPlaying) other.Stop();
        _duckRoutine = null;
        _setVolumeRoutine = null;
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
