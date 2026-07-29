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
            if (QuestManager.Instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': QuestManager.Instance es null — trigger bloqueado.");
#endif
                return false;
            }

            var state = QuestManager.Instance.GetState(requiredQuest.questId);

            // Sin requisito de paso: comportamiento original (misión iniciada o completada).
            if (requiredStepIndex < 0)
            {
                bool metLegacy = state != QuestState.Inactive;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!metLegacy)
                    Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': misión '{requiredQuest.questId}' está Inactive — trigger bloqueado (requiere iniciada o completada).");
#endif
                return metLegacy;
            }

            // Con requisito de paso: la misión debe estar activa y encontrarse EXACTAMENTE en ese paso.
            if (state != QuestState.Active)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': misión '{requiredQuest.questId}' está en estado '{state}' (se requiere Active) — trigger bloqueado.");
#endif
                return false;
            }
            if (QuestManager.Instance.IsStepCompleted(requiredQuest.questId, requiredStepIndex))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': misión '{requiredQuest.questId}' ya tiene el paso {requiredStepIndex} completado — trigger bloqueado.");
#endif
                return false;
            }
            if (requiredStepIndex > 0 &&
                !QuestManager.Instance.IsStepCompleted(requiredQuest.questId, requiredStepIndex - 1))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': misión '{requiredQuest.questId}' NO tiene completado el paso {requiredStepIndex - 1} (previo a {requiredStepIndex}) — trigger bloqueado.");
#endif
                return false;
            }

            return true;
        }

        private void OnTriggerEnter(Collider collision)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!collision.CompareTag(ElementToCompare))
                return; // ruido esperado (otros colliders no-Player), no logueamos
            if (!isEnabled)
            {
                Debug.LogWarning($"[OnTriggerEnter_Event] '{name}': isEnabled=false (CanNotTrigger() fue llamado) — trigger bloqueado.");
                return;
            }
            Debug.Log($"[OnTriggerEnter_Event] '{name}': colisión de '{collision.name}' válida, comprobando requisito de misión...");
#endif
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
