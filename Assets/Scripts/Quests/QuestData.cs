using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Juego/Quest", fileName = "Q_NewQuest")]
public class QuestData : ScriptableObject
{
    public string questId;
    
    [Header("Localización")]
    [Tooltip("ID de localización para el nombre (ej: 'QUEST_MISSION1_NAME'). Si está vacío, usa displayName.")]
    public string displayNameId;
    
    [Tooltip("Nombre de la quest (usado si displayNameId está vacío o no se usa localización)")]
    public string displayName;
    
    [Tooltip("ID de localización para la descripción (ej: 'QUEST_MISSION1_DESC'). Si está vacío, usa description.")]
    public string descriptionId;
    
    [TextArea]
    [Tooltip("Descripción de la quest (usada si descriptionId está vacío o no se usa localización)")]
    public string description;

    [Header("Descripción detallada (panel de detalle de misión)")]
    [Tooltip("ID de localización para la descripción detallada mostrada en el panel de detalle de misión (ej: 'QUEST_MISSION1_DETAIL'). Debe guiar al jugador paso a paso cuando la quest tenga varios steps. Si está vacío, usa detailedDescription.")]
    public string detailedDescriptionId;

    [TextArea(3, 10)]
    [Tooltip("Descripción detallada de la quest (usada si detailedDescriptionId está vacío o no se usa localización). Si también está vacía, se recurre a la descripción corta.")]
    public string detailedDescription;

    [System.Serializable]
    public class Step
    {
        [Tooltip("ID de localización para la descripción del paso (ej: 'QUEST_MISSION1_STEP1'). Si está vacío, usa description.")]
        public string descriptionId;
        
        [Tooltip("Descripción del paso (usada si descriptionId está vacío o no se usa localización)")]
        public string description;
        
        public string conditionId;
    }
    public Step[] steps;
    
    /// <summary>Obtiene el nombre localizado de la quest</summary>
    public string GetLocalizedName()
    {
        if (!string.IsNullOrEmpty(displayNameId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(displayNameId, displayName);
        }
        return displayName;
    }
    
    /// <summary>Obtiene la descripción localizada de la quest</summary>
    public string GetLocalizedDescription()
    {
        if (!string.IsNullOrEmpty(descriptionId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(descriptionId, description);
        }
        return description;
    }
    
    /// <summary>
    /// Obtiene la descripción detallada y localizada de la quest, pensada para el panel de detalle de misión
    /// (guía completa, con los pasos explicados en conjunto si la quest tiene más de uno).
    /// Si no hay texto detallado configurado, recurre a la descripción corta (GetLocalizedDescription).
    /// </summary>
    public string GetLocalizedDetailedDescription()
    {
        string result = null;

        if (!string.IsNullOrEmpty(detailedDescriptionId) && LocalizationManager.Instance != null)
        {
            result = LocalizationManager.Instance.Get(detailedDescriptionId, detailedDescription);
        }
        else if (!string.IsNullOrEmpty(detailedDescription))
        {
            result = detailedDescription;
        }

        return string.IsNullOrEmpty(result) ? GetLocalizedDescription() : result;
    }

    /// <summary>Obtiene la descripción localizada de un paso específico</summary>
    public string GetLocalizedStepDescription(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= steps.Length) return "";
        
        var step = steps[stepIndex];
        if (!string.IsNullOrEmpty(step.descriptionId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(step.descriptionId, step.description);
        }
        return step.description;
    }
}