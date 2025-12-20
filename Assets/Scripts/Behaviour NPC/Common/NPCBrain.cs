using System;
using UnityEngine;
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
    }
}
