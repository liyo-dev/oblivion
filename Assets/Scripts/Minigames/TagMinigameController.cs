using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Core;
using Game.NPC;
using Game.NPC.Common;
using Game.NPC.Modules;
using Sendero.Core.Feedback;

/// <summary>
/// Controlador del minijuego "Pilla Pilla" (Tag).
/// El jugador debe huir del perseguidor durante X segundos.
/// Si es atrapado, se reinicia. Si sobrevive, gana.
/// El jugador puede ir a donde quiera (incluso teletransportarse), 
/// pero el perseguidor le seguirá a todas partes.
/// </summary>
public class TagMinigameController : MonoBehaviour
{
    private enum ProtectionButton
    {
        A,
        B,
        X,
        Y
    }

    [System.Serializable]
    private sealed class ProtectNpcTarget
    {
        [Tooltip("Transform raíz del NPC a proteger")]
        public Transform npcRoot;
        [Tooltip("Punto opcional donde instanciar el escudo. Si está vacío, usa npcRoot")]
        public Transform shieldAnchor;

        [HideInInspector] public bool isProtected;
        [HideInInspector] public ProtectionButton requiredButton;
        [HideInInspector] public Transform systemsRoot;
        [HideInInspector] public NPCAlertIconController iconController;
        [HideInInspector] public GameObject spawnedShield;
    }

    private sealed class SuspendedBehaviourState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    [Header("Configuración")]
    [SerializeField] private string minigameId = "TAG_MINIGAME_01";
    [SerializeField] private float duration = 30f;
    [SerializeField] private float countdownBeforeStart = 3f;
    [Tooltip("Si true, fuerza el cambio a Will y deshabilita el cambio de personaje durante el minijuego")]
    [SerializeField] private bool requiresWill = false;

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
    [SerializeField] private TextMeshProUGUI objectiveText;
    [Tooltip("Texto pequeño que muestra el hint de salida durante el minijuego (ej: '· Start: Salir').")]
    [SerializeField] private TextMeshProUGUI exitHintText;

    [Header("UI — Pantalla de Instrucciones")]
    [Tooltip("Panel que muestra las instrucciones antes de empezar. Puede ser null (se salta la pantalla de instrucciones).")]
    [SerializeField] private GameObject instructionPanel;
    [Tooltip("Texto donde se muestran las instrucciones del minijuego.")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [Tooltip("Texto del prompt para comenzar (ej: '— Pulsa [A] para empezar —').")]
    [SerializeField] private TextMeshProUGUI startPromptText;

    [Header("Objetivo: Proteger NPCs")]
    [SerializeField] private ProtectNpcTarget[] protectTargets;
    [SerializeField] private int requiredProtectedCount = 10;
    [SerializeField] private bool autoPopulateProtectTargetsIfEmpty = true;
    [SerializeField] private int autoPopulateTargetCount = 10;
    [SerializeField] private float protectDistance = 2.8f;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Vector3 shieldOffset = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private bool parentShieldToNpc = true;
    [SerializeField] private bool clearShieldsOnStop = true;
    [SerializeField] private bool clearShieldsOnWin = false;

    [Header("Iconos Botón NPC (arrastrar Sprites X/Y/A/B)")]
    [SerializeField] private Sprite iconButtonASprite;
    [SerializeField] private Sprite iconButtonBSprite;
    [SerializeField] private Sprite iconButtonXSprite;
    [SerializeField] private Sprite iconButtonYSprite;
    [Tooltip("Multiplicador de escala para los iconos (renombrado para forzar actualización)")]
    [SerializeField] private float iconScaleMultiplierV4 = 3.0f;
    [Tooltip("Altura objetivo en unidades de mundo (renombrado para forzar actualización)")]
    [SerializeField] private float iconTargetHeightV4 = 0.4f;
    [SerializeField] private int iconSpriteSortingOrder = 250;

    [Header("Feedback (Minijuego)")]
    [SerializeField] private float introShakeIntensity = 0.18f;
    [SerializeField] private float introShakeDuration = 0.2f;
    [SerializeField] private float chaseStartShakeIntensity = 0.24f;
    [SerializeField] private float chaseStartShakeDuration = 0.25f;
    [SerializeField] private float caughtShakeIntensity = 0.45f;
    [SerializeField] private float caughtShakeDuration = 0.35f;
    [SerializeField] private Color caughtFlashColor = new Color(1f, 0.18f, 0.18f, 0.22f);
    [SerializeField] private float caughtFlashDuration = 0.12f;

    [Header("Plano Intro Estela")]
    [SerializeField] private bool enableEstelaIntroCameraBeat = true;
    [SerializeField] private vThirdPersonCamera introCamera;
    [SerializeField] private float introCameraBeatDelay = 0.15f;
    [SerializeField] private float introCameraBeatDuration = 0.85f;

    [Header("Animación Player (protección)")]
    [SerializeField] private int playerUpperBodyLayer = 1;
    [SerializeField] private string playerCastLeftStatePath = "UpperBody.Magic.MagicLeft";
    [SerializeField] private string playerCastRightStatePath = "UpperBody.Magic.MagicRight";
    [SerializeField] private string playerCastSpecialStatePath = "UpperBody.Magic.MagicSpecial";
    [SerializeField] private float playerCastLayerFadeOut = 0.22f;

    [Header("Mensajes (claves de traducción)")]
    [Tooltip("Clave de traducción del hint de salida durante el juego (ej: '· Start: Salir').")]
    [SerializeField] private string exitHintMessage = "MINIGAME_TAG_EXIT_HINT";
    [Tooltip("Clave de traducción con las instrucciones del minijuego (mostrado antes de comenzar).")]
    [SerializeField] private string instructionMessage = "MINIGAME_TAG_INSTRUCTION";
    [SerializeField] private string countdownTaskDescription = "MINIGAME_TAG_COUNTDOWN";
    [SerializeField] private bool showEscapeCountdown = false;
    [Tooltip("Clave de traducción del mensaje que aparece tras la victoria con cuenta atrás de huida.")]
    [SerializeField] private string escapeMessage = "MINIGAME_TAG_ESCAPE";
    [Tooltip("Segundos de cuenta atrás de huida.")]
    [SerializeField] private float escapeCountdownDuration = 10f;
    [SerializeField] private string objectiveFormat = "MINIGAME_TAG_OBJECTIVE";

    [Header("UI — Pantalla de Instrucciones (Botón Salir)")]
    [Tooltip("Botón para abandonar el minijuego desde la pantalla de instrucciones.")]
    [SerializeField] private UnityEngine.UI.Button exitButton;

    [Header("Eventos")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameWon;
    public UnityEvent OnMinigameLost;
    public UnityEvent OnPlayerCaught;
    public UnityEvent OnMinigameAborted;

    // Flag global que bloquea el menú de pausa mientras cualquier instancia del minijuego está activa
    public static bool IsAnyMinigameActive { get; private set; }
    // Instancia que activó el flag — solo ella puede desactivarlo
    private static TagMinigameController _activeMinigameInstance;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsAnyMinigameActive = false; _activeMinigameInstance = null; }
#endif

    // Estado interno
    private float remainingTime;
    private bool isRunning = false;
    private bool _waitingForStart = false;
    private bool _waitingForKingdomExit = false; // NPCs protegidos, esperando que el jugador salga del reino
    private bool isCountingDown = false;
    private Transform player;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private Vector3 chaserStartPosition;
    private Quaternion chaserStartRotation;
    private int catchCount = 0;
    private int protectedCount = 0;
    
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

    // Control del player durante el minijuego
    private Animator _playerAnimator;
    private PlayerActionManager _playerActionManager;
    private PlayerActionManager _modeOwnerActionManager;
    private bool _playerMinigameModeApplied = false;
    private Coroutine _playerCastRoutine;
    private GameObject _iconTemplateA;
    private GameObject _iconTemplateB;
    private GameObject _iconTemplateX;
    private GameObject _iconTemplateY;
    private GameObject _fallbackIconTemplateA;
    private GameObject _fallbackIconTemplateB;
    private GameObject _fallbackIconTemplateX;
    private GameObject _fallbackIconTemplateY;
    private readonly List<SuspendedBehaviourState> _suspendedTargetNpcSystems = new List<SuspendedBehaviourState>();
    private bool _targetNpcSystemsSuspended;
    private bool _loggedMissingProtectionConfig;

    // Para integración con sistema narrativo
    public string MinigameId => minigameId;

    // Nombre de la escena donde se ejecuta el minijuego (capturado antes de hacerse persistente)
    private string _minigameSceneName;
    // Guard para no mostrar el popup de confirmación de salida más de una vez
    private bool _isWaitingForAbortConfirmation;
    
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
    
    private int RequiredProtectCount
    {
        get
        {
            int configured = Mathf.Max(0, requiredProtectedCount);
            int available = CountValidProtectTargets();
            return Mathf.Min(configured, available);
        }
    }

    private bool HasProtectionObjectiveConfigured => RequiredProtectCount > 0;

    private int CountValidProtectTargets()
    {
        if (protectTargets == null) return 0;

        int count = 0;
        for (int i = 0; i < protectTargets.Length; i++)
        {
            if (protectTargets[i] != null && protectTargets[i].npcRoot != null)
                count++;
        }

        return count;
    }

    private void TryAutoPopulateProtectionTargets()
    {
        if (!autoPopulateProtectTargetsIfEmpty) return;
        if (CountValidProtectTargets() > 0) return;

        int desiredCount = Mathf.Max(1, autoPopulateTargetCount);
        var allNpcManagers = FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include);
        if (allNpcManagers == null || allNpcManagers.Length == 0)
            return;

        var candidates = new List<NPCBehaviourManagerV2>(allNpcManagers.Length);
        for (int i = 0; i < allNpcManagers.Length; i++)
        {
            var npc = allNpcManagers[i];
            if (npc == null) continue;
            if (chaserNPC != null && npc == chaserNPC) continue;
            if (chaser != null && npc.transform == chaser.transform) continue;
            if (!npc.gameObject.activeInHierarchy) continue;

            // Excluir miembros del equipo del jugador
            var partyMember = npc.GetComponent<NPCPartyMember>();
            if (partyMember != null && partyMember.IsInParty) continue;

            candidates.Add(npc);
        }

        if (candidates.Count == 0) return;

        if (player != null)
        {
            Vector3 playerPos = player.position;
            candidates.Sort((a, b) =>
            {
                float da = (a.transform.position - playerPos).sqrMagnitude;
                float db = (b.transform.position - playerPos).sqrMagnitude;
                return da.CompareTo(db);
            });
        }

        int finalCount = Mathf.Min(desiredCount, candidates.Count);
        var generatedTargets = new ProtectNpcTarget[finalCount];
        for (int i = 0; i < finalCount; i++)
        {
            generatedTargets[i] = new ProtectNpcTarget
            {
                npcRoot = candidates[i].transform
            };
        }

        protectTargets = generatedTargets;
        Debug.Log($"[TagMinigame] 🛡️ Objetivo auto-configurado con {finalCount} NPCs (protectTargets estaba vacío).");
    }

