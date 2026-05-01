using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("Archivos")]
    public string saveFileName = "save.json";

    string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ServiceLocator.Register(this);
    }

    void OnDestroy()
    {
        ServiceLocator.Unregister(this);
    }

    public bool HasSave() => File.Exists(SavePath);

    public bool Save(PlayerSaveData data, SaveRequestContext context = SaveRequestContext.Manual)
    {
        try
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveSystem] Partida guardada en: {SavePath}");
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
        if (HasSave() && LoadFromPath(SavePath, out data))
            return true;

        data = default;
        return false;
    }

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
            
            // Sanitizar listas que podrían ser null en saves antiguos
            if (data != null)
            {
                data.partyMemberIds ??= new System.Collections.Generic.List<string>();
                data.unlockedTeleportPoints ??= new System.Collections.Generic.List<string>();
                data.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
                data.seenLorePopupIds ??= new System.Collections.Generic.List<string>();
            }
            
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
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log($"[SaveSystem] Partida eliminada: {SavePath}");
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] Error al borrar: {e}");
            return false;
        }
    }
}
