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
        
        [Header("Icono de quest")]
        [Tooltip("Escala del icono sobre la cabeza del NPC. Ajusta para igualar el tamaño con otros NPCs.")]
        [SerializeField] private float questIconScale = 1f;

        [Header("Distancia de visibilidad")]
        [Tooltip("Distancia máxima (unidades de mundo) a la que se muestra el icono sobre la cabeza del NPC. 0 = sin límite.")]
        [SerializeField] private float questIconMaxDistance = 30f;

        private NPCQuestConfig.QuestIconState _lastIconState = NPCQuestConfig.QuestIconState.None;
        private GameObject _currentIconPrefab;
        private bool _isInDialogue;
        private bool _iconForcedHidden;
        private float _iconCheckTime;
        
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[NPCQuestIconManager:{name}] Configuration es null — desactivando");
#endif
                enabled = false;
                return;
            }

            _questConfig = _npcManager.Configuration.questConfig;
            if (_questConfig == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[NPCQuestIconManager:{name}] questConfig es null — desactivando");
#endif
                enabled = false;
                return;
            }

            // Crear el controlador de iconos en un hijo dedicado para que no comparta
            // el NPCAlertIconController del raíz con NPCInteractiveNarrativeExecutor.
            // Si ambos usaran el mismo controlador, ShowAlertIcon() del ejecutor narrativo
            // llamaría HideAlertIconImmediate() y destruiría el icono de quest.
            const string childName = "_QuestIconController";
            var iconChild = transform.Find(childName);
            if (iconChild == null)
            {
                iconChild = new GameObject(childName).transform;
                iconChild.SetParent(transform, false);
            }
            _iconController = iconChild.GetComponent<NPCAlertIconController>();
            if (_iconController == null)
            {
                _iconController = iconChild.gameObject.AddComponent<NPCAlertIconController>();
            }

            bool qmAvailable = QuestManager.Instance != null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCQuestIconManager:{name}] Start — questConfig={_questConfig.name}, QM={qmAvailable}");
#endif

            // Suscribirse a eventos del QuestManager
            if (qmAvailable)
            {
                QuestManager.Instance.OnQuestStarted += OnQuestChanged;
                QuestManager.Instance.OnQuestCompleted += OnQuestChanged;
                QuestManager.Instance.OnQuestsChanged += OnAnyQuestChanged;
            }
            else
            {
                // QM no disponible aún: suscribirse a OnProfileReady para actualizar cuando esté listo
                GameBootService.OnProfileReady += OnProfileReady;
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

            GameBootService.OnProfileReady -= OnProfileReady;
            DialogueManager.OnDialogueStarted -= OnDialogueStarted;
            DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        }

        private void OnProfileReady()
        {
            // QM no estaba disponible en Start() — suscribirse ahora y actualizar icono
            GameBootService.OnProfileReady -= OnProfileReady;
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestStarted += OnQuestChanged;
                QuestManager.Instance.OnQuestCompleted += OnQuestChanged;
                QuestManager.Instance.OnQuestsChanged += OnAnyQuestChanged;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCQuestIconManager:{name}] OnProfileReady — re-evaluando icono (QM={QuestManager.Instance != null})");
#endif
            UpdateIconState();
        }
        
        private bool IsPlayerTooFar()
        {
            if (questIconMaxDistance <= 0f) return false;
            var player = _npcManager?.Player;
            if (player == null) return false;
            float sqrDist = (player.position - transform.position).sqrMagnitude;
            return sqrDist > questIconMaxDistance * questIconMaxDistance;
        }

        private void Update()
        {
            // Verificar si el NPC está ejecutando una narrativa
            bool isExecuting = _narrativeExecutor != null && _narrativeExecutor.IsExecuting;

            // Verificar si el NPC está en combate
            bool isInCombat = _npcManager != null && _npcManager.Context != null && _npcManager.Context.IsInCombat;

            // Verificar si el jugador está demasiado lejos
            bool isTooFar = IsPlayerTooFar();

            if (isExecuting || _isInDialogue || isInCombat || isTooFar)
            {
                // Forzar ocultar icono mientras ejecuta, está en diálogo, combate o fuera de rango
                if (!_iconForcedHidden)
                {
                    _iconForcedHidden = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[NPCQuestIconManager:{name}] Forzando ocultar — executing={isExecuting}, diálogo={_isInDialogue}, combate={isInCombat}, lejano={isTooFar}");
#endif
                    HideIcon();
                }
            }
            else if (_iconForcedHidden)
            {
                // Restaurar icono cuando termine.
                // Resetear caché para forzar re-evaluación completa.
                _iconForcedHidden = false;
                _lastIconState = NPCQuestConfig.QuestIconState.None;
                _currentIconPrefab = null;
                UpdateIconState();
            }
            else if (Time.time > _iconCheckTime)
            {
                // Verificación periódica: detectar si el icono fue destruido por un sistema
                // externo (ej: NPCInteractiveNarrativeExecutor llamando ShowAlertIcon)
                // sin que el estado de quest haya cambiado, lo que deja la caché "estancada".
                _iconCheckTime = Time.time + 2f;
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

            // Cuando el icono está forzado oculto, el bloque else-if de Update() resetea
            // la caché y re-evalúa al despejar la condición.
            if (_iconForcedHidden) return;

            var newState = _questConfig.GetCurrentIconState();
            var newPrefab = _questConfig.GetCurrentIconPrefab();

            bool shouldShow = newState != NPCQuestConfig.QuestIconState.None && newPrefab != null;
            // Detectar icono destruido por un sistema externo sin cambio de estado de quest
            bool iconMissing = shouldShow && !_iconController.HasPersistentIcon;
            bool stateChanged = newState != _lastIconState || newPrefab != _currentIconPrefab;

            _lastIconState = newState;
            _currentIconPrefab = newPrefab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCQuestIconManager:{name}] UpdateIconState — estado={newState}, prefab={newPrefab?.name ?? "NULL"}, stateChanged={stateChanged}, iconMissing={iconMissing}");
#endif

            if (stateChanged || iconMissing)
            {
                if (shouldShow)
                    ShowIcon(newPrefab);
                else
                    HideIcon();
            }
        }
        
        private void ShowIcon(GameObject prefab)
        {
            if (_iconController == null || prefab == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCQuestIconManager:{name}] ShowIcon — prefab={prefab.name}");
#endif

            _iconController.SetIconOffset(_questConfig.questIconOffset);
            _iconController.SetIconScale(questIconScale);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[NPCQuestIconManager:{name}] Ocultando icono de quest");
#endif
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