    private void EnsureObjectiveTextReference()
    {
        if (objectiveText != null || uiContainer == null) return;

        var texts = uiContainer.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var candidate = texts[i];
            if (candidate == null) continue;
            string n = candidate.name.ToLowerInvariant();
            if (n.Contains("objective") || n.Contains("objetivo"))
            {
                objectiveText = candidate;
                return;
            }
        }

        var runtimeTextGo = new GameObject("ObjectiveText_Runtime", typeof(RectTransform), typeof(TextMeshProUGUI));
        runtimeTextGo.transform.SetParent(uiContainer.transform, false);
        var rt = runtimeTextGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 44f);
        rt.sizeDelta = new Vector2(560f, 52f);

        objectiveText = runtimeTextGo.GetComponent<TextMeshProUGUI>();
        objectiveText.alignment = TextAlignmentOptions.Center;
        objectiveText.fontSize = 30f;
        objectiveText.color = Color.white;
        objectiveText.text = string.Empty;
        Debug.Log("[TagMinigame] 🛠️ objectiveText no asignado: creado TextMeshProUGUI runtime para el contador de protección.");
    }

    private void CachePlayerRuntimeReferences()
    {
        if (player == null)
        {
            _playerAnimator = null;
            _playerActionManager = null;
            return;
        }

        _playerAnimator = player.GetComponent<Animator>() ?? player.GetComponentInChildren<Animator>();
        _playerActionManager = player.GetComponent<PlayerActionManager>() ?? player.GetComponentInParent<PlayerActionManager>();
    }

    private void EnableMinigameActionRestrictions()
    {
        IsAnyMinigameActive = true;
        _activeMinigameInstance = this;
        CachePlayerRuntimeReferences();
        if (_playerActionManager == null) return;

        if (_modeOwnerActionManager != null && _modeOwnerActionManager != _playerActionManager && _playerMinigameModeApplied)
        {
            _modeOwnerActionManager.PopMode(ActionMode.Minigame);
            _playerMinigameModeApplied = false;
        }

        _modeOwnerActionManager = _playerActionManager;

        if (_playerMinigameModeApplied) return;

        _modeOwnerActionManager.PushMode(ActionMode.Minigame);
        _playerMinigameModeApplied = true;
    }

    private void DisableMinigameActionRestrictions()
    {
        if (_activeMinigameInstance == this)
        {
            IsAnyMinigameActive = false;
            _activeMinigameInstance = null;
        }

        if (_modeOwnerActionManager != null && _playerMinigameModeApplied)
        {
            _modeOwnerActionManager.PopMode(ActionMode.Minigame);
        }

        _playerMinigameModeApplied = false;
        _modeOwnerActionManager = null;
    }

    private void SuspendTargetNpcSystemsForMinigame()
    {
        if (_targetNpcSystemsSuspended || !HasProtectionObjectiveConfigured || protectTargets == null)
            return;

        for (int i = 0; i < protectTargets.Length; i++)
        {
            var target = protectTargets[i];
            if (target == null || target.npcRoot == null)
                continue;

            Transform systemsRoot = ResolveNpcSystemsRoot(target.npcRoot);
            target.systemsRoot = systemsRoot;

            HideExternalNpcIcons(systemsRoot);
            SuspendBehavioursInHierarchy<NPCQuestIconManager>(systemsRoot);
            SuspendBehavioursInHierarchy<NPCInteractiveNarrativeExecutor>(systemsRoot);
            SuspendBehavioursInHierarchy<NPCPersistentIconController>(systemsRoot);
            SuspendBehavioursInHierarchy<NPCCombatBrain>(systemsRoot);
            SuspendBehavioursInHierarchy<Interactable>(systemsRoot);
        }

        _targetNpcSystemsSuspended = true;
    }

    private void RestoreTargetNpcSystemsAfterMinigame()
    {
        if (_suspendedTargetNpcSystems.Count == 0)
        {
            _targetNpcSystemsSuspended = false;
            return;
        }

        for (int i = _suspendedTargetNpcSystems.Count - 1; i >= 0; i--)
        {
            var state = _suspendedTargetNpcSystems[i];
            if (state == null || state.behaviour == null)
                continue;

            state.behaviour.enabled = state.wasEnabled;
        }

        _suspendedTargetNpcSystems.Clear();
        _targetNpcSystemsSuspended = false;
    }

    private static void HideExternalNpcIcons(Transform npcRoot)
    {
        if (npcRoot == null) return;

        var alertIcons = npcRoot.GetComponentsInChildren<NPCAlertIconController>(true);
        for (int i = 0; i < alertIcons.Length; i++)
        {
            if (alertIcons[i] != null)
                alertIcons[i].HideAlertIconImmediate();
        }

        var persistentIcons = npcRoot.GetComponentsInChildren<NPCPersistentIconController>(true);
        for (int i = 0; i < persistentIcons.Length; i++)
        {
            if (persistentIcons[i] != null)
                persistentIcons[i].HideIcon();
        }
    }

    private static Transform ResolveNpcSystemsRoot(Transform targetTransform)
    {
        if (targetTransform == null) return null;

        var npcManager = targetTransform.GetComponentInParent<NPCBehaviourManagerV2>();
        if (npcManager != null)
            return npcManager.transform;

        return targetTransform;
    }

    private void SuspendBehavioursInHierarchy<T>(Transform root) where T : Behaviour
    {
        if (root == null) return;

        var behaviours = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            TrackAndDisableBehaviour(behaviours[i]);
        }
    }

    private void TrackAndDisableBehaviour(Behaviour behaviour)
    {
        if (behaviour == null) return;
        if (IsBehaviourAlreadySuspended(behaviour)) return;

        _suspendedTargetNpcSystems.Add(new SuspendedBehaviourState
        {
            behaviour = behaviour,
            wasEnabled = behaviour.enabled
        });

        behaviour.enabled = false;
    }

    private bool IsBehaviourAlreadySuspended(Behaviour behaviour)
    {
        for (int i = 0; i < _suspendedTargetNpcSystems.Count; i++)
        {
            if (_suspendedTargetNpcSystems[i] != null && _suspendedTargetNpcSystems[i].behaviour == behaviour)
                return true;
        }

        return false;
    }

    void Awake()
    {
        if (uiContainer) uiContainer.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        EnsureObjectiveTextReference();
        UpdateObjectiveUI();
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

        DisableMinigameActionRestrictions();
        HideAllProtectionIcons();
        RestoreTargetNpcSystemsAfterMinigame();
    }
    
    /// <summary>
    /// Llamado cuando se carga una nueva escena.
    /// Teletransporta al perseguidor cerca del jugador si el minijuego está activo.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isRunning && !isCountingDown)
            return;

        // Si la escena carga durante la cuenta atrás (antes de que isRunning sea true),
        // abortar inmediatamente: la corutina StartWithCountdown seguiría corriendo
        // y llamaría a StartChasing() en la nueva escena (p.ej. menú principal) sin jugador.
        if (isCountingDown && !isRunning)
        {
            Debug.LogWarning($"[TagMinigame] 🌍 Escena '{scene.name}' cargada durante cuenta atrás. Abortando minijuego.");
            StopMinigame();
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
            CachePlayerRuntimeReferences();
            if (_playerMinigameModeApplied)
                EnableMinigameActionRestrictions();
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
            // Si no hay jugador en la nueva escena (p.ej. el jugador salió al menú principal),
            // abortar el minijuego para limpiar la UI y el estado.
            Debug.LogWarning("[TagMinigame] 🌍 ⚠️ No se encontró jugador en la nueva escena. Abortando minijuego.");
            StopMinigame();
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
            CachePlayerRuntimeReferences();
            if (_playerMinigameModeApplied)
                EnableMinigameActionRestrictions();
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
        CachePlayerRuntimeReferences();

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

    private void InitializeProtectionTargetsForRound(bool clearShields)
    {
        protectedCount = 0;

        if (protectTargets == null || protectTargets.Length == 0)
        {
            UpdateObjectiveUI();
            return;
        }

        for (int i = 0; i < protectTargets.Length; i++)
        {
            var target = protectTargets[i];
            if (target == null || target.npcRoot == null) continue;

            target.isProtected = false;
            target.requiredButton = GetRandomProtectionButton();
            target.systemsRoot = ResolveNpcSystemsRoot(target.npcRoot);

            if (target.iconController == null)
            {
                Transform iconRoot = target.systemsRoot != null ? target.systemsRoot : target.npcRoot;
                target.iconController = iconRoot.GetComponent<NPCAlertIconController>();
                if (target.iconController == null)
                    target.iconController = iconRoot.gameObject.AddComponent<NPCAlertIconController>();
            }

            if (clearShields && target.spawnedShield != null)
            {
                Destroy(target.spawnedShield);
                target.spawnedShield = null;
            }

            target.iconController.HideAlertIconImmediate();
        }

        UpdateObjectiveUI();
    }

    private void HideAllProtectionIcons()
    {
        if (protectTargets == null) return;

        for (int i = 0; i < protectTargets.Length; i++)
        {
            var target = protectTargets[i];
            if (target == null || target.iconController == null) continue;
            target.iconController.HideAlertIconImmediate();
        }
    }

    private void DisposeRuntimeIconTemplates()
    {
        DestroyRuntimeIconTemplate(ref _iconTemplateA);
        DestroyRuntimeIconTemplate(ref _iconTemplateB);
        DestroyRuntimeIconTemplate(ref _iconTemplateX);
        DestroyRuntimeIconTemplate(ref _iconTemplateY);
        DestroyRuntimeIconTemplate(ref _fallbackIconTemplateA);
        DestroyRuntimeIconTemplate(ref _fallbackIconTemplateB);
        DestroyRuntimeIconTemplate(ref _fallbackIconTemplateX);
        DestroyRuntimeIconTemplate(ref _fallbackIconTemplateY);
    }

    private static void DestroyRuntimeIconTemplate(ref GameObject template)
    {
        if (template == null) return;
        UnityEngine.Object.Destroy(template);
        template = null;
    }

    private void ClearAllProtectionShields()
    {
        if (protectTargets == null) return;

        for (int i = 0; i < protectTargets.Length; i++)
        {
            var target = protectTargets[i];
            if (target == null || target.spawnedShield == null) continue;
            Destroy(target.spawnedShield);
            target.spawnedShield = null;
        }
    }

    private ProtectionButton GetRandomProtectionButton()
    {
        return (ProtectionButton)Random.Range(0, 4);
    }

    private GameObject GetIconPrefabForButton(ProtectionButton button)
    {
        switch (button)
        {
            case ProtectionButton.A:
            {
                var spriteTemplate = ResolveRuntimeIconTemplate(ref _iconTemplateA, iconButtonASprite, "ProtectBtn_A");
                return spriteTemplate != null ? spriteTemplate : ResolveTextIconTemplate(ref _fallbackIconTemplateA, "A", "ProtectBtn_A_Text");
            }
            case ProtectionButton.B:
            {
                var spriteTemplate = ResolveRuntimeIconTemplate(ref _iconTemplateB, iconButtonBSprite, "ProtectBtn_B");
                return spriteTemplate != null ? spriteTemplate : ResolveTextIconTemplate(ref _fallbackIconTemplateB, "B", "ProtectBtn_B_Text");
            }
            case ProtectionButton.X:
            {
                var spriteTemplate = ResolveRuntimeIconTemplate(ref _iconTemplateX, iconButtonXSprite, "ProtectBtn_X");
                return spriteTemplate != null ? spriteTemplate : ResolveTextIconTemplate(ref _fallbackIconTemplateX, "X", "ProtectBtn_X_Text");
            }
            case ProtectionButton.Y:
            {
                var spriteTemplate = ResolveRuntimeIconTemplate(ref _iconTemplateY, iconButtonYSprite, "ProtectBtn_Y");
                return spriteTemplate != null ? spriteTemplate : ResolveTextIconTemplate(ref _fallbackIconTemplateY, "Y", "ProtectBtn_Y_Text");
            }
            default: return null;
        }
    }

    private GameObject ResolveRuntimeIconTemplate(ref GameObject templateCache, Sprite sprite, string templateName)
    {
        if (templateCache != null) return templateCache;
        if (sprite == null) return null;

        templateCache = CreateRuntimeIconTemplate(sprite, templateName);
        return templateCache;
    }

    private GameObject ResolveTextIconTemplate(ref GameObject templateCache, string text, string templateName)
    {
        if (templateCache != null) return templateCache;
        templateCache = CreateTextIconTemplate(text, templateName);
        return templateCache;
    }

    private GameObject CreateRuntimeIconTemplate(Sprite sprite, string templateName)
    {
        var go = new GameObject(templateName);
        go.hideFlags = HideFlags.HideAndDontSave;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = iconSpriteSortingOrder;

        float scale = ResolveSpriteIconScale(sprite);
        // ✅ FIX: Flip X scale to correct mirroring issue
        go.transform.localScale = new Vector3(-scale, scale, 1f);
        go.SetActive(false);
        return go;
    }

    private GameObject CreateTextIconTemplate(string text, string templateName)
    {
        var go = new GameObject(templateName);
        go.hideFlags = HideFlags.HideAndDontSave;

        var label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 2.8f;
        label.color = Color.white;
        label.outlineWidth = 0.28f;
        label.outlineColor = Color.black;

        float scale = ResolveTextIconScale();
        // ✅ FIX: Flip X scale to correct mirroring issue
        go.transform.localScale = new Vector3(-scale, scale, 1f);
        go.SetActive(false);
        return go;
    }

    private float ResolveSpriteIconScale(Sprite sprite)
    {
        float normalizedMultiplier = Mathf.Clamp(iconScaleMultiplierV4, 0.01f, 5f);
        float desiredHeight = Mathf.Max(0.01f, iconTargetHeightV4);
        float spriteHeight = sprite != null ? Mathf.Max(0.001f, sprite.bounds.size.y) : 1f;
        return (desiredHeight / spriteHeight) * normalizedMultiplier;
    }

    private float ResolveTextIconScale()
    {
        float normalizedMultiplier = Mathf.Clamp(iconScaleMultiplierV4, 0.01f, 5f);
        float desiredHeight = Mathf.Max(0.01f, iconTargetHeightV4);
        return desiredHeight * normalizedMultiplier * 0.35f;
    }

    private ProtectionButton? ReadProtectionButtonPressed()
    {
        if (GamepadInputReader.JumpPressed) return ProtectionButton.A;
        if (GamepadInputReader.AttackMagicRightPressed) return ProtectionButton.B;
        if (GamepadInputReader.AttackMagicLeftPressed) return ProtectionButton.X;
        if (GamepadInputReader.AttackMagicSpecialPressed) return ProtectionButton.Y;
        return null;
    }

    private void UpdateProtectionObjectiveGameplay()
    {
        if (!HasProtectionObjectiveConfigured || player == null || protectTargets == null) return;

        float maxDistanceSqr = protectDistance * protectDistance;
        ProtectionButton? pressedButton = ReadProtectionButtonPressed();

        ProtectNpcTarget nearestInRange = null;
        ProtectNpcTarget bestMatchingTarget = null;
        float nearestInRangeDistSqr = float.MaxValue;
        float bestMatchingDistSqr = float.MaxValue;

        for (int i = 0; i < protectTargets.Length; i++)
        {
            var target = protectTargets[i];
            if (target == null || target.npcRoot == null) continue;

            if (target.isProtected)
            {
                if (target.iconController != null)
                    target.iconController.HideAlertIconImmediate();
                continue;
            }

            float distSqr = (player.position - target.npcRoot.position).sqrMagnitude;
            bool inRange = distSqr <= maxDistanceSqr;

            if (inRange)
            {
                if (target.iconController != null)
                {
                    var iconPrefab = GetIconPrefabForButton(target.requiredButton);
                    if (iconPrefab != null)
                        target.iconController.ShowPersistentIcon(iconPrefab);
                }

                if (distSqr < nearestInRangeDistSqr)
                {
                    nearestInRangeDistSqr = distSqr;
                    nearestInRange = target;
                }

                if (pressedButton.HasValue && target.requiredButton == pressedButton.Value && distSqr < bestMatchingDistSqr)
                {
                    bestMatchingDistSqr = distSqr;
                    bestMatchingTarget = target;
                }
            }
            else if (target.iconController != null)
            {
                target.iconController.HideAlertIconImmediate();
            }
        }

        if (!pressedButton.HasValue)
            return;

        if (bestMatchingTarget != null)
        {
            ProtectNpc(bestMatchingTarget);
            return;
        }

        // Si pulsa un botón incorrecto cerca de un NPC, se rerollea el botón objetivo para aumentar dificultad.
        if (nearestInRange != null)
        {
            nearestInRange.requiredButton = GetRandomProtectionButton();
            if (nearestInRange.iconController != null)
            {
                nearestInRange.iconController.HideAlertIconImmediate();
                var iconPrefab = GetIconPrefabForButton(nearestInRange.requiredButton);
                if (iconPrefab != null)
                    nearestInRange.iconController.ShowPersistentIcon(iconPrefab);
            }
        }
    }

    private void ProtectNpc(ProtectNpcTarget target)
    {
        if (target == null || target.npcRoot == null || target.isProtected) return;

        target.isProtected = true;
        protectedCount++;

        if (target.iconController != null)
            target.iconController.HideAlertIconImmediate();

        PlayPlayerProtectionCastAnimation(target.requiredButton);
        SpawnShieldForTarget(target);
        UpdateObjectiveUI();

        if (HasProtectionObjectiveConfigured && protectedCount >= RequiredProtectCount)
        {
            if (showEscapeCountdown)
                StartCoroutine(WaitForKingdomExit());
            else
                WinMinigame();
        }
    }

    private void SpawnShieldForTarget(ProtectNpcTarget target)
    {
        if (shieldPrefab == null || target == null || target.npcRoot == null) return;

        if (target.spawnedShield != null)
        {
            Destroy(target.spawnedShield);
            target.spawnedShield = null;
        }

        Transform anchor = target.shieldAnchor != null ? target.shieldAnchor : target.npcRoot;
        Vector3 spawnPos = anchor.position + shieldOffset;
        Quaternion spawnRot = anchor.rotation;

        if (parentShieldToNpc)
        {
            target.spawnedShield = Instantiate(shieldPrefab, spawnPos, spawnRot, anchor);
        }
        else
        {
            target.spawnedShield = Instantiate(shieldPrefab, spawnPos, spawnRot);
        }
    }

    private void PlayPlayerProtectionCastAnimation(ProtectionButton button)
    {
        if (_playerAnimator == null) return;

        string statePath;
        switch (button)
        {
            case ProtectionButton.X:
                statePath = playerCastLeftStatePath;
                break;
            case ProtectionButton.B:
                statePath = playerCastRightStatePath;
                break;
            case ProtectionButton.Y:
            case ProtectionButton.A:
                statePath = playerCastSpecialStatePath;
                break;
            default:
                statePath = playerCastSpecialStatePath;
                break;
        }

        if (string.IsNullOrEmpty(statePath)) return;
        if (_playerAnimator.layerCount <= playerUpperBodyLayer) return;

        _playerAnimator.SetLayerWeight(playerUpperBodyLayer, 1f);
        _playerAnimator.Play(statePath, playerUpperBodyLayer, 0f);

        if (_playerCastRoutine != null)
            StopCoroutine(_playerCastRoutine);
        _playerCastRoutine = StartCoroutine(FadeOutPlayerCastLayer());
    }

    private IEnumerator FadeOutPlayerCastLayer()
    {
        yield return new WaitForSeconds(0.2f);

        if (_playerAnimator == null || _playerAnimator.layerCount <= playerUpperBodyLayer)
            yield break;

        float elapsed = 0f;
        float startWeight = _playerAnimator.GetLayerWeight(playerUpperBodyLayer);
        float fadeDuration = Mathf.Max(0.01f, playerCastLayerFadeOut);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_playerAnimator == null) yield break;

            float t = Mathf.Clamp01(elapsed / fadeDuration);
            _playerAnimator.SetLayerWeight(playerUpperBodyLayer, Mathf.Lerp(startWeight, 0f, t));
            yield return null;
        }

        if (_playerAnimator != null && _playerAnimator.layerCount > playerUpperBodyLayer)
            _playerAnimator.SetLayerWeight(playerUpperBodyLayer, 0f);

        _playerCastRoutine = null;
    }

    private void UpdateObjectiveUI()
    {
        EnsureObjectiveTextReference();
        if (objectiveText == null) return;

        if (_waitingForKingdomExit) return;

        if (!HasProtectionObjectiveConfigured)
        {
            objectiveText.text = string.Empty;
            return;
        }

        int required = RequiredProtectCount;
        objectiveText.text = string.Format(Loc(objectiveFormat), Mathf.Min(protectedCount, required), required);
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

        DisableMinigameActionRestrictions();
        HideAllProtectionIcons();
        RestoreTargetNpcSystemsAfterMinigame();
        DisposeRuntimeIconTemplates();
    }

    void Update()
    {
        if (!isRunning && !isCountingDown) return;

        if (GamepadInputReader.StartPressed)
        {
            AbortMinigame();
            return;
        }

        if (!isRunning) return;

        remainingTime -= Time.deltaTime;

        if (HasProtectionObjectiveConfigured)
        {
            UpdateProtectionObjectiveGameplay();

            if (!isRunning)
                return;
        }

        UpdateTimerUI();
        UpdateObjectiveUI();

        if (remainingTime <= 0f)
        {
            // Pierde por tiempo si: no protegió todos los NPCs, O si aún esperaba salir del reino
            if (HasProtectionObjectiveConfigured && (protectedCount < RequiredProtectCount || _waitingForKingdomExit))
            {
                _waitingForKingdomExit = false;
                LoseByTimeout();
            }
            else
            {
                WinMinigame();
            }
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
        if (isRunning || isCountingDown || _waitingForStart)
        {
            Debug.LogWarning("[TagMinigame] Ya está en ejecución.");
            return;
        }

        // No arrancar si el minijuego ya fue superado y está guardado
        if (IsAlreadyCompleted())
        {
            Debug.Log($"[TagMinigame] Minijuego '{minigameId}' ya completado, se omite.");
            return;
        }

        if (requiresWill)
            WillOnlyMomentManager.Instance?.EnterMoment(minigameId);

        if (player == null && PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
        {
            player = playerGo.transform;
        }
        CachePlayerRuntimeReferences();
        
        // ✅ Resolver referencias del NPC perseguidor si no están asignadas
        ResolveNPCReferences();
        EnsureObjectiveTextReference();
        TryAutoPopulateProtectionTargets();

        // ✅ Log de diagnóstico
        Debug.Log($"[TagMinigame] ========== DIAGNÓSTICO ==========");
        Debug.Log($"[TagMinigame] chaserNPC: {(chaserNPC != null ? chaserNPC.name : "NULL")}");
        Debug.Log($"[TagMinigame] chaser (ChaserAI): {(chaser != null ? chaser.name : "NULL")}");
        Debug.Log($"[TagMinigame] player: {(player != null ? player.name : "NULL")}");
        Debug.Log($"[TagMinigame] _chaserAnimator: {(_chaserAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] _chaserNpcAnimator: {(_chaserNpcAnimator != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] angerVFXPrefab: {(angerVFXPrefab != null ? "✅" : "❌")}");
        Debug.Log($"[TagMinigame] =================================");

        _minigameSceneName = gameObject.scene.name;
        Debug.Log($"[TagMinigame] Iniciando minijuego '{minigameId}'...");
        catchCount = 0;
        protectedCount = 0;

        int availableProtectTargets = CountValidProtectTargets();
        int requiredForRound = RequiredProtectCount;
        Debug.Log($"[TagMinigame] 🛡️ Objetivo protección: disponibles={availableProtectTargets}, requeridos={requiredForRound}, configurado={requiredProtectedCount}");

        if (availableProtectTargets == 0 && !_loggedMissingProtectionConfig)
        {
            Debug.LogWarning("[TagMinigame] ⚠️ No hay NPCs válidos en protectTargets. No se activará la mecánica de proteger NPCs hasta configurar/auto-detectar objetivos.");
            _loggedMissingProtectionConfig = true;
        }

        if (requiredProtectedCount > availableProtectTargets && availableProtectTargets > 0)
        {
            Debug.LogWarning($"[TagMinigame] Solo hay {availableProtectTargets} NPCs configurados para proteger. El objetivo real será {RequiredProtectCount}.");
        }

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

        StartCoroutine(ShowInstructionsThenStart());
    }

    private IEnumerator ShowInstructionsThenStart()
    {
        if (instructionPanel == null)
        {
            yield return StartCoroutine(StartWithCountdown());
            yield break;
        }

        _waitingForStart = true;
        bool userAborted = false;

        // Activar el modo Minigame ya desde las instrucciones para que el menú de pausa no se abra con Start
        EnableMinigameActionRestrictions();

        // Bloquear movimiento mientras se leen las instrucciones
        bool instructionMoveLocked = false;
        if (_modeOwnerActionManager != null)
        {
            _modeOwnerActionManager.PushMode(ActionMode.Inventory);
            instructionMoveLocked = true;
        }

        // Si el panel es hijo de un objeto desactivado (ej: uiContainer), activar el padre
        // y limpiar los textos de juego para que no se vean durante las instrucciones
        bool uiContainerWasActive = uiContainer != null && uiContainer.activeSelf;
        if (uiContainer && !instructionPanel.activeInHierarchy)
        {
            uiContainer.SetActive(true);
            if (timerText) timerText.text = "";
            if (countdownText) countdownText.text = "";
            if (objectiveText) objectiveText.text = "";
        }

        instructionPanel.SetActive(true);
        if (instructionText)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TagMinigame] instructionText — spriteAsset: {(instructionText.spriteAsset != null ? instructionText.spriteAsset.name : "null (hereda de font/TMP_Settings)")} | fontSize: {instructionText.fontSize} | autoSize: {instructionText.enableAutoSizing} (min:{instructionText.fontSizeMin} max:{instructionText.fontSizeMax})");
#endif
            instructionText.text = Loc(instructionMessage);
        }
        if (startPromptText) startPromptText.text = string.Empty;

        // Conectar el botón de salir si existe
        if (exitButton) exitButton.onClick.AddListener(() => RequestAbortWithConfirmation(() => userAborted = true));

        // Un frame de gracia para que el input que abrió el diálogo no cuente
        yield return null;

        while (!GamepadInputReader.JumpPressed && !GamepadInputReader.SubmitPressed && !userAborted)
        {
            if (GamepadInputReader.StartPressed)
                RequestAbortWithConfirmation(() => userAborted = true);
            yield return null;
        }

        if (exitButton) exitButton.onClick.RemoveAllListeners();
        instructionPanel.SetActive(false);
        if (!uiContainerWasActive && uiContainer) uiContainer.SetActive(false);

        // Restaurar movimiento al salir de las instrucciones
        if (instructionMoveLocked && _modeOwnerActionManager != null)
            _modeOwnerActionManager.PopMode(ActionMode.Inventory);

        if (userAborted)
        {
            DoAbort();
            yield break;
        }

        _waitingForStart = false;
        yield return StartCoroutine(StartWithCountdown());
    }

    public void AbortMinigame()
    {
        bool wasActive = _waitingForStart || isRunning || isCountingDown;
        if (!wasActive) return;

        RequestAbortWithConfirmation(DoAbort);
    }

    private void RequestAbortWithConfirmation(System.Action onConfirmed)
    {
        if (_isWaitingForAbortConfirmation) return;
        _isWaitingForAbortConfirmation = true;

        var popup = ConfirmationPopupUI.Instance;
        if (popup != null)
        {
            popup.Show(
                Loc("CONFIRM_MINIGAME_ABORT"),
                onConfirm: () => { _isWaitingForAbortConfirmation = false; onConfirmed?.Invoke(); },
                onCancel:  () => { _isWaitingForAbortConfirmation = false; }
            );
        }
        else
        {
            Debug.LogWarning("[TagMinigame] ConfirmationPopupUI.Instance es null — ConfirmationPopupUI no está en la escena Start.unity. Abortando sin confirmación.");
            _isWaitingForAbortConfirmation = false;
            onConfirmed?.Invoke();
        }
    }

    private void DoAbort()
    {
        StopMinigame();

        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
            signals.RaiseCustom($"MINIGAME_ABORTED:{minigameId}", name);

        OnMinigameAborted?.Invoke();

        if (GameBootService.IsPresetOverrideActive)
            GameBootService.ReloadTestPreset();
        else
        {
            var bootProfile = GameBootService.Profile;
            var saveSystem = GameBootService.SaveSystem;
            if (bootProfile != null && saveSystem != null)
                bootProfile.LoadProfile(saveSystem);
        }
        Time.timeScale = 1f;
        SceneTransitionLoader.Load(_minigameSceneName);
    }

    // Resetea el estado en memoria al último guardado y recarga la escena.
    // Se llama cuando el jugador pierde (atrapado o tiempo agotado).
    // Usa SceneTransitionLoader.Load (igual que DoAbort) para que el ciclo de boot
    // completo se ejecute y PlayerParty restaure el party desde el preset con los
    // NPCs nuevos ya en escena — el reload aditivo de ConfirmationPopupUI disparaba
    // OnProfileReady antes de que los nuevos NPCs existieran, dejando a Estela fuera del party.
    private void ReloadSceneFromLastSave()
    {
        string sceneName = _minigameSceneName;
        StopMinigame();

        if (GameBootService.IsPresetOverrideActive)
        {
            GameBootService.ReloadTestPreset();
        }
        else
        {
            var bootProfile = GameBootService.Profile;
            var saveSystem = GameBootService.SaveSystem;
            if (bootProfile != null && saveSystem != null)
                bootProfile.LoadProfile(saveSystem);
        }

        Time.timeScale = 1f;
        SceneTransitionLoader.Load(sceneName);
    }

    private IEnumerator StartWithCountdown(bool isRestart = false)
    {
        isCountingDown = true;
        EnableMinigameActionRestrictions();
        SuspendTargetNpcSystemsForMinigame();

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
        if (exitHintText) exitHintText.text = Loc(exitHintMessage);
        UpdateObjectiveUI();

        InitializeProtectionTargetsForRound(clearShields: true);
        ResetPositions();
        
        // ✅ Emitir señal de inicio AHORA para que la música comience desde la cuenta atrás
        // En reinicios, esto permite que la música continue o se reinicie
        RaiseStartSignal();
        if (!isRestart)
        {
            StartCoroutine(PlayEstelaIntroCameraBeat());
            FeedbackService.CameraShake(introShakeIntensity, introShakeDuration);
        }
        
        // ✅ Activar efectos de enfado durante la cuenta atrás
        StartAngerEffects();

        // Durante el countdown: la tarea aparece donde estaba el 3,2,1
        // y el número aparece donde estaba el timer (arriba)
        if (countdownText) countdownText.text = Loc(countdownTaskDescription);

        float countdown = countdownBeforeStart;
        while (countdown > 0)
        {
            // El número de cuenta atrás ocupa la posición del timer
            if (timerText) timerText.text = Mathf.CeilToInt(countdown).ToString();

            // Incrementar efecto de enfado progresivamente
            float angerProgress = 1f - (countdown / countdownBeforeStart);
            UpdateAngerIntensity(angerProgress);

            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        // Restaurar el timer en su posición al terminar la cuenta atrás
        if (timerText) timerText.text = FormatTime(duration);
        if (countdownText) countdownText.text = "";
        
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
            FeedbackService.CameraShake(chaseStartShakeIntensity, chaseStartShakeDuration);
            
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
            signals.RaiseCustom(eventKey, name);
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
            // ✅ FIX: Forzar una salida limpia del estado actual ANTES de deshabilitar.
            // Si se deja el componente activo hasta este punto, puede seguir en
            // FollowPlayerState (u otro estado) que dejó el NavMeshAgent con
            // updatePosition=false (p.ej. Estela parada junto al jugador). Al hacer
            // "enabled = false" directamente, Unity NO llama a OnExit() del estado
            // actual, y ese flag queda congelado: ChaserAI.StartChasing() cree que
            // persigue (isChasing=true, destino fijado) pero el Transform nunca se
            // actualiza → el NPC se queda "pillado" sin moverse visualmente.
            // ForceIdle() pasa por NPCBrain.ChangeState(), que sí invoca OnExit()
            // del estado saliente y deja el agente en un estado limpio y conocido.
            chaserNPC.ForceIdle();
            chaserNPC.enabled = false;
            Debug.Log($"[TagMinigame] 🔧 NPCBehaviourManagerV2 de '{chaserNPC.name}' deshabilitado para el minijuego (tras ForceIdle)");
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
        _waitingForStart = false;
        _isWaitingForAbortConfirmation = false;
        isTeleporting = false;
        if (instructionPanel) instructionPanel.SetActive(false);

        if (requiresWill)
            WillOnlyMomentManager.Instance?.ExitMoment(minigameId);

        // ✅ Limpiar efectos visuales
        StopAngerEffects();

        if (chaser) chaser.StopChasing();
        ReleaseIntroCameraLock();
        if (uiContainer) uiContainer.SetActive(false);
        if (countdownText) countdownText.text = string.Empty;
        if (exitHintText) exitHintText.text = string.Empty;

        HideAllProtectionIcons();
        RestoreTargetNpcSystemsAfterMinigame();
        if (clearShieldsOnStop)
            ClearAllProtectionShields();
        
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

        DisableMinigameActionRestrictions();
        UpdateObjectiveUI();

        Debug.Log("[TagMinigame] Minijuego detenido.");
    }

    private void OnCaught()
    {
        if (!isRunning) return;

        catchCount++;
        Debug.Log($"[TagMinigame] ¡Jugador atrapado! (Vez #{catchCount})");

        FeedbackService.CameraShake(caughtShakeIntensity, caughtShakeDuration);
        FeedbackService.ScreenFlash(caughtFlashColor, caughtFlashDuration);
        ReleaseIntroCameraLock();
        OnPlayerCaught?.Invoke();

        StartCoroutine(RestartAfterCaught());
    }

    private IEnumerator RestartAfterCaught()
    {
        isRunning = false;
        isTeleporting = false;

        // FIX: a diferencia de StopMinigame() y WinMinigame(), esta ruta de fallo recargaba
        // la escena sin liberar el bloqueo de WillOnlyMomentManager. Si el minijuego requería
        // Will (requiresWill=true), el cambio de personaje quedaba bloqueado de forma
        // PERMANENTE para el resto de la sesión — incluso tras recargar partida, porque
        // WillOnlyMomentManager es un singleton persistente (DontDestroyOnLoad) y nada volvía
        // a llamar ExitMoment() para este momento. Bug de referencia: tras ser "atrapado" en un
        // minijuego con requiresWill, el DPad de cambio de personaje dejaba de responder para
        // siempre, incluso en partidas totalmente distintas cargadas después.
        if (requiresWill)
            WillOnlyMomentManager.Instance?.ExitMoment(minigameId);

        if (chaser) chaser.StopChasing();
        HideAllProtectionIcons();

        yield return new WaitForSeconds(1f);

        yield return FeedbackService.ScreenFadeAsync(Color.black, 0.5f, fadeIn: true);

        Debug.Log($"[TagMinigame] 🔄 Reiniciando desde el último guardado (atrapado #{catchCount})");
        ReloadSceneFromLastSave();
    }

    private void LoseByTimeout()
    {
        if (!isRunning) return;

        isRunning = false;
        isTeleporting = false;

        // FIX: mismo leak que en RestartAfterCaught (ver comentario ahí) — liberar el momento
        // Will-only también en la derrota por tiempo agotado.
        if (requiresWill)
            WillOnlyMomentManager.Instance?.ExitMoment(minigameId);

        if (chaser) chaser.StopChasing();

        OnMinigameLost?.Invoke();

        StartCoroutine(RestartAfterTimeout());
    }

    private IEnumerator RestartAfterTimeout()
    {
        HideAllProtectionIcons();

        yield return new WaitForSeconds(1f);

        yield return FeedbackService.ScreenFadeAsync(Color.black, 0.5f, fadeIn: true);

        Debug.Log("[TagMinigame] 🔄 Reiniciando desde el último guardado (tiempo agotado)");
        ReloadSceneFromLastSave();
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

    /// <summary>
    /// Resuelve una clave de traducción. Si no hay LocalizationManager devuelve la clave como fallback.
    /// </summary>
    private string Loc(string key) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, key) : key;

    /// <summary>
    /// Devuelve true si el minijuego ya fue completado y está guardado en el save.
    /// </summary>
    public bool IsAlreadyCompleted()
    {
        var preset = GameBootService.IsAvailable ? GameBootService.Profile?.GetActivePresetResolved() : null;
        if (preset?.completedInteractiveNarratives == null) return false;
        return preset.completedInteractiveNarratives.Contains(minigameId);
    }

    /// <summary>
    /// Registra el minijuego como completado en el preset activo (persiste al guardar).
    /// </summary>
    private void MarkAsCompleted()
    {
        var preset = GameBootService.IsAvailable ? GameBootService.Profile?.GetActivePresetResolved() : null;
        if (preset == null)
        {
            Debug.LogWarning("[TagMinigame] No se pudo registrar la victoria: preset no disponible.");
            return;
        }
        preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
        if (!preset.completedInteractiveNarratives.Contains(minigameId))
        {
            preset.completedInteractiveNarratives.Add(minigameId);
            Debug.Log($"[TagMinigame] ✅ Victoria registrada en save: '{minigameId}'");
        }
    }

    private void WinMinigame()
    {
        isRunning = false;
        isTeleporting = false;
        if (requiresWill)
            WillOnlyMomentManager.Instance?.ExitMoment(minigameId);
        Debug.Log($"[TagMinigame] ¡Victoria! Objetivo completado.");

        // Persistir la victoria para que no vuelva a arrancar al cargar partida
        MarkAsCompleted();

        if (chaser) chaser.StopChasing();
        ReleaseIntroCameraLock();

        HideAllProtectionIcons();
        RestoreTargetNpcSystemsAfterMinigame();
        if (clearShieldsOnWin)
            ClearAllProtectionShields();
        
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

        // ✅ Reunir a todos los miembros del party junto al jugador inmediatamente
        if (Game.NPC.PlayerParty.HasInstance)
            Game.NPC.PlayerParty.Instance.TeleportAllMembersToPlayer();

        DisableMinigameActionRestrictions();
        UpdateObjectiveUI();

        OnMinigameWon?.Invoke();

        RaiseWinSignal();

        StartCoroutine(HideUIAfterDelay(3f));
    }

    /// <summary>
    /// Muestra el countdown "¡Sal del Reino!" y espera a que el jugador cruce el trigger de salida.
    /// La victoria no se registra hasta que ocurra.
    /// </summary>
    private IEnumerator WaitForKingdomExit()
    {
        _waitingForKingdomExit = true;

        if (objectiveText)
            objectiveText.text = Loc(escapeMessage);

        float blinkTimer = 0f;
        bool visible = true;
        while (_waitingForKingdomExit && remainingTime > 0f)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.5f)
            {
                blinkTimer = 0f;
                visible = !visible;
                if (objectiveText) objectiveText.enabled = visible;
            }
            yield return null;
        }

        if (objectiveText) objectiveText.enabled = true;
    }

    /// <summary>
    /// Llamado por MinigameExitTrigger cuando el jugador cruza la zona de salida del reino.
    /// Si el minijuego está esperando la salida, completa la victoria.
    /// </summary>
    public void PlayerExitedKingdom()
    {
        if (!_waitingForKingdomExit) return;
        _waitingForKingdomExit = false;
        WinMinigame();
    }

    private IEnumerator EscapeCountdownAfterWin()
    {
        yield return new WaitForSeconds(3f);

        float countdown = Mathf.Max(1f, escapeCountdownDuration);
        while (countdown > 0f)
        {
            if (timerText) timerText.text = Mathf.CeilToInt(countdown).ToString();
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        if (timerText)   timerText.text   = "";
        if (uiContainer) uiContainer.SetActive(false);
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
            signals.RaiseCustom(eventKey, name);
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

    private IEnumerator PlayEstelaIntroCameraBeat()
    {
        if (!enableEstelaIntroCameraBeat || chaser == null)
            yield break;

        if (introCamera == null)
            PlayerService.TryGetComponent(out introCamera, includeInactive: true, allowSceneLookup: true);

        if (introCamera == null)
            yield break;

        if (introCameraBeatDelay > 0f)
            yield return new WaitForSeconds(introCameraBeatDelay);

        introCamera.SetLockTarget(chaser.transform);

        float hold = Mathf.Max(0.05f, introCameraBeatDuration);
        yield return new WaitForSeconds(hold);

        ReleaseIntroCameraLock();
    }

    private void ReleaseIntroCameraLock()
    {
        if (introCamera != null)
            introCamera.ClearLockTarget();
    }
}
