namespace SenderoNarrativeCore.Runtime.Services
{
    /// <summary>
    /// Interface for playing music and sound effects requested by narrative nodes.
    /// </summary>
    public interface IAudioService
    {
        void PlayMusic(string musicId);
        void PlaySfx(string sfxId);
    }
}
