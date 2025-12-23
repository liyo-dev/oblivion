﻿using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Game.Cinematics
{
    /// <summary>
    /// Gestor global de cinemáticas que pausa la escena principal sin desactivar GameObjects.
    /// Esto evita problemas con coroutines y mantiene los componentes funcionando.
    /// </summary>
    public class CinematicManager : MonoBehaviour
    {
        private static CinematicManager _instance;
        public static CinematicManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[CinematicManager]");
                    _instance = go.AddComponent<CinematicManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Layer Settings")]
        [SerializeField] private string cinematicLayerName = "Cinematic";
        [SerializeField] private int cinematicLayer = 31; // Layer para objetos de cinemática

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Estado de cinemática
        private bool _isInCinematic = false;
        private int _cinematicCount = 0; // Para manejar cinemáticas anidadas
        
        // Referencias a componentes que necesitan ser pausados/ocultados
        private List<IPausableDuringCinematic> _pausableComponents = new List<IPausableDuringCinematic>();
        private List<IHideableDuringCinematic> _hideableComponents = new List<IHideableDuringCinematic>();
        
        // Cache de estados de MonoBehaviours pausados
        private Dictionary<MonoBehaviour, bool> _pausedBehaviours = new Dictionary<MonoBehaviour, bool>();
        private Dictionary<Animator, float> _pausedAnimators = new Dictionary<Animator, float>();
        private Dictionary<ParticleSystem, bool> _pausedParticleSystems = new Dictionary<ParticleSystem, bool>();
        
        public bool IsInCinematic => _isInCinematic;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (debugMode)
                Debug.Log("[CinematicManager] Inicializado");
        }

        /// <summary>
        /// Registra un componente que debe pausarse durante cinemáticas
        /// </summary>
        public void RegisterPausable(IPausableDuringCinematic pausable)
        {
            if (!_pausableComponents.Contains(pausable))
            {
                _pausableComponents.Add(pausable);
                
                // Si ya estamos en cinemática, pausar inmediatamente
                if (_isInCinematic)
                {
                    pausable.OnCinematicPause();
                }
            }
        }

        /// <summary>
        /// Desregistra un componente pausable
        /// </summary>
        public void UnregisterPausable(IPausableDuringCinematic pausable)
        {
            _pausableComponents.Remove(pausable);
        }

        /// <summary>
        /// Registra un componente que debe ocultarse durante cinemáticas (ej: HUD)
        /// </summary>
        public void RegisterHideable(IHideableDuringCinematic hideable)
        {
            if (!_hideableComponents.Contains(hideable))
            {
                _hideableComponents.Add(hideable);
                
                // Si ya estamos en cinemática, ocultar inmediatamente
                if (_isInCinematic)
                {
                    hideable.OnCinematicHide();
                }
            }
        }

        /// <summary>
        /// Desregistra un componente que se oculta
        /// </summary>
        public void UnregisterHideable(IHideableDuringCinematic hideable)
        {
            _hideableComponents.Remove(hideable);
        }

        /// <summary>
        /// Inicia una cinemática (pausa la escena principal)
        /// </summary>
        public void BeginCinematic()
        {
            _cinematicCount++;
            
            if (_cinematicCount == 1) // Primera cinemática
            {
                _isInCinematic = true;
                
                if (debugMode)
                    Debug.Log($"[CinematicManager] ▶️ Iniciando cinemática (count={_cinematicCount})");
                
                // Pausar todos los componentes registrados
                foreach (var pausable in _pausableComponents)
                {
                    if (pausable != null)
                    {
                        pausable.OnCinematicPause();
                    }
                }
                
                // Ocultar todos los componentes registrados (HUD, etc)
                foreach (var hideable in _hideableComponents)
                {
                    if (hideable != null)
                    {
                        hideable.OnCinematicHide();
                    }
                }
                
                // Pausar automáticamente componentes comunes en la escena
                PauseSceneComponents();
            }
            else if (debugMode)
            {
                Debug.Log($"[CinematicManager] ▶️ Cinemática anidada (count={_cinematicCount})");
            }
        }

        /// <summary>
        /// Finaliza una cinemática (reactiva la escena principal)
        /// </summary>
        public void EndCinematic()
        {
            _cinematicCount = Mathf.Max(0, _cinematicCount - 1);
            
            if (_cinematicCount == 0) // Última cinemática finalizada
            {
                if (debugMode)
                    Debug.Log($"[CinematicManager] ⏸️ Finalizando cinemática (count={_cinematicCount})");
                
                _isInCinematic = false;
                
                // Reanudar todos los componentes registrados
                foreach (var pausable in _pausableComponents)
                {
                    if (pausable != null)
                    {
                        pausable.OnCinematicResume();
                    }
                }
                
                // Mostrar todos los componentes registrados (HUD, etc)
                foreach (var hideable in _hideableComponents)
                {
                    if (hideable != null)
                    {
                        hideable.OnCinematicShow();
                    }
                }
                
                // Reanudar componentes automáticos
                ResumeSceneComponents();
            }
            else if (debugMode)
            {
                Debug.Log($"[CinematicManager] ⏸️ Cinemática anidada finalizada (count={_cinematicCount})");
            }
        }

        /// <summary>
        /// Pausa automáticamente componentes comunes de la escena
        /// </summary>
        private void PauseSceneComponents()
        {
            _pausedBehaviours.Clear();
            _pausedAnimators.Clear();
            _pausedParticleSystems.Clear();
            
            // Pausar Animators (excepto los de la capa cinemática)
            var animators = ServiceLocator.GetAll<Animator>();
            foreach (var animator in animators)
            {
                if (animator.gameObject.layer == cinematicLayer)
                    continue; // No pausar objetos cinemáticos
                
                if (animator.enabled)
                {
                    _pausedAnimators[animator] = animator.speed;
                    animator.speed = 0f;
                }
            }
            
            // Pausar Particle Systems (VFX de hechizos, etc)
            var particleSystems = ServiceLocator.GetAll<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                if (ps.gameObject.layer == cinematicLayer)
                    continue;
                
                if (ps.isPlaying)
                {
                    _pausedParticleSystems[ps] = true;
                    ps.Pause();
                }
            }
            
            if (debugMode)
                Debug.Log($"[CinematicManager] Pausados: {_pausedAnimators.Count} animators, {_pausedParticleSystems.Count} particle systems");
        }

        /// <summary>
        /// Reanuda los componentes de la escena
        /// </summary>
        private void ResumeSceneComponents()
        {
            // Reanudar Animators
            foreach (var kvp in _pausedAnimators)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.speed = kvp.Value;
                }
            }
            
            // Reanudar Particle Systems
            foreach (var kvp in _pausedParticleSystems)
            {
                if (kvp.Key != null && kvp.Value)
                {
                    kvp.Key.Play();
                }
            }
            
            if (debugMode)
                Debug.Log($"[CinematicManager] Reanudados: {_pausedAnimators.Count} animators, {_pausedParticleSystems.Count} particle systems");
            
            _pausedBehaviours.Clear();
            _pausedAnimators.Clear();
            _pausedParticleSystems.Clear();
        }

        /// <summary>
        /// Fuerza la salida de cualquier cinemática activa (útil para debugging)
        /// </summary>
        public void ForceEndAllCinematics()
        {
            if (debugMode)
                Debug.LogWarning($"[CinematicManager] 🚨 Forzando fin de todas las cinemáticas (count={_cinematicCount})");
            
            _cinematicCount = 0;
            
            if (_isInCinematic)
            {
                EndCinematic();
            }
        }
    }

    /// <summary>
    /// Interfaz para componentes que deben pausarse durante cinemáticas
    /// </summary>
    public interface IPausableDuringCinematic
    {
        void OnCinematicPause();
        void OnCinematicResume();
    }

    /// <summary>
    /// Interfaz para componentes que deben ocultarse durante cinemáticas (ej: HUD)
    /// </summary>
    public interface IHideableDuringCinematic
    {
        void OnCinematicHide();
        void OnCinematicShow();
    }
}

