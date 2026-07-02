using System;
using UnityEngine;
using UnityEngine.Events;

namespace Director
{
    public class OnTriggerEnter_Event : MonoBehaviour
    {
        public UnityEvent OnTriggerEnterEvent;
        public Action ActionAfterTrigger;
        public bool DestroyElement;
        public string ElementToCompare;
        public float DelayBeforeTrigger;
        public bool OneTimeEvent;
        private bool m_IsTriggerEnter;
        private bool isEnabled;

        [Header("Requisito de misión")]
        [Tooltip("Si se asigna, el trigger solo se activa cuando esta misión esté en curso o completada.")]
        [SerializeField] private QuestData requiredQuest;

        private void Start()
        {
            isEnabled = true;
        }

        private bool IsQuestRequirementMet()
        {
            if (requiredQuest == null) return true;
            if (QuestManager.Instance == null) return false;
            return QuestManager.Instance.GetState(requiredQuest.questId) != QuestState.Inactive;
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (collision.CompareTag(ElementToCompare) && isEnabled && IsQuestRequirementMet())
            {
                if (DestroyElement) { Destroy(gameObject); }
                if (OneTimeEvent) m_IsTriggerEnter = true;
                if (OneTimeEvent && m_IsTriggerEnter) return;

                if (DelayBeforeTrigger > 0)
                {
                    Invoke("TriggerEvent", DelayBeforeTrigger);
                }
                else
                {
                    TriggerEvent();
                }
            }
        }

        private void TriggerEvent()
        {
                OnTriggerEnterEvent?.Invoke();
                ActionAfterTrigger?.Invoke();
            
        }

        public void CanTrigger() => isEnabled = true;
        public void CanNotTrigger() => isEnabled = false;
    }
}
