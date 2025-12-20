using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado Cinemático - El NPC ejecuta una secuencia programada de acciones
    /// (movimiento, animaciones, diálogos, etc.) controlada externamente.
    /// Este estado es usado por el grafo narrativo para cinemáticas.
    /// </summary>
    public class CinematicState : NPCStateBase
    {
        private CinematicSequence _currentSequence;
        private bool _sequenceCompleted;
        
        public override string StateName => "Cinematic";
        
        /// <summary>
        /// Inicia una secuencia cinemática
        /// </summary>
        public void StartSequence(CinematicSequence sequence)
        {
            _currentSequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _sequenceCompleted = false;
        }
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            
            context.IsInCinematic = true;
            StopMovement(context);
            
            if (_currentSequence == null)
            {
                context.LogWarning($"[{StateName}] No hay secuencia asignada, completando inmediatamente");
                _sequenceCompleted = true;
            }
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (_currentSequence != null && !_sequenceCompleted)
            {
                _currentSequence.Update(context);
                
                if (_currentSequence.IsCompleted)
                {
                    context.Log($"[{StateName}] Secuencia completada");
                    _sequenceCompleted = true;
                }
            }
        }
        
        public override void OnExit(Common.NPCStateContext context)
        {
            base.OnExit(context);
            
            context.IsInCinematic = false;
            
            if (_currentSequence != null)
            {
                _currentSequence.Cleanup(context);
                _currentSequence = null;
            }
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // El estado cinemático solo termina cuando la secuencia se completa
            if (_sequenceCompleted || _currentSequence == null)
            {
                context.Log($"[{StateName}] Secuencia finalizada, volviendo a Idle");
                return new IdleState();
            }
            
            return null; // Continuar en cinemática
        }
        
        /// <summary>
        /// Cancela la secuencia actual prematuramente
        /// </summary>
        public void CancelSequence()
        {
            _sequenceCompleted = true;
        }
    }
    
    /// <summary>
    /// Clase base para secuencias cinemáticas
    /// </summary>
    public abstract class CinematicSequence
    {
        public bool IsCompleted { get; protected set; }
        
        public abstract void Update(Common.NPCStateContext context);
        public virtual void Cleanup(Common.NPCStateContext context) { }
    }
    
    /// <summary>
    /// Secuencia simple de movimiento a un punto
    /// </summary>
    public class MoveToPoscionSequence : CinematicSequence
    {
        private readonly Vector3 _targetPosition;
        private readonly float _maxDuration;
        private readonly bool _turnAroundOnArrival;
        private float _timer;
        private bool _hasSetDestination;
        
        public MoveToPoscionSequence(Vector3 targetPosition, float maxDuration = 15f, bool turnAroundOnArrival = false)
        {
            _targetPosition = targetPosition;
            _maxDuration = maxDuration;
            _turnAroundOnArrival = turnAroundOnArrival;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            // Establecer destino en el primer frame
            if (!_hasSetDestination)
            {
                if (context.Agent == null || !context.Agent.isOnNavMesh)
                {
                    context.LogWarning("[CinematicSequence] Agent no válido o no está en NavMesh, completando");
                    IsCompleted = true;
                    return;
                }
                
                Common.NavMeshAgentUtility.SetDestination(context.Agent, _targetPosition);
                _hasSetDestination = true;
                context.Log($"[CinematicSequence] Destino establecido: {_targetPosition}");
            }
            
            _timer += Time.deltaTime;
            
            // Timeout
            if (_timer >= _maxDuration)
            {
                context.LogWarning($"[CinematicSequence] Timeout alcanzado ({_maxDuration}s), completando");
                IsCompleted = true;
                return;
            }
            
            // Actualizar animación
            if (context.Agent != null && context.Animator != null)
            {
                float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
                context.Animator.SetMovementSpeed(speedFactor);
            }
            
            // Verificar si llegó
            if (HasReachedDestination(context))
            {
                context.Log("[CinematicSequence] Destino alcanzado");
                
                // Girar 180° si está configurado
                if (_turnAroundOnArrival)
                {
                    var newRotation = context.Transform.rotation * Quaternion.Euler(0, 180, 0);
                    context.Transform.rotation = newRotation;
                    context.Log("[CinematicSequence] Girado 180°");
                }
                
                IsCompleted = true;
            }
        }
        
        public override void Cleanup(Common.NPCStateContext context)
        {
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
            }
            
            if (context.Animator != null)
            {
                context.Animator.ResetMovement();
            }
        }
        
        private bool HasReachedDestination(Common.NPCStateContext context)
        {
            var agent = context.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return false;
            
            if (agent.pathPending)
                return false;
            
            float stoppingDist = context.Config != null ? context.Config.stoppingDistance : 0.5f;
            return agent.remainingDistance <= stoppingDist + 0.1f;
        }
    }
    
    /// <summary>
    /// Secuencia compuesta de múltiples acciones
    /// </summary>
    public class CompositeSequence : CinematicSequence
    {
        private readonly List<CinematicAction> _actions;
        private int _currentActionIndex;
        
        public CompositeSequence()
        {
            _actions = new List<CinematicAction>();
        }
        
        public CompositeSequence AddAction(CinematicAction action)
        {
            _actions.Add(action);
            return this;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            if (_actions.Count == 0)
            {
                IsCompleted = true;
                return;
            }
            
            // Ejecutar acción actual
            var currentAction = _actions[_currentActionIndex];
            currentAction.Update(context);
            
            // Si se completó, pasar a la siguiente
            if (currentAction.IsCompleted)
            {
                context.Log($"[CompositeSequence] Acción {_currentActionIndex} completada");
                _currentActionIndex++;
                
                // Si no hay más acciones, completar secuencia
                if (_currentActionIndex >= _actions.Count)
                {
                    context.Log("[CompositeSequence] Todas las acciones completadas");
                    IsCompleted = true;
                }
            }
        }
        
        public override void Cleanup(Common.NPCStateContext context)
        {
            foreach (var action in _actions)
            {
                action.Cleanup(context);
            }
        }
    }
    
    /// <summary>
    /// Acción individual dentro de una secuencia
    /// </summary>
    public abstract class CinematicAction
    {
        public bool IsCompleted { get; protected set; }
        public abstract void Update(Common.NPCStateContext context);
        public virtual void Cleanup(Common.NPCStateContext context) { }
    }
    
    /// <summary>
    /// Acción: Mover a posición
    /// </summary>
    public class MoveToAction : CinematicAction
    {
        private readonly Vector3 _targetPosition;
        private readonly float _maxDuration;
        private bool _hasSetDestination;
        private float _timer;
        
        public MoveToAction(Vector3 targetPosition, float maxDuration = 10f)
        {
            _targetPosition = targetPosition;
            _maxDuration = maxDuration;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            if (!_hasSetDestination)
            {
                Common.NavMeshAgentUtility.SetDestination(context.Agent, _targetPosition);
                _hasSetDestination = true;
            }
            
            _timer += Time.deltaTime;
            
            // Timeout
            if (_timer >= _maxDuration)
            {
                IsCompleted = true;
                return;
            }
            
            // Actualizar animación
            float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
            context.Animator?.SetMovementSpeed(speedFactor);
            
            // Verificar llegada
            var agent = context.Agent;
            if (agent != null && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                IsCompleted = true;
            }
        }
        
        public override void Cleanup(Common.NPCStateContext context)
        {
            Common.NavMeshAgentUtility.HardStop(context.Agent);
            context.Animator?.ResetMovement();
        }
    }
    
    /// <summary>
    /// Acción: Esperar X segundos
    /// </summary>
    public class WaitAction : CinematicAction
    {
        private readonly float _duration;
        private float _timer;
        
        public WaitAction(float duration)
        {
            _duration = duration;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                IsCompleted = true;
            }
        }
    }
    
    /// <summary>
    /// Acción: Reproducir animación
    /// </summary>
    public class PlayAnimationAction : CinematicAction
    {
        private readonly string _animationTrigger;
        private readonly float _duration;
        private float _timer;
        
        public PlayAnimationAction(string animationTrigger, float duration)
        {
            _animationTrigger = animationTrigger;
            _duration = duration;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            if (_timer == 0f && context.UnityAnimator != null)
            {
                context.UnityAnimator.SetTrigger(_animationTrigger);
                context.Log($"[PlayAnimationAction] Trigger '{_animationTrigger}' activado");
            }
            
            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                IsCompleted = true;
            }
        }
    }
    
    /// <summary>
    /// Acción: Girar hacia una dirección
    /// </summary>
    public class RotateToAction : CinematicAction
    {
        private readonly Quaternion _targetRotation;
        private readonly float _duration;
        private Quaternion _startRotation;
        private float _timer;
        
        public RotateToAction(Quaternion targetRotation, float duration = 0.5f)
        {
            _targetRotation = targetRotation;
            _duration = duration;
        }
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            if (_timer == 0f)
            {
                _startRotation = context.Transform.rotation;
            }
            
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);
            
            context.Transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
            
            if (t >= 1f)
            {
                IsCompleted = true;
            }
        }
    }
}

