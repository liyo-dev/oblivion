using UnityEngine;

// Componente simple que expone una instancia de PlayerAbilities
// Añádelo al GameObject del Player para que los sistemas que busquen
// un campo 'abilities' lo encuentren mediante reflexión.
public class PlayerAbilitiesComponent : MonoBehaviour
{
    [Tooltip("Instancia de PlayerAbilities (Swim, Jump, Climb, Magic)")]
    public PlayerAbilities abilities = new PlayerAbilities();

#if UNITY_EDITOR
    [ContextMenu("Apply Active Preset Abilities")]
    private void ApplyPresetAbilities()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset != null && preset.abilities != null)
        {
            abilities = preset.abilities;
            Debug.Log("[PlayerAbilitiesComponent] Applied abilities from active preset.");
        }
        else
        {
            Debug.LogWarning("[PlayerAbilitiesComponent] No active preset with abilities found.");
        }
    }
#endif
}

