using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>
    /// Gestiona el icono persistente de quest sobre la cabeza del NPC.
    /// Se anade automaticamente cuando el NPC tiene un NPCQuestConfig configurado.
    /// Oculta el icono durante dialogos o mientras el NPC ejecuta acciones.
    /// </summary>
    public class NPCQuestIconManager : MonoBehaviour
    {
        private NPCBehaviourManagerV2 _npcManager;
        private NPCQuestConfig _questConfig;
        private NPCAlertIconController _iconController;
        private NPCInteractiveNarrativeExecutor _narrativeExecutor;
        
        private NPCQuestConfig.QuestIconState _lastIconState = NPCQuestConfig.QuestIconState.None;
        private GameObject _currentIconPrefab;
        private bool _isInDialogue;
        private bool _iconForcedHidden;
        
        private void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _narrativeExecutor = GetComponent<NPCInteractiveNarrativeExecutor>();
            
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCQuestIconManager:{name}] Falta NPCBehaviourManagerV2");
                enabled = false;
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
            
            // Suscribirse a eventos del QuestManager
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted += OnQuestChanged;
                QuestManager.Instance.OnQuestCompleted += OnQuestChanged;
                QuestManager.Instance.OnQuestsChanged += OnAnyQuestChanged;
            }
            
            // Suscribirse a eventos de diálogo
            DialogueManager.OnDialogueStarted += OnDialogueStarted;
            DialogueManager.OnDialogueClosed += OnDialogueClosed;
            
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
            
            DialogueManager.OnDialogueStarted -= OnDialogueStarted;
            DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        }
        
        private void Update()
        {
            // Verificar si el NPC está ejecutando una narrativa
            bool isExecuting = _narrativeExecutor != null && _narrativeExecutor.IsExecuting;
            
            // Verificar si el NPC está en combate
            bool isInCombat = _npcManager != null && _npcManager.Context != null && _npcManager.Context.IsInCombat;
            
            if (isExecuting || _isInDialogue || isInCombat)
            {
                // Forzar ocultar icono mientras ejecuta, está en diálogo o combate
                if (!_iconForcedHidden)
                {
                    _iconForcedHidden = true;
                    HideIcon();
                }
            }
            else if (_iconForcedHidden)
            {
                // Restaurar icono cuando termine.
                // Resetear caché para forzar re-evaluación completa:
                // si el estado cambió mientras estaba force-hidden (ej: nueva quest iniciada
                // por grafo narrativo durante diálogo/cinemática), _lastIconState ya tiene
                // el nuevo valor y UpdateIconState() no detectaría cambio → icono no aparece.
                _iconForcedHidden = false;
                _lastIconState = NPCQuestConfig.QuestIconState.None;
                _currentIconPrefab = null;
                UpdateIconState();
            }
        }
        
        private void OnDialogueStarted(Transform npcInvolved)
        {
            // Solo ocultar si el diálogo es con este NPC
            if (npcInvolved == transform)
            {
                _isInDialogue = true;
            }
        }
        
        private void OnDialogueClosed(Transform npcInvolved)
        {
            // Solo restaurar si el diálogo era con este NPC
            if (npcInvolved == transform)
            {
                _isInDialogue = false;
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

            // Actualizar marcador de minimapa con el sprite del prefab
            var sr = prefab.GetComponentInChildren<SpriteRenderer>();
            Sprite markerSprite = sr != null ? sr.sprite : null;

            var marker = GetComponent<MinimapMarker>() ?? gameObject.AddComponent<MinimapMarker>();
            marker.SetIcon(markerSprite, Color.white);
            marker.SetVisible(true);
        }

        private void HideIcon()
        {
            if (_iconController == null) return;

            if (_iconController.HasPersistentIcon)
            {
                _iconController.HideAlertIcon();
                Debug.Log($"[NPCQuestIconManager:{name}] Ocultando icono de quest");
            }

            GetComponent<MinimapMarker>()?.SetVisible(false);
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

