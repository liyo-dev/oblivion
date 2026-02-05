using UnityEngine;

namespace Quests
{
    /// <summary>
    /// Componente auxiliar que verifica si los miembros del party satisfacen
    /// los requisitos de quests activas cuando el jugador interactúa con un NPC.
    /// Esto soluciona problemas de timing cuando el evento OnMemberJoined no se dispara correctamente.
    /// </summary>
    public class QuestPartyMemberChecker : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Si está activado, verifica automáticamente al iniciar el diálogo")]
        [SerializeField] private bool checkOnDialogueStart = true;
        
        [Tooltip("Si está activado, verifica cada vez que se completa un nodo de diálogo")]
        [SerializeField] private bool checkOnDialogueNodeCompleted = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        private void Start()
        {
            // Suscribirse a eventos de diálogo si están disponibles
            if (checkOnDialogueStart || checkOnDialogueNodeCompleted)
            {
                var narrativeExecutor = GetComponent<Game.NPC.Modules.NPCInteractiveNarrativeExecutor>();
                if (narrativeExecutor != null)
                {
                    if (checkOnDialogueStart)
                    {
                        // TODO: Suscribirse al evento de inicio de diálogo cuando esté disponible
                        Log("Suscrito a eventos de diálogo (inicio)");
                    }
                }
                else
                {
                    LogWarning("No se encontró NPCInteractiveNarrativeExecutor en este GameObject");
                }
            }
        }
        
        /// <summary>
        /// Llama a este método desde un nodo de diálogo o evento para forzar la verificación.
        /// Puede ser llamado desde un UnityEvent o un nodo personalizado.
        /// </summary>
        public void CheckPartyMembersForQuests()
        {
            Log("CheckPartyMembersForQuests llamado");
            
            if (QuestManager.Instance == null)
            {
                LogWarning("QuestManager no está disponible");
                return;
            }
            
            QuestManager.Instance.ForceCheckPartyMembersForActiveQuests();
        }
        
        /// <summary>
        /// Se puede llamar desde un trigger cuando el jugador se acerca al NPC.
        /// </summary>
        public void OnPlayerEnter()
        {
            Log("Jugador entró en rango, verificando party members...");
            CheckPartyMembersForQuests();
        }
        
        private void Log(string message)
        {
            if (debugMode)
                Debug.Log($"[QuestPartyMemberChecker:{gameObject.name}] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (debugMode)
                Debug.LogWarning($"[QuestPartyMemberChecker:{gameObject.name}] ⚠️ {message}");
        }
    }
}
