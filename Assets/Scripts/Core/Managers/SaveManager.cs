public class SaveManager : ISaveManager
{
    public bool HasSave => false;

    public void Initialize() { }
    public void Dispose() { }

    public string GetLastSpawnId() => "default";
    public void SaveSpawnId(string id) { }
}
