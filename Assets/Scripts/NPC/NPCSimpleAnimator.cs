using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCSimpleAnimator : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre EXACTO del estado (Blend Tree) de locomoción en la capa Base.")]
    [SerializeField] private string locomotionState = "Free Locomotion";

    [SerializeField] private string greetState    = "Greeting01_NoWeapon";
    [SerializeField] private string interactState = "InteractWithPeople_NoWeapon";

    [Header("Saludo automático")]
    [SerializeField] private bool greetOnSight = true;
    [SerializeField] private float greetRadius = 3.0f;
    [Range(1f, 180f)] [SerializeField] private float fovDegrees = 110f;
    [SerializeField] private bool requirePlayerLookingAtMe = true;
    [Range(0f, 1f)] [SerializeField] private float playerLookDotThreshold = 0.6f;
    [SerializeField] private float greetCooldown = 4.0f;
    [SerializeField] private LayerMask occluders = ~0;

    [Header("Rotación al interactuar")]
    [SerializeField] private bool rotateToPlayerOnInteract = true;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Referencias")]
    [SerializeField] private Transform playerOverride; // opcional
    [SerializeField] private Transform lookFrom;       // si null, transform
    [SerializeField] private AmbientInhibitor ambientInhibitor; // opcional

    [Header("Depuración")]
    [SerializeField] private bool drawGizmos = true;

    // Estado interno
    bool isInteracting = false;
    bool greetOnCooldown = false;
    Transform player, playerCam;
    Coroutine faceCo;
    readonly Dictionary<string, float> clipLenCache = new();

    // Parámetros (hash)
    static readonly int InputMagnitude_Hash = Animator.StringToHash("InputMagnitude");

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        lookFrom = transform;
        ambientInhibitor = GetComponent<AmbientInhibitor>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!lookFrom) lookFrom = transform;
        if (!ambientInhibitor) ambientInhibitor = GetComponent<AmbientInhibitor>();
        CacheClipLengths();

        // Player/cámara
        if (playerOverride) player = playerOverride;
        else
        {
            var pGo = GameObject.FindGameObjectWithTag("Player");
            if (pGo) player = pGo.transform;
        }
        if (Camera.main) playerCam = Camera.main.transform;

        // Enlazar con Interactable si existe (no modifica tu script)
        var interactable = GetComponent<Interactable>();
        if (interactable)
        {
            interactable.OnStarted.AddListener(BeginInteraction);
            interactable.OnFinished.AddListener(EndInteraction);
        }

        // Locomoción controlada por Agent (no root-motion)
        if (animator) animator.applyRootMotion = false;
    }

    void Start()
    {
        // Entra al Blend Tree de locomoción desde el inicio
        PlayLocomotion();
        if (animator) animator.SetFloat(InputMagnitude_Hash, 0f);
    }

    void Update()
    {
        if (!greetOnSight || isInteracting || !player) return;

        Vector3 from = (lookFrom ? lookFrom.position : transform.position);
        Vector3 toPlayer = player.position - from;
        float dist = toPlayer.magnitude;
        if (dist > greetRadius) return;

        // FOV
        Vector3 forward = (lookFrom ? lookFrom.forward : transform.forward);
        Vector3 dir = toPlayer.normalized;
        float dot = Vector3.Dot(forward, dir);
        float fovDot = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
        if (dot < fovDot) return;

        // ¿El jugador/cámara me mira?
        if (requirePlayerLookingAtMe && playerCam)
        {
            Vector3 toNpc = (from - playerCam.position).normalized;
            float lookDot = Vector3.Dot(playerCam.forward, toNpc);
            if (lookDot < playerLookDotThreshold) return;
        }

        // Línea de visión (evita paredes entre medias)
        if (Physics.Raycast(from + Vector3.up * 1.6f, dir, out var hit, greetRadius, occluders))
        {
            if (hit.transform != player && !hit.transform.IsChildOf(player)) return;
        }

        if (!greetOnCooldown) StartCoroutine(DoGreeting());
    }

    // ===== Interacción =====
    public void BeginInteraction()
    {
        if (isInteracting) return;
        isInteracting = true;

        ambientInhibitor?.Lock();

        StopFacing();
        if (rotateToPlayerOnInteract && player)
            faceCo = StartCoroutine(FaceTarget(player));

        CrossFade(interactState, 0.1f);
    }

    public void EndInteraction()
    {
        if (!isInteracting) return;
        isInteracting = false;

        StopFacing();
        PlayLocomotion();

        ambientInhibitor?.Unlock();
    }

    IEnumerator FaceTarget(Transform t)
    {
        while (isInteracting && t)
        {
            Vector3 from = (lookFrom ? lookFrom.position : transform.position);
            Vector3 dir = t.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                var rotXform = (lookFrom ? lookFrom : transform);
                rotXform.rotation = Quaternion.Slerp(rotXform.rotation, target, Time.deltaTime * rotateSpeed);
            }
            yield return null;
        }
    }

    void StopFacing()
    {
        if (faceCo != null) { StopCoroutine(faceCo); faceCo = null; }
    }

    // ===== Saludo =====
    IEnumerator DoGreeting()
    {
        greetOnCooldown = true;

        CrossFade(greetState, 0.08f);
        float len = GetClipLengthSafe(greetState);
        yield return new WaitForSeconds(len > 0f ? len : 1f);

        if (!isInteracting) PlayLocomotion();

        yield return new WaitForSeconds(greetCooldown);
        greetOnCooldown = false;
    }

    // ===== Utilidades públicas =====
    public void SetPlayer(Transform newPlayer, Transform newPlayerCam = null)
    {
        player = newPlayer;
        playerCam = newPlayerCam ? newPlayerCam : (Camera.main ? Camera.main.transform : playerCam);
    }

    public void TriggerGreeting()
    {
        if (!greetOnCooldown && !isInteracting) StartCoroutine(DoGreeting());
    }

    public void PlayOneShot(string stateName)
    {
        StartCoroutine(CoPlayOneShot(stateName));
    }
    IEnumerator CoPlayOneShot(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) yield break;
        CrossFade(stateName, 0.08f);
        float len = GetClipLengthSafe(stateName);
        yield return new WaitForSeconds(len > 0f ? len : 1f);
        if (!isInteracting) PlayLocomotion();
    }

    // ===== Helpers de anim =====
    void PlayLocomotion()
    {
        if (!animator || string.IsNullOrEmpty(locomotionState)) return;
        animator.CrossFadeInFixedTime(locomotionState, 0.1f, 0, 0f);
    }

    void CrossFade(string stateName, float fade)
    {
        if (!animator || string.IsNullOrEmpty(stateName)) return;
        animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
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

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Vector3 pos = (lookFrom ? lookFrom.position : transform.position);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawSphere(pos, greetRadius);

        float half = 0.5f * fovDegrees * Mathf.Deg2Rad;
        Vector3 fwd = (lookFrom ? lookFrom.forward : transform.forward);
        Vector3 left = Quaternion.Euler(0, -Mathf.Rad2Deg * half, 0) * fwd;
        Vector3 right = Quaternion.Euler(0,  Mathf.Rad2Deg * half, 0) * fwd;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(pos, pos + left.normalized * greetRadius);
        Gizmos.DrawLine(pos, pos + right.normalized * greetRadius);
    }
}
