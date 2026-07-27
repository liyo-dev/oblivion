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

        [Tooltip("Índice del paso (0-based) en el que debe estar la misión para que el trigger se active.\n" +
                 "-1 = sin requisito de paso (comportamiento original: basta con que la misión no esté Inactive).\n" +
                 "N >= 0 = la misión debe estar Active, con el paso N sin completar y el paso N-1 (si existe) completado.\n" +
                 "Ejemplo: 1 = solo durante el step 01 (una vez completado el step 00, antes de completar el step 01).")]
        [SerializeField] private int requiredStepIndex = -1;

        private void Start()
        {
            isEnabled = true;
        }

        private bool IsQuestRequirementMet()
        {
            if (requiredQuest == null) return true;
            if (QuestManager.Instance == null) return false;

            var state = QuestManager.Instance.GetState(requiredQuest.questId);

            // Sin requisito de paso: comportamiento original (misión iniciada o completada).
            if (requiredStepIndex < 0)
                return state != QuestState.Inactive;

            // Con requisito de paso: la misión debe estar activa y encontrarse EXACTAMENTE en ese paso.
            if (state != QuestState.Active) return false;
            if (QuestManager.Instance.IsStepCompleted(requiredQuest.questId, requiredStepIndex)) return false;
            if (requiredStepIndex > 0 &&
                !QuestManager.Instance.IsStepCompleted(requiredQuest.questId, requiredStepIndex - 1))
                return false;

            return true;
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
