using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class QuestNpcSession : MonoBehaviour, IInteractionSession
{
    [Header("Datos")]
    [SerializeField] private QuestData quest;
    [SerializeField] private DialogueAsset dlgAvailable, dlgInProgress, dlgReadyToTurnIn, dlgCompleted;

    [Header("Opcional")]
    [SerializeField] private bool lockedAtStart = false;
    [SerializeField] private bool autoAcceptOnAvailable = true;
    [SerializeField] private bool markFirstStepOnAccept = true;
    [SerializeField] private int firstStepIndex = 0;

    [Header("Cadena / Recompensas / Eventos")]
    [SerializeField] private QuestData nextQuestToOffer;           // se activará tras TurnIn
    public UnityEvent onAccepted;         // justo tras aceptar
    public UnityEvent onTurnedIn;         // justo al entregar (antes de CompleteQuest)
    public UnityEvent onCompleted;        // tras CompleteQuest
    [Tooltip("Se dispara cuando inicia el diálogo de oferta (dlgAvailable).")]
    public UnityEvent onOfferDialogueStarted;
    [Tooltip("Se dispara cuando finaliza el diálogo de oferta (dlgAvailable).")]
    public UnityEvent onOfferDialogueFinished;

    private Action _onFinish;

    public void BeginSession(GameObject interactor, Action onFinish)
    {
        _onFinish = onFinish;
        
        Debug.Log($"[QuestNpcSession:{name}] 🔔 BeginSession llamado - quest={quest?.questId}");

        if (DialogueManager.Instance == null || quest == null) { End(); return; }
        if (lockedAtStart) { onOfferDialogueStarted?.Invoke(); StartDlg(dlgAvailable, End); return; }

        var qm = QuestManager.Instance;
        if (qm == null) { End(); return; }

        var state = qm.GetState(quest.questId);
        
        Debug.Log($"[QuestNpcSession:{name}] 📊 Quest state: {state}");

        switch (state)
        {
            case QuestState.Inactive:
                onOfferDialogueStarted?.Invoke();
                StartDlg(dlgAvailable, () =>
                {
                        onOfferDialogueFinished?.Invoke();
                    if (autoAcceptOnAvailable)
                    {
                        qm.StartQuest(quest.questId);
                        if (markFirstStepOnAccept) qm.MarkStepDone(quest.questId, firstStepIndex);
                        onAccepted?.Invoke();
                    }
                    End();
                });
                break;

            case QuestState.Active:
                if (qm.AreAllStepsCompleted(quest.questId))
                {
                    StartDlg(dlgReadyToTurnIn, () =>
                    {
                        onTurnedIn?.Invoke(); // recompensa/cinemática aquí
                        qm.CompleteQuest(quest.questId);
                        onCompleted?.Invoke();

                        if (nextQuestToOffer)        // encadenar siguiente misión
                            qm.StartQuest(nextQuestToOffer.questId);

                        End();
                    });
                }
                else
                {
                    StartDlg(dlgInProgress, End);
                }
                break;

            case QuestState.Completed:
                Debug.Log($"[QuestNpcSession:{name}] ✅ Quest completada, reproduciendo dlgCompleted");
                StartDlg(dlgCompleted, End);
                break;
        }
    }

    void StartDlg(DialogueAsset asset, Action after)
    {
        if (asset == null) { after?.Invoke(); return; }
        // Pasar el transform de este NPC al DialogueManager para la cámara de diálogo
        DialogueManager.Instance.StartDialogue(asset, transform, () => after?.Invoke());
    }

    void End()
    {
        var cb = _onFinish; _onFinish = null;
        cb?.Invoke();
    }
    
    /// <summary>
    /// Inicia la siguiente quest (nextQuestToOffer).
    /// Llámalo desde onPostActionCompleted para encadenar quests después del post-action.
    /// </summary>
    public void StartNextQuest()
    {
        if (nextQuestToOffer == null)
        {
            Debug.LogWarning($"[QuestNpcSession:{name}] StartNextQuest llamado pero nextQuestToOffer es NULL");
            return;
        }
        
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            Debug.Log($"[QuestNpcSession:{name}] 🔗 Iniciando siguiente quest: {nextQuestToOffer.questId}");
            qm.StartQuest(nextQuestToOffer.questId);
        }
    }
}
