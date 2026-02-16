using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using Game.NPC;

/// <summary>
/// Controlador del minijuego "Pilla Pilla" (Tag).
/// El jugador debe huir del perseguidor durante X segundos.
/// Si es atrapado, se reinicia. Si sobrevive, gana.
/// El jugador puede ir a donde quiera (incluso teletransportarse), 
/// pero el perseguidor le seguirá a todas partes.
/// </summary>
public class TagMinigameController : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private string minigameId = "TAG_MINIGAME_01";
    [SerializeField] private float duration = 30f;
    [SerializeField] private float countdownBeforeStart = 3f;

    [Header("Referencias")]
    [Tooltip("Si está vacío, se buscará usando chaserNarrativeId")]
    [SerializeField] private ChaserAI chaser;
    [Tooltip("Si está vacío, usa la posición actual del jugador al iniciar")]
    [SerializeField] private Transform playerSpawnPoint;
    [Tooltip("Si está vacío, usa la posición actual del perseguidor al iniciar")]
    [SerializeField] private Transform chaserSpawnPoint;
    
    [Header("Gestión del Party")]
    [Tooltip("ID narrativo del NPC perseguidor (ej: 'NPC_InteractiveNarrative_Config_Estela_b17a2d68'). Se usa si chaserNPC está vacío.")]
    [SerializeField] private string chaserNarrativeId = "";
    [Tooltip("NPCBehaviourManagerV2 del perseguidor (alternativa a chaserNarrativeId)")]
    [SerializeField] private NPCBehaviourManagerV2 chaserNPC;
    [Tooltip("Si true, el perseguidor vuelve al party al ganar el minijuego")]
    [SerializeField] private bool rejoinPartyOnWin = true;
    
    [Header("NPCs que se unen al Party al Ganar")]
    [Tooltip("NPCs adicionales que se unirán al party cuando el jugador gane el minijuego")]
    [SerializeField] private NPCBehaviourManagerV2[] npcsToJoinOnWin;
    
    [Header("Dificultad")]
    [Tooltip("Velocidad del perseguidor durante el minijuego")]
    [SerializeField] private float chaserSpeed = 6f;
    [Tooltip("Velocidad de aceleración del perseguidor")]
    [SerializeField] private float chaserAcceleration = 12f;
    [Tooltip("Distancia a la que el perseguidor atrapa al jugador")]
    [SerializeField] private float catchDistance = 1.5f;
    
    [Header("Seguimiento de Teletransporte")]
    [Tooltip("Distancia máxima antes de teletransportar al perseguidor cerca del jugador")]
    [SerializeField] private float maxDistanceBeforeTeleport = 30f;
    [Tooltip("Distancia a la que aparece el perseguidor tras teletransportarse")]
    [SerializeField] private float teleportSpawnDistance = 8f;
    [Tooltip("Delay antes de teletransportar (da tiempo al jugador a reaccionar)")]
    [SerializeField] private float teleportDelay = 1.5f;
    
    [Header("Efectos de Cuenta Atrás")]
    [Tooltip("Nombre de animación de enfado/batalla (ej: Idle_Battle_NoWeapon). Se reproducirá en UpperBody layer.")]
    [SerializeField] private string angryAnimationName = "Idle_Battle_NoWeapon";
    [Tooltip("Prefab de VFX de enfado que se instanciará como hijo del perseguidor")]
    [SerializeField] private GameObject angerVFXPrefab;
    [Tooltip("Offset de posición del VFX respecto al perseguidor")]
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Color del tinte de enfado para el material del perseguidor")]
    [SerializeField] private Color angerTintColor = new Color(1f, 0.4f, 0.4f, 1f);
    
    [Header("Expresión Facial (EmotionController)")]
    [Tooltip("Emoción que mostrará el perseguidor durante el minijuego")]
    [SerializeField] private NPCEmotion minigameEmotion = NPCEmotion.Angry;

    [Header("UI")]
    [SerializeField] private GameObject uiContainer;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Mensajes")]
    [SerializeField] private string startMessage = "¡HUYE!";
    [SerializeField] private string caughtMessage = "¡Te atraparon!";
    [SerializeField] private string winMessage = "¡Escapaste!";

    [Header("Eventos")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameWon;
    public UnityEvent OnMinigameLost;
    public UnityEvent OnPlayerCaught;

    // Estado interno
    private float remainingTime;
    private bool isRunning = false;
    private bool isCountingDown = false;
    private Transform player;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private Vector3 chaserStartPosition;
    private Quaternion chaserStartRotation;
    private int catchCount = 0;
    
    // Estado del party
    private bool chaserWasInParty = false;
    
    // Seguimiento de teletransporte
    private bool isTeleporting = false;
    
    // Persistencia entre escenas
    private Transform _chaserOriginalParent;
    private bool _chaserWasMadePersistent;
    private Transform _controllerOriginalParent;
    private bool _controllerWasMadePersistent;
    
    // Efectos visuales de enfado
    private Animator _chaserAnimator;
    private NPCSimpleAnimator _chaserNpcAnimator;
    private Renderer[] _chaserRenderers;
    private Color[] _originalColors;
    private bool _angerEffectActive = false;
    private GameObject _vfxInstance; // Instancia del VFX de enfado
    private float _originalUpperBodyWeight = 0f; // Peso original de la capa UpperBody
    private const int UPPER_BODY_LAYER = 1; // Índice de la capa UpperBody
    
    // ✅ FIX: Control del sistema de comportamiento del NPC durante el minijuego
    private bool _npcBehaviourWasEnabled = false;
    
    // Control de expresión facial (EmotionController)
    private NPCEmotionController _chaserEmotionController;
    private NPCEmotion _originalEmotion = NPCEmotion.Neutral;
    
    // Valores originales del ChaserAI para restaurar al terminar
    private float _originalChaserSpeed;
    private float _originalCatchDistance;

    // Para integración con sistema narrativo
    public string MinigameId => minigameId;
    
    /// <summary>
    /// Resuelve las referencias del NPC perseguidor si no están asignadas directamente.
    /// Busca por chaserNarrativeId o desde el NPCRegistry.
    /// </summary>
    private void ResolveNPCReferences()
    {
        // Si ya tenemos el chaserNPC, obtener ChaserAI de él si no lo tenemos
        if (chaserNPC != null && chaser == null)
        {
            // ✅ FIX: Asegurar que el NavMeshAgent existe antes de añadir ChaserAI
            var navAgent = chaserNPC.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = chaserNPC.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
                Debug.Log($"[TagMinigame] ✅ NavMeshAgent añadido a {chaserNPC.name}");
            }
            
            chaser = chaserNPC.GetComponent<ChaserAI>();
            if (chaser == null)
            {
                // Intentar añadir ChaserAI dinámicamente si no existe
                chaser = chaserNPC.gameObject.AddComponent<ChaserAI>();
                Debug.Log($"[TagMinigame] ✅ ChaserAI añadido a {chaserNPC.name}");
            }
            
            // ✅ FIX: Asegurar que el NPC esté activo
            if (!chaserNPC.gameObject.activeInHierarchy)
            {
                chaserNPC.gameObject.SetActive(true);
                Debug.Log($"[TagMinigame] ✅ Perseguidor {chaserNPC.name} activado");
            }
            
            // Configurar componentes visuales
            _chaserAnimator = chaserNPC.GetComponent<Animator>();
            _chaserNpcAnimator = chaserNPC.GetComponent<NPCSimpleAnimator>();
            _chaserRenderers = chaserNPC.GetComponentsInChildren<Renderer>();
            CacheOriginalColors();
            
            Debug.Log($"[TagMinigame] 🎬 Animator: {(_chaserAnimator != null ? "✅" : "❌")}, NPCSimpleAnimator: {(_chaserNpcAnimator != null ? "✅" : "❌")}");
            Debug.Log($"[TagMinigame] 🤖 NavMeshAgent: {(navAgent != null ? "✅" : "❌")}, isOnNavMesh: {(navAgent != null && navAgent.isOnNavMesh ? "✅" : "❌")}");
            
            // Suscribir al evento
            if (chaser != null)
            {
                chaser.OnCaughtPlayer -= OnCaught; // Por si acaso
                chaser.OnCaughtPlayer += OnCaught;
            }
            
            return;
        }
        
        // Si tenemos un ID narrativo, buscar el NPC en el registro
        if (!string.IsNullOrEmpty(chaserNarrativeId) && chaserNPC == null)
        {
            if (NPCRegistry.HasInstance)
            {
                chaserNPC = NPCRegistry.Instance.GetNPCByID(chaserNarrativeId);
                
                if (chaserNPC != null)
                {
                    Debug.Log($"[TagMinigame] ✅ NPC encontrado por ID: {chaserNarrativeId} -> {chaserNPC.name}");
                    
                    // ✅ FIX: Asegurar que el NavMeshAgent existe
                    var navAgent = chaserNPC.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navAgent == null)
                    {
                        navAgent = chaserNPC.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
                        Debug.Log($"[TagMinigame] ✅ NavMeshAgent añadido a {chaserNPC.name}");
                    }
                    
                    // Obtener o crear ChaserAI
                    chaser = chaserNPC.GetComponent<ChaserAI>();
                    if (chaser == null)
                    {
                        chaser = chaserNPC.gameObject.AddComponent<ChaserAI>();
                        Debug.Log($"[TagMinigame] ✅ ChaserAI añadido a {chaserNPC.name}");
                    }
                    
                    // ✅ FIX: Asegurar que el NPC esté activo
                    if (!chaserNPC.gameObject.activeInHierarchy)
                    {
                        chaserNPC.gameObject.SetActive(true);
                        Debug.Log($"[TagMinigame] ✅ Perseguidor {chaserNPC.name} activado");
                    }
                    
                    // Configurar componentes visuales
                    _chaserAnimator = chaserNPC.GetComponent<Animator>();
                    _chaserNpcAnimator = chaserNPC.GetComponent<NPCSimpleAnimator>();
                    _chaserRenderers = chaserNPC.GetComponentsInChildren<Renderer>();
                    CacheOriginalColors();
                    
                    Debug.Log($"[TagMinigame] 🎬 Animator: {(_chaserAnimator != null ? "✅" : "❌")}, NPCSimpleAnimator: {(_chaserNpcAnimator != null ? "✅" : "❌")}");
                    Debug.Log($"[TagMinigame] 🤖 NavMeshAgent: {(navAgent != null ? "✅" : "❌")}, isOnNavMesh: {(navAgent != null && navAgent.isOnNavMesh ? "✅" : "❌")}");
                    
                    // Suscribir al evento
                    chaser.OnCaughtPlayer -= OnCaught;
                    chaser.OnCaughtPlayer += OnCaught;
                }
                else
                {
                    Debug.LogError($"[TagMinigame] ❌ No se encontró NPC con ID: {chaserNarrativeId}");
                }
            }
            else
            {
                Debug.LogWarning("[TagMinigame] ⚠️ NPCRegistry no disponible");
            }
        }
        
        if (chaser == null)
        {
            Debug.LogError("[TagMinigame] ❌ No se pudo resolver el perseguidor. Asigna chaserNarrativeId o chaserNPC.");
        }
    }
    
    /// <summary>
    /// Hace al perseguidor persistente entre escenas para que no se destruya al cambiar de zona
    /// </summary>
    private void MakeChaserPersistent()
    {
        if (chaser == null) return;
        
        // Guardar el padre original para poder restaurarlo después
        _chaserOriginalParent = chaser.transform.parent;
        
        // Mover a la raíz y marcar como DontDestroyOnLoad
        chaser.transform.SetParent(null);
        DontDestroyOnLoad(chaser.gameObject);
        
        _chaserWasMadePersistent = true;
        Debug.Log($"[TagMinigame] 🔒 Perseguidor '{chaser.name}' marcado como persistente entre escenas");
    }
    
    /// <summary>
    /// Hace al TagMinigameController persistente para que sobreviva cambios de escena
    /// </summary>
    private void MakeControllerPersistent()
    {
        if (_controllerWasMadePersistent) return;
        
        _controllerOriginalParent = transform.parent;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        
        _controllerWasMadePersistent = true;
        Debug.Log($"[TagMinigame] 🔒 Controller '{name}' marcado como persistente entre escenas");
    }
    
    /// <summary>
    /// Restaura al perseguidor a su estado normal (no persistente)
    /// </summary>
    private void RestoreChaserPersistence()
    {
        if (!_chaserWasMadePersistent || chaser == null) return;
        
        // Mover al perseguidor a la escena activa actual (donde está el jugador)
        if (player != null && player.gameObject.scene.isLoaded)
        {
            var targetScene = player.gameObject.scene;
            if (chaser.gameObject.scene != targetScene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chaser.gameObject, targetScene);
                Debug.Log($"[TagMinigame] 🚀 Perseguidor movido a escena '{targetScene.name}' al restaurar");
            }
        }
        
        // Si tenía un padre original y aún existe, restaurarlo
        if (_chaserOriginalParent != null)
        {
            chaser.transform.SetParent(_chaserOriginalParent);
        }
        
        _chaserWasMadePersistent = false;
        Debug.Log($"[TagMinigame] 🔓 Perseguidor '{chaser.name}' restaurado");
    }
    
    /// <summary>
    /// Restaura al controller a su estado normal y lo destruye si es necesario
    /// </summary>
    private void RestoreControllerPersistence()
    {
        if (!_controllerWasMadePersistent) return;
        
        // Mover el controller a la escena del jugador
        if (player != null && player.gameObject.scene.isLoaded)
        {
            var targetScene = player.gameObject.scene;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, targetScene);
        }
        
        _controllerWasMadePersistent = false;
        Debug.Log($"[TagMinigame] 🔓 Controller '{name}' restaurado");
    }

    void Awake()
    {
        if (uiContainer) uiContainer.SetActive(false);
    }
    
    void OnEnable()
    {
        // Suscribirse a ambos eventos de teletransporte (TeleportSystem y TeleportService)
        TeleportSystem.OnTeleportCompleted += OnPlayerTeleported;
        TeleportService.OnTeleportEnded += OnPlayerTeleported;
        
        // ✅ Suscribirse a cambios de escena
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDisable()
    {
        // Desuscribirse de ambos eventos de teletransporte
        TeleportSystem.OnTeleportCompleted -= OnPlayerTeleported;
        TeleportService.OnTeleportEnded -= OnPlayerTeleported;
        
        // ✅ Desuscribirse de cambios de escena
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    /// <summary>
    /// Llamado cuando se carga una nueva escena.
    /// Teletransporta al perseguidor cerca del jugador si el minijuego está activo.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isRunning)
        {
            return;
        }
        
        Debug.Log($"[TagMinigame] 🌍 Nueva escena cargada: '{scene.name}' (mode={mode})");
        
        // Dar tiempo a que la escena se inicialice
        StartCoroutine(HandleSceneChange(scene));
    }
    
    /// <summary>
    /// Maneja el cambio de escena durante el minijuego
    /// </summary>
    private IEnumerator HandleSceneChange(Scene newScene)
    {
        // Esperar a que la escena se estabilice
        yield return null;
        yield return new WaitForSeconds(0.5f);
        
        if (!isRunning) yield break;
        
        // Re-buscar al jugador en la nueva escena
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
        {
            player = playerGo.transform;
            Debug.Log($"[TagMinigame] 🌍 Player encontrado en nueva escena: {player.name} @ {player.position}");
            
            // Verificar/re-resolver perseguidor
            if (chaser == null)
            {
                Debug.LogWarning("[TagMinigame] 🌍 Perseguidor perdido tras cambio de escena, re-resolviendo...");
                ResolveNPCReferences();
            }
            
            if (chaser != null)
            {
                // Mover perseguidor a la escena del jugador y teletransportar
                MoveCharserToPlayerScene();
                StartCoroutine(TeleportChaserToPlayer());
            }
            else
            {
                Debug.LogError("[TagMinigame] 🌍 ❌ No se pudo encontrar/recuperar al perseguidor tras cambio de escena");
            }
        }
        else
        {
            Debug.LogWarning("[TagMinigame] 🌍 ⚠️ No se pudo encontrar al jugador en la nueva escena");
        }
    }
    
    /// <summary>
    /// Llamado cuando el jugador se teletransporta (por portal, etc.)
    /// Teletransporta inmediatamente al perseguidor cerca del jugador
    /// </summary>
    private void OnPlayerTeleported()
    {
        if (!isRunning) 
        {
            Debug.Log("[TagMinigame] 🚀 OnPlayerTeleported llamado pero minijuego no está corriendo");
            return;
        }
        
        Debug.Log($"[TagMinigame] 🚀 ===== JUGADOR TELETRANSPORTADO =====");
        Debug.Log($"[TagMinigame] 🚀 isRunning={isRunning}, chaser={(chaser != null ? chaser.name : "NULL")}");
        StartCoroutine(TeleportChaserAfterPlayerTeleport());
    }
    
    /// <summary>
    /// Teletransporta al perseguidor después de que el jugador se teletransporta
    /// </summary>
    private IEnumerator TeleportChaserAfterPlayerTeleport()
    {
        Debug.Log("[TagMinigame] 🚀 TeleportChaserAfterPlayerTeleport - Iniciando...");
        
        // Esperar un frame para que el jugador se estabilice en su nueva posición
        yield return null;
        yield return new WaitForSeconds(0.3f);
        
        if (!isRunning) 
        {
            Debug.Log("[TagMinigame] ❌ Minijuego detenido durante la espera");
            yield break;
        }
        
        // Re-buscar al jugador (puede haber cambiado de referencia tras el cambio de escena)
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
        {
            player = playerGo.transform;
            Debug.Log($"[TagMinigame] ✅ Player actualizado: {player.name} en posición {player.position}");
        }
        else
        {
            Debug.LogError("[TagMinigame] ❌ No se pudo encontrar al jugador después del teletransporte");
            yield break;
        }
        
        // Verificar que el perseguidor siga existiendo
        if (chaser == null)
        {
            Debug.LogWarning("[TagMinigame] ⚠️ Perseguidor perdido, intentando re-resolver...");
            ResolveNPCReferences();
        }
        
        if (chaser == null)
        {
            Debug.LogError("[TagMinigame] ❌ No se pudo recuperar al perseguidor");
            yield break;
        }
        
        Debug.Log($"[TagMinigame] ✅ Chaser disponible: {chaser.name}");
        
        // ✅ Mover al perseguidor a la misma escena que el jugador
        MoveCharserToPlayerScene();
        
        // Forzar el teletransporte del perseguidor
        StartCoroutine(TeleportChaserToPlayer());
    }
    
    /// <summary>
    /// Mueve al perseguidor a la misma escena que el jugador (si no es DontDestroyOnLoad)
    /// o simplemente lo teletransporta si ya es persistente
    /// </summary>
    private void MoveCharserToPlayerScene()
    {
        if (chaser == null || player == null)
        {
            Debug.LogWarning($"[TagMinigame] ⚠️ MoveCharserToPlayerScene: chaser={chaser}, player={player}");
            return;
        }
        
        var playerScene = player.gameObject.scene;
        var chaserScene = chaser.gameObject.scene;
        
        Debug.Log($"[TagMinigame] 📍 Escena jugador: '{playerScene.name}' (isLoaded={playerScene.isLoaded}), Escena perseguidor: '{chaserScene.name}'");
        
        // Si el perseguidor está en DontDestroyOnLoad (escena especial), no podemos moverlo de escena
        // pero eso está bien - DontDestroyOnLoad funciona en todas las escenas
        if (chaserScene.name == "DontDestroyOnLoad")
        {
            Debug.Log($"[TagMinigame] ✅ Perseguidor está en DontDestroyOnLoad - funcionará en cualquier escena");
            
            // ✅ Reinicializar el ChaserAI para la nueva escena
            chaser.ReinitializeAfterSceneChange(player);
            
            // Asegurar que el NavMeshAgent pueda recalcular su path en la nueva área
            var agent = chaser.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                // Warpear al perseguidor cerca del jugador en la nueva escena
                Vector3 nearPlayerPos = player.position - player.forward * 8f;
                if (UnityEngine.AI.NavMesh.SamplePosition(nearPlayerPos, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[TagMinigame] 🚀 Perseguidor warpeado a NavMesh cerca del jugador: {hit.position}");
                }
            }
            return;
        }
        
        if (playerScene != chaserScene && playerScene.isLoaded)
        {
            // Mover el perseguidor a la escena del jugador
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chaser.gameObject, playerScene);
            Debug.Log($"[TagMinigame] 🚀 Perseguidor movido a escena '{playerScene.name}'");
        }
        else if (playerScene == chaserScene)
        {
            Debug.Log($"[TagMinigame] ✅ Perseguidor ya está en la escena correcta '{playerScene.name}'");
        }
    }

    void Start()
    {
        // Usar PlayerService para mejor rendimiento (evitar FindGameObjectWithTag)
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
        {
            player = playerGo.transform;
        }

        if (chaser)
        {
            chaser.OnCaughtPlayer += OnCaught;
            
            // Guardar posición inicial del chaser
            chaserStartPosition = chaser.transform.position;
            chaserStartRotation = chaser.transform.rotation;
            
            // Obtener componentes para efectos visuales
            _chaserAnimator = chaser.GetComponent<Animator>();
            _chaserRenderers = chaser.GetComponentsInChildren<Renderer>();
            CacheOriginalColors();
        }
    }
    
    /// <summary>
    /// Cachea los colores originales de los materiales para poder restaurarlos
    /// </summary>
    private void CacheOriginalColors()
    {
        if (_chaserRenderers == null || _chaserRenderers.Length == 0) return;
        
        // Propiedades de color comunes en diferentes shaders
        string[] colorProperties = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_FaceColor" };
        
        _originalColors = new Color[_chaserRenderers.Length];
        for (int i = 0; i < _chaserRenderers.Length; i++)
        {
            if (_chaserRenderers[i] == null)
            {
                _originalColors[i] = Color.white;
                continue;
            }
            
            var mat = _chaserRenderers[i].material;
            bool found = false;
            
            // Intentar con cada propiedad de color
            foreach (var colorProp in colorProperties)
            {
                if (mat.HasProperty(colorProp))
                {
                    _originalColors[i] = mat.GetColor(colorProp);
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                _originalColors[i] = Color.white;
            }
        }
        
        Debug.Log($"[TagMinigame] 🎨 Colores originales cacheados: {_originalColors.Length} renderers");
    }

    void OnDestroy()
    {
        if (chaser)
        {
            chaser.OnCaughtPlayer -= OnCaught;
        }
    }

    void Update()
    {
        if (!isRunning) return;

        remainingTime -= Time.deltaTime;
        UpdateTimerUI();

        if (remainingTime <= 0f)
        {
            WinMinigame();
            return;
        }
        
        // Verificar si el jugador se ha teletransportado lejos
        CheckPlayerTeleport();
    }
    
    /// <summary>
    /// Verifica si el jugador se ha alejado demasiado (teletransporte) y teletransporta al perseguidor
    /// </summary>
    private void CheckPlayerTeleport()
    {
        if (player == null || chaser == null || isTeleporting) return;
        
        float distance = Vector3.Distance(player.position, chaser.transform.position);
        
        // Si el jugador está muy lejos, teletransportar al perseguidor
        if (distance > maxDistanceBeforeTeleport)
        {
            Debug.Log($"[TagMinigame] 🚀 Jugador detectado a {distance:F1}m - Teletransportando perseguidor...");
            StartCoroutine(TeleportChaserToPlayer());
        }
    }
    
    /// <summary>
    /// Teletransporta al perseguidor cerca del jugador con un pequeño delay
    /// </summary>
    private IEnumerator TeleportChaserToPlayer()
    {
        isTeleporting = true;
        
        // Pequeño delay para dar feedback al jugador
        yield return new WaitForSeconds(teleportDelay);
        
        if (!isRunning || player == null || chaser == null)
        {
            isTeleporting = false;
            yield break;
        }
        

        // Intentar posiciones alternativas si la de detrás no es válida
        Vector3[] directions = new Vector3[]
        {
            -player.forward,    // Detrás
            player.right,       // Derecha
            -player.right,      // Izquierda
            player.forward,     // Delante (último recurso)
        };
        
        Vector3 targetPos = player.position;
        bool foundValid = false;
        
        foreach (var dir in directions)
        {
            Vector3 testPos = player.position + dir.normalized * teleportSpawnDistance;
            
            // Verificar que haya suelo usando NavMesh si está disponible
            if (UnityEngine.AI.NavMesh.SamplePosition(testPos, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                targetPos = hit.position;
                foundValid = true;
                break;
            }
        }
        
        if (!foundValid)
        {
            // Fallback: posición directa detrás
            targetPos = player.position - player.forward * teleportSpawnDistance;
        }
        
        // Teletransportar al perseguidor
        chaser.TeleportTo(targetPos);
        
        Debug.Log($"[TagMinigame] ⚡ Perseguidor teletransportado a {targetPos}");
        
        // ✅ Reinicializar el NavMeshAgent para la nueva escena
        chaser.ReinitializeAfterSceneChange(player);
        
        // Pequeña pausa antes de que el perseguidor pueda atrapar (para dar reacción)
        yield return new WaitForSeconds(0.5f);
        
        isTeleporting = false;
    }

    public void StartMinigame()
    {
        if (isRunning || isCountingDown)
        {
            Debug.LogWarning("[TagMinigame] Ya está en ejecución.");
            return;
        }
        
        // ✅ Resolver referencias del NPC perseguidor si no están asignadas
        ResolveNPCReferences();

        // ✅ Log de diagnóstico
        Debug.Log($"[TagMinigame] ========== DIAGNÓSTICO ==========");
        Debug.Log($"[TagMinigame] chaserNPC: {(chaserNPC != null ? chaserNPC.name : "NULL")}");
        Debug.Log($"[TagMinigame] chaser (ChaserAI): {(chaser != null ? chaser.name : "NULL")}");
        Debug.Log($"[TagMinigame] player: {(player != null ? player.name : "NULL")}");
        Debug.Log($"[TagMinigame] _chaserAnimator: {(_chaserAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] _chaserNpcAnimator: {(_chaserNpcAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] angerVFXPrefab: {(angerVFXPrefab != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] =================================");

        Debug.Log($"[TagMinigame] Iniciando minijuego '{minigameId}'...");
        catchCount = 0;

        // Guardar posición inicial del jugador (usar spawn point si existe, si no la posición actual)
        if (player)
        {
            playerStartPosition = playerSpawnPoint ? playerSpawnPoint.position : player.position;
            playerStartRotation = playerSpawnPoint ? playerSpawnPoint.rotation : player.rotation;
        }

        // Guardar posición inicial del perseguidor (usar spawn point si existe, si no la posición actual)
        if (chaser)
        {
            Vector3 chaserPos = chaserSpawnPoint ? chaserSpawnPoint.position : chaser.transform.position;
            Quaternion chaserRot = chaserSpawnPoint ? chaserSpawnPoint.rotation : chaser.transform.rotation;
            
            chaserStartPosition = chaserPos;
            chaserStartRotation = chaserRot;
            chaser.SetStartPosition(chaserPos, chaserRot);
        }

        StartCoroutine(StartWithCountdown());
    }

    private IEnumerator StartWithCountdown(bool isRestart = false)
    {
        isCountingDown = true;

        // ✅ Solo hacer estas operaciones en el inicio inicial, no en reinicios
        if (!isRestart)
        {
            // Sacar al perseguidor del party si está en él
            RemoveChaserFromParty();
            
            // Verificar que el chaser sigue existiendo después de sacarlo del party
            if (chaser == null && chaserNPC != null)
            {
                Debug.LogWarning("[TagMinigame] ⚠️ chaser perdido después de RemoveChaserFromParty, re-obteniendo...");
                chaser = chaserNPC.GetComponent<ChaserAI>();
                if (chaser == null)
                {
                    chaser = chaserNPC.gameObject.AddComponent<ChaserAI>();
                }
            }
            
            // Asegurar que el perseguidor está activo
            if (chaser != null && !chaser.gameObject.activeInHierarchy)
            {
                chaser.gameObject.SetActive(true);
                Debug.Log("[TagMinigame] ✅ Perseguidor reactivado");
            }
            
            // Hacer al controller persistente para que sobreviva cambios de escena
            MakeControllerPersistent();
            
            // Hacer al perseguidor persistente para que sobreviva cambios de escena
            MakeChaserPersistent();
        }
        else
        {
            Debug.Log("[TagMinigame] 🔄 Reiniciando cuenta atrás (reinicio tras captura)");
        }

        if (uiContainer) uiContainer.SetActive(true);
        if (timerText) timerText.text = FormatTime(duration);

        ResetPositions();
        
        // ✅ Emitir señal de inicio AHORA para que la música comience desde la cuenta atrás
        // En reinicios, esto permite que la música continue o se reinicie
        RaiseStartSignal();
        
        // ✅ Activar efectos de enfado durante la cuenta atrás
        StartAngerEffects();

        float countdown = countdownBeforeStart;
        while (countdown > 0)
        {
            if (countdownText) countdownText.text = Mathf.CeilToInt(countdown).ToString();
            
            // Incrementar efecto de enfado progresivamente
            float angerProgress = 1f - (countdown / countdownBeforeStart);
            UpdateAngerIntensity(angerProgress);
            
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        if (countdownText) countdownText.text = "";
        ShowMessage(startMessage, 1.5f);
        
        // ✅ Los efectos de enfado (cara roja, VFX) se mantienen durante TODO el minijuego
        // Solo se desactivan cuando termina (en WinMinigame, StopMinigame, o si te atrapan)
        
        // ✅ Asegurar que el color rojo está al máximo al iniciar la persecución
        UpdateAngerIntensity(1f);
        
        // ✅ FIX CRÍTICO: Deshabilitar el sistema de comportamiento del NPC
        // DESPUÉS de los efectos de enfado, para que ChaserAI pueda tomar control
        DisableNPCBehaviour();

        isCountingDown = false;
        isRunning = true;
        remainingTime = duration;

        if (chaser)
        {
            Debug.Log($"[TagMinigame] 🏃 ========== INICIANDO PERSECUCIÓN ==========");
            Debug.Log($"[TagMinigame] 🏃 Perseguidor: '{chaser.name}'");
            Debug.Log($"[TagMinigame] 🏃 Posición perseguidor: {chaser.transform.position}");
            Debug.Log($"[TagMinigame] 🏃 Posición jugador: {(player != null ? player.position.ToString() : "NULL")}");
            Debug.Log($"[TagMinigame] 🏃 GameObject activo: {chaser.gameObject.activeInHierarchy}");
            Debug.Log($"[TagMinigame] 🏃 Componente habilitado: {chaser.enabled}");
            
            // ✅ Aplicar configuración de dificultad al perseguidor
            ApplyChaserDifficulty();
            
            Debug.Log($"[TagMinigame] 🏃 Llamando a StartChasing()...");
            chaser.StartChasing();
            Debug.Log($"[TagMinigame] ✅ StartChasing() completado");
        }
        else
        {
            Debug.LogError("[TagMinigame] ❌ ========== ERROR ==========");
            Debug.LogError($"[TagMinigame] ❌ chaser es NULL - no se puede iniciar persecución!");
            Debug.LogError($"[TagMinigame] ❌ chaserNPC: {(chaserNPC != null ? chaserNPC.name : "NULL")}");
        }

        OnMinigameStarted?.Invoke();
        Debug.Log($"[TagMinigame] ¡Minijuego iniciado! Duración: {duration}s, Chaser: {(chaser != null ? chaser.name : "NULL")}");
    }
    
    /// <summary>
    /// Emite la señal de inicio del minijuego para que otros sistemas (AudioService, etc.) reaccionen
    /// </summary>
    private void RaiseStartSignal()
    {
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
        {
            string eventKey = $"MINIGAME_START:{minigameId}";
            signals.RaiseCustom(eventKey);
            Debug.Log($"[TagMinigame] 🎮 Señal de inicio emitida: '{eventKey}'");
        }
    }
    
    /// <summary>
    /// Aplica la configuración de dificultad al perseguidor
    /// </summary>
    private void ApplyChaserDifficulty()
    {
        if (chaser == null) return;
        
        // Guardar valores originales
        _originalChaserSpeed = chaser.ChaseSpeed;
        _originalCatchDistance = chaser.CatchDistance;
        
        // Aplicar nuevos valores de dificultad
        chaser.ChaseSpeed = chaserSpeed;
        chaser.CatchDistance = catchDistance;
        chaser.Acceleration = chaserAcceleration;
        
        Debug.Log($"[TagMinigame] ⚡ Dificultad aplicada: Velocidad {_originalChaserSpeed} → {chaserSpeed}, " +
                  $"Captura {_originalCatchDistance} → {catchDistance}, Aceleración: {chaserAcceleration}");
    }
    
    /// <summary>
    /// Restaura los valores originales del perseguidor
    /// </summary>
    private void RestoreChaserDifficulty()
    {
        if (chaser == null) return;
        
        chaser.ChaseSpeed = _originalChaserSpeed;
        chaser.CatchDistance = _originalCatchDistance;
        
        Debug.Log($"[TagMinigame] 🔄 Dificultad restaurada: Velocidad {_originalChaserSpeed}, Captura {_originalCatchDistance}");
    }
    
    #region Efectos de Enfado
    
    /// <summary>
    /// Inicia los efectos visuales de enfado del perseguidor
    /// </summary>
    private void StartAngerEffects()
    {
        if (_angerEffectActive) return;
        _angerEffectActive = true;
        
        // ✅ Intentar resolver componentes desde múltiples fuentes
        GameObject targetGO = null;
        if (chaserNPC != null)
        {
            targetGO = chaserNPC.gameObject;
        }
        else if (chaser != null)
        {
            targetGO = chaser.gameObject;
        }
        
        if (targetGO != null && (_chaserAnimator == null || _chaserRenderers == null || _chaserNpcAnimator == null))
        {
            Debug.Log($"[TagMinigame] 🔧 Resolviendo componentes visuales desde '{targetGO.name}'...");
            _chaserAnimator = targetGO.GetComponent<Animator>();
            _chaserNpcAnimator = targetGO.GetComponent<NPCSimpleAnimator>();
            _chaserRenderers = targetGO.GetComponentsInChildren<Renderer>();
            CacheOriginalColors();
            Debug.Log($"[TagMinigame] 🔧 Renderers encontrados: {(_chaserRenderers != null ? _chaserRenderers.Length : 0)}");
        }
        
        // ✅ Resolver EmotionController y aplicar emoción de enfado
        if (targetGO != null && _chaserEmotionController == null)
        {
            _chaserEmotionController = targetGO.GetComponent<NPCEmotionController>();
            if (_chaserEmotionController != null)
            {
                Debug.Log($"[TagMinigame] 🎭 EmotionController encontrado en '{targetGO.name}'");
            }
        }
        
        // ✅ Aplicar emoción de enfado (y guardar la original)
        if (_chaserEmotionController != null)
        {
            // Guardar emoción original para restaurar después
            _originalEmotion = _chaserEmotionController.CurrentEmotion;
            
            // Aplicar emoción de enfado
            _chaserEmotionController.SetEmotion(minigameEmotion);
            Debug.Log($"[TagMinigame] 😠 Emoción cambiada a '{minigameEmotion}' (original: {_originalEmotion})");
        }
        
        // Diagnóstico adicional
        Debug.Log($"[TagMinigame] 🔴 === INICIO EFECTOS DE ENFADO ===");
        Debug.Log($"[TagMinigame] 🔴 chaserNPC: {(chaserNPC != null ? chaserNPC.name : "NULL")}");
        Debug.Log($"[TagMinigame] 🔴 chaser (ChaserAI): {(chaser != null ? chaser.name : "NULL")}");
        Debug.Log($"[TagMinigame] 🔴 _chaserAnimator: {(_chaserAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] 🔴 _chaserNpcAnimator: {(_chaserNpcAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] 🔴 angerVFXPrefab: {(angerVFXPrefab != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] 🔴 angryAnimationName: '{angryAnimationName}'");
        
        // Activar animación de batalla/enfado EN LA CAPA UPPERBODY con peso
        bool animationPlayed = false;
        
        if (_chaserAnimator != null && !string.IsNullOrEmpty(angryAnimationName))
        {
            try
            {
                // Verificar si tiene la capa UpperBody (layer 1)
                if (_chaserAnimator.layerCount > UPPER_BODY_LAYER)
                {
                    // ✅ Guardar el peso original de la capa UpperBody
                    _originalUpperBodyWeight = _chaserAnimator.GetLayerWeight(UPPER_BODY_LAYER);
                    
                    // ✅ Configurar el peso de la capa UpperBody a 1 para que se vea la animación
                    _chaserAnimator.SetLayerWeight(UPPER_BODY_LAYER, 1f);
                    
                    // ✅ Reproducir la animación de enfado en la capa UpperBody
                    _chaserAnimator.Play(angryAnimationName, UPPER_BODY_LAYER);
                    
                    animationPlayed = true;
                    Debug.Log($"[TagMinigame] 😠 Animación '{angryAnimationName}' en UpperBody (Layer {UPPER_BODY_LAYER}) con peso 1.0 (original: {_originalUpperBodyWeight})");
                    Debug.Log($"[TagMinigame] 🏃 La capa Base seguirá reproduciendo animaciones de movimiento");
                }
                else
                {
                    // Fallback: reproducir en capa base si no hay UpperBody
                    _chaserAnimator.Play(angryAnimationName);
                    animationPlayed = true;
                    Debug.Log($"[TagMinigame] 😠 Animación '{angryAnimationName}' en Layer base (no hay UpperBody)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TagMinigame] ⚠️ Error al reproducir animación: {e.Message}");
            }
        }
        
        if (!animationPlayed)
        {
            Debug.LogWarning($"[TagMinigame] ⚠️ No se pudo reproducir animación de enfado '{angryAnimationName}'");
        }
        
        // ✅ Instanciar VFX de enfado como hijo del perseguidor
        if (angerVFXPrefab != null && targetGO != null)
        {
            // Destruir instancia anterior si existe
            if (_vfxInstance != null)
            {
                Destroy(_vfxInstance);
            }
            
            // Instanciar como hijo del perseguidor
            _vfxInstance = Instantiate(angerVFXPrefab, targetGO.transform);
            _vfxInstance.transform.localPosition = vfxOffset;
            _vfxInstance.transform.localRotation = Quaternion.identity;
            
            // Reproducir partículas si las tiene
            var particles = _vfxInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
            }
            
            Debug.Log($"[TagMinigame] 💨 VFX de enfado instanciado como hijo de '{targetGO.name}'");
        }
        else
        {
            if (angerVFXPrefab == null)
                Debug.LogWarning("[TagMinigame] ⚠️ No hay prefab de VFX (angerVFXPrefab) asignado");
        }
        
        Debug.Log("[TagMinigame] 🔴 Efectos de enfado iniciados");
    }
    
    /// <summary>
    /// Actualiza la intensidad del efecto de enfado (color rojo progresivo)
    /// </summary>
    /// <param name="progress">0 = normal, 1 = máximo enfado</param>
    private void UpdateAngerIntensity(float progress)
    {
        if (_chaserRenderers == null || _originalColors == null) return;
        
        // Propiedades de color comunes en diferentes shaders
        string[] colorProperties = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_FaceColor" };
        
        for (int i = 0; i < _chaserRenderers.Length; i++)
        {
            if (_chaserRenderers[i] == null) continue;
            
            var mat = _chaserRenderers[i].material;
            
            // Intentar con cada propiedad de color
            foreach (var colorProp in colorProperties)
            {
                if (mat.HasProperty(colorProp))
                {
                    // Interpolar entre color original y color de enfado
                    Color lerpedColor = Color.Lerp(_originalColors[i], angerTintColor, progress);
                    mat.SetColor(colorProp, lerpedColor);
                    break; // Solo aplicar a la primera propiedad encontrada
                }
            }
        }
    }
    
    /// <summary>
    /// Detiene los efectos visuales de enfado y restaura colores originales
    /// </summary>
    private void StopAngerEffects()
    {
        if (!_angerEffectActive) return;
        _angerEffectActive = false;
        
        // Propiedades de color comunes en diferentes shaders
        string[] colorProperties = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_FaceColor" };
        
        // Restaurar colores originales
        if (_chaserRenderers != null && _originalColors != null)
        {
            for (int i = 0; i < _chaserRenderers.Length; i++)
            {
                if (_chaserRenderers[i] == null) continue;
                
                var mat = _chaserRenderers[i].material;
                
                // Intentar con cada propiedad de color
                foreach (var colorProp in colorProperties)
                {
                    if (mat.HasProperty(colorProp))
                    {
                        mat.SetColor(colorProp, _originalColors[i]);
                        break;
                    }
                }
            }
        }
        
        // ✅ Destruir VFX instanciado
        if (_vfxInstance != null)
        {
            Destroy(_vfxInstance);
            _vfxInstance = null;
            Debug.Log("[TagMinigame] 💨 VFX de enfado destruido");
        }
        
        // ✅ Restaurar peso original de la capa UpperBody
        if (_chaserAnimator != null && _chaserAnimator.layerCount > UPPER_BODY_LAYER)
        {
            _chaserAnimator.SetLayerWeight(UPPER_BODY_LAYER, _originalUpperBodyWeight);
            Debug.Log($"[TagMinigame] 🎬 Peso de UpperBody restaurado a {_originalUpperBodyWeight}");
        }
        
        // ✅ Restaurar emoción original del EmotionController
        if (_chaserEmotionController != null)
        {
            _chaserEmotionController.SetEmotion(_originalEmotion);
            Debug.Log($"[TagMinigame] 🎭 Emoción restaurada a '{_originalEmotion}'");
        }
        
        Debug.Log("[TagMinigame] ✅ Efectos de enfado desactivados, colores restaurados");
    }
    
    #endregion
    
    /// <summary>
    /// Saca al perseguidor del party temporalmente
    /// </summary>
    private void RemoveChaserFromParty()
    {
        if (chaserNPC == null)
        {
            Debug.Log("[TagMinigame] No hay chaserNPC configurado");
            return;
        }
        
        // Verificar si está en el party usando NPCBehaviourManagerV2
        chaserWasInParty = chaserNPC.IsInPlayerParty();
        
        if (chaserWasInParty)
        {
            // Usar el método del NPCBehaviourManagerV2 que dispara eventos
            chaserNPC.LeavePlayerParty();
            Debug.Log($"[TagMinigame] 👋 {chaserNPC.name} sacado del party para el minijuego");
        }
        else
        {
            Debug.Log($"[TagMinigame] {chaserNPC.name} no estaba en el party");
        }
        
        // NOTA: DisableNPCBehaviour() se llama después de los efectos de enfado,
        // justo antes de iniciar la persecución
    }
    
    /// <summary>
    /// Devuelve al perseguidor al party
    /// </summary>
    private void ReturnChaserToParty()
    {
        if (chaserNPC == null || !chaserWasInParty)
        {
            return;
        }
        
        if (!rejoinPartyOnWin)
        {
            Debug.Log("[TagMinigame] rejoinPartyOnWin=false, el perseguidor no vuelve al party");
            return;
        }
        
        // Usar el método del NPCBehaviourManagerV2 que dispara eventos
        chaserNPC.JoinPlayerParty();
        Debug.Log($"[TagMinigame] ✨ {chaserNPC.name} ha vuelto al party");
    }
    
    /// <summary>
    /// Deshabilita el sistema de comportamiento del NPC para que ChaserAI pueda tomar control.
    /// </summary>
    private void DisableNPCBehaviour()
    {
        if (chaserNPC == null) return;
        
        // Guardar el estado actual
        _npcBehaviourWasEnabled = chaserNPC.enabled;
        
        // ✅ Deshabilitar el NPCBehaviourManagerV2 para que no interfiera con ChaserAI
        if (chaserNPC.enabled)
        {
            chaserNPC.enabled = false;
            Debug.Log($"[TagMinigame] 🔧 NPCBehaviourManagerV2 de '{chaserNPC.name}' deshabilitado para el minijuego");
        }
        
        // ✅ También pausar el NPCSimpleAnimator si existe, para que ChaserAI controle las animaciones
        if (_chaserNpcAnimator != null)
        {
            // No deshabilitamos el animator, pero el ChaserAI usará SetMovementSpeed()
            Debug.Log($"[TagMinigame] 🎬 NPCSimpleAnimator presente - ChaserAI controlará las animaciones");
        }
        
        // ✅ Asegurar que el NavMeshAgent esté configurado correctamente para ChaserAI
        var navAgent = chaserNPC.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            // Resetear el path y estado del NavMeshAgent
            navAgent.ResetPath();
            navAgent.isStopped = true;
            Debug.Log($"[TagMinigame] 🤖 NavMeshAgent reseteado - listo para ChaserAI");
        }
    }
    
    /// <summary>
    /// Rehabilita el sistema de comportamiento del NPC después del minijuego.
    /// </summary>
    private void EnableNPCBehaviour()
    {
        if (chaserNPC == null) return;
        
        // ✅ Rehabilitar el NPCBehaviourManagerV2
        if (_npcBehaviourWasEnabled && !chaserNPC.enabled)
        {
            chaserNPC.enabled = true;
            Debug.Log($"[TagMinigame] 🔧 NPCBehaviourManagerV2 de '{chaserNPC.name}' rehabilitado");
            
            // ✅ Forzar al NPC a estado Idle para que retome control correctamente
            chaserNPC.ForceIdle();
        }
    }
    
    public void StopMinigame()
    {
        StopAllCoroutines();
        isRunning = false;
        isCountingDown = false;
        isTeleporting = false;
        
        // ✅ Limpiar efectos visuales
        StopAngerEffects();

        if (chaser) chaser.StopChasing();
        if (uiContainer) uiContainer.SetActive(false);
        
        // ✅ Restaurar configuración de dificultad del perseguidor
        RestoreChaserDifficulty();
        
        // ✅ Restaurar persistencia del perseguidor
        RestoreChaserPersistence();
        
        // ✅ Restaurar persistencia del controller
        RestoreControllerPersistence();
        
        // ✅ Rehabilitar el sistema de comportamiento del NPC
        EnableNPCBehaviour();
        
        // ✅ Devolver al party (si estaba)
        ReturnChaserToParty();

        Debug.Log("[TagMinigame] Minijuego detenido.");
    }

    private void OnCaught()
    {
        if (!isRunning) return;

        catchCount++;
        Debug.Log($"[TagMinigame] ¡Jugador atrapado! (Vez #{catchCount})");

        OnPlayerCaught?.Invoke();
        ShowMessage(caughtMessage, 2f);

        StartCoroutine(RestartAfterCaught());
    }

    private IEnumerator RestartAfterCaught()
    {
        isRunning = false;
        
        // Detener la persecución mientras se reinicia
        if (chaser) chaser.StopChasing();

        yield return new WaitForSeconds(1.5f);

        // ✅ Reiniciar posiciones
        ResetPositions();
        
        // ✅ Rehabilitar temporalmente el comportamiento del NPC para la cuenta atrás
        EnableNPCBehaviour();

        yield return new WaitForSeconds(0.5f);
        
        // ✅ REINICIAR COMPLETAMENTE: Comenzar de nuevo con cuenta atrás y tiempo completo
        Debug.Log($"[TagMinigame] 🔄 Reiniciando minijuego completo (atrapado #{catchCount})");
        
        // Iniciar la cuenta atrás de nuevo (esto también reinicia el tiempo)
        StartCoroutine(StartWithCountdown(isRestart: true));
    }

    private void ResetPositions()
    {
        if (player)
        {
            var charController = player.GetComponent<CharacterController>();
            if (charController)
            {
                charController.enabled = false;
                player.position = playerStartPosition;
                player.rotation = playerStartRotation;
                charController.enabled = true;
            }
            else
            {
                player.position = playerStartPosition;
                player.rotation = playerStartRotation;
            }
        }

        if (chaser)
        {
            chaser.ResetToStart();
        }
    }

    private void WinMinigame()
    {
        isRunning = false;
        isTeleporting = false;
        Debug.Log($"[TagMinigame] ¡Victoria! El jugador escapó.");

        if (chaser) chaser.StopChasing();

        ShowMessage(winMessage, 3f);
        
        // ✅ Limpiar efectos visuales de enfado
        StopAngerEffects();
        
        // ✅ Restaurar configuración de dificultad del perseguidor
        RestoreChaserDifficulty();
        
        // ✅ Restaurar persistencia del perseguidor
        RestoreChaserPersistence();
        
        // ✅ Restaurar persistencia del controller
        RestoreControllerPersistence();
        
        // ✅ Rehabilitar el sistema de comportamiento del NPC
        EnableNPCBehaviour();
        
        // ✅ Devolver al party (si estaba)
        ReturnChaserToParty();
        
        // ✅ Unir NPCs adicionales al party
        JoinAdditionalNPCsToParty();

        OnMinigameWon?.Invoke();

        RaiseWinSignal();

        StartCoroutine(HideUIAfterDelay(3f));
    }
    
    /// <summary>
    /// Une los NPCs configurados en npcsToJoinOnWin al party del jugador
    /// </summary>
    private void JoinAdditionalNPCsToParty()
    {
        if (npcsToJoinOnWin == null || npcsToJoinOnWin.Length == 0)
            return;
            
        foreach (var npc in npcsToJoinOnWin)
        {
            if (npc == null) continue;
            
            if (!npc.IsInPlayerParty())
            {
                bool joined = npc.JoinPlayerParty();
                if (joined)
                {
                    Debug.Log($"[TagMinigame] ✨ {npc.name} se unió al party al ganar el minijuego");
                }
                else
                {
                    Debug.LogWarning($"[TagMinigame] ⚠️ No se pudo unir {npc.name} al party");
                }
            }
            else
            {
                Debug.Log($"[TagMinigame] {npc.name} ya estaba en el party");
            }
        }
    }
    
    /// <summary>
    /// Método público para añadir un NPC al party.
    /// Puede ser llamado desde eventos de Unity o desde código.
    /// </summary>
    /// <param name="npc">El NPCBehaviourManagerV2 del NPC que se unirá al party</param>
    public void AddNpcToParty(NPCBehaviourManagerV2 npc)
    {
        if (npc == null)
        {
            Debug.LogWarning("[TagMinigame] AddNpcToParty: NPC es null");
            return;
        }
        
        if (!npc.IsInPlayerParty())
        {
            bool joined = npc.JoinPlayerParty();
            if (joined)
            {
                Debug.Log($"[TagMinigame] ✨ {npc.name} se unió al party (llamado manualmente)");
            }
            else
            {
                Debug.LogWarning($"[TagMinigame] ⚠️ No se pudo unir {npc.name} al party");
            }
        }
        else
        {
            Debug.Log($"[TagMinigame] {npc.name} ya estaba en el party");
        }
    }

    private void RaiseWinSignal()
    {
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
        {
            string eventKey = $"MINIGAME_{minigameId}_WON";
            signals.RaiseCustom(eventKey);
            Debug.Log($"[TagMinigame] Señal emitida: '{eventKey}'");
        }
        else
        {
            Debug.LogWarning("[TagMinigame] No se encontró DefaultNarrativeSignals para emitir la señal de victoria.");
        }
    }

    private IEnumerator HideUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (uiContainer) uiContainer.SetActive(false);
    }

    private void UpdateTimerUI()
    {
        if (timerText)
        {
            timerText.text = FormatTime(remainingTime);
        }
    }

    private void ShowMessage(string msg, float messageDuration)
    {
        if (messageText)
        {
            StopCoroutine(nameof(ClearMessageAfter));
            messageText.text = msg;
            StartCoroutine(ClearMessageAfter(messageDuration));
        }
    }

    private IEnumerator ClearMessageAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (messageText) messageText.text = "";
    }

    private string FormatTime(float time)
    {
        time = Mathf.Max(0, time);
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    public float RemainingTime => remainingTime;
    public bool IsRunning => isRunning;
    public int CatchCount => catchCount;
}
