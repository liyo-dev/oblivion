using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>
    /// Gestiona el icono persistente de quest sobre la cabeza del NPC.
    /// Se anade automaticamente cuando el NPC tiene un NPCQuestConfig configurado.
    /// </summary>
    public class NPCQuestIconManager : MonoBehaviour
    {
        private NPCBehaviourManagerV2 _npcManager;
        private NPCQuestConfig _questConfig;
        private NPCAlertIconController _iconController;
        
        private NPCQuestConfig.QuestIconState _lastIconState = NPCQuestConfig.QuestIconState.None;
        private GameObject _currentIconPrefab;
        
        private void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCQuestIconManager:{name}] Falta NPCBehaviourManagerV2");
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            if (_npcManager.Configuration == null)
            {
                enabled = false;
                return;
            }
            
            _questConfig = _npcManager.Configuration.questConfig;
            if (_questConfig == null)
            {
                enabled = false;
                return;
            }
            
            // Obtener o crear el controlador de iconos
            _iconController = GetComponent<NPCAlertIconController>();
            if (_iconController == null)
            {
                _iconController = gameObject.AddComponent<NPCAlertIconController>();
            }
            
            // Suscribirse a eventos del QuestManager para actualizar el icono
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted += OnQuestChanged;
                QuestManager.Instance.OnQuestCompleted += OnQuestChanged;
                QuestManager.Instance.OnQuestsChanged += OnAnyQuestChanged;
            }
            
            // Verificar estado inicial
            UpdateIconState();
        }
        
        private void OnDestroy()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted -= OnQuestChanged;
                QuestManager.Instance.OnQuestCompleted -= OnQuestChanged;
                QuestManager.Instance.OnQuestsChanged -= OnAnyQuestChanged;
            }
        }
        
        private void OnQuestChanged(string questId)
        {
            if (IsQuestFromThisNPC(questId))
            {
                UpdateIconState();
            }
        }
        
        private void OnAnyQuestChanged()
        {
            // Cuando cualquier quest cambia, verificar si afecta a este NPC
            UpdateIconState();
        }
        
        private bool IsQuestFromThisNPC(string questId)
        {
            if (_questConfig?.questChain == null) return false;
            
            foreach (var entry in _questConfig.questChain)
            {
                if (entry?.questData != null && entry.questData.questId == questId)
                    return true;
            }
            return false;
        }
        
        public void UpdateIconState()
        {
            if (_questConfig == null || _iconController == null) return;
            
            var newState = _questConfig.GetCurrentIconState();
            var newPrefab = _questConfig.GetCurrentIconPrefab();
            
            if (newState != _lastIconState || newPrefab != _currentIconPrefab)
            {
                _lastIconState = newState;
                _currentIconPrefab = newPrefab;
                
                if (newState == NPCQuestConfig.QuestIconState.None || newPrefab == null)
                {
                    HideIcon();
                }
                else
                {
                    ShowIcon(newPrefab);
                }
                
                Debug.Log($"[NPCQuestIconManager:{name}] Estado del icono: {newState}");
            }
        }
        
        private void ShowIcon(GameObject prefab)
        {
            if (_iconController == null || prefab == null) return;
            
            _iconController.SetIconOffset(_questConfig.questIconOffset);
            _iconController.ShowPersistentIcon(prefab);
            
            Debug.Log($"[NPCQuestIconManager:{name}] Mostrando icono de quest");
        }
        
        private void HideIcon()
        {
            if (_iconController == null) return;
            
            if (_iconController.HasPersistentIcon)
            {
                _iconController.HideAlertIcon();
                Debug.Log($"[NPCQuestIconManager:{name}] Ocultando icono de quest");
            }
        }
        
        [ContextMenu("Force Update Icon")]
        public void ForceUpdateIcon()
        {
            _lastIconState = NPCQuestConfig.QuestIconState.None;
            _currentIconPrefab = null;
            UpdateIconState();
        }
    }
}

