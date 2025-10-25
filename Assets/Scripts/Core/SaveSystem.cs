using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("Archivos")]
    public string manualFileName = "save.json";
    public string autoFileName = "autosave.json";

    string ManualPath => Path.Combine(Application.persistentDataPath, manualFileName);
    string AutoPath => Path.Combine(Application.persistentDataPath, autoFileName);

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public bool HasManualSave() => File.Exists(ManualPath);
    public bool HasAutoSave() => File.Exists(AutoPath);
    public bool HasSave() => HasManualSave() || HasAutoSave();

    public bool Save(PlayerSaveData data, SaveRequestContext context = SaveRequestContext.Manual)
    {
        var path = context == SaveRequestContext.Auto ? AutoPath : ManualPath;
        var label = context == SaveRequestContext.Auto ? "Auto" : "Manual";
        try
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            Debug.Log($"[SaveSystem] Guardado {label} en: {path}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al guardar ({label}): {e}");
            return false;
        }
    }

    public bool Load(out PlayerSaveData data)
    {
        // Preferir el guardado manual; si no existe, intentar el auto.
        if (HasManualSave() && LoadFromPath(ManualPath, out data))
            return true;

        if (HasAutoSave() && LoadFromPath(AutoPath, out data))
            return true;

        data = default;
        return false;
    }

    public bool LoadAuto(out PlayerSaveData data) => LoadFromPath(AutoPath, out data);

    bool LoadFromPath(string path, out PlayerSaveData data)
    {
        try
        {
            if (!File.Exists(path))
            {
                data = default;
                return false;
            }

            var json = File.ReadAllText(path);
            data = JsonUtility.FromJson<PlayerSaveData>(json);
            return data != null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al cargar ({path}): {e}");
            data = default;
            return false;
        }
    }

    public bool Delete()
    {
        bool ok = true;
        ok &= DeleteFileIfExists(ManualPath);
        ok &= DeleteFileIfExists(AutoPath);
        return ok;
    }

    bool DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al borrar '{path}': {e}");
            return false;
        }
    }
}
