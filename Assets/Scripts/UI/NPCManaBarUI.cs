using UnityEngine;
using UnityEngine.UI;
using Game.NPC;

namespace Game.UI
{
    public class NPCManaBarUI : MonoBehaviour
    {
        [SerializeField] private Slider manaSlider;
        private NPCCombatBrain combatBrain;

        void Awake()
        {
            combatBrain = GetComponentInParent<NPCCombatBrain>();
            if (combatBrain == null)
            {
                Debug.LogError("[NPCManaBarUI] No se encontró NPCCombatBrain en los padres.");
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (combatBrain != null && manaSlider != null)
            {
               // manaSlider.maxValue = combatBrain.settings.maxMana;
                //manaSlider.value = combatBrain.GetCurrentMana();
            }
        }
    }
}
