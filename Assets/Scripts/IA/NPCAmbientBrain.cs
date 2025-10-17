using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class NPCAmbientBrain : MonoBehaviour
{
    [Header("Animator & Locomotion")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre o RUTA COMPLETA del Blend Tree de locomoción. Ej.: \"Base Layer.Locomotion.Free Locomotion\"")]
    [SerializeField] private string locomotionState = "Base Layer.Locomotion.Free Locomotion";

    [Tooltip("Parámetro del Blend Tree (normalmente InputMagnitude)")]
    [SerializeField] private string locomotionParam = "InputMagnitude";

    [SerializeField] private bool useRootMotion = false; // normalmente false: manda el Agent

    [Header("Clips Ambientales (nombres exactos)")]
    public string greetState        = "Greeting01_NoWeapon";
    public string drinkState        = "DrinkPotion_NoWeapon";
    public string sleepState        = "Sleeping_NoWeapon";
    public string lookAroundState   = "SenseSomethingSearching_NoWeapon";
    public string foundSomething    = "FoundSomething_NoWeapon";
    public string interactPeople    = "InteractWithPeople_NoWeapon";
    public string danceState        = "Dance_NoWeapon";
    public string dizzyState        = "Dizzy_NoWeapon";
    public string celebrateState    = "LevelUp_NoWeapon";

    [Header("Wander")]
    public float wanderRadius = 8f;
    [Min(0f)] public float minIdleBeforeMove = 1.0f;
    [Min(0f)] public float maxIdleBeforeMove = 3.0f;

    [Header("Agente")]
    public float overrideAgentSpeed = 0f;

    [Header("Planificador de Acciones")]
    [Range(0f,1f)] public float actionChance = 0.55f;
    public Vector2 loopActionDuration = new Vector2(4f, 10f);
    public float maxPointSearchDist = 25f;

    [Header("Puntos de Acción (opcional)")]
    public List<NPCActionPoint> actionPoints = new List<NPCActionPoint>();

    [Header("Saludo al jugador (opcional)")]
    public bool greetOnSight = true;
    public float greetRadius = 3f;
    [Range(1f,180f)] public float fovDegrees = 110f;
    public float greetCooldown = 4f;

    [Header("IK Mirar al jugador")]
    public bool useIKLookAt = true;
    [Range(0f,1f)] public float lookAtWeight = 0.6f;
    [Range(0f,1f)] public float bodyWeight = 0.1f, headWeight = 0.9f, eyesWeight = 0.7f;

    [Header("Interacción con jugador")]
    [SerializeField] private bool rotateToPlayerOnInteract = true;
    [SerializeField] private float rotateSpeed = 10f;

    // ===== Modo RETADOR tipo Pokémon =====
    [Header("Modo Retador (tipo Pokémon)")]
    public bool challengerMode = false;
    public float challengeSightRadius = 10f;
    public float challengeStopDistance = 2.2f;
    public float approachRepathInterval = 0.25f;
    public float loseSightGraceSeconds = 1.5f;
    public string alertState = "SenseSomethingSearching_NoWeapon";
    public string challengeState = "Challenging_NoWeapon";

    [Tooltip("Texto que enviamos al sistema de diálogo al lanzar Challenging_NoWeapon.")]
    [TextArea] public string challengeDialogue = "¡Te reto a un combate!";

    [Tooltip("Durante el reto (desde que te ve) bloquea el movimiento del jugador.")]
    public bool lockPlayerDuringChallenge = true;

    [Tooltip("Prefab opcional del \"!\" sobre la cabeza al verte.")]
    public GameObject exclamationPrefab;
    public Vector3 exclamationOffset = new Vector3(0, 2.0f, 0);

    // ----- Eventos externos (para conectar sin dependencias duras) -----
    [System.Serializable] public class StringEvent : UnityEvent<string> {}
    [Header("Eventos de integración")]
    public UnityEvent OnChallengeStarted;          // ya lo tenías: lanza entrada a combate
    public UnityEvent OnPlayerLockRequest;         // bloquea input del jugador
    public UnityEvent OnPlayerUnlockRequest;       // desbloquea input del jugador
    public StringEvent OnDialogueRequest;          // abre tu UI de diálogo (recibe el texto)

    // ===== Internals =====
    NavMeshAgent _agent;
    Transform _player, _playerCam;

    int _inputMagHash;
    readonly Dictionary<string, float> _clipLen = new();

    // Resolución de estados
    readonly Dictionary<string, int> _stateHash  = new();   // nombre -> hash
    readonly Dictionary<string, int> _stateLayer = new();   // nombre -> capa

    bool _running;
    bool _greetOnCd;

    bool _isInteracting = false; // interacción “social”
    bool _isChallenging = false; // flujo Pokémon activo
    Coroutine _faceCo;

    // Aliases de nombres mal escritos → correctos
    static readonly Dictionary<string,string> _aliases = new Dictionary<string, string>()
    {
        { "FoundSom_NoWeapon", "FoundSomething_NoWeapon" },
        { "SenseSome_NoWeapon", "SenseSomethingSearching_NoWeapon" },
        { "SenseSome", "SenseSomethingSearching_NoWeapon" },
        { "FoundSom", "FoundSomething_NoWeapon" }
    };

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void OnValidate()
    {
        if (!string.IsNullOrEmpty(foundSomething) && _aliases.TryGetValue(foundSomething, out var f)) foundSomething = f;
        if (!string.IsNullOrEmpty(lookAroundState) && _aliases.TryGetValue(lookAroundState, out var l)) lookAroundState = l;
        if (!string.IsNullOrEmpty(alertState) && _aliases.TryGetValue(alertState, out var a)) alertState = a;
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (overrideAgentSpeed > 0f) _agent.speed = overrideAgentSpeed;
        if (animator) animator.applyRootMotion = useRootMotion;

        _inputMagHash = Animator.StringToHash(locomotionParam);
        CacheClipLengths();

        var pGo = GameObject.FindGameObjectWithTag("Player");
        if (pGo) _player = pGo.transform;
        if (Camera.main) _playerCam = Camera.main.transform;

        if (actionPoints.Count == 0)
        {
            var all = FindObjectsOfType<NPCActionPoint>();
            foreach (var ap in all)
                if ((ap.transform.position - transform.position).sqrMagnitude <= maxPointSearchDist * maxPointSearchDist)
                    actionPoints.Add(ap);
        }

        var interactable = GetComponent<Interactable>();
        if (interactable)
        {
            interactable.OnStarted.AddListener(BeginInteraction);
            interactable.OnFinished.AddListener(EndInteraction);
        }

        // Pre-resolver
        EnsureResolved(locomotionState);
        EnsureResolved(greetState);
        EnsureResolved(drinkState);
        EnsureResolved(sleepState);
        EnsureResolved(lookAroundState);
        EnsureResolved(foundSomething);
        EnsureResolved(interactPeople);
        EnsureResolved(danceState);
        EnsureResolved(dizzyState);
        EnsureResolved(celebrateState);
        EnsureResolved(alertState);
        EnsureResolved(challengeState);
    }

    void OnEnable()
    {
        StartBrain();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _running = false;
        SafeStop(true);
    }

    void Start()
    {
        GoLocomotion();
        if (animator) animator.SetFloat(_inputMagHash, 0f);
    }

    void Update()
    {
        // Actualiza InputMagnitude SIEMPRE (antes de returns)
        if (animator && _agent && _agent.isOnNavMesh)
        {
            float speed01 = (_agent.speed <= 0.01f) ? 0f : Mathf.Clamp01(_agent.velocity.magnitude / _agent.speed);
            animator.SetFloat(_inputMagHash, speed01, 0.1f, Time.deltaTime);
        }

        // Si está en reto o interactuando, no dispares saludos ni decisiones
        if (_isChallenging || _isInteracting) return;

        if (greetOnSight && !_greetOnCd && _player && PlayerInFOV(greetRadius))
            StartCoroutine(CoGreet());

        if (challengerMode && _player && PlayerInFOV(challengeSightRadius))
            StartCoroutine(CoChallengeFlow()); // idempotente por flag
    }

    // ====== Cerebro principal ======
    void StartBrain()
    {
        if (_running) return;
        _running = true;
        StartCoroutine(CoBrain());
    }

    IEnumerator CoBrain()
    {
        yield return new WaitForSeconds(Random.Range(0f,0.6f)); // desincroniza

        while (_running && isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Random.Range(minIdleBeforeMove, maxIdleBeforeMove));

            if (_isInteracting || _isChallenging) { yield return null; continue; }

            if (Random.value < actionChance && TryPickAction(out var act))
                yield return DoAction(act);
            else
            {
                if (TryGetRandomNavmeshPoint(transform.position, wanderRadius, out var dest))
                {
                    MoveTo(dest, 0f);
                    while (isActiveAndEnabled && _agent && _agent.isOnNavMesh && !_agent.pathPending &&
                           _agent.remainingDistance > _agent.stoppingDistance + 0.1f)
                        yield return null;
                    SafeStop(true);
                }
            }

            yield return null;
        }
    }

    // ====== Acciones ======
    public enum AmbientAction { Greet, Drink, Sleep, LookAround, Found, InteractPeople, Dance, Dizzy, Celebrate }

    bool TryPickAction(out AmbientAction action)
    {
        var pool = new List<AmbientAction>();
        AddIfHasClip(greetState, AmbientAction.Greet, pool);
        AddIfHasClip(lookAroundState, AmbientAction.LookAround, pool);
        AddIfHasClip(foundSomething, AmbientAction.Found, pool);
        AddIfHasClip(interactPeople, AmbientAction.InteractPeople, pool);
        AddIfHasClip(danceState, AmbientAction.Dance, pool);
        AddIfHasClip(dizzyState, AmbientAction.Dizzy, pool);
        AddIfHasClip(celebrateState, AmbientAction.Celebrate, pool);
        AddIfHasClip(drinkState, AmbientAction.Drink, pool);
        AddIfHasClip(sleepState, AmbientAction.Sleep, pool);

        if (pool.Count == 0) { action = default; return false; }

        WeightBiasByPoint(pool, AmbientAction.Drink, NPCActionType.DrinkSpot);
        WeightBiasByPoint(pool, AmbientAction.Sleep, NPCActionType.SleepSpot);
        WeightBiasByPoint(pool, AmbientAction.Dance, NPCActionType.DanceFloor);
        WeightBiasByPoint(pool, AmbientAction.InteractPeople, NPCActionType.SocialSpot);
        WeightBiasByPoint(pool, AmbientAction.LookAround, NPCActionType.LookSpot);

        action = pool[Random.Range(0, pool.Count)];
        return true;
    }

    void AddIfHasClip(string state, AmbientAction a, List<AmbientAction> dst)
    {
        if (!string.IsNullOrEmpty(state)) dst.Add(a);
    }

    void WeightBiasByPoint(List<AmbientAction> pool, AmbientAction a, NPCActionType t)
    {
        if (FindClosestPoint(t, out _)) pool.Add(a);
    }

    IEnumerator DoAction(AmbientAction a)
    {
        if (_isInteracting || _isChallenging) yield break;

        NPCActionPoint point = null;
        switch (a)
        {
            case AmbientAction.Drink:          FindClosestPoint(NPCActionType.DrinkSpot, out point); break;
            case AmbientAction.Sleep:          FindClosestPoint(NPCActionType.SleepSpot, out point); break;
            case AmbientAction.Dance:          FindClosestPoint(NPCActionType.DanceFloor, out point); break;
            case AmbientAction.InteractPeople: FindClosestPoint(NPCActionType.SocialSpot, out point); break;
            case AmbientAction.LookAround:     FindClosestPoint(NPCActionType.LookSpot, out point); break;
        }

        if (point)
        {
            MoveTo(point.transform.position, point.arriveRadius);
            while (isActiveAndEnabled && _agent && _agent.isOnNavMesh && !_agent.pathPending &&
                   _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, point.arriveRadius) + 0.05f)
                yield return null;
            SafeStop(true);
            FaceToward(point.transform.position, 0.25f);
        }

        string state = null; bool loopish = false;
        switch (a)
        {
            case AmbientAction.Greet:          state = greetState; break;
            case AmbientAction.Drink:          state = drinkState; break;
            case AmbientAction.Sleep:          state = sleepState; loopish = true; break;
            case AmbientAction.LookAround:     state = lookAroundState; break;
            case AmbientAction.Found:          state = foundSomething; break;
            case AmbientAction.InteractPeople: state = interactPeople; break;
            case AmbientAction.Dance:          state = danceState; loopish = true; break;
            case AmbientAction.Dizzy:          state = dizzyState; break;
            case AmbientAction.Celebrate:      state = celebrateState; break;
        }

        if (!string.IsNullOrEmpty(state))
        {
            if (!CrossFadeResolved(state, 0.08f))
            {
                string corrected = TryAliasOrFuzzy(state);
                if (!string.IsNullOrEmpty(corrected) && corrected != state) CrossFadeResolved(corrected, 0.08f);
                else Debug.LogWarning($"[NPCAmbientBrain] No se encontró el estado '{state}'.", this);
            }

            float len = GetClipLen(state);
            if (loopish)
            {
                float target = Random.Range(loopActionDuration.x, loopActionDuration.y);
                float t = 0f; while (t < target) { t += Time.deltaTime; yield return null; }
            }
            else
            {
                yield return new WaitForSeconds(len > 0f ? len : 1f);
            }
        }

        GoLocomotion();
    }

    // ======= Interacción con jugador (social) =======
    public void BeginInteraction()
    {
        if (_isInteracting || _isChallenging) return;
        _isInteracting = true;

        SafeStop(true);

        if (rotateToPlayerOnInteract && _player)
        {
            if (_faceCo != null) StopCoroutine(_faceCo);
            _faceCo = StartCoroutine(FaceTarget(_player));
        }

        if (!string.IsNullOrEmpty(interactPeople))
        {
            if (animator.layerCount > 1) animator.SetLayerWeight(1, 1f); // UpperBody ON si tienes
            CrossFadeResolved(interactPeople, 0.1f);
        }
    }

    public void EndInteraction()
    {
        if (!_isInteracting) return;
        _isInteracting = false;

        if (_faceCo != null) { StopCoroutine(_faceCo); _faceCo = null; }

        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f); // UpperBody OFF
        GoLocomotion();
    }

    IEnumerator FaceTarget(Transform t)
    {
        var pivot = transform;
        while ((_isInteracting || _isChallenging) && t)
        {
            Vector3 dir = t.position - pivot.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion q = Quaternion.LookRotation(dir.normalized, Vector3.up);
                pivot.rotation = Quaternion.Slerp(pivot.rotation, q, Time.deltaTime * rotateSpeed);
            }
            yield return null;
        }
    }

    // ======= Flujo RETADOR tipo Pokémon (corregido) =======
    IEnumerator CoChallengeFlow()
    {
        if (_isChallenging) yield break;
        _isChallenging = true;

        // Bloquea movimiento del jugador mientras dura el reto
        if (lockPlayerDuringChallenge) OnPlayerLockRequest?.Invoke();

        // Exclamación opcional
        GameObject ex = null;
        if (exclamationPrefab)
            ex = Instantiate(exclamationPrefab, transform.position + exclamationOffset, Quaternion.identity, transform);

        // 1) Alerta (parado)
        SafeStop(true);
        if (!string.IsNullOrEmpty(alertState)) CrossFadeResolved(alertState, 0.08f);

        float alertLen = Mathf.Max(0.6f, GetClipLen(alertState));
        float tAlert = 0f;
        while (tAlert < alertLen)
        {
            if (!PlayerInFOV(challengeSightRadius))
            {
                yield return new WaitForSeconds(Mathf.Min(loseSightGraceSeconds, alertLen - tAlert));
                break;
            }
            tAlert += Time.deltaTime;
            yield return null;
        }

        if (ex) Destroy(ex);

        // 2) APROXIMACIÓN: asegura Blend Tree activo y UpperBody apagada
        GoLocomotion();                             // <- fuerza el árbol de locomoción
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f); // UpperBody OFF durante la carrera

        float repathTimer = 0f;
        float loseSightTimer = 0f;

        // opcional: encarar mientras se acerca
        if (_faceCo != null) { StopCoroutine(_faceCo); _faceCo = null; }
        if (_player) _faceCo = StartCoroutine(FaceTarget(_player));

        while (true)
        {
            if (_player == null) break;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= challengeStopDistance) break;

            if (!PlayerInFOV(challengeSightRadius))
            {
                loseSightTimer += Time.deltaTime;
                if (loseSightTimer >= loseSightGraceSeconds) { break; }
            }
            else loseSightTimer = 0f;

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                MoveTo(_player.position, challengeStopDistance);
                repathTimer = approachRepathInterval;
            }

            yield return null;
        }

        SafeStop(true);

        // 3) RETO: hablar parado (UpperBody opcional) + abrir diálogo
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 1f); // UpperBody ON para hablar
        if (!string.IsNullOrEmpty(challengeState)) CrossFadeResolved(challengeState, 0.1f);

        // Abre diálogo externo
        if (!string.IsNullOrWhiteSpace(challengeDialogue))
            OnDialogueRequest?.Invoke(challengeDialogue);

        float challengeLen = Mathf.Max(1.0f, GetClipLen(challengeState));
        yield return new WaitForSeconds(challengeLen);

        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f); // UpperBody OFF

        // 4) Dispara evento para que tu sistema inicie el combate
        OnChallengeStarted?.Invoke();

        // 5) Limpieza
        if (_faceCo != null) { StopCoroutine(_faceCo); _faceCo = null; }
        GoLocomotion();

        // Si quieres que el player siga bloqueado hasta que el sistema de combate lo decida,
        // no invoques aquí el unlock. Si prefieres desbloquear ahora, descomenta:
        // OnPlayerUnlockRequest?.Invoke();

        _isChallenging = false;
    }

    // ======= Resolución de estados =======
    bool CrossFadeResolved(string stateNameOrPath, float fade)
    {
        if (!animator) return false;
        if (!EnsureResolved(stateNameOrPath)) return false;

        int hash = _stateHash[stateNameOrPath];
        int layer = _stateLayer[stateNameOrPath];
        animator.CrossFadeInFixedTime(hash, fade, layer, 0f);
        return true;
    }

    void GoLocomotion()
    {
        CrossFadeResolved(locomotionState, 0.1f);
        // por si venimos de hablar en UpperBody
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
    }

    bool EnsureResolved(string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath) || animator == null) return false;

        if (_aliases.TryGetValue(nameOrPath, out var mapped)) nameOrPath = mapped;
        if (_stateHash.ContainsKey(nameOrPath)) return true;

        string n = nameOrPath;
        string[] candidates = new string[] { n, $"Base Layer.{n}", $"Base Layer.Locomotion.{n}" };

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            foreach (var cand in candidates)
            {
                int h = Animator.StringToHash(cand);
                if (animator.HasState(layer, h))
                {
                    _stateHash[nameOrPath]  = h;
                    _stateLayer[nameOrPath] = layer;
                    return true;
                }
            }
        }

        int directHash = Animator.StringToHash(nameOrPath);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, directHash))
            {
                _stateHash[nameOrPath]  = directHash;
                _stateLayer[nameOrPath] = layer;
                return true;
            }
        }

        return false;
    }

    string TryAliasOrFuzzy(string original)
    {
        if (_aliases.TryGetValue(original, out var mapped)) return mapped;

        if (animator && animator.runtimeAnimatorController != null)
        {
            string norm(string s) => s.Replace("_", "").ToLowerInvariant();
            string o = norm(original);
            AnimationClip best = null; int bestScore = int.MaxValue;

            foreach (var c in animator.runtimeAnimatorController.animationClips)
            {
                string cn = norm(c.name);
                int score = Mathf.Abs(cn.Length - o.Length);
                if (cn.Contains(o) || o.Contains(cn)) score = 0;
                if (score < bestScore) { bestScore = score; best = c; }
                if (score == 0) break;
            }
            if (best != null) return best.name;
        }
        return null;
    }

    // ===== Util =====
    void MoveTo(Vector3 pos, float arriveRadius)
    {
        if (_agent == null) return;

        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, Mathf.Max(1f, wanderRadius), NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                return;
        }

        _agent.isStopped = false;
        _agent.SetDestination(pos);
        if (arriveRadius > 0f) _agent.stoppingDistance = Mathf.Min(arriveRadius, 1.0f);
    }

    void SafeStop(bool stop)
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = stop;
    }

    bool TryGetRandomNavmeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 randomPoint = origin + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(randomPoint, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = origin;
        return false;
    }

    bool FindClosestPoint(NPCActionType t, out NPCActionPoint best)
    {
        best = null;
        float bestSqr = float.MaxValue;
        var p = transform.position;
        foreach (var ap in actionPoints)
        {
            if (!ap || (ap.type != t && t != NPCActionType.Generic)) continue;
            float d = (ap.transform.position - p).sqrMagnitude;
            if (d < bestSqr && d <= maxPointSearchDist * maxPointSearchDist)
            {
                bestSqr = d; best = ap;
            }
        }
        return best != null;
    }

    void FaceToward(Vector3 targetPos, float lerp = 0.25f)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Mathf.Clamp01(lerp));
    }

    void CacheClipLengths()
    {
        if (!animator || animator.runtimeAnimatorController == null) return;
        foreach (var c in animator.runtimeAnimatorController.animationClips)
            if (!_clipLen.ContainsKey(c.name)) _clipLen.Add(c.name, c.length);
    }

    float GetClipLen(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0f;
        if (_clipLen.TryGetValue(name, out var l)) return l;
        if (animator && animator.runtimeAnimatorController != null)
        {
            foreach (var c in animator.runtimeAnimatorController.animationClips)
                if (c.name == name) { _clipLen[name] = c.length; return c.length; }
        }
        return 0f;
    }

    bool PlayerInFOV(float radius)
    {
        if (_player == null) return false;
        Vector3 from = transform.position;
        Vector3 to = _player.position - from;
        if (to.sqrMagnitude > radius * radius) return false;

        float dot = Vector3.Dot(transform.forward, to.normalized);
        float fovDot = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
        return dot >= fovDot;
    }

    IEnumerator CoGreet()
    {
        _greetOnCd = true;
        if (CrossFadeResolved(greetState, 0.08f))
        {
            float len = GetClipLen(greetState);
            yield return new WaitForSeconds(len > 0f ? len : 1f);
            GoLocomotion();
        }
        yield return new WaitForSeconds(greetCooldown);
        _greetOnCd = false;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!useIKLookAt || animator == null || _playerCam == null) return;
        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, 0.5f);
        animator.SetLookAtPosition(_playerCam.position + _playerCam.forward * 100f);
    }
}
