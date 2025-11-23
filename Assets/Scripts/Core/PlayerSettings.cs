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

    [Serializable]
    private class PlayerSettingsData
    {
        public string language = "es";
        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public bool invertLook = false;
        public bool invertFlightLook = false;
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

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _data = LoadFromDisk();
        _loaded = true;
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

    public static void SetInvertLook(bool invert)
    {
        EnsureLoaded();
        if (_data.invertLook == invert)
            return;

        _data.invertLook = invert;
        SaveToDisk();
        InvertLookChanged?.Invoke(invert);
    }

    public static void SetInvertFlightLook(bool invert)
    {
        EnsureLoaded();
        if (_data.invertFlightLook == invert)
            return;

        _data.invertFlightLook = invert;
        SaveToDisk();
        InvertFlightLookChanged?.Invoke(invert);
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
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerSettings] Error al guardar settings: {e.Message}");
        }
    }
}
