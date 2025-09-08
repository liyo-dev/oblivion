public interface ISaveManager : IGameService
{
    bool HasSave { get; }
    string GetLastSpawnId();
    void SaveSpawnId(string id);
}