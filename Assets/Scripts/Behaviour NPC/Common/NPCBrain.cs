using System;
using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC.Common
{
    public class NPCBrain
    {
        private INPCState _currentState;
        private INPCState _previousState;
        private readonly NPCStateContext _context;
        public INPCState CurrentState => _currentState;
        public INPCState PreviousState => _previousState;
        public NPCStateContext Context => _context;
        public event Action<INPCState, INPCState> OnStateChanged;
        public NPCBrain(NPCStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public void ChangeState(INPCState newState)
        {
            if (newState == null)
            {
                _context.LogError("Intento de cambiar a un estado null");
                return;
            }
            if (_currentState != null)
            {
                _context.Log($"Saliendo del estado: {_currentState.StateName}");
                try
                {
                    _currentState.OnExit(_context);
                }
                catch (Exception ex)
                {
                    _context.LogError($"Error al salir del estado {_currentState.StateName}: {ex.Message}");
                }
            }
            _previousState = _currentState;
            _currentState = newState;
            _context.Log($"Entrando al estado: {_currentState.StateName}");
            try
            {
                _currentState.OnEnter(_context);
                OnStateChanged?.Invoke(_previousState, _currentState);
            }
            catch (Exception ex)
            {
                _context.LogError($"Error al entrar al estado {_currentState.StateName}: {ex.Message}");
            }
        }
        public void Update()
        {
            if (_currentState == null)
            {
                _context.LogWarning("No hay estado activo en el brain");
                return;
            }
            try
            {
                _currentState.OnUpdate(_context);
                var nextState = _currentState.CheckTransitions(_context);
                if (nextState != null && nextState != _currentState)
                {
                    ChangeState(nextState);
                }
            }
            catch (Exception ex)
            {
                _context.LogError($"Error en Update del estado {_currentState.StateName}: {ex.Message}");
            }
        }
        public void ForceState(INPCState newState)
        {
            ChangeState(newState);
        }
        public bool ReturnToPreviousState()
        {
            if (_previousState == null)
            {
                _context.LogWarning("No hay estado previo al que volver");
                return false;
            }
            ChangeState(_previousState);
            return true;
        }
        
        /// <summary>
        /// Maneja la interacción del jugador con el NPC.
        /// Delega según el tipo de configuración disponible.
        /// </summary>
        public bool HandleInteraction(GameObject interactor)
        {
            if (_context == null || _context.Config == null)
            {
                Debug.LogWarning($"[NPCBrain] No hay contexto o configuración disponible");
                return false;
            }
            
            var config = _context.Config;
            
            // Prioridad 1: Interactive Narrative Config (cadena de acciones)
            if (config.interactiveNarrativeConfig != null)
            {
                var executor = _context.Transform.GetComponent<NPCInteractiveNarrativeExecutor>();
                if (executor != null)
                {
                    _context.IsInteracting = true;
                    return executor.TryExecuteNarrative();
                }
                else
                {
                    Debug.LogError($"[NPCBrain] InteractiveNarrativeConfig presente pero no hay NPCInteractiveNarrativeExecutor en GameObject");
                }
            }
            
            // Prioridad 2: Quest Config (sistema de quests)
            if (config.HasBehaviour(NPCBehaviourType.Quest) && config.questConfig != null)
            {
                _context.IsInteracting = true;
                return config.questConfig.ProcessInteraction(interactor, _context);
            }
            
            Debug.LogWarning($"[NPCBrain] No hay configuración de InteractiveNarrative ni Quest");
            return false;
        }
    }
}
