namespace SenderoNarrativeCore.Runtime.Services
{
    /// <summary>
    /// Interface that projects should implement to connect the narrative system to their quest tracking logic.
    /// </summary>
    public interface IQuestService
    {
        /// <summary>
        /// Starts a quest by identifier.
        /// </summary>
        void StartQuest(string questId);

        /// <summary>
        /// Marks a quest as completed.
        /// </summary>
        void CompleteQuest(string questId);
    }
}
