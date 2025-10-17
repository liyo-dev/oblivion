using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class NPCAmbientBrain : MonoBehaviour
{
    [Header("Animator & Locomotion")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre o RUTA COMPLETA del Blend Tree de locomoción. Ej.: \"Free Locomotion\" o \"Base Layer.Locomotion.Free Locomotion\"")]
    [SerializeField] private string locomotionState = "Free Locomotion";

    [Tooltip("Parámetro del Blend Tree (normalmente InputMagnitude)")]
    [SerializeField] private string locomotionParam = "InputMagnitude";

    [SerializeField] private bool useRootMotion = false; // normalmente false

    [Header("Clips Ambientales (nombre o ruta completa)")]
    public string greetState        = "Greeting01_NoWeapon";
    public string drinkState        = "DrinkPotion_NoWeapon";
    public string sleepState        = "Sleeping_NoWeapon";
    public string lookAroundState   = "SenseSome_NoWeapon";
    public string foundSomething    = "FoundSom_NoWeapon";
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

    // ===== Internals =====
    NavMeshAgent _agent;
    Transform _player, _playerCam;

    int _inputMagHash;
    readonly Dictionary<string, float> _clipLen = new();

    // Resolución de estados
    readonly Dictionary<string, int> _stateHash = new();     // nombre -> hash
    readonly Dictionary<string, int> _stateLayer = new();     // nombre -> capa
    bool _running;
    bool _greetOnCd;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (overrideAgentSpeed > 0f) _agent.speed = overrideAgentSpeed;
        if (animator) animator.applyRootMotion = useRootMotion;

        _inputMagHash = Animator.StringToHash(locomotionParam);
        CacheClipLengths();

        // Player / cámara
        var pGo = GameObject.FindGameObjectWithTag("Player");
        if (pGo) _player = pGo.transform;
        if (Camera.main) _playerCam = Camera.main.transform;

        // Autorellenar ActionPoints cercanos si lista vacía
        if (actionPoints.Count == 0)
        {
            var all = FindObjectsOfType<NPCActionPoint>();
            foreach (var ap in all)
                if ((ap.transform.position - transform.position).sqrMagnitude <= maxPointSearchDist * maxPointSearchDist)
                    actionPoints.Add(ap);
        }

        // Pre-resolver estados críticos
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
        // Param del Blend Tree según velocidad del Agent
        if (animator && _agent && _agent.isOnNavMesh)
        {
            float speed01 = (_agent.speed <= 0.01f) ? 0f : Mathf.Clamp01(_agent.velocity.magnitude / _agent.speed);
            animator.SetFloat(_inputMagHash, speed01, 0.1f, Time.deltaTime);
        }

        // Saludo reactivo simple
        if (greetOnSight && !_greetOnCd && _player && PlayerInFOV(greetRadius))
            StartCoroutine(CoGreet());
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
        if (FindClosestPoint(t, out _)) pool.Add(a); // sesgo ligero
    }

    IEnumerator DoAction(AmbientAction a)
    {
        // 1) Ir a un punto si aplica
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

        // 2) Ejecutar clip
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
                Debug.LogWarning($"[NPCAmbientBrain] No se encontró el estado '{state}'. " +
                                 "Usa el nombre EXACTO o la ruta completa (p. ej. 'Base Layer.Locomotion.NombreEstado').", this);
            }
            else
            {
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
        }

        // 3) Volver a locomoción
        GoLocomotion();
    }

    // ======= Resolución de estados / reproducción segura =======

    // Reproduce estado resolviendo hash y capa automáticamente
    bool CrossFadeResolved(string stateNameOrPath, float fade)
    {
        if (!animator) return false;
        if (!EnsureResolved(stateNameOrPath)) return false;

        int hash = _stateHash[stateNameOrPath];
        int layer = _stateLayer[stateNameOrPath];
        animator.CrossFadeInFixedTime(hash, fade, layer, 0f);
        return true;
    }

    // Cambia a locomoción con resolución robusta
    void GoLocomotion()
    {
        CrossFadeResolved(locomotionState, 0.1f);
    }

    // Intenta resolver nombre/ruta → (hash, layer). Cachea. Devuelve true si existe.
    bool EnsureResolved(string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath) || animator == null) return false;
        if (_stateHash.ContainsKey(nameOrPath)) return true;

        // candidatos de ruta: tal cual, Base Layer.{n}, Base Layer.Locomotion.{n}
        string n = nameOrPath;
        string[] candidates = new string[] {
            n,
            $"Base Layer.{n}",
            $"Base Layer.Locomotion.{n}"
        };

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            foreach (var cand in candidates)
            {
                int h = Animator.StringToHash(cand);
                if (animator.HasState(layer, h))
                {
                    _stateHash[nameOrPath] = h;
                    _stateLayer[nameOrPath] = layer;
                    return true;
                }
            }
        }

        // último intento: si ya viene con "Base Layer." asumimos tal cual pero prueba en todas las capas
        int directHash = Animator.StringToHash(nameOrPath);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, directHash))
            {
                _stateHash[nameOrPath] = directHash;
                _stateLayer[nameOrPath] = layer;
                return true;
            }
        }

        return false;
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

    // IK mirar a la cámara del jugador (opcional)
    void OnAnimatorIK(int layerIndex)
    {
        if (!useIKLookAt || animator == null || _playerCam == null) return;
        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, 0.5f);
        animator.SetLookAtPosition(_playerCam.position + _playerCam.forward * 100f);
    }
}
