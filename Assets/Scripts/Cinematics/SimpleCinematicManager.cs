using UnityEngine;
using Unity.Cinemachine;

namespace Game.Cinematics
{
    /// <summary>
    /// Singleton opcional para ayudar a gestionar el estado global de las cinemáticas
    /// (como resetear cámaras al terminar).
    /// </summary>
    public class SimpleCinematicManager : MonoBehaviour
    {
        public static SimpleCinematicManager Instance { get; private set; }
        
        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
        #endif
        
        private CinemachineCamera _gameplayCamera;
        private int _defaultPriority = 10;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            
            // Buscar cámara principal de gameplay para restaurarla luego
            // Asumimos que es la que tiene mayor prioridad al inicio o la etiquetada
            // Puedes ajustarlo según tu proyecto
            _gameplayCamera = FindFirstObjectByType<CinemachineCamera>(); 
            if (_gameplayCamera) _defaultPriority = _gameplayCamera.Priority.Value;
        }

        public void RestoreGameplayCamera()
        {
            // Bajar prioridad a todas las cámaras de cinemática
            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach(var c in allCams)
            {
                if (c != _gameplayCamera)
                    c.Priority.Value = 0;
            }
            
            if (_gameplayCamera)
                _gameplayCamera.Priority.Value = _defaultPriority;
        }
    }
}
