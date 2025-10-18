using System.Collections;
using System.Collections.Generic;
using Alex.NPC.Common;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
[System.Obsolete("Usa NPCBehaviourManager para configurar ambientación, misiones y retos.")]
public class NPCAmbientBrain : MonoBehaviour
{
    [Header("Animator & Locomotion")]
    [SerializeField] private Animator animator;
    [SerializeField] private string locomotionState = "Base Layer.Locomotion.Free Locomotion";
    [SerializeField] private string locomotionParam = "InputMagnitude";
    [SerializeField] private bool useRootMotion = false;

    [Header("Clips Ambientales (nombres exactos)")]
    public string greetState = "Greeting01_NoWeapon";
    public string drinkState = "DrinkPotion_NoWeapon";
    public string sleepState = "Sleeping_NoWeapon";
    public string lookAroundState = "SenseSomethingSearching_NoWeapon";
    public string foundSomething = "FoundSomething_NoWeapon";
    public string interactPeople = "InteractWithPeople_NoWeapon";
    public string danceState = "Dance_NoWeapon";
    public string dizzyState = "Dizzy_NoWeapon";
    public string celebrateState = "LevelUp_NoWeapon";

    [Header("Wander")]
    public float wanderRadius = 8f;
    [Min(0f)] public float minIdleBeforeMove = 1.0f;
    [Min(0f)] public float maxIdleBeforeMove = 3.0f;

    [Header("Agente")]
    public float overrideAgentSpeed = 0f;

    [Header("Planificador de Acciones")]
    [Range(0f, 1f)] public float actionChance = 0.55f;
    public Vector2 loopActionDuration = new Vector2(4f, 10f);
    public float maxPointSearchDist = 25f;

    [Header("Puntos de Acción (opcional)")]
    public List<NPCActionPoint> actionPoints = new List<NPCActionPoint>();

    [Header("Saludo al jugador (opcional)")]
    public bool greetOnSight = true;
    public float greetRadius = 3f;
    [Range(1f, 180f)] public float fovDegrees = 110f;
    public float greetCooldown = 4f;

    [Header("IK Mirar al jugador")]
    public bool useIKLookAt = true;
    [Range(0f, 1f)] public float lookAtWeight = 0.6f;
    [Range(0f, 1f)] public float bodyWeight = 0.1f, headWeight = 0.9f, eyesWeight = 0.7f;

    [Header("Interacción con jugador")]
    [SerializeField] private bool rotateToPlayerOnInteract = true;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Player Override (opcional)")]
    [SerializeField] private Transform playerOverride;
    [SerializeField] private Transform playerCameraOverride;

    [Header("Modo Retador (tipo Pokémon)")]
    public bool challengerMode = false;
    public float challengeSightRadius = 10f;
    public float challengeStopDistance = 2.2f;
    public float approachRepathInterval = 0.25f;
    public float loseSightGraceSeconds = 1.5f;

    [Tooltip("Animación breve cuando detecta al jugador (antes de avanzar).")]
    public string challengeAlertState = "FoundSomething_NoWeapon";
    public float challengeAlertMinSeconds = 0.75f;

    [Tooltip("Anim al llegar/retar (se reproduce a la vez que se abre el diálogo).")]
    public string challengeState = "Challenging_NoWeapon";

    [Tooltip("Se muestra al verte. Usa tu prefab con anim propia (billboard/UI).")]
    public GameObject exclamationPrefab;
    public Vector3 exclamationOffset = new Vector3(0, 2.0f, 0);
    public float exclamationSeconds = 2f;

    [Tooltip("Durante el reto bloquea el movimiento del jugador (simula que 'A' le para).")]
    public bool lockPlayerDuringChallenge = true;

