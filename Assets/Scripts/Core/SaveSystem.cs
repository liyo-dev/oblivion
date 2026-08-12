using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("Archivos")]
    public string saveFileName = "save.json";

    string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    void Awake()
    {
        var existing = FindAnyObjectByType<SaveSystem>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }
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
            // Escritura atómica: escribimos a .tmp y luego reemplazamos.
            // Si el proceso se interrumpe durante la escritura el save existente queda intacto.
            var tmpPath = SavePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            // FIX A4/medio (auditoría 2026-08-07): Delete()+Move() deja una ventana real sin
            // ningún save.json en disco entre ambas llamadas — un crash justo ahí deja al
            // jugador sin partida guardada en absoluto. File.Replace es una operación atómica a
            // nivel de sistema de archivos: nunca hay un instante sin save.json válido.
            if (File.Exists(SavePath))
                File.Replace(tmpPath, SavePath, null);
            else
                File.Move(tmpPath, SavePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SaveSystem] Partida guardada en: {SavePath}");
#endif
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

        // FIX A4/medio (auditoría 2026-08-07): fallback de última instancia. Si save.json no
        // existe o está corrupto pero queda un save.json.tmp de un guardado interrumpido a mitad
        // de escritura (crash entre WriteAllText y Replace/Move), ese .tmp es la única copia
        // reciente que puede existir — probarlo antes de rendirse.
        var tmpPath = SavePath + ".tmp";
        if (File.Exists(tmpPath) && LoadFromPath(tmpPath, out data))
        {
            Debug.LogWarning("[SaveSystem] save.json no disponible/corrupto; recuperado desde save.json.tmp de un guardado interrumpido.");
            return true;
        }

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
                data.npcRelationships ??= new System.Collections.Generic.List<NPCRelationshipRegistry.SaveEntry>();
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
