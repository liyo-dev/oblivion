using Oblivion.Core.Feedback;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema de salud específico para el jugador que se integra con GameBootProfile
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerHealthSystem : MonoBehaviour
{
    [Header("Configuración de Daño")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [Tooltip("Si está activo, el jugador no puede morir (útil para testing)")]
    [SerializeField] private bool godMode; // por defecto false

    [Header("Regeneración de Vida")]
    [Tooltip("Activa la regeneración pasiva de vida")]
    [SerializeField] private bool enableHealthRegen = true;
    [Tooltip("Vida por segundo que se regenera")]
    [SerializeField] private float healthRegenPerSecond = 3f;
    [Tooltip("Retraso (segundos) después de recibir daño antes de empezar a regenerar")]
    [SerializeField] private float healthRegenDelayAfterDamage = 2f;
    [Tooltip("Evita micro-actualizaciones: margen mínimo de cambio antes de notificar UI")]
    [SerializeField] private float healthRegenNotifyEpsilon = 0.01f;
    
    [Header("Animaciones")]
    [SerializeField] private string damageAnimationName = "TakeDamage";
    [SerializeField] private string deathAnimationName = "Die02_NoWeapon";
    [SerializeField] private string healAnimationName = "Heal";
    [SerializeField] private int upperBodyLayer = 1; // Layer para animaciones del torso superior
    
    [Header("Efectos Visuales")]
    [SerializeField] private float damageFlashDuration = 0.2f;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private GameObject damageVFX;
    [SerializeField] private GameObject healVFX;
    
    [Header("Invulnerabilidad Visual")]
    [SerializeField] private float invulnerabilityFlashRate = 0.1f;
    
    [Header("Camera Shake (opcional)")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.3f;
    
    [Header("Knockback (opcional)")]
    [SerializeField] private bool enableKnockback = true;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private AnimationCurve knockbackCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    
    [Header("Audio")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip deathSound;
    
    [Header("Eventos")]
    public UnityEvent<float> OnHealthChanged; // healthPercentage (0-1)
    public UnityEvent<float, float> OnDamageTaken; // (damageAmount, currentHealth)
    public UnityEvent<float, float> OnHealed; // (healAmount, currentHealth)
    public UnityEvent OnPlayerDeath;
    public UnityEvent OnPlayerRevived;
    
    // Componentes
    private Animator _animator;
    private AudioSource _audioSource;
    private Renderer[] _renderers;
    private Material[] _originalMaterials;

    // Cache para resolución de estados del Animator
    private readonly Dictionary<string, int> _stateHash = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _stateLayer = new Dictionary<string, int>();

    // Estado local del sistema de salud
    private float _currentHp;
    private float _maxHp;
    private bool _isInvulnerable = false;
    private bool _isDead; // por defecto false
    private float _invulnerableUntil = -999f;

    // Evitar doble inicialización
    private bool _initialized;

    // Corrutinas
    private Coroutine _invulnerabilityFlashCoroutine;
    private Coroutine _damageFlashCoroutine;

    // Estado de regeneración
    private float _lastDamageTime = -999f;
    private float _lastNotifiedHealth;
    
    // Propiedades públicas usando GameBootProfile
    public bool IsAlive => _currentHp > 0 && !_isDead;
    public bool IsInvulnerable => _isInvulnerable || Time.time < _invulnerableUntil;
    public float CurrentHealth => _currentHp;
    public float MaxHealth => _maxHp;
    public float HealthPercentage => _maxHp > 0 ? _currentHp / _maxHp : 0f;
    public bool IsGodModeActive => godMode;
    
    // Eventos C#
    public event Action<float> OnHealthPercentageChanged;
    public event Action<float> OnDamageReceived;
    public event Action OnDied;
    
    void Awake()
    {
        _animator = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
        
        // Si no hay AudioSource, creamos uno
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        // Obtener renderers para efectos visuales
        _renderers = GetComponentsInChildren<Renderer>();
        CacheOriginalMaterials();
    }

    // Reemplaza Start/Coroutine por evento del boot service
    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;

        // Inicializar desde GameBootProfile
        var bootProfile = GameBootService.Profile;
        var preset = bootProfile?.GetActivePresetResolved();
        
        if (preset != null)
        {
            _maxHp = preset.maxHP;
            _currentHp = preset.currentHP;
            _isDead = _currentHp <= 0;
            _lastNotifiedHealth = _currentHp;
            
            Debug.Log($"[PlayerHealthSystem] Inicializado con vida: {_currentHp:0.1f}/{_maxHp:0.1f} ({HealthPercentage:P1}) - Estado: {(_isDead ? "MUERTO" : "VIVO")}");
        }
        else
        {
            Debug.LogWarning("[PlayerHealthSystem] No se encontró preset válido, usando valores por defecto");
            _maxHp = 100f;
            _currentHp = 100f;
            _isDead = false;
            _lastNotifiedHealth = _currentHp;
        }
        
        // Notificar UI inicial después de la inicialización
        UpdateUI();

        _initialized = true;
        // No necesitamos seguir suscritos tras inicializar
        GameBootService.OnProfileReady -= HandleProfileReady;
    }
    
    private void CacheOriginalMaterials()
    {
        if (_renderers == null) return;
        
        _originalMaterials = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                // Crear una instancia del material para evitar modificar el material compartido
                _originalMaterials[i] = new Material(_renderers[i].material);
                _renderers[i].material = _originalMaterials[i];
            }
        }
        
        Debug.Log($"[PlayerHealthSystem] Materiales originales cacheados para {_originalMaterials.Length} renderers");
    }
    
    /// <summary>
    /// Aplica daño al jugador
    /// </summary>
    public bool TakeDamage(float damageAmount)
    {
        if (!IsAlive || damageAmount <= 0f || godMode) return false;
        if (IsInvulnerable) return false;
        
        float oldHealth = _currentHp;
        _currentHp = Mathf.Max(0f, oldHealth - damageAmount);
        
        // Actualizar también el GameBootProfile si es necesario
        UpdateGameBootProfile();
        
        // Activar invulnerabilidad
        StartInvulnerability();
        
        // IMPORTANTE: Ejecutar animación INMEDIATAMENTE
        TriggerAnimation(damageAnimationName);
        
        // Efectos visuales y sonoros
        PlayDamageEffects();
        
        // Aplicar knockback DESPUÉS de la animación
        if (enableKnockback)
        {
            StartCoroutine(DelayedKnockback());
        }
        
        // Verificar muerte
        if (_currentHp <= 0 && !_isDead)
        {
            Die();
        }
        
        // Notificar eventos
        OnDamageTaken?.Invoke(damageAmount, _currentHp);
        OnDamageReceived?.Invoke(damageAmount);
        UpdateUI();
        
        Debug.Log($"[PlayerHealth] Jugador recibió {damageAmount} daño. Vida: {_currentHp}/{_maxHp}");
        
        return true;
    }
    
    private IEnumerator DelayedKnockback()
    {
        // Pequeño delay para que la animación empiece primero
        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(ApplyKnockback());
    }
    
    /// <summary>
    /// Cura al jugador
    /// </summary>
    public bool Heal(float healAmount)
    {
        if (healAmount <= 0f) return false;
        if (_currentHp >= _maxHp) return false;
        
        float oldHealth = _currentHp;
        _currentHp = Mathf.Min(_maxHp, oldHealth + healAmount);
        float actualHeal = _currentHp - oldHealth;
        
        if (actualHeal <= 0f) return false;
        
        // Actualizar GameBootProfile
        UpdateGameBootProfile();
        
        // Verificar revivir
        if (_isDead && _currentHp > 0)
        {
            ReviveInternal();
        }
        
        // Efectos solo si el jugador está vivo
        if (_currentHp > 0)
        {
            PlayHealEffects();
            TriggerAnimation(healAnimationName);
        }
        
        // Notificar eventos
        OnHealed?.Invoke(actualHeal, _currentHp);
        UpdateUI();
        
        Debug.Log($"[PlayerHealth] Jugador curado {actualHeal}. Vida: {_currentHp}/{_maxHp} - Estado: {(_currentHp > 0 ? "VIVO" : "MUERTO")}");
        
        return true;
    }
    
    /// <summary>
    /// Mata instantáneamente al jugador
    /// </summary>
    public void Kill()
    {
        if (!IsAlive || godMode) return;
        
        _currentHp = 0f;
        UpdateGameBootProfile();
        
        if (!_isDead)
        {
            Die();
        }
        
        UpdateUI();
    }
    
    /// <summary>
    /// Revive al jugador con vida completa
    /// </summary>
    public void Revive(float healthPercentage = 1f)
    {
        healthPercentage = Mathf.Clamp01(healthPercentage);
        _currentHp = _maxHp * healthPercentage;
        
        UpdateGameBootProfile();
        
        if (_isDead && _currentHp > 0)
        {
            ReviveInternal();
        }
        
        UpdateUI();
    }
    
    /// <summary>
    /// Actualiza el GameBootProfile con los valores actuales de salud
    /// </summary>
    private void UpdateGameBootProfile()
    {
        var bootProfile = GameBootService.Profile;
        if (bootProfile != null)
        {
            var preset = bootProfile.GetActivePresetResolved();
            if (preset != null)
            {
                preset.currentHP = _currentHp;
                preset.maxHP = _maxHp;
            }
        }
    }
    
    /// <summary>
    /// Establece la salud máxima y actualiza la actual proporcionalmente
    /// </summary>
    public void SetMaxHealth(float newMaxHp)
    {
        if (newMaxHp <= 0) return;
        
        float healthRatio = _maxHp > 0 ? _currentHp / _maxHp : 1f;
        _maxHp = newMaxHp;
        _currentHp = _maxHp * healthRatio;
        
        UpdateGameBootProfile();
        UpdateUI();
    }
    
    /// <summary>
    /// Establece la salud actual directamente
    /// </summary>
    public void SetCurrentHealth(float newCurrentHp)
    {
        _currentHp = Mathf.Clamp(newCurrentHp, 0f, _maxHp);
        
        bool wasDead = _isDead;
        bool shouldBeDead = _currentHp <= 0;
        
        if (!wasDead && shouldBeDead)
        {
            Die();
        }
        else if (wasDead && !shouldBeDead)
        {
            ReviveInternal();
        }
        
        UpdateGameBootProfile();
        UpdateUI();
    }
    
    private void Die()
    {
        if (_isDead) return;

        _isDead = true;

        // Asegurar que la salud está a 0
        _currentHp = 0f;

        // Sincronizar profile y UI INMEDIATAMENTE para que el HUD muestre correctamente 0
        UpdateGameBootProfile();
        UpdateUI();

        // Notificar eventos lo antes posible (antes de intentar animaciones/sonidos que podrían lanzar)
        try
        {
            OnPlayerDeath?.Invoke();
            OnDied?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerHealth] Excepción al invocar eventos de muerte: {e}");
        }

        Debug.Log("[PlayerHealth] ¡El jugador ha muerto!");

        // Intentar ejecutar efectos (animación/sonido) de forma segura; si fallan, no impedimos la notificación de GameOver
        try
        {
            TriggerAnimation(deathAnimationName);
            PlaySound(deathSound);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerHealth] Error al reproducir animación/sonido de muerte: {e}");
        }

        // Mostrar pantalla de Game Over (si existe un GameOverManager en la escena)
        try
        {
            GameOverManager.NotifyGameOver();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerHealth] Error notificando GameOver: {e}");
        }
    }

    private void ReviveInternal()
    {
        if (!_isDead) return;

        _isDead = false;

        // Teletransportar al último anchor conocido si existe
        try
        {
            if (!string.IsNullOrEmpty(SpawnManager.CurrentAnchorId))
            {
                SpawnManager.TeleportToCurrent(true);
                Debug.Log("[PlayerHealth] Revivido y teletransportado al último punto de partida guardado");
            }
            else
            {
                Debug.Log("[PlayerHealth] Revivido pero no hay anchor guardado (CurrentAnchorId vacío)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerHealth] Error al teletransportar en ReviveInternal: {ex.Message}");
        }

        // Actualizar profile/UI para asegurar que la HUD muestra el estado correcto
        UpdateGameBootProfile();
        UpdateUI();

        // Notificar eventos
        try
        {
            OnPlayerRevived?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerHealth] Excepción al invocar OnPlayerRevived: {ex.Message}");
        }

        Debug.Log("[PlayerHealth] ¡El jugador ha revivido!");
    }
    
    private void StartInvulnerability()
    {
        _invulnerableUntil = Time.time + invulnerabilityDuration;
        
        if (invulnerabilityDuration > 0f)
        {
            StartInvulnerabilityFlash();
        }
    }
    
    private void PlayDamageEffects()
    {
        StartDamageFlash();
        SpawnVFX(damageVFX);
        PlaySound(damageSound);
        
        if (enableCameraShake)
        {
            FeedbackService.CameraShake(shakeIntensity, shakeDuration);
        }
    }
    
    private void PlayHealEffects()
    {
        SpawnVFX(healVFX);
        PlaySound(healSound);
    }
    
    private void StartDamageFlash()
    {
        if (_damageFlashCoroutine != null)
            StopCoroutine(_damageFlashCoroutine);
        _damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }
    
    private IEnumerator DamageFlashCoroutine()
    {
        SetRenderersColor(damageFlashColor);
        yield return new WaitForSeconds(damageFlashDuration);
        RestoreOriginalMaterials();
    }
    
    private void StartInvulnerabilityFlash()
    {
        if (_invulnerabilityFlashCoroutine != null)
            StopCoroutine(_invulnerabilityFlashCoroutine);
        _invulnerabilityFlashCoroutine = StartCoroutine(InvulnerabilityFlashCoroutine());
    }
    
    private IEnumerator InvulnerabilityFlashCoroutine()
    {
        while (Time.time < _invulnerableUntil)
        {
            SetRenderersVisibility(false);
            yield return new WaitForSeconds(invulnerabilityFlashRate);
            SetRenderersVisibility(true);
            yield return new WaitForSeconds(invulnerabilityFlashRate);
        }
        SetRenderersVisibility(true);
    }
    
    private void SetRenderersColor(Color color)
    {
        if (_renderers == null || _originalMaterials == null) return;
        
        for (int i = 0; i < _renderers.Length && i < _originalMaterials.Length; i++)
        {
            if (_renderers[i] != null && _originalMaterials[i] != null)
            {
                _renderers[i].material.color = color;
            }
        }
    }
    
    private void SetRenderersVisibility(bool visible)
    {
        if (_renderers == null) return;
        
        foreach (var rend in _renderers)
        {
            if (rend != null)
                rend.enabled = visible;
        }
    }
    
    private void RestoreOriginalMaterials()
    {
        if (_renderers == null || _originalMaterials == null) return;
        
        for (int i = 0; i < _renderers.Length && i < _originalMaterials.Length; i++)
        {
            if (_renderers[i] != null && _originalMaterials[i] != null)
            {
                // Restaurar el color original del material
                _renderers[i].material.color = _originalMaterials[i].color;
            }
        }
        
        Debug.Log("[PlayerHealthSystem] Materiales restaurados al color original");
    }
    
    private void SpawnVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab != null)
        {
            var vfx = Instantiate(vfxPrefab, transform.position, transform.rotation);
            Destroy(vfx, 3f);
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void TriggerAnimation(string animationName)
    {
        if (_animator == null || string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"[PlayerHealthSystem] No se puede reproducir animación - Animator: {(_animator != null ? "OK" : "NULL")}, AnimationName: '{animationName}'");
            return;
        }

        // Asegurar que el layer configurado existe antes de usarlo
        if (upperBodyLayer >= 0 && upperBodyLayer < _animator.layerCount)
        {
            _animator.SetLayerWeight(upperBodyLayer, 1f);
        }

        // Intentar reproducir usando CrossFade en el estado resuelto (más seguro)
        if (CrossFadeResolved(animationName, 0.05f))
        {
            int layer = _stateLayer.ContainsKey(animationName) ? _stateLayer[animationName] : 0;
            Debug.Log($"[PlayerHealthSystem] Reproduciendo animación (CrossFade): {animationName} [layer {layer}]");
            return;
        }

        // Si CrossFade falló, intentamos resolver el estado y usar Play por hash (más seguro que Play(string))
        if (EnsureResolved(animationName))
        {
            int hash = _stateHash[animationName];
            int layer = _stateLayer[animationName];
            _animator.Play(hash, layer, 0f);
            Debug.Log($"[PlayerHealthSystem] Reproduciendo animación (Play por hash): {animationName} en layer {layer}");
            return;
        }

        Debug.LogWarning($"[PlayerHealthSystem] No se encontró el estado '{animationName}' en Animator. Asegúrate del nombre EXACTO o usa la ruta completa (p. ej. 'Base Layer.NombreEstado').");
    }

    // ===== Helpers para resolución segura de estados en el Animator =====
    private bool CrossFadeResolved(string stateNameOrPath, float fade)
    {
        if (_animator == null) return false;
        if (!EnsureResolved(stateNameOrPath)) return false;

        int hash = _stateHash[stateNameOrPath];
        int layer = _stateLayer[stateNameOrPath];
        _animator.CrossFadeInFixedTime(hash, fade, layer, 0f);
        return true;
    }

    private bool EnsureResolved(string nameOrPath)
    {
        if (string.IsNullOrEmpty(nameOrPath) || _animator == null) return false;
        if (_stateHash.ContainsKey(nameOrPath)) return true;

        // Probar variantes comunes de nombre (por ejemplo sufijos usados en el animator como _NoWeapon)
        string[] suffixes = new string[] { "", "_NoWeapon", "-NoWeapon" };
        var candidateList = new List<string>();
        foreach (var s in suffixes)
        {
            candidateList.Add(nameOrPath + s);
            candidateList.Add($"Base Layer.{nameOrPath}{s}");
            candidateList.Add($"Base Layer.Locomotion.{nameOrPath}{s}");
        }
        string[] candidates = candidateList.ToArray();

        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            foreach (var cand in candidates)
            {
                int h = Animator.StringToHash(cand);
                if (_animator.HasState(layer, h))
                {
                    _stateHash[nameOrPath] = h;
                    _stateLayer[nameOrPath] = layer;
                    return true;
                }
            }
        }

        // intento directo por si ya viene con ruta completa
        int directHash = Animator.StringToHash(nameOrPath);
        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            if (_animator.HasState(layer, directHash))
            {
                _stateHash[nameOrPath] = directHash;
                _stateLayer[nameOrPath] = layer;
                return true;
            }
        }

        // Depuración: listar candidatos probados (solo si no se encontró)
        try
        {
            Debug.LogWarning($"[PlayerHealthSystem] EnsureResolved: no se encontró '{nameOrPath}'. Candidatos probados: {string.Join(", ", candidates)}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerHealthSystem] EnsureResolved: excepción al listar candidatos: {ex.Message}");
        }

        return false;
    }
    
    [ContextMenu("Dump Animator States")]
    public void DumpAnimatorStates()
    {
        if (_animator == null)
        {
            Debug.LogWarning("[PlayerHealthSystem] DumpAnimatorStates: Animator es null");
            return;
        }

        var rc = _animator.runtimeAnimatorController;
        if (rc == null)
        {
            Debug.LogWarning("[PlayerHealthSystem] DumpAnimatorStates: runtimeAnimatorController es null");
            return;
        }

        Debug.Log($"[PlayerHealthSystem] Runtime clips ({rc.animationClips.Length}): {string.Join(", ", Array.ConvertAll(rc.animationClips, c => c.name))}");

        // Probar las mismas variantes que EnsureResolved para cada layer
        string[] suffixes = new string[] { "", "_NoWeapon", "-NoWeapon" };
        var candidateList = new List<string>();
        foreach (var s in suffixes)
        {
            candidateList.Add(damageAnimationName + s);
            candidateList.Add($"Base Layer.{damageAnimationName}{s}");
            candidateList.Add($"Base Layer.Locomotion.{damageAnimationName}{s}");
        }
        string[] candidates = candidateList.ToArray();

        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            var layerName = _animator.GetLayerName(layer);
            foreach (var cand in candidates)
            {
                int h = Animator.StringToHash(cand);
                bool has = _animator.HasState(layer, h);
                Debug.Log($"[PlayerHealthSystem] Layer {layer} ('{layerName}') - HasState('{cand}') = {has}");
            }
        }
    }
    
    private void UpdateUI()
    {
        float healthPercentage = HealthPercentage;
        OnHealthChanged?.Invoke(healthPercentage);
        OnHealthPercentageChanged?.Invoke(healthPercentage);
    }

    private IEnumerator ApplyKnockback()
    {
        Vector3 knockbackDirection = -transform.forward; // Empujar en la dirección opuesta a donde mira el jugador

        // Si el objeto tiene Rigidbody, aplicar una fuerza/impulso en vez de mover transform directamente.
        // Mover el transform de un objeto controlado por física puede producir valores NaN en la velocidad.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Normalizar y comprobar NaN por seguridad
            if (!float.IsNaN(knockbackDirection.x) && !float.IsNaN(knockbackDirection.y) && !float.IsNaN(knockbackDirection.z))
            {
                Vector3 impulse = knockbackDirection.normalized * knockbackForce;
                rb.AddForce(impulse, ForceMode.Impulse);
            }
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            float t = elapsed / knockbackDuration;
            float curveValue = knockbackCurve.Evaluate(t);

            // Aplicar fuerza de empuje (fallback para objetos sin Rigidbody)
            Vector3 delta = knockbackDirection * (knockbackForce * curveValue * Time.deltaTime);
            if (!float.IsNaN(delta.x) && !float.IsNaN(delta.y) && !float.IsNaN(delta.z))
            {
                transform.position += delta;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    // Métodos públicos para configuración
    public void SetGodMode(bool isEnabled)
    {
        godMode = isEnabled;
        Debug.Log($"[PlayerHealth] God Mode: {(isEnabled ? "ACTIVADO" : "DESACTIVADO")}");
    }
}
