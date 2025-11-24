using System.Collections;
using Game.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[System.Obsolete("Usa NPCBehaviourManager con el módulo de combate.")]
public class SimpleNPCCombat : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;

    [Header("Rangos")]
    public float detectionRange = 12f;
    public float attackRange = 2.2f;
    public float projectileRange = 10f;

    [Header("Daño y Prefabs")]
    public float meleeDamage = 10f;
    public float projectileDamage = 8f;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("Cooldowns")]
    public float meleeCooldown = 1.2f;
    public float projectileCooldown = 3f;

    [Header("Estados de animación")]
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string meleeState = "Attack";
    public string projectileState = "ProjectileAttack";

    AnimatorStateCache _stateCache;
    float _lastMelee = -999f;
    float _lastProjectile = -999f;
    bool _isAttacking;

    void Awake()
    {
        animator ??= GetComponentInChildren<Animator>(true);
        agent ??= GetComponent<NavMeshAgent>();

        if (!player)
            player = PlayerLocator.ResolvePlayer();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            _stateCache = new AnimatorStateCache(animator);
            _stateCache.Preload(idleState, walkState, meleeState, projectileState);
        }
    }

    void OnEnable()
    {
        NavMeshAgentUtility.EnsureAgentOnNavMesh(agent, transform.position, 2f);
    }

    void Update()
    {
        if (player == null || _isAttacking)
            return;

        Vector3 toPlayer = player.position - transform.position;
        float sqrDist = toPlayer.sqrMagnitude;

        if (sqrDist > detectionRange * detectionRange)
        {
            SetIdle();
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            return;
        }

        LookAtPlayer();

        if (sqrDist <= attackRange * attackRange)
        {
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            TryMelee();
            return;
        }

        if (projectilePrefab &&
            sqrDist <= projectileRange * projectileRange &&
            Time.time >= _lastProjectile + projectileCooldown)
        {
            NavMeshAgentUtility.SafeSetStopped(agent, true);
            StartCoroutine(DoProjectile());
            return;
        }

        if (NavMeshAgentUtility.EnsureAgentOnNavMesh(agent, transform.position, 2f))
            NavMeshAgentUtility.SetDestination(agent, player.position);

        SetWalk();
    }

    void TryMelee()
    {
        if (Time.time < _lastMelee + meleeCooldown)
            return;

        StartCoroutine(DoMelee());
    }

    IEnumerator DoMelee()
    {
        _isAttacking = true;
        _lastMelee = Time.time;

        CrossFade(meleeState, 0.05f);

        yield return new WaitForSeconds(0.3f);

        if (player && Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f)
            ApplyDamageToPlayer(meleeDamage);

        yield return new WaitForSeconds(0.4f);
        _isAttacking = false;
    }

    IEnumerator DoProjectile()
    {
        _isAttacking = true;
        _lastProjectile = Time.time;

        CrossFade(projectileState, 0.05f);
        yield return new WaitForSeconds(0.45f);

        if (projectilePrefab && projectileSpawnPoint && player)
        {
            Vector3 dir = (player.position - projectileSpawnPoint.position).normalized;
            var go = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(dir));
            if (go.TryGetComponent<EnemyProjectile>(out var proj))
                proj.Initialize(dir, projectileDamage);
        }

        yield return new WaitForSeconds(0.35f);
        _isAttacking = false;
    }

    void ApplyDamageToPlayer(float damage)
    {
        if (!player)
            return;

        if (player.TryGetComponent<PlayerHealthSystem>(out var health))
        {
            health.TakeDamage(damage);
            return;
        }

        if (player.TryGetComponent<IDamageable>(out var damageable) && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            return;
        }

        Debug.LogWarning($"[SimpleNPCCombat] No se encontró sistema de salud en el jugador para aplicar {damage} de daño.");
    }

    void LookAtPlayer()
    {
        if (!player)
            return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 6f);
    }

    void SetIdle() => CrossFade(idleState, 0.05f);
    void SetWalk() => CrossFade(walkState, 0.05f);

    void CrossFade(string stateName, float fade)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (!_stateCache?.CrossFade(stateName, fade) ?? true)
            animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }
}