    [TextArea] public string fallbackDialogue = "¡Te reto!";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Header("Eventos de integración")]
    public UnityEvent OnChallengeStarted;
    public UnityEvent OnPlayerLockRequest;
    public UnityEvent OnPlayerUnlockRequest;
    public StringEvent OnDialogueRequest;

    NavMeshAgent _agent;
    Transform _player;
    Transform _playerCam;
    Interactable _interactable;

    AnimatorStateCache _stateCache;
    AnimatorClipCache _clipCache;
    readonly Dictionary<string, string> _stateNameCache = new();

    readonly List<AmbientAction> _actionPool = new();

    int _inputMagHash;
    bool _running;
    bool _greetOnCooldown;
    bool _isInteracting;
    bool _isChallenging;
    bool _playerInSight;

    Coroutine _brainRoutine;
    Coroutine _faceRoutine;
    Coroutine _challengeRoutine;

    static readonly Dictionary<string, string> _aliases = new()
    {
        { "FoundSom_NoWeapon", "FoundSomething_NoWeapon" },
        { "SenseSome_NoWeapon", "SenseSomethingSearching_NoWeapon" },
        { "SenseSome", "SenseSomethingSearching_NoWeapon" },
        { "FoundSom", "FoundSomething_NoWeapon" }
    };

    enum AmbientAction
    {
        Greet,
        Drink,
        Sleep,
        LookAround,
        Found,
        InteractPeople,
        Dance,
        Dizzy,
        Celebrate
    }

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        animator ??= GetComponentInChildren<Animator>(true);

        if (overrideAgentSpeed > 0f)
            _agent.speed = overrideAgentSpeed;

        if (animator != null)
        {
            animator.applyRootMotion = useRootMotion;
            _stateCache = new AnimatorStateCache(animator);
            _clipCache = new AnimatorClipCache(animator);
            _inputMagHash = Animator.StringToHash(locomotionParam);
            PreloadStates();
        }

        PlayerService.OnPlayerRegistered += HandlePlayerRegistered;
        PlayerService.OnPlayerUnregistered += HandlePlayerUnregistered;

        ResolvePlayerReferences();

        if (actionPoints.Count == 0)
            PopulateNearbyActionPoints();

        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
        {
            _interactable.OnStarted.AddListener(BeginInteraction);
            _interactable.OnFinished.AddListener(EndInteraction);
        }
    }

    void Start()
    {
        GoLocomotion();
        if (animator != null)
            animator.SetFloat(_inputMagHash, 0f);
    }

    void OnEnable()
    {
        StartBrain();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _running = false;
        _isChallenging = false;
        _isInteracting = false;
        _brainRoutine = null;
        _challengeRoutine = null;
        _faceRoutine = null;
        NavMeshAgentUtility.SafeSetStopped(_agent, true);
    }

    void OnDestroy()
    {
        PlayerService.OnPlayerRegistered -= HandlePlayerRegistered;
        PlayerService.OnPlayerUnregistered -= HandlePlayerUnregistered;
    }

    void Update()
    {
        if (animator != null && _agent != null)
        {
            float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
            animator.SetFloat(_inputMagHash, speed, 0.1f, Time.deltaTime);
        }

        if (_isChallenging || _isInteracting)
            return;

        if (_player == null)
        {
            ResolvePlayerReferences();
            if (_player == null)
                DebugLog("Player sigue sin resolverse; aguardando siguiente frame.");
        }

        if (greetOnSight && !_greetOnCooldown && _player && PlayerInFOV(greetRadius, "Greet"))
            StartCoroutine(CoGreet());

        if (challengerMode && !_isChallenging && _player && PlayerInFOV(challengeSightRadius, "Challenge"))
        {
            if (_challengeRoutine == null)
                _challengeRoutine = StartCoroutine(CoChallengeFlow());
        }
    }

    void StartBrain()
    {
        if (_running)
            return;

        _running = true;
        _brainRoutine = StartCoroutine(CoBrain());
    }

    IEnumerator CoBrain()
    {
        yield return new WaitForSeconds(Random.Range(0f, 0.6f));

        while (_running && isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Random.Range(minIdleBeforeMove, maxIdleBeforeMove));

            if (_isInteracting || _isChallenging)
            {
                yield return null;
                continue;
            }

            if (Random.value < actionChance && TryPickAction(out var action))
                yield return ExecuteAction(action);
            else
                yield return WanderOnce();

            yield return null;
        }
    }

    IEnumerator WanderOnce()
    {
        if (_agent == null)
            yield break;

        if (!NavMeshAgentUtility.EnsureAgentOnNavMesh(_agent, transform.position, wanderRadius))
            yield break;

        if (!NavMeshAgentUtility.TryGetRandomPoint(transform.position, wanderRadius, out var dest))
            yield break;

        NavMeshAgentUtility.SetDestination(_agent, dest);

        while (ShouldContinueWalking())
            yield return null;

        NavMeshAgentUtility.SafeSetStopped(_agent, true);
    }

    bool ShouldContinueWalking()
    {
        return isActiveAndEnabled &&
               _agent != null &&
               _agent.isOnNavMesh &&
               !_agent.pathPending &&
               _agent.remainingDistance > _agent.stoppingDistance + 0.1f;
    }

    bool TryPickAction(out AmbientAction action)
    {
        _actionPool.Clear();

        AddActionIfValid(AmbientAction.Greet, greetState);
        AddActionIfValid(AmbientAction.Drink, drinkState);
        AddActionIfValid(AmbientAction.Sleep, sleepState);
        AddActionIfValid(AmbientAction.LookAround, lookAroundState);
        AddActionIfValid(AmbientAction.Found, foundSomething);
        AddActionIfValid(AmbientAction.InteractPeople, interactPeople);
        AddActionIfValid(AmbientAction.Dance, danceState);
        AddActionIfValid(AmbientAction.Dizzy, dizzyState);
        AddActionIfValid(AmbientAction.Celebrate, celebrateState);

        if (_actionPool.Count == 0)
        {
            action = default;
            return false;
        }

        action = _actionPool[Random.Range(0, _actionPool.Count)];
        return true;
    }

    void AddActionIfValid(AmbientAction action, string stateName)
    {
        stateName = ResolveStateName(stateName);
        if (string.IsNullOrEmpty(stateName))
            return;

        _actionPool.Add(action);

        if (TryFindPointFor(action, out _))
            _actionPool.Add(action); // pequeño sesgo
    }

    IEnumerator ExecuteAction(AmbientAction action)
    {
        if (_isInteracting || _isChallenging)
            yield break;

        NPCActionPoint point = null;
        if (TryFindPointFor(action, out var candidate))
            point = candidate;

        if (point != null)
        {
            yield return MoveTo(point.transform.position, point.arriveRadius);
            FaceToward(point.transform.position, 0.25f);
        }

        var state = ResolveStateName(GetStateFor(action));
        if (!string.IsNullOrEmpty(state))
        {
            if (!CrossFade(state, 0.08f))
                Debug.LogWarning($"[NPCAmbientBrain] No se encontró el estado '{state}'.", this);

            if (IsLoopingAction(action))
            {
                float target = Random.Range(loopActionDuration.x, loopActionDuration.y);
                float elapsed = 0f;
                while (elapsed < target)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                float len = _clipCache?.GetLength(state) ?? 0f;
                yield return new WaitForSeconds(len > 0f ? len : 1f);
            }
        }

        GoLocomotion();
    }

    IEnumerator MoveTo(Vector3 position, float arriveRadius)
    {
        if (_agent == null)
            yield break;

        if (!NavMeshAgentUtility.EnsureAgentOnNavMesh(_agent, transform.position, wanderRadius))
            yield break;

        NavMeshAgentUtility.SetDestination(_agent, position, arriveRadius > 0f ? Mathf.Min(arriveRadius, 1f) : _agent.stoppingDistance);

        while (isActiveAndEnabled &&
               _agent != null &&
               _agent.isOnNavMesh &&
               !_agent.pathPending &&
               _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, arriveRadius) + 0.05f)
        {
            yield return null;
        }

        NavMeshAgentUtility.SafeSetStopped(_agent, true);
    }

    // ===== Interacción social =====
    public void BeginInteraction()
    {
        if (_isInteracting || _isChallenging)
            return;

        _isInteracting = true;
        NavMeshAgentUtility.SafeSetStopped(_agent, true);

        if (rotateToPlayerOnInteract && _player)
        {
            StopFacing();
            _faceRoutine = StartCoroutine(FaceTarget(_player));
        }

        if (!string.IsNullOrEmpty(interactPeople))
        {
            if (animator != null && animator.layerCount > 1)
                animator.SetLayerWeight(1, 1f);
            CrossFade(interactPeople, 0.1f);
        }
    }

    public void EndInteraction()
    {
        if (!_isInteracting)
            return;

        _isInteracting = false;
        StopFacing();

        if (animator != null && animator.layerCount > 1)
            animator.SetLayerWeight(1, 0f);

        GoLocomotion();
    }

    IEnumerator FaceTarget(Transform target)
    {
        while ((_isInteracting || _isChallenging) && target)
        {
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * rotateSpeed);
            }
            yield return null;
        }
    }

    void StopFacing()
    {
        if (_faceRoutine != null)
            StopCoroutine(_faceRoutine);
        _faceRoutine = null;
    }

    // ===== Saludo =====
    IEnumerator CoGreet()
    {
        _greetOnCooldown = true;

        if (CrossFade(greetState, 0.08f))
        {
            float len = _clipCache?.GetLength(ResolveStateName(greetState)) ?? 0f;
            yield return new WaitForSeconds(len > 0f ? len : 1f);
            GoLocomotion();
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(greetCooldown);
        _greetOnCooldown = false;
    }

    // ===== Reto =====
    IEnumerator CoChallengeFlow()
    {
        if (_isChallenging || _player == null)
            yield break;

        DebugLog("Entrando en CoChallengeFlow");

        _isChallenging = true;

        GameObject ex = null;
        if (exclamationPrefab)
            ex = Instantiate(exclamationPrefab, transform.position + exclamationOffset, Quaternion.identity, transform);

        if (lockPlayerDuringChallenge)
            OnPlayerLockRequest?.Invoke();

        NavMeshAgentUtility.SafeSetStopped(_agent, true);
        StopFacing();
        if (_player)
            _faceRoutine = StartCoroutine(FaceTarget(_player));

        float alertDuration = Mathf.Max(0f, challengeAlertMinSeconds);
        if (CrossFade(challengeAlertState, 0.08f))
        {
            float clipLen = _clipCache?.GetLength(ResolveStateName(challengeAlertState)) ?? 0f;
            if (clipLen > 0f)
                alertDuration = Mathf.Max(alertDuration, clipLen);
        }

        if (alertDuration <= 0f)
            alertDuration = 0.6f;

        float alertTimer = 0f;
        float lostTimer = 0f;
        while (alertTimer < alertDuration)
        {
            if (_player == null)
                break;

            if (!PlayerInFOV(challengeSightRadius))
            {
                lostTimer += Time.deltaTime;
                if (lostTimer >= loseSightGraceSeconds)
                    break;
            }
            else
            {
                lostTimer = 0f;
            }

            alertTimer += Time.deltaTime;
            yield return null;
        }

        if (_player == null || lostTimer >= loseSightGraceSeconds)
        {
            if (ex) Destroy(ex);
            if (lockPlayerDuringChallenge)
                OnPlayerUnlockRequest?.Invoke();
            DebugLog("Challenge cancelado: jugador fuera de vista tras alerta.");
            CleanupChallenge();
            yield break;
        }

        GoLocomotion();
        if (animator != null && animator.layerCount > 1)
            animator.SetLayerWeight(1, 0f);

        float repathTimer = 0f;
        float loseSightTimer = 0f;
        float iconTimer = 0f;

        while (true)
        {
            if (_player == null)
            {
                DebugLog("Challenge cancelado: player perdido durante aproximación.");
                break;
            }

            if (ex && exclamationSeconds > 0f)
            {
                iconTimer += Time.deltaTime;
                if (iconTimer >= exclamationSeconds)
                {
                    Destroy(ex);
                    ex = null;
                }
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= challengeStopDistance)
                break;

            if (!PlayerInFOV(challengeSightRadius))
            {
                loseSightTimer += Time.deltaTime;
                if (loseSightTimer >= loseSightGraceSeconds)
                {
                    DebugLog("Challenge cancelado: jugador fuera de vista durante aproximación.");
                    break;
                }
            }
            else
            {
                loseSightTimer = 0f;
            }

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                NavMeshAgentUtility.SetDestination(_agent, _player.position, challengeStopDistance);
                repathTimer = approachRepathInterval;
            }

            yield return null;
        }

        NavMeshAgentUtility.SafeSetStopped(_agent, true);
        if (ex) Destroy(ex);

        if (animator != null && animator.layerCount > 1)
            animator.SetLayerWeight(1, 1f);

        CrossFade(challengeState, 0.1f);

        if (_interactable && _player)
        {
            _interactable.Interact(_player.gameObject);
            DebugLog("Interactable disparado, esperando cierre de diálogo.");
        }
        else if (!string.IsNullOrWhiteSpace(fallbackDialogue))
        {
            OnDialogueRequest?.Invoke(fallbackDialogue);
            DebugLog("Fallback de diálogo disparado.");
        }
        else
        {
            DebugLog("Sin diálogo asignado; finalizando challenge inmediatamente.");
        }

        yield return StartCoroutine(WaitDialogueToClose());

        OnChallengeStarted?.Invoke();
        DebugLog("OnChallengeStarted invocado.");

        if (animator != null && animator.layerCount > 1)
            animator.SetLayerWeight(1, 0f);

        CleanupChallenge();
    }

    void CleanupChallenge()
    {
        if (_faceRoutine != null)
            StopCoroutine(_faceRoutine);
        _faceRoutine = null;

        GoLocomotion();
        _isChallenging = false;
        _challengeRoutine = null;
    }

    // ===== Helpers de estados/anim =====
    bool CrossFade(string stateName, float fade)
    {
        string resolved = ResolveStateName(stateName);
        if (string.IsNullOrEmpty(resolved) || animator == null)
            return false;

        if (_stateCache != null && _stateCache.CrossFade(resolved, fade))
            return true;

        animator.CrossFadeInFixedTime(resolved, fade, 0, 0f);
        return true;
    }

    void GoLocomotion()
    {
        CrossFade(locomotionState, 0.1f);
        if (animator != null && animator.layerCount > 1)
            animator.SetLayerWeight(1, 0f);
    }

    string ResolveStateName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        if (_stateNameCache.TryGetValue(raw, out var cached))
            return cached;

        string input = raw;
        if (_aliases.TryGetValue(input, out var alias))
            input = alias;

        if (_stateCache != null && _stateCache.TryResolve(input, out _))
        {
            _stateNameCache[raw] = input;
            return input;
        }

        string fuzzy = FindClosestClipName(input);
        if (!string.IsNullOrEmpty(fuzzy))
        {
            _stateNameCache[raw] = fuzzy;
            return fuzzy;
        }

        _stateNameCache[raw] = input;
        return input;
    }

    string FindClosestClipName(string desired)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return null;

        string Normalize(string s) => s.Replace("_", "").ToLowerInvariant();
        string target = Normalize(desired);

        AnimationClip best = null;
        int bestScore = int.MaxValue;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            string name = Normalize(clip.name);
            int score = Mathf.Abs(name.Length - target.Length);
            if (name.Contains(target) || target.Contains(name))
                score = 0;
            if (score < bestScore)
            {
                bestScore = score;
                best = clip;
            }
            if (score == 0)
                break;
        }

        return best != null ? best.name : null;
    }

    void PreloadStates()
    {
        if (_stateCache == null)
            return;

        _stateCache.Preload(
            locomotionState,
            greetState,
            drinkState,
            sleepState,
            lookAroundState,
            foundSomething,
            interactPeople,
            danceState,
            dizzyState,
            celebrateState,
            challengeAlertState,
            challengeState);
    }

    // ===== Utilidades varias =====
    void PopulateNearbyActionPoints()
    {
        var all = FindObjectsOfType<NPCActionPoint>();
        foreach (var ap in all)
        {
            if (!ap) continue;
            if ((ap.transform.position - transform.position).sqrMagnitude <= maxPointSearchDist * maxPointSearchDist)
                actionPoints.Add(ap);
        }
    }

    bool TryFindPointFor(AmbientAction action, out NPCActionPoint point)
    {
        var type = GetActionType(action);
        if (type == NPCActionType.Generic)
        {
            point = null;
            return false;
        }
        return FindClosestPoint(type, out point);
    }

    bool FindClosestPoint(NPCActionType type, out NPCActionPoint best)
    {
        best = null;
        float bestSqr = float.MaxValue;
        Vector3 origin = transform.position;

        foreach (var ap in actionPoints)
        {
            if (!ap) continue;
            if (ap.type != type && type != NPCActionType.Generic) continue;

            float d = (ap.transform.position - origin).sqrMagnitude;
            if (d < bestSqr && d <= maxPointSearchDist * maxPointSearchDist)
            {
                bestSqr = d;
                best = ap;
            }
        }

        return best != null;
    }

    bool PlayerInFOV(float radius, string context = null)
    {
        if (_player == null)
            return false;

        Vector3 to = _player.position - transform.position;
        if (to.sqrMagnitude > radius * radius)
        {
            SetPlayerInSight(false, context, "fuera de radio");
            return false;
        }

        float dot = Vector3.Dot(transform.forward, to.normalized);
        float fovDot = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
        bool inside = dot >= fovDot;
        SetPlayerInSight(inside, context, inside ? "dentro de FOV" : "fuera de FOV");
        return inside;
    }

    void FaceToward(Vector3 targetPos, float lerp = 0.25f)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Mathf.Clamp01(lerp));
    }

    void ResolvePlayerReferences()
    {
        _player = playerOverride ? playerOverride : PlayerLocator.ResolvePlayer();
        _playerCam = playerCameraOverride ? playerCameraOverride : PlayerLocator.ResolvePlayerCamera();
    }

    void HandlePlayerRegistered(GameObject playerGo)
    {
        if (playerGo != null)
        {
            if (!playerOverride)
                _player = playerGo.transform;

            if (!playerCameraOverride)
                _playerCam = PlayerLocator.ResolvePlayerCamera();
        }
    }

    void HandlePlayerUnregistered()
    {
        if (!playerOverride)
            _player = null;

        if (!playerCameraOverride)
            _playerCam = null;
    }

    void SetPlayerInSight(bool value, string context, string reason)
    {
        if (_playerInSight == value)
            return;

        _playerInSight = value;
        if (string.IsNullOrEmpty(context)) return;

        DebugLog(value
            ? $"Jugador detectado ({context}) → {reason}"
            : $"Jugador perdido ({context}) → {reason}");
    }

    void DebugLog(string message)
    {
        if (!logDebug) return;
        Debug.Log($"[NPCAmbientBrain:{name}] {message}", this);
    }

    string GetStateFor(AmbientAction action) => action switch
    {
        AmbientAction.Greet => greetState,
        AmbientAction.Drink => drinkState,
        AmbientAction.Sleep => sleepState,
        AmbientAction.LookAround => lookAroundState,
        AmbientAction.Found => foundSomething,
        AmbientAction.InteractPeople => interactPeople,
        AmbientAction.Dance => danceState,
        AmbientAction.Dizzy => dizzyState,
        AmbientAction.Celebrate => celebrateState,
        _ => null
    };

    NPCActionType GetActionType(AmbientAction action) => action switch
    {
        AmbientAction.Drink => NPCActionType.DrinkSpot,
        AmbientAction.Sleep => NPCActionType.SleepSpot,
        AmbientAction.Dance => NPCActionType.DanceFloor,
        AmbientAction.InteractPeople => NPCActionType.SocialSpot,
        AmbientAction.LookAround => NPCActionType.LookSpot,
        _ => NPCActionType.Generic
    };

    bool IsLoopingAction(AmbientAction action) => action is AmbientAction.Sleep or AmbientAction.Dance;

    void OnAnimatorIK(int layerIndex)
    {
        if (!useIKLookAt || animator == null || _playerCam == null)
            return;

        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, 0.5f);
        animator.SetLookAtPosition(_playerCam.position + _playerCam.forward * 100f);
    }

    IEnumerator WaitDialogueToClose(float safetyTimeout = 60f)
    {
        var dm = DialogueManager.Instance;
        if (dm == null)
            yield break;

        float t = 0f;
        while (!dm.IsOpen && t < 2f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (dm.IsOpen && t < safetyTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}
