using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("Archivo")]
    public string fileName = "save.json";

    string FullPath => Path.Combine(Application.persistentDataPath, fileName);

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public bool HasSave() => File.Exists(FullPath);

    public bool Save(PlayerSaveData data)
    {
        try
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FullPath, json);
            Debug.Log($"[SaveSystem] Guardado en: {FullPath}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al guardar: {e}");
            return false;
        }
    }

    public bool Load(out PlayerSaveData data)
    {
        try
        {
            if (!HasSave()) { data = default; return false; }
            var json = File.ReadAllText(FullPath);
            data = JsonUtility.FromJson<PlayerSaveData>(json);
            return data != null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al cargar: {e}");
            data = default;
            return false;
        }
    }

    public bool Delete()
    {
        try
        {
            if (HasSave()) File.Delete(FullPath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al borrar: {e}");
            return false;
        }
    }
}