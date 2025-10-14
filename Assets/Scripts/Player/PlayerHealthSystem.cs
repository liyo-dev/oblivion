using Oblivion.Core.Feedback;
using System;
using System.Collections;
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
    [SerializeField] private string deathAnimationName = "Death";
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
        _lastDamageTime = Time.time;
        
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
        _lastDamageTime = Time.time;
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
        
        // Efectos
        TriggerAnimation(deathAnimationName);
        PlaySound(deathSound);
        
        // Notificar eventos
        OnPlayerDeath?.Invoke();
        OnDied?.Invoke();
        
        Debug.Log("[PlayerHealth] ¡El jugador ha muerto!");
    }
    
    private void ReviveInternal()
    {
        if (!_isDead) return;
        
        _isDead = false;
        
        // Notificar eventos
        OnPlayerRevived?.Invoke();
        
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
        if (_animator != null && !string.IsNullOrEmpty(animationName))
        {
            // Asegurar que el layer esté activo
            if (upperBodyLayer > 0)
            {
                _animator.SetLayerWeight(upperBodyLayer, 1f);
            }
            
            // Reproducir la animación directamente por nombre
            _animator.Play(animationName);
            
            Debug.Log($"[PlayerHealthSystem] Reproduciendo animación: {animationName} en layer {upperBodyLayer}");
        }
        else
        {
            Debug.LogWarning($"[PlayerHealthSystem] No se puede reproducir animación - Animator: {(_animator != null ? "OK" : "NULL")}, AnimationName: '{animationName}'");
        }
    }
    
    private void UpdateUI()
    {
        float healthPercentage = HealthPercentage;
        OnHealthChanged?.Invoke(healthPercentage);
        OnHealthPercentageChanged?.Invoke(healthPercentage);
    }

    void Update()
    {
        // Regeneración pasiva de vida
        if (enableHealthRegen && IsAlive && _currentHp < _maxHp)
        {
            if (Time.time - _lastDamageTime >= healthRegenDelayAfterDamage)
            {
                float before = _currentHp;
                _currentHp = Mathf.Min(_maxHp, _currentHp + Mathf.Max(0f, healthRegenPerSecond) * Time.deltaTime);
                if (Mathf.Abs(_currentHp - before) > Mathf.Epsilon)
                {
                    UpdateGameBootProfile();
                    // Notificar UI de forma moderada
                    if (Mathf.Abs(_currentHp - _lastNotifiedHealth) >= healthRegenNotifyEpsilon)
                    {
                        _lastNotifiedHealth = _currentHp;
                        UpdateUI();
                    }
                }
            }
        }
    }
    
    private IEnumerator ApplyKnockback()
    {
        Vector3 knockbackDirection = -transform.forward; // Empujar en la dirección opuesta a donde mira el jugador
        float elapsed = 0f;
        
        while (elapsed < knockbackDuration)
        {
            float t = elapsed / knockbackDuration;
            float curveValue = knockbackCurve.Evaluate(t);
            
            // Aplicar fuerza de empuje
            transform.position += knockbackDirection * (knockbackForce * curveValue * Time.deltaTime);
            
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
    
    public void SetInvulnerabilityDuration(float duration)
    {
        invulnerabilityDuration = Mathf.Max(0f, duration);
    }
    
    // Métodos de testing
    public void TestDamage(float amount)
    {
        TakeDamage(amount);
    }
    
    public void TestHeal(float amount)
    {
        Heal(amount);
    }
    
    // ===== MÉTODOS PARA ANIMATION EVENTS =====
    // Estos métodos son llamados por Animation Events en las animaciones
    
    /// <summary>
    /// Llamado por Animation Event cuando se ejecuta un hechizo/magia
    /// </summary>
    public void OnMagicExecute()
    {
        Debug.Log("[PlayerHealthSystem] Animation Event: OnMagicExecute - Ignorando para sistema de salud");
        // NO hacer nada aquí para evitar interferencias con el sistema de salud
    }
    
    /// <summary>
    /// Llamado por Animation Event cuando termina el cast de magia
    /// </summary>
    public void OnMagicCastEnd()
    {
        Debug.Log("[PlayerHealthSystem] Animation Event: OnMagicCastEnd - Ignorando para sistema de salud");
        // NO hacer nada aquí para evitar interferencias con el sistema de salud
    }
    
    /// <summary>
    /// Llamado por Animation Event para efectos de daño (opcional)
    /// </summary>
    public void OnDamageAnimationComplete()
    {
        Debug.Log("[PlayerHealthSystem] Animation Event: OnDamageAnimationComplete");
        // Este método SÍ puede tener lógica útil cuando termina la animación de daño
    }
}
