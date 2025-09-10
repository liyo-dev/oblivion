using System.IO;
using UnityEngine;

public static class SaveSystem
{
    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "player_save.json");

    public static void Save(PlayerSaveData data)
    {
        File.WriteAllText(Path, JsonUtility.ToJson(data, true));
#if UNITY_EDITOR
        Debug.Log("[SaveSystem] Saved: " + Path);
#endif
    }
    public static bool Exists() => File.Exists(Path);
    public static PlayerSaveData Load() => Exists() ? JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(Path)) : null;
    public static void Delete(){ if (Exists()) File.Delete(Path); }
}