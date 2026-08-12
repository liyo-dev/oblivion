using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace EasyTransition
{

    public class TransitionManager : MonoBehaviour
    {        
        [SerializeField] private GameObject transitionTemplate;

        private bool runningTransition;

        public UnityAction onTransitionBegin;
        public UnityAction onTransitionCutPointReached;
        public UnityAction onTransitionEnd;

        private static TransitionManager instance;
        private static bool duplicateWarningShown;

        // FIX (auditoría 2026-08-11): Instance() logueaba LogError cada vez que 'instance' era
        // null, sin distinguir un uso indebido real (llamar antes de que Awake() haya corrido) de
        // un cierre normal del juego / salida de Play Mode. TransitionManager es DontDestroyOnLoad
        // y se destruye junto con el resto de la escena al cerrar; el orden de destrucción entre
        // objetos no está garantizado, así que varios OnDestroy() de otros sistemas (sequencers de
        // cinemática, TeleportService, ...) que consultan TransitionManager.Instance() para
        // desuscribirse de sus eventos podían ejecutarse DESPUÉS de que este objeto ya se hubiera
        // destruido a sí mismo, generando un LogError en cada cierre de partida/salida de Play Mode
        // aunque no hubiera ningún bug real. applicationIsQuitting distingue ambos casos: Unity
        // envía OnApplicationQuit a todos los objetos activos (también al salir de Play Mode en el
        // Editor) ANTES de empezar a destruirlos, así que el flag ya está activo cuando esos otros
        // OnDestroy() se ejecutan.
        private static bool applicationIsQuitting;

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            duplicateWarningShown = false;
            applicationIsQuitting = false;
        }
#endif

        private void Awake()
        {
            // Robust singleton: si ya existe otra instancia, destruir esta y conservar la original.
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                ServiceLocator.Register(this);
            }
            else if (instance != this)
            {
#if UNITY_EDITOR
                if (!duplicateWarningShown)
                {
                    Debug.Log("[TransitionManager] Duplicate detected while loading additive scene. Destroying new instance.");
                    duplicateWarningShown = true;
                }
#endif
                Destroy(gameObject);
                return;
            }
        }

        private void OnApplicationQuit()
        {
            // Ver comentario de applicationIsQuitting arriba: llega antes que los OnDestroy() de
            // todos los demás objetos, tanto al cerrar el build como al salir de Play Mode.
            applicationIsQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                ServiceLocator.Unregister(this);
                instance = null;
            }
        }

        public static TransitionManager Instance()
        {
            if (instance == null)
            {
                // Durante el cierre del juego / salida de Play Mode es normal y esperable que
                // otros OnDestroy() consulten esto después de que la instancia ya no exista: no es
                // un error, así que no se loguea (evita el spam "You tried to access the instance
                // before it exists." en cada cierre de partida).
                if (!applicationIsQuitting)
                    Debug.LogError("You tried to access the instance before it exists.");
                return null;
            }

            return instance;
        }

        public bool IsRunning => runningTransition;

        public void ForceResetTransition() => runningTransition = false;

        /// <summary>
        /// Starts a transition without loading a new level.
        /// </summary>
        /// <param name="transition">The settings of the transition you want to use.</param>
        /// <param name="startDelay">The delay before the transition starts.</param>
        public void Transition(TransitionSettings transition, float startDelay)
        {
            if (transition == null)
            {
                Debug.LogError("You have to assing a transition.");
                return;
            }
            if (runningTransition)
            {
                Debug.LogWarning("[TransitionManager] Transition already running — ignoring new request.");
                return;
            }

            runningTransition = true;
            StartCoroutine(Timer(startDelay, transition));
        }

        /// <summary>
        /// Loads the new Scene with a transition.
        /// </summary>
        /// <param name="sceneName">The name of the scene you want to load.</param>
        /// <param name="transition">The settings of the transition you want to use to load you new scene.</param>
        /// <param name="startDelay">The delay before the transition starts.</param>
        public void Transition(string sceneName, TransitionSettings transition, float startDelay)
        {
            if (transition == null || runningTransition)
            {
                Debug.LogError("You have to assing a transition.");
                return;
            }

            runningTransition = true;
            StartCoroutine(Timer(sceneName, startDelay, transition));
        }

        /// <summary>
        /// Loads the new Scene with a transition.
        /// </summary>
        /// <param name="sceneIndex">The index of the scene you want to load.</param>
        /// <param name="transition">The settings of the transition you want to use to load you new scene.</param>
        /// <param name="startDelay">The delay before the transition starts.</param>
        public void Transition(int sceneIndex, TransitionSettings transition, float startDelay)
        {
            if (transition == null || runningTransition)
            {
                Debug.LogError("You have to assing a transition.");
                return;
            }

            runningTransition = true;
            StartCoroutine(Timer(sceneIndex, startDelay, transition));
        }

        /// <summary>
        /// Gets the index of a scene from its name.
        /// </summary>
        /// <param name="sceneName">The name of the scene you want to get the index of.</param>
        int GetSceneIndex(string sceneName)
        {
            return SceneManager.GetSceneByName(sceneName).buildIndex;
        }

        // FIX Bajos (auditoría 2026-08-07): las tres corrutinas Timer solo ponían
        // runningTransition = false al final del bloque "feliz". Si cualquier suscriptor de
        // onTransitionBegin/onTransitionCutPointReached/onTransitionEnd lanzaba una excepción sin
        // capturar, Unity aborta la corrutina en ese punto — el código de después (incluido el
        // reset del flag) nunca se ejecuta, y runningTransition se queda en true para siempre:
        // todas las transiciones futuras de todo el juego quedan bloqueadas silenciosamente
        // ("Transition already running — ignoring new request."). Un try/finally (sin catch, así
        // que yield return sigue siendo válido dentro del try) garantiza el reset pase lo que
        // pase, sin ocultar la excepción original (Unity la sigue logueando igual).
        IEnumerator Timer(string sceneName, float startDelay, TransitionSettings transitionSettings)
        {
            try
            {
                yield return new WaitForSecondsRealtime(startDelay);

                onTransitionBegin?.Invoke();

                var template = Instantiate(transitionTemplate);
                template.GetComponent<Transition>().transitionSettings = transitionSettings;

                float transitionTime = transitionSettings.transitionTime;
                if (transitionSettings.autoAdjustTransitionTime)
                    transitionTime = transitionTime / transitionSettings.transitionSpeed;

                yield return new WaitForSecondsRealtime(transitionTime);

                onTransitionCutPointReached?.Invoke();

                SceneManager.LoadScene(sceneName);

                yield return new WaitForSecondsRealtime(transitionSettings.destroyTime);

                onTransitionEnd?.Invoke();
            }
            finally
            {
                // Asegura que el flag se limpia siempre, incluso si algo de arriba lanzó.
                runningTransition = false;
            }
        }

        IEnumerator Timer(int sceneIndex, float startDelay, TransitionSettings transitionSettings)
        {
            try
            {
                yield return new WaitForSecondsRealtime(startDelay);

                onTransitionBegin?.Invoke();

                var template = Instantiate(transitionTemplate);
                template.GetComponent<Transition>().transitionSettings = transitionSettings;

                float transitionTime = transitionSettings.transitionTime;
                if (transitionSettings.autoAdjustTransitionTime)
                    transitionTime = transitionTime / transitionSettings.transitionSpeed;

                yield return new WaitForSecondsRealtime(transitionTime);

                onTransitionCutPointReached?.Invoke();

                SceneManager.LoadScene(sceneIndex);

                yield return new WaitForSecondsRealtime(transitionSettings.destroyTime);

                onTransitionEnd?.Invoke();
            }
            finally
            {
                runningTransition = false;
            }
        }

        IEnumerator Timer(float delay, TransitionSettings transitionSettings)
        {
            try
            {
                yield return new WaitForSecondsRealtime(delay);

                onTransitionBegin?.Invoke();

                var template = Instantiate(transitionTemplate);
                template.GetComponent<Transition>().transitionSettings = transitionSettings;

                float transitionTime = transitionSettings.transitionTime;
                if (transitionSettings.autoAdjustTransitionTime)
                    transitionTime = transitionTime / transitionSettings.transitionSpeed;

                yield return new WaitForSecondsRealtime(transitionTime);

                onTransitionCutPointReached?.Invoke();

                template.GetComponent<Transition>().OnSceneLoad(SceneManager.GetActiveScene(), LoadSceneMode.Single);

                yield return new WaitForSecondsRealtime(transitionSettings.destroyTime);

                onTransitionEnd?.Invoke();
            }
            finally
            {
                runningTransition = false;
            }
        }

        // FIX Bajos: este Start() era un poll infinito cada 1s que nunca podía detectar nada útil
        // — cualquier instancia duplicada ya se destruye en Awake() antes de que su propio Start()
        // llegue a correr, así que la única instancia que sobrevive para ejecutar este bucle
        // siempre tiene instance == this. Se elimina: no aportaba protección real, solo trabajo
        // per-tick indefinido durante toda la partida.
    }

}
