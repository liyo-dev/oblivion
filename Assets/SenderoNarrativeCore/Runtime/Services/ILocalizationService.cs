namespace SenderoNarrativeCore.Runtime.Services
{
    /// <summary>
    /// Interface for retrieving localized strings used by narrative content.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Gets localized text for a specific key.
        /// </summary>
        /// <param name="key">Localization key.</param>
        /// <returns>Localized string if found; otherwise the key or a fallback.</returns>
        string GetText(string key);
    }
}
