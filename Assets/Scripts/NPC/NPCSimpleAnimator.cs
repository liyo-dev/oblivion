using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCSimpleAnimator : MonoBehaviour
{
    [Header("Animator (sin transiciones, por código)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleState     = "Idle_Normal_NoWeapon";
    [SerializeField] private string greetState    = "Greeting01_NoWeapon";
    [SerializeField] private string interactState = "InteractWithPeople_NoWeapon";

    [Header("Saludo automático")]
    [SerializeField] private bool greetOnSight = true;
    [SerializeField] private float greetRadius = 3.0f;

    [Tooltip("Ángulo de FOV del NPC (solo saluda si el player está delante).")]
    [Range(1f, 180f)] [SerializeField] private float fovDegrees = 110f;

    [Tooltip("Requiere que el jugador/cámara esté mirando al NPC para saludar.")]
    [SerializeField] private bool requirePlayerLookingAtMe = true;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float playerLookDotThreshold = 0.6f;

    [SerializeField] private float greetCooldown = 4.0f;
    [SerializeField] private LayerMask occluders = ~0; // para raycast opcional

    [Header("Rotación al interactuar")]
    [SerializeField] private bool rotateToPlayerOnInteract = true;
    [SerializeField] private float rotateSpeed = 10f; // grados/seg aprox (lerp suave)

    [Header("Referencias")]
    [SerializeField] private Transform playerOverride; // opcional
    [SerializeField] private Transform lookFrom;       // origen para mirar (si null, transform)

    [Header("Integración mundo vivo (opcional)")]
    [Tooltip("Si existe, bloqueará el AmbientAgent durante la interacción para que no pise animaciones.")]
    [SerializeField] private AmbientInhibitor ambientInhibitor; // opcional

    [Header("Depuración")]
    [SerializeField] private bool drawGizmos = true;

    // estado
    bool isInteracting = false;
    bool greetOnCooldown = false;
    Transform player, playerCam;
    Coroutine faceCo;
    readonly Dictionary<string, float> clipLenCache = new();

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        lookFrom = transform;
        ambientInhibitor = GetComponent<AmbientInhibitor>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!lookFrom) lookFrom = transform;
        if (!ambientInhibitor) ambientInhibitor = GetComponent<AmbientInhibitor>();
        CacheClipLengths();

        // Player y cámara
        if (playerOverride) player = playerOverride;
        else
        {
            var pGo = GameObject.FindGameObjectWithTag("Player");
            if (pGo) player = pGo.transform;
        }
        if (Camera.main) playerCam = Camera.main.transform;

        // Enganche automático a Interactable (sin tocar su script)
        var interactable = GetComponent<Interactable>();
        if (interactable)
        {
            interactable.OnStarted.AddListener(BeginInteraction);
            interactable.OnFinished.AddListener(EndInteraction);
        }
    }

    void Start()
    {
        PlayState(idleState);
    }

    void Update()
    {
        if (!greetOnSight || isInteracting || !player) return;

        Vector3 toPlayer = (player.position - lookFrom.position);
        float dist = toPlayer.magnitude;
        if (dist > greetRadius) return;

        // FOV: ¿está delante del NPC?
        Vector3 forward = lookFrom.forward;
        Vector3 dir = toPlayer.normalized;
        float dot = Vector3.Dot(forward, dir);
        float fovDot = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
        if (dot < fovDot) return; // fuera del cono → no saluda

        // ¿El jugador/cámara me mira?
        if (requirePlayerLookingAtMe)
        {
            Vector3 toNpc = (lookFrom.position - (playerCam ? playerCam.position : player.position)).normalized;
            Vector3 lookForward = (playerCam ? playerCam.forward : player.forward);
            float lookDot = Vector3.Dot(lookForward, toNpc);
            if (lookDot < playerLookDotThreshold) return;
        }

        // Línea de visión básica (evita paredes)
        if (Physics.Raycast(lookFrom.position + Vector3.up * 1.6f, dir, out var hit, greetRadius, occluders))
        {
            if (hit.transform != player && !hit.transform.IsChildOf(player)) return;
        }

        if (!greetOnCooldown) StartCoroutine(DoGreeting());
    }

    // ==== Interacción (vienen del Interactable vía listeners) ====
    public void BeginInteraction()
    {
        if (isInteracting) return;
        isInteracting = true;

        // Bloquea el mundo vivo si existe
        ambientInhibitor?.Lock();

        StopFacing();
        if (rotateToPlayerOnInteract && player) faceCo = StartCoroutine(FaceTarget(player));

        PlayState(interactState);
    }

    public void EndInteraction()
    {
        if (!isInteracting) return;
        isInteracting = false;

        StopFacing();
        PlayState(idleState);

        // Libera el bloqueo del mundo vivo
        ambientInhibitor?.Unlock();
    }

    IEnumerator FaceTarget(Transform t)
    {
        if (!t) yield break;
        while (isInteracting)
        {
            Vector3 dir = (t.position - lookFrom.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                lookFrom.rotation = Quaternion.Slerp(lookFrom.rotation, target, Time.deltaTime * rotateSpeed);
            }
            yield return null;
        }
    }

    void StopFacing()
    {
        if (faceCo != null)
        {
            StopCoroutine(faceCo);
            faceCo = null;
        }
    }

    // ==== Saludo ====
    IEnumerator DoGreeting()
    {
        greetOnCooldown = true;

        PlayState(greetState);
        float len = GetClipLengthSafe(greetState);
        if (len <= 0f) len = 1.0f;

        yield return new WaitForSeconds(len);
        if (!isInteracting) PlayState(idleState);

        yield return new WaitForSeconds(greetCooldown);
        greetOnCooldown = false;
    }

    // ==== Utilidades públicas extra ====
    /// <summary>Permite fijar/actualizar el player desde fuera si cambias de avatar.</summary>
    public void SetPlayer(Transform newPlayer, Transform newPlayerCam = null)
    {
        player = newPlayer;
        playerCam = newPlayerCam ? newPlayerCam : (Camera.main ? Camera.main.transform : playerCam);
    }

    /// <summary>Dispara manualmente el saludo aunque no cumpla condiciones de Update.</summary>
    public void TriggerGreeting()
    {
        if (!greetOnCooldown && !isInteracting) StartCoroutine(DoGreeting());
    }

    /// <summary>Reproduce un estado puntual por nombre y vuelve a idle.</summary>
    public void PlayOneShot(string stateName)
    {
        StartCoroutine(CoPlayOneShot(stateName));
    }
    IEnumerator CoPlayOneShot(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) yield break;
        PlayState(stateName);
        float len = GetClipLengthSafe(stateName);
        yield return new WaitForSeconds(len > 0f ? len : 1f);
        if (!isInteracting) PlayState(idleState);
    }

    // ==== Animación util ====
    void PlayState(string stateName)
    {
        if (!animator || string.IsNullOrEmpty(stateName)) return;
        animator.Play(stateName, 0, 0f);
    }

    void CacheClipLengths()
    {
        if (!animator || animator.runtimeAnimatorController == null) return;
        foreach (var c in animator.runtimeAnimatorController.animationClips)
            if (!clipLenCache.ContainsKey(c.name)) clipLenCache.Add(c.name, c.length);
    }

    float GetClipLengthSafe(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0f;
        if (clipLenCache.TryGetValue(name, out var l)) return l;

        if (animator && animator.runtimeAnimatorController != null)
        {
            foreach (var c in animator.runtimeAnimatorController.animationClips)
                if (c.name == name) { clipLenCache[name] = c.length; return c.length; }
        }
        return 0f;
    }

    // ==== Gizmos ====
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawSphere((lookFrom ? lookFrom.position : transform.position), greetRadius);

        // FOV
        Vector3 pos = (lookFrom ? lookFrom.position : transform.position);
        Vector3 fwd = (lookFrom ? lookFrom.forward : transform.forward);
        float half = 0.5f * fovDegrees * Mathf.Deg2Rad;
        Vector3 left = Quaternion.Euler(0, -Mathf.Rad2Deg * half, 0) * fwd;
        Vector3 right = Quaternion.Euler(0,  Mathf.Rad2Deg * half, 0) * fwd;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(pos, pos + left.normalized * greetRadius);
        Gizmos.DrawLine(pos, pos + right.normalized * greetRadius);
    }
}
