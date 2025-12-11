using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Referencias UI - Image Health Bar")]
    [SerializeField] private Image healthBarFill; // La Image principal que se llena/vacía
    [SerializeField] private Image healthBarBackground; // Imagen de fondo (opcional)
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private CanvasGroup healthBarCanvasGroup;
    
    [Header("Colores de la Barra")]
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private float warningThreshold = 0.5f;
    [SerializeField] private float criticalThreshold = 0.25f;
    
    [Header("Animaciones")]
    [SerializeField] private bool animateHealthChanges = true;
    [SerializeField] private float animationSpeed = 2f;
    [SerializeField] private bool showDamageFlash = true;
    [SerializeField] private float damageFlashDuration = 0.3f;
    
    [Header("Configuración")]
    [SerializeField] private bool showHealthNumbers = true;
    [SerializeField] private bool autoHide; // quitar valor por defecto para evitar warning de serializado
    [SerializeField] private float autoHideDelay = 3f;
    
    private PlayerHealthSystem _playerHealthSystem;
    private float _targetFillAmount = 1f;
    private float _currentFillAmount = 1f;
    private Coroutine _animationCoroutine;
    private Coroutine _autoHideCoroutine;
    private Coroutine _damageFlashCoroutine;

    void OnEnable()
    {
        FindPlayerHealthSystem();
        InitializeUI();
        
        // Forzar un repintado inmediato con la vida real si el sistema está disponible
        if (_playerHealthSystem != null)
        {
            _currentFillAmount = _targetFillAmount = _playerHealthSystem.HealthPercentage;
            UpdateHealthDisplay(_currentFillAmount, _playerHealthSystem.CurrentHealth, _playerHealthSystem.MaxHealth);
        }
        
        if (autoHide)
            ShowHealthBar();
    }
    
    void OnDisable()
    {
        // Desuscribir para no recibir eventos mientras está inactivo
        if (_playerHealthSystem != null)
        {
            _playerHealthSystem.OnHealthChanged.RemoveListener(UpdateHealthBar);
            _playerHealthSystem.OnDamageTaken.RemoveListener(OnDamageTaken);
        }
        // Detener corutinas si estuvieran en curso
        if (_animationCoroutine != null) { StopCoroutine(_animationCoroutine); _animationCoroutine = null; }
        if (_autoHideCoroutine != null) { StopCoroutine(_autoHideCoroutine); _autoHideCoroutine = null; }
        if (_damageFlashCoroutine != null) { StopCoroutine(_damageFlashCoroutine); _damageFlashCoroutine = null; }
    }
    
    private void FindPlayerHealthSystem()
    {
        // Obtener PlayerHealthSystem directamente desde el ServiceLocator
        _playerHealthSystem = ServiceLocator.Get<PlayerHealthSystem>(false);
        
        if (_playerHealthSystem != null)
        {
            _playerHealthSystem.OnHealthChanged.AddListener(UpdateHealthBar);
            _playerHealthSystem.OnDamageTaken.AddListener(OnDamageTaken);
            
            Debug.Log("[PlayerHealthUI] Conectado al sistema de salud del jugador");
        }
        else
        {
            Debug.LogWarning("[PlayerHealthUI] No se encontró PlayerHealthSystem");
        }
    }
    
    private void InitializeUI()
    {
        if (healthBarFill != null)
        {
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
            // Inicializar con valores coherentes si tenemos el sistema de salud
            if (_playerHealthSystem != null)
            {
                float ratio = _playerHealthSystem.HealthPercentage;
                healthBarFill.fillAmount = ratio;
                healthBarFill.color = GetHealthColor(ratio);
            }
            else
            {
                healthBarFill.fillAmount = 1f;
                healthBarFill.color = healthyColor;
            }
        }
        
        if (_playerHealthSystem != null)
        {
            UpdateHealthDisplay(_playerHealthSystem.HealthPercentage, _playerHealthSystem.CurrentHealth, _playerHealthSystem.MaxHealth);
        }
        else
        {
            UpdateHealthDisplay(1f, 100f, 100f);
        }
    }
    
    public void UpdateHealthBar(float healthRatio)
    {
        _targetFillAmount = Mathf.Clamp01(healthRatio);
        
        float currentHealth = _playerHealthSystem != null ? _playerHealthSystem.CurrentHealth : 0f;
        float maxHealth = _playerHealthSystem != null ? _playerHealthSystem.MaxHealth : 100f;

        // Si el objeto está desactivado o el componente no está habilitado, no iniciar corutinas
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            _currentFillAmount = _targetFillAmount;
            UpdateHealthDisplay(_currentFillAmount, currentHealth, maxHealth);
            return;
        }
        
        if (animateHealthChanges)
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimateHealthChange(currentHealth, maxHealth));
        }
        else
        {
            _currentFillAmount = _targetFillAmount;
            UpdateHealthDisplay(_currentFillAmount, currentHealth, maxHealth);
        }
        
        if (autoHide)
        {
            ShowHealthBar();
            RestartAutoHideTimer();
        }
    }
    
    private System.Collections.IEnumerator AnimateHealthChange(float currentHealth, float maxHealth)
    {
        while (Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.01f)
        {
            _currentFillAmount = Mathf.MoveTowards(_currentFillAmount, _targetFillAmount, 
                                                   animationSpeed * Time.deltaTime);
            
            float displayHealth = _currentFillAmount * maxHealth;
            UpdateHealthDisplay(_currentFillAmount, displayHealth, maxHealth);
            
            yield return null;
        }
        
        _currentFillAmount = _targetFillAmount;
        UpdateHealthDisplay(_currentFillAmount, currentHealth, maxHealth);
    }
    
    private void UpdateHealthDisplay(float fillAmount, float current, float max)
    {
        // Actualizar el fillAmount de la Image
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = fillAmount;
            healthBarFill.color = GetHealthColor(fillAmount);
        }
        
        // Actualizar texto si está configurado
        if (healthText != null && showHealthNumbers)
        {
            healthText.text = $"{current:0}/{max:0}";
        }
    }
    
    private Color GetHealthColor(float fillAmount)
    {
        if (fillAmount <= criticalThreshold)
        {
            return criticalColor;
        }
        else if (fillAmount <= warningThreshold)
        {
            float t = (fillAmount - criticalThreshold) / (warningThreshold - criticalThreshold);
            return Color.Lerp(criticalColor, warningColor, t);
        }
        else
        {
            float t = (fillAmount - warningThreshold) / (1f - warningThreshold);
            return Color.Lerp(warningColor, healthyColor, t);
        }
    }
    
    private void OnDamageTaken(float damageAmount, float currentHealth)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            // Si está inactivo, no lanzar corutina; la barra se actualizará cuando se active
            return;
        }

        if (showDamageFlash)
        {
            if (_damageFlashCoroutine != null)
                StopCoroutine(_damageFlashCoroutine);
            _damageFlashCoroutine = StartCoroutine(DamageFlashEffect());
        }
    }
    
    private System.Collections.IEnumerator DamageFlashEffect()
    {
        if (healthBarCanvasGroup != null)
        {
            float originalAlpha = healthBarCanvasGroup.alpha;
            
            healthBarCanvasGroup.alpha = 1.5f;
            yield return new WaitForSeconds(damageFlashDuration * 0.3f);
            
            float elapsed = 0f;
            while (elapsed < damageFlashDuration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (damageFlashDuration * 0.7f);
                healthBarCanvasGroup.alpha = Mathf.Lerp(1.5f, originalAlpha, t);
                yield return null;
            }
            
            healthBarCanvasGroup.alpha = originalAlpha;
        }
        else if (healthBarFill != null)
        {
            // Si no hay CanvasGroup, hacer flash directo en la barra
            Color originalColor = healthBarFill.color;
            Color flashColor = Color.white;
            
            healthBarFill.color = flashColor;
            yield return new WaitForSeconds(damageFlashDuration * 0.3f);
            
            float elapsed = 0f;
            while (elapsed < damageFlashDuration * 0.7f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (damageFlashDuration * 0.7f);
                healthBarFill.color = Color.Lerp(flashColor, originalColor, t);
                yield return null;
            }
            
            healthBarFill.color = originalColor;
        }
    }
    
    private void ShowHealthBar()
    {
        if (healthBarCanvasGroup != null)
        {
            healthBarCanvasGroup.alpha = 1f;
            healthBarCanvasGroup.interactable = true;
        }
    }
    
    private void HideHealthBar()
    {
        if (healthBarCanvasGroup != null)
        {
            healthBarCanvasGroup.alpha = 0.3f;
            healthBarCanvasGroup.interactable = false;
        }
    }
    
    private void RestartAutoHideTimer()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
        if (_autoHideCoroutine != null)
            StopCoroutine(_autoHideCoroutine);
        _autoHideCoroutine = StartCoroutine(AutoHideTimer());
    }
    
    private System.Collections.IEnumerator AutoHideTimer()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HideHealthBar();
    }
    
    public void ForceUpdate()
    {
        if (_playerHealthSystem != null)
        {
            float ratio = _playerHealthSystem.HealthPercentage;
            UpdateHealthBar(ratio);
        }
    }
}
