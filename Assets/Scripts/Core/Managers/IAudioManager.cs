public interface IAudioManager : IGameService
{
    void PlaySFX(string id);
    void PlayMusic(string id, float fade = 0.5f);
    void SetMasterVolume(float v);
}

public class AudioManager : IAudioManager
{
    private readonly ProjectConfig _cfg;
    private UnityEngine.AudioSource _music;
    private UnityEngine.GameObject _go;

    public AudioManager(ProjectConfig cfg) => _cfg = cfg;

    public void Initialize()
    {
        _go = new UnityEngine.GameObject("AudioManager");
        UnityEngine.Object.DontDestroyOnLoad(_go);
        _music = _go.AddComponent<UnityEngine.AudioSource>();
        _music.loop = true;
        // carga bancos/tabla IDs si usas Addressables
    }

    public void Dispose()
    {
        if (_go != null) UnityEngine.Object.Destroy(_go);
    }

    public void PlaySFX(string id) {  }
    public void PlayMusic(string id, float fade = 0.5f) {  }
    public void SetMasterVolume(float v) { UnityEngine.AudioListener.volume = v; }
}
