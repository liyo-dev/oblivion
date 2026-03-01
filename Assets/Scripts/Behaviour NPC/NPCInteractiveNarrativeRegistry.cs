using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Registro estático y optimizado de NPCInteractiveNarrativeExecutor.
    /// Evita el uso de FindObjectsByType() para mejor rendimiento.
    /// Similar al patrón usado en AnchorRegistry.
    /// </summary>
    public static class NPCInteractiveNarrativeRegistry
    {
        private static readonly Dictionary<string, NPCInteractiveNarrativeExecutor> _byId = new Dictionary<string, NPCInteractiveNarrativeExecutor>();
        private static readonly List<NPCInteractiveNarrativeExecutor> _all = new List<NPCInteractiveNarrativeExecutor>();

        /// <summary>
        /// Registra un executor. Debe ser llamado desde OnEnable del executor.
        /// </summary>
        public static void Register(NPCInteractiveNarrativeExecutor executor)
        {
            if (executor == null)
            {
                Debug.LogWarning("[NPCInteractiveNarrativeRegistry] Intento de registrar executor null");
                return;
            }

            // Agregar a la lista general si no existe
            if (!_all.Contains(executor))
            {
                _all.Add(executor);
                // Debug.Log($"[NPCInteractiveNarrativeRegistry] ✅ Registrado en lista general: {executor.name}");
            }

            // Registrar por ID si tiene uno configurado
            var config = executor.GetConfiguration();
            if (config != null && config.persistState && !string.IsNullOrEmpty(config.persistenceId))
            {
                string id = config.persistenceId;
                
                if (_byId.TryGetValue(id, out var existing))
                {
                    if (existing != null && existing != executor)
                    {
                        if (existing.name == executor.name)
                        {
                            // Mismo NPC, distinta instancia: ocurre al recargar escena mientras el NPC
                            // del party sigue vivo (DontDestroyOnLoad). La nueva instancia de escena
                            // reemplaza la referencia obsoleta — comportamiento esperado.
                            Debug.Log($"[NPCInteractiveNarrativeRegistry] ♻️ Reemplazando referencia obsoleta de '{id}' ({executor.name}) — recarga de escena");
                        }
                        else
                        {
                            // IDs iguales en NPCs DISTINTOS: esto sí es un error de configuración.
                            Debug.LogError($"[NPCInteractiveNarrativeRegistry] ❌ ERROR CRÍTICO: persistenceId DUPLICADO '{id}'\n" +
                                           $"  → NPC existente: '{existing.name}'\n" +
                                           $"  → NPC nuevo: '{executor.name}'\n" +
                                           $"  ⚠️ Esto causa que el guardado de estado de un NPC afecte al otro.\n" +
                                           $"  🔧 SOLUCIÓN: Asigna un persistenceId ÚNICO a cada NPCInteractiveNarrativeConfig.");
                        }
                    }
                    // Si es el mismo executor, solo actualizamos (re-registro)
                }
                
                _byId[id] = executor;
                // Debug.Log($"[NPCInteractiveNarrativeRegistry] ✅ Registrado por ID '{id}': {executor.name}");
            }
            else
            {
                // Debug.Log($"[NPCInteractiveNarrativeRegistry] ℹ️ Registrado sin ID persistente: {executor.name} (config={config != null}, persistState={config?.persistState}, id={config?.persistenceId})");
            }
        }

        /// <summary>
        /// Des-registra un executor. Debe ser llamado desde OnDisable del executor.
        /// </summary>
        public static void Unregister(NPCInteractiveNarrativeExecutor executor)
        {
            if (executor == null) return;

            bool wasInList = _all.Remove(executor);
            
            if (wasInList)
            {
                Debug.Log($"[NPCInteractiveNarrativeRegistry] ✅ Des-registrado de lista general: {executor.name}");
            }

            // Remover del diccionario por ID si existe
            var config = executor.GetConfiguration();
            if (config != null && config.persistState && !string.IsNullOrEmpty(config.persistenceId))
            {
                string id = config.persistenceId;
                if (_byId.TryGetValue(id, out var existing) && existing == executor)
                {
                    _byId.Remove(id);
                    Debug.Log($"[NPCInteractiveNarrativeRegistry] ✅ Des-registrado por ID '{id}': {executor.name}");
                }
            }
        }

        /// <summary>
        /// Obtiene un executor por su persistence ID
        /// </summary>
        public static NPCInteractiveNarrativeExecutor GetById(string persistenceId)
        {
            if (string.IsNullOrEmpty(persistenceId)) return null;
            _byId.TryGetValue(persistenceId, out var executor);
            return executor;
        }

        /// <summary>
        /// Obtiene todos los executors registrados (copia de seguridad)
        /// </summary>
        public static List<NPCInteractiveNarrativeExecutor> GetAll()
        {
            // Limpiar referencias null antes de devolver
            _all.RemoveAll(e => e == null);
            return new List<NPCInteractiveNarrativeExecutor>(_all);
        }

        /// <summary>
        /// Obtiene un executor por nombre del GameObject
        /// </summary>
        public static NPCInteractiveNarrativeExecutor GetByName(string npcName)
        {
            if (string.IsNullOrEmpty(npcName)) return null;
            
            foreach (var executor in _all)
            {
                if (executor != null && executor.name == npcName)
                {
                    return executor;
                }
            }
            return null;
        }

        /// <summary>
        /// Limpia el registro completamente.
        /// Se llama al cargar partida o iniciar nueva partida para que los NPCs
        /// se re-registren con el estado correcto del preset cargado.
        /// </summary>
        /// <param name="fullClear">
        /// Si true, limpia también _all y _byId (usar cuando la escena va a recargarse y los
        /// executors llamarán OnEnable de nuevo). Si false, solo resetea estados (reset en mitad
        /// de sesión con los NPCs aún en escena — OnEnable ya pasó y no se volverá a llamar).
        /// </param>
        public static void Clear(bool fullClear = false)
        {
            // Resetear el estado de ejecución de todos los executors
            int resetCount = 0;
            foreach (var executor in _all)
            {
                if (executor != null)
                {
                    executor.ResetState();
                    resetCount++;
                }
            }

            _all.RemoveAll(e => e == null);

            if (fullClear)
            {
                // Limpieza completa previa a recarga de escena: los executors volverán a
                // registrarse solos vía OnEnable cuando la nueva escena cargue.
                _all.Clear();
                _byId.Clear();
                Debug.Log($"[NPCInteractiveNarrativeRegistry] 🗑️ Registro limpiado completamente (pre-carga de escena), {resetCount} estado(s) reseteados");
            }
            else
            {
                Debug.Log($"[NPCInteractiveNarrativeRegistry] 🔄 Estados reseteados en {resetCount} executor(es), registro mantiene {_all.Count} entradas");
            }
        }
        
        /// <summary>
        /// Fuerza a todos los executors registrados a re-restaurar su estado desde el preset.
        /// Útil después de cargar una partida cuando los NPCs ya están en escena.
        /// </summary>
        public static void ForceRestoreAllStates()
        {
            int restored = 0;
            foreach (var executor in _all)
            {
                if (executor != null)
                {
                    executor.ResetState();
                    // El executor restaurará su estado en el siguiente frame cuando detecte que debe hacerlo
                    restored++;
                }
            }
            Debug.Log($"[NPCInteractiveNarrativeRegistry] 🔄 Forzada restauración de estado en {restored} executor(es)");
        }

        /// <summary>
        /// Obtiene estadísticas del registro para debug
        /// </summary>
        public static string GetDebugInfo()
        {
            _all.RemoveAll(e => e == null);
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[NPCInteractiveNarrativeRegistry] 📊 Estado del Registro:");
            sb.AppendLine($"  • Total executors: {_all.Count}");
            sb.AppendLine($"  • Registrados con ID: {_byId.Count}");
            sb.AppendLine();
            
            sb.AppendLine("Executors en lista general:");
            for (int i = 0; i < _all.Count; i++)
            {
                var executor = _all[i];
                if (executor != null)
                {
                    var config = executor.GetConfiguration();
                    string id = config?.persistenceId ?? "SIN ID";
                    bool hasConfig = config != null;
                    bool persistState = config?.persistState ?? false;
                    
                    sb.AppendLine($"  {i + 1}. {executor.name}");
                    sb.AppendLine($"     - ID: {id}");
                    sb.AppendLine($"     - Config: {(hasConfig ? "✅" : "❌")}");
                    sb.AppendLine($"     - PersistState: {persistState}");
                    sb.AppendLine($"     - GameObject: {(executor.gameObject.activeInHierarchy ? "Activo" : "Inactivo")}");
                }
                else
                {
                    sb.AppendLine($"  {i + 1}. NULL");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("Executors registrados por ID:");
            foreach (var kvp in _byId)
            {
                if (kvp.Value != null)
                {
                    sb.AppendLine($"  • '{kvp.Key}' → {kvp.Value.name}");
                }
                else
                {
                    sb.AppendLine($"  • '{kvp.Key}' → NULL");
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Diccionario de lectura para acceso directo (avanzado)
        /// </summary>
        public static IReadOnlyDictionary<string, NPCInteractiveNarrativeExecutor> AllById => _byId;
    }
}

