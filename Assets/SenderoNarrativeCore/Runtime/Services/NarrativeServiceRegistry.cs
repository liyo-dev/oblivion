using System;
using System.Collections.Generic;

namespace SenderoNarrativeCore.Runtime.Services
{
    /// <summary>
    /// Lightweight service registry that allows projects to register their own systems for use by the narrative runtime.
    /// </summary>
    public static class NarrativeServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a service implementation for a given interface type.
        /// </summary>
        /// <typeparam name="T">Interface type.</typeparam>
        /// <param name="instance">Instance implementing the interface.</param>
        public static void Register<T>(T instance) where T : class
        {
            Services[typeof(T)] = instance;
        }

        /// <summary>
        /// Retrieves a registered service instance.
        /// </summary>
        /// <typeparam name="T">Interface type.</typeparam>
        /// <returns>Instance if registered; otherwise null.</returns>
        public static T GetService<T>() where T : class
        {
            Services.TryGetValue(typeof(T), out var service);
            return service as T;
        }

        /// <summary>
        /// Clears all registered services. Useful when entering or exiting play mode in the editor.
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
