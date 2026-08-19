using System;
using System.IO;
using UnityEngine;

public static class PlayerSettings
{
    private const string SettingsFileName = "player_settings.json";

    public static event Action<string> LanguageChanged;
    public static event Action<AudioBus, float> VolumeChanged;
    public static event Action<bool> InvertLookChanged;
    public static event Action<bool> InvertFlightLookChanged;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        LanguageChanged = null;
        VolumeChanged = null;
        InvertLookChanged = null;
        InvertFlightLookChanged = null;
        _loaded = false;
        _data = null;
    }
#endif

    [Serializable]
    private class PlayerSettingsData
    {
        public string language = "es";
        // FEATURE (primer arranque): distingue "nunca se ha elegido idioma" de "se ha elegido
        // español" (que además es el valor por defecto de arriba). Sin este flag sería
        // imposible saber si player_settings.json no existe porque el jugador nunca ha tocado
        // ajustes, o porque explícitamente confirmó español en el selector de idioma inicial.
        // Ver PlayerSettings.MarkLanguageSelected() y LanguageSelectPanel.
        public bool languageSelected = false;
        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public float musicVolume = 1f;
        public bool invertLook = false;
        public bool invertFlightLook = false;
        public float lookSensitivity = 1f;
        public bool vibration = true;
        public bool subtitles = true;
        public bool fullscreen = true;
    }

    private static bool _loaded;
    private static PlayerSettingsData _data;

    public static string Language
    {
        get
        {
            EnsureLoaded();
            return _data.language;
        }
    }

    /// <summary>
    /// True si el jugador ya ha confirmado un idioma alguna vez en esta instalación (ver
    /// MarkLanguageSelected). MainMenuController lo consulta para decidir si debe mostrar el
    /// selector de idioma de primer arranque antes de dejar ver el menú.
    /// </summary>
    public static bool LanguageSelected
    {
        get
        {
            EnsureLoaded();
            return _data.languageSelected;
        }
    }

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return _data.masterVolume;
        }
    }

    public static float SfxVolume
    {
        get
        {
            EnsureLoaded();
            return _data.sfxVolume;
        }
    }

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return _data.musicVolume;
        }
    }

    public static bool InvertLook
    {
        get
        {
            EnsureLoaded();
            return _data.invertLook;
        }
    }

    public static bool InvertFlightLook
    {
        get
        {
            EnsureLoaded();
            return _data.invertFlightLook;
        }
    }

    public static float LookSensitivity
    {
        get
        {
            EnsureLoaded();
            return _data.lookSensitivity;
        }
    }

    public static bool Vibration
    {
        get { EnsureLoaded(); return _data.vibration; }
    }

    public static bool Subtitles
    {
        get { EnsureLoaded(); return _data.subtitles; }
    }

    public static bool Fullscreen
    {
        get { EnsureLoaded(); return _data.fullscreen; }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _data = LoadFromDisk();
        _loaded = true;
        Debug.Log($"[PlayerSettings] Cargado: invertLook={_data.invertLook}, invertFlightLook={_data.invertFlightLook}");
    }

    public static void SetLanguage(string locale)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(locale) || string.Equals(_data.language, locale, StringComparison.OrdinalIgnoreCase))
            return;

        _data.language = locale;
        SaveToDisk();
        LocalizationManager.Instance?.ChangeLanguage(locale);
        LanguageChanged?.Invoke(locale);
    }

    /// <summary>
    /// Marca que el jugador ya ha confirmado un idioma en esta instalación. Se llama justo
    /// después de SetLanguage() desde LanguageSelectPanel al elegir en el selector de primer
    /// arranque. Se persiste explícitamente aparte de SetLanguage() porque, si el jugador elige
    /// el mismo idioma que ya es por defecto ("es"), SetLanguage() no escribiría nada a disco
    /// por sí solo (no-op cuando el locale no cambia) y el flag nunca llegaría a guardarse.
    /// Idempotente: una vez a true, no vuelve a tocar disco.
    /// </summary>
    public static void MarkLanguageSelected()
    {
        EnsureLoaded();
        if (_data.languageSelected)
            return;

        _data.languageSelected = true;
        SaveToDisk();
    }

    public static void SetMasterVolume(float value01)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp01(value01);
        if (Mathf.Approximately(_data.masterVolume, clamped))
            return;

        _data.masterVolume = clamped;
        SaveToDisk();
        ApplyVolume(AudioBus.Master, clamped);
    }

    public static void SetSfxVolume(float value01)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp01(value01);
        if (Mathf.Approximately(_data.sfxVolume, clamped))
            return;

        _data.sfxVolume = clamped;
        SaveToDisk();
        ApplyVolume(AudioBus.Sfx, clamped);
    }

    public static void SetMusicVolume(float value01)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp01(value01);
        if (Mathf.Approximately(_data.musicVolume, clamped))
            return;

        _data.musicVolume = clamped;
        SaveToDisk();
        ApplyVolume(AudioBus.Music, clamped);
    }

    public static void SetInvertLook(bool invert)
    {
        EnsureLoaded();
        if (_data.invertLook == invert)
            return;

        _data.invertLook = invert;
        SaveToDisk();
        Debug.Log($"[PlayerSettings] InvertLook cambiado a: {invert}");
        InvertLookChanged?.Invoke(invert);
    }

    public static void SetInvertFlightLook(bool invert)
    {
        EnsureLoaded();
        if (_data.invertFlightLook == invert)
            return;

        _data.invertFlightLook = invert;
        SaveToDisk();
        Debug.Log($"[PlayerSettings] InvertFlightLook cambiado a: {invert}");
        InvertFlightLookChanged?.Invoke(invert);
    }

    public static void SetLookSensitivity(float value)
    {
        EnsureLoaded();
        float clamped = Mathf.Clamp(value, 0.1f, 5f);
        if (Mathf.Approximately(_data.lookSensitivity, clamped))
            return;
        _data.lookSensitivity = clamped;
        SaveToDisk();
    }

    public static void SetVibration(bool enabled)
    {
        EnsureLoaded();
        if (_data.vibration == enabled) return;
        _data.vibration = enabled;
        SaveToDisk();
    }

    public static void SetSubtitles(bool enabled)
    {
        EnsureLoaded();
        if (_data.subtitles == enabled) return;
        _data.subtitles = enabled;
        SaveToDisk();
    }

    public static void SetFullscreen(bool enabled)
    {
        EnsureLoaded();
        if (_data.fullscreen == enabled) return;
        _data.fullscreen = enabled;
        SaveToDisk();
        Screen.fullScreen = enabled;
    }

    public static Vector2 ApplyLookInversion(Vector2 lookInput, bool flightContext = false)
    {
        EnsureLoaded();
        bool invert = flightContext ? _data.invertFlightLook : _data.invertLook;
        if (invert)
            lookInput.y = -lookInput.y;
        return lookInput;
    }

    public static void ApplyAudioToService(AudioService service)
    {
        EnsureLoaded();
        if (!service)
            return;

        service.SetVolume(AudioBus.Master, _data.masterVolume);
        service.SetVolume(AudioBus.Sfx, _data.sfxVolume);
        service.SetVolume(AudioBus.Music, _data.musicVolume);
    }

    private static void ApplyVolume(AudioBus bus, float value)
    {
        var audio = AudioService.Instance;
        if (audio)
            audio.SetVolume(bus, value);
        VolumeChanged?.Invoke(bus, value);
    }

    private static string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFileName);

    private static PlayerSettingsData LoadFromDisk()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var data = JsonUtility.FromJson<PlayerSettingsData>(json);
                if (data != null)
                    return data;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerSettings] Error al leer settings: {e.Message}");
        }

        return new PlayerSettingsData();
    }

    private static void SaveToDisk()
    {
        if (!_loaded || _data == null)
            return;

        try
        {
            var json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(SettingsPath, json);
            Debug.Log($"[PlayerSettings] Guardado en: {SettingsPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerSettings] Error al guardar settings: {e.Message}");
        }
    }

    /// <summary>
    /// Método de debug para resetear la configuración a valores por defecto.
    /// Útil para testing.
    /// </summary>
    public static void ResetToDefaults()
    {
        _data = new PlayerSettingsData();
        _loaded = true;
        SaveToDisk();
        Debug.Log("[PlayerSettings] Configuración reseteada a valores por defecto.");
    }
}
