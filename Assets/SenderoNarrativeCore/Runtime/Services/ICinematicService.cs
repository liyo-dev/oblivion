namespace SenderoNarrativeCore.Runtime.Services
{
    /// <summary>
    /// Interface for playing cinematic sequences or timelines.
    /// </summary>
    public interface ICinematicService
    {
        void PlayTimeline(string timelineId);
    }
}
