﻿﻿﻿using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Componente que maneja el ciclo de vida del combate del NPC:
    /// - Suscripción a eventos de Damageable
    /// - Reproducción de diálogos al ser derrotado
    /// - Cambio de estado post-derrota
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class NPCCombatLifecycleHandler : MonoBehaviour
    {
        private NPCBehaviourManagerV2 _npcManager;
        private Damageable _damageable;
        private NPCCombatConfig _combatConfig;
        private bool _hasBeenDefeated;
        private bool _isProcessingDefeat;
        
        /// <summary>
        /// Indica si el NPC ha sido derrotado y NO debe volver a entrar en combate
        /// </summary>
        public bool IsDefeatedAndInactive => _hasBeenDefeated;
        
        private void Awake()
        {
            Initialize();
        }
        
        /// <summary>
        /// Inicializa las referencias. Puede llamarse manualmente si el componente se añade en runtime.
        /// </summary>
        public void Initialize()
        {
            if (_npcManager != null && _damageable != null)
                return; // Ya inicializado
            
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _damageable = GetComponent<Damageable>();
            
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⚙️ Inicializando - NPCManager: {_npcManager != null}, Damageable: {_damageable != null}");
            
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ No se encontró NPCBehaviourManagerV2");
                enabled = false;
                return;
            }
            
            if (_damageable == null)
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ No se encontró Damageable");
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            // Obtener configuración de combate
            if (_npcManager.Configuration != null)
            {
                _combatConfig = _npcManager.Configuration.combatConfig;
            }
            
            // Suscribirse al evento de muerte
            _damageable.OnDied += HandleNPCDeath;
            
            // Configurar Damageable para que no se destruya automáticamente
            _damageable.SetDestroyOnDeath(false);
        }
        
        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.OnDied -= HandleNPCDeath;
            }
        }
        
        /// <summary>
        /// Maneja la interacción post-derrota
        /// </summary>
        public bool HandlePostDefeatInteraction(GameObject interactor)
        {
            if (!_hasBeenDefeated)
                return false;
            
            // Si hay diálogo después de la derrota, reproducirlo
            if (_combatConfig != null && _combatConfig.dialogueAfterDefeat != null)
            {
                var dm = DialogueManager.Instance;
                if (dm != null)
                {
                    dm.StartDialogue(_combatConfig.dialogueAfterDefeat, transform, null);
                    return true;
                }
            }
            
            return false;
        }
        
        private void HandleNPCDeath()
        {
            if (_isProcessingDefeat)
            {
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⏸️ HandleNPCDeath llamado pero ya se está procesando");
                return;
            }
            
            _isProcessingDefeat = true;
            _hasBeenDefeated = true;
            
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⚔️ NPC derrotado - Iniciando proceso de derrota");
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🔍 _npcManager: {(_npcManager != null ? "✅" : "❌ NULL")}, Context: {(_npcManager?.Context != null ? "✅" : "❌ NULL")}");
            
            // ✅ IMPORTANTE: Marcar el NPC como derrotado en el contexto
            if (_npcManager != null && _npcManager.Context != null)
            {
                _npcManager.Context.IsInCombat = false;
                _npcManager.Context.WasDefeatedInCombat = true; // ✅ ESTO EVITA QUE VUELVA A ENTRAR EN COMBATE
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Context.WasDefeatedInCombat = true (IsInCombat: {_npcManager.Context.IsInCombat})");
            }
            else
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ NO SE PUDO ESTABLECER WasDefeatedInCombat - Context es null");
            }
            
            // Reproducir diálogo de derrota si existe
            if (_combatConfig != null && _combatConfig.dialogueOnDefeat != null)
            {
                var dm = DialogueManager.Instance;
                if (dm != null)
                {
                    Debug.Log($"[NPCCombatLifecycleHandler:{name}] 💬 Reproduciendo diálogo de derrota");
                    dm.StartDialogue(_combatConfig.dialogueOnDefeat, transform, OnDefeatDialogueComplete);
                    return;
                }
            }
            
            // Si no hay diálogo, completar inmediatamente
            OnDefeatDialogueComplete();
        }
        
        private void OnDefeatDialogueComplete()
        {
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] Diálogo de derrota completado - Configurando como interactable");
            
            // Cambiar el GameObject a la layer "Interactable"
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Cambiado a layer Interactable");
            }
            else
            {
                Debug.LogWarning($"[NPCCombatLifecycleHandler:{name}] ⚠️ Layer 'Interactable' no encontrada");
            }
            
            // ✅ ASEGURAR QUE EL COLLIDER ESTÉ ACTIVO Y CONFIGURADO COMO TRIGGER
            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
                capsuleCollider.isTrigger = true; // IMPORTANTE: debe ser trigger para InteractionDetector
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ CapsuleCollider activado y configurado como trigger");
            }
            else
            {
                Debug.LogWarning($"[NPCCombatLifecycleHandler:{name}] ⚠️ No se encontró CapsuleCollider");
            }
            
            // ASEGURAR que existe el componente Interactable (añadirlo si no existe)
            var interactable = GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<Interactable>();
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Componente Interactable añadido automáticamente");
            }
            
            // Habilitar el componente - el InteractionDetector detectará automáticamente si el jugador está cerca
            interactable.enabled = true;
            
            // Configurar el diálogo de "after defeat" si existe en combatConfig
            if (_combatConfig != null && _combatConfig.dialogueAfterDefeat != null)
            {
                interactable.SetDialogue(_combatConfig.dialogueAfterDefeat);
                interactable.SetMode(InteractableMode.OpenDialogue);
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Configurado diálogo after defeat: {_combatConfig.dialogueAfterDefeat.name}");
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] 📍 GameObject: layer={LayerMask.LayerToName(gameObject.layer)}, enabled={interactable.enabled}");
            }
            else
            {
                Debug.LogWarning($"[NPCCombatLifecycleHandler:{name}] ⚠️ No hay dialogueAfterDefeat configurado en CombatConfig. El NPC será interactable pero sin diálogo.");
            }
            
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ℹ️ El hint aparecerá automáticamente cuando el jugador se acerque (controlado por InteractionDetector)");
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🔍 Verifica que el jugador tenga InteractionDetector con LayerMask configurada para incluir 'Interactable'");
            
            // Cambiar a estado Idle después de la derrota (con animaciones normales, no de batalla)
            if (_npcManager.Context != null && _npcManager.Context.Brain != null)
            {
                _npcManager.Context.Brain.ChangeState(new States.IdleState());
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] Cambiado a IdleState (animaciones normales)");
            }
            
            _isProcessingDefeat = false;
        }
        
        public bool HasBeenDefeated => _hasBeenDefeated;
    }
}

