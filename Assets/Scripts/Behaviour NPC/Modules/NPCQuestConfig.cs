﻿﻿using UnityEngine;
using System.Linq;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de misiones para NPCs.
    /// Aquí se configura la cadena de misiones (Quest Chain) que ofrece el NPC.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Quest_Config", menuName = "NPC/Módulos/Quest Config", order = 4)]
    public class NPCQuestConfig : NPCModuleConfigBase
    {
        [Header("Quest Chain")]
        [Tooltip("Cadena de misiones que ofrece este NPC. Cada elemento representa una quest en orden.")]
        public QuestChainEntry[] questChain = System.Array.Empty<QuestChainEntry>();
        [Header("Item Detection (Global)")]
        [Tooltip("¿Detectar automáticamente cuando el jugador entrega ítems? (Se aplica globalmente)")]
        public bool enableItemDetection = true;
        [Min(0f)]
        [Tooltip("Radio de detección de ítems por defecto")]
        public float detectionRadius = 3f;
        [Range(0f, 180f)]
        [Tooltip("Ángulo de detección de ítemsor defecto")]
        public float detectionAngle = 90f;
        [Tooltip("Layer de detección de ítems")]
        public LayerMask detectionLayer = ~0;
        [Min(0.05f)]
        [Tooltip("Intervalo de escaneo de ítems")]
        public float detectionInterval = 0.33f;
        [Header("Behavior")]
        [Min(0f)]
        [Tooltip("Velocidad de rotación hacia el jugador (grados por segundo) - Ahora manejado por DialogueManager")]
        public float rotationSpeed = 360f;
        
        [Header("Persistent Icon")]
        [Tooltip("¿Ocultar el icono persistente automáticamente cuando todas las quests se completen?")]
        public bool hideIconWhenAllCompleted = true;

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            if (questChain == null || questChain.Length == 0)
            {
                errorMessage = "Quest chain no puede estar vacío. Añade al menos una quest.";
                return false;
            }
            for (int i = 0; i < questChain.Length; i++)
            {
                var entry = questChain[i];
                if (entry == null)
                {
                    errorMessage = $"Quest chain entry {i} es null";
                    return false;
                }
                if (entry.questData == null)
                {
                    errorMessage = $"Quest chain entry {i} no tiene questData asignado";
                    return false;
                }
                if (entry.autoDetectItemDelivery && string.IsNullOrEmpty(entry.itemTag))
                {
                    errorMessage = $"Quest {i} tiene autoDetectItemDelivery activado pero itemTag vacío";
                    return false;
                }
                if (entry.requireItemInInventory && entry.requiredItem == null)
                {
                    errorMessage = $"Quest {i} requiere ítem pero requiredItem es null";
                    return false;
                }
            }
            if (detectionRadius <= 0f)
            {
                errorMessage = "Detection radius debe ser mayor a 0";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Procesa la interacción del jugador con el NPC.
        /// Maneja la lógica de quests, completion modes y diálogos.
        /// </summary>
        public bool ProcessInteraction(GameObject interactor, Common.NPCStateContext context)
        {
            Debug.Log($"[NPCQuestConfig.ProcessInteraction] Iniciando interacción con NPC. Interactor: {interactor?.name}, Context válido: {context != null}");
            
            // ✅ Activar animación de hablar/interactuar
            // La rotación ahora la maneja automáticamente DialogueManager.StartDialogue(dialogue, npcTransform, ...)
            Debug.Log($"[NPCQuestConfig.ProcessInteraction] Activando animación de hablar");
            StartTalkingAnimation(context);
            
            var qm = QuestManager.Instance;
            if (qm == null)
            {
                Debug.LogError("[NPCQuestConfig] QuestManager.Instance es null");
                return false;
            }

            if (questChain == null || questChain.Length == 0)
            {
                Debug.LogWarning("[NPCQuestConfig] questChain vacío");
                return false;
            }

            // Buscar quest activa en la cadena (de atrás hacia adelante)
            for (int i = questChain.Length - 1; i >= 0; i--)
            {
                var entry = questChain[i];
                if (entry?.questData == null) continue;

                var questId = entry.questData.questId;
                var state = qm.GetState(questId);

                if (state == QuestState.Active || state == QuestState.Completed)
                {
                    HandleQuestState(qm, entry, questId, state, context);
                    return true;
                }
            }

            // Si no hay quest activa, iniciar la primera
            var first = questChain[0];
            if (first?.questData != null)
            {
                var firstState = qm.GetState(first.questData.questId);
                if (firstState == QuestState.Inactive)
                {
                    // Si tiene dlgBefore, reproducir diálogo primero y luego iniciar quest
                    if (first.dlgBefore != null)
                    {
                        first.onOfferDialogueStarted?.Invoke();
                        PlayDialogueWithCallback(first.dlgBefore, context, () =>
                        {
                            // Callback ejecutado cuando termina el diálogo
                            qm.AddQuest(first.questData);
                            qm.StartQuest(first.questData.questId);
                            first.onOfferDialogueFinished?.Invoke();
                        });
                    }
                    else
                    {
                        // Sin diálogo, NO iniciar automáticamente (se iniciará desde otro lado)
                        Debug.Log($"[NPCQuestConfig] Quest '{first.questData.questId}' sin dlgBefore - no se inicia automáticamente");
                    }
                    return true;
                }
            }

            return false;
        }

        private void HandleQuestState(QuestManager qm, QuestChainEntry entry, string questId, QuestState state, Common.NPCStateContext context)
        {
            switch (state)
            {
                case QuestState.Active:
                    bool allDone = qm.AreAllStepsCompleted(questId);

                    switch (entry.completionMode)
                    {
                        case QuestCompletionMode.AutoCompleteOnTalk:
                            CompleteAllSteps(qm, entry, questId, context);
                            break;

                        case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                        case QuestCompletionMode.Manual:
                            if (allDone)
                            {
                                FinishQuest(qm, entry, questId, context);
                            }
                            else
                            {
                                PlayDialogue(entry.dlgInProgress, context);
                            }
                            break;
                    }
                    break;

                case QuestState.Completed:
                    PlayDialogue(entry.dlgCompleted, context);
                    break;
            }
        }

        private void CompleteAllSteps(QuestManager qm, QuestChainEntry entry, string questId, Common.NPCStateContext context)
        {
            var quest = qm.GetAll().FirstOrDefault(q => q.Id == questId);
            if (quest?.Steps != null)
            {
                for (int i = 0; i < quest.Steps.Length; i++)
                {
                    if (!quest.Steps[i].completed)
                    {
                        qm.MarkStepDone(questId, i);
                    }
                }
            }
            FinishQuest(qm, entry, questId, context);
        }

        private void FinishQuest(QuestManager qm, QuestChainEntry entry, string questId, Common.NPCStateContext context)
        {
            qm.CompleteQuest(questId);
            entry.onQuestCompleted?.Invoke();
            PlayDialogue(entry.dlgTurnIn, context);
            
            // Buscar la siguiente quest en la cadena
            TryStartNextQuestInChain(questId, context);
        }

        private void TryStartNextQuestInChain(string completedQuestId, Common.NPCStateContext context)
        {
            // Encontrar índice de la quest completada
            int completedIndex = -1;
            for (int i = 0; i < questChain.Length; i++)
            {
                if (questChain[i]?.questData?.questId == completedQuestId)
                {
                    completedIndex = i;
                    break;
                }
            }

            if (completedIndex < 0 || completedIndex >= questChain.Length - 1)
                return; // No hay siguiente quest

            var nextEntry = questChain[completedIndex + 1];
            if (nextEntry?.questData == null) return;

            var qm = QuestManager.Instance;
            if (qm == null) return;

            var nextState = qm.GetState(nextEntry.questData.questId);
            if (nextState != QuestState.Inactive) return; // Ya está iniciada

            // Si tiene dlgBefore, reproducir diálogo y luego iniciar quest
            if (nextEntry.dlgBefore != null)
            {
                nextEntry.onOfferDialogueStarted?.Invoke();
                PlayDialogueWithCallback(nextEntry.dlgBefore, context, () =>
                {
                    // Callback ejecutado cuando termina el diálogo
                    qm.AddQuest(nextEntry.questData);
                    qm.StartQuest(nextEntry.questData.questId);
                    nextEntry.onOfferDialogueFinished?.Invoke();
                });
            }
            // Si NO tiene dlgBefore, NO iniciar automáticamente
            // (se iniciará desde otro lugar: grafo narrativo, etc.)
        }

        private void PlayDialogue(DialogueAsset dialogue, Common.NPCStateContext context)
        {
            if (dialogue == null)
            {
                Debug.LogWarning("[NPCQuestConfig.PlayDialogue] dialogue es null - no se reproduce diálogo");
                return;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                Debug.LogError("[NPCQuestConfig] DialogueManager.Instance es null");
                return;
            }

            Debug.Log($"[NPCQuestConfig.PlayDialogue] Iniciando diálogo '{dialogue.name}' con NPC en {context.Transform?.position}. Líneas: {dialogue.lines?.Length ?? 0}");
            
            // Callback para detener animación de hablar cuando termine el diálogo
            dm.StartDialogue(dialogue, context.Transform, () => 
            {
                Debug.Log($"[NPCQuestConfig.PlayDialogue] Diálogo '{dialogue.name}' terminado - deteniendo animación");
                StopTalkingAnimation(context);
            });
        }

        private void PlayDialogueWithCallback(DialogueAsset dialogue, Common.NPCStateContext context, System.Action onFinished)
        {
            if (dialogue == null)
            {
                onFinished?.Invoke();
                return;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                Debug.LogError("[NPCQuestConfig] DialogueManager.Instance es null");
                onFinished?.Invoke();
                return;
            }

            // Combinar callback original con detener animación de hablar
            System.Action combinedCallback = () =>
            {
                StopTalkingAnimation(context);
                onFinished?.Invoke();
            };

            // ✅ DialogueManager maneja la rotación del NPC automáticamente
            dm.StartDialogue(dialogue, context.Transform, combinedCallback);
        }
        

        /// <summary>
        /// Activa la animación de hablar en el NPC
        /// </summary>
        private void StartTalkingAnimation(Common.NPCStateContext context)
        {
            if (context?.Animator == null) return;
            
            // Reproducir animación de interacción (saludo/hablar)
            context.Animator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
            
            // Usar el NPCSimpleAnimator para activar la animación de hablar
            context.Animator.SetTalking(true);
        }
        
        /// <summary>
        /// Detiene la animación de hablar en el NPC
        /// </summary>
        private void StopTalkingAnimation(Common.NPCStateContext context)
        {
            if (context?.Animator == null) return;
            
            // Desactivar la animación de hablar
            context.Animator.SetTalking(false);
        }
    }
}