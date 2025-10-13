using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Barra de vida para bosses que aparece automáticamente en la esquina inferior derecha
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Referencias del Boss")]
    [SerializeField] private Damageable bossDamageable;
    [SerializeField] private string bossName = "Boss Demonio";

    [Header("UI - Barra de Vida")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image healthBarBackground;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Sprites Opcionales")]
    [SerializeField] private Image bossIcon; // Cara del boss
    [SerializeField] private Sprite customHealthBarSprite; // Sprite personalizado para la barra
    [SerializeField] private Sprite customBossIconSprite; // Sprite de la cara del boss

    [Header("Colores")]
    [SerializeField] private Color healthyColor = new Color(0.8f, 0.2f, 0.2f); // Rojo oscuro
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private float warningThreshold = 0.5f;
    [SerializeField] private float criticalThreshold = 0.25f;

    [Header("Animación")]
    [SerializeField] private bool animateHealthChanges = true;
    [SerializeField] private float animationSpeed = 3f;
    [SerializeField] private bool showDamageFlash = true;
    [SerializeField] private float damageFlashDuration = 0.2f;

    [Header("Configuración")]
    [SerializeField] private bool showHealthNumbers = true;
    [SerializeField] private bool autoShow = true;
    [SerializeField] private bool autoHideOnDeath = true;
    [SerializeField] private bool autoCreateSprites = true; // Crear sprites automáticamente

    private float _targetFillAmount = 1f;
    private float _currentFillAmount = 1f;
    private CanvasGroup _canvasGroup;
    private bool _isVisible = false;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Crear sprites por defecto si están habilitados
        if (autoCreateSprites)
        {
            CreateDefaultSprites();
        }

        // Aplicar sprites personalizados si están asignados
        if (customHealthBarSprite && healthBarFill)
        {
            healthBarFill.sprite = customHealthBarSprite;
        }

        if (customBossIconSprite && bossIcon)
        {
            bossIcon.sprite = customBossIconSprite;
            bossIcon.gameObject.SetActive(true);
        }
        else if (bossIcon && bossIcon.sprite != null)
        {
            bossIcon.gameObject.SetActive(true);
        }
        else if (bossIcon)
        {
            bossIcon.gameObject.SetActive(false);
        }

        // Ocultar inicialmente
        if (!autoShow)
        {
            _canvasGroup.alpha = 0f;
            _isVisible = false;
        }
    }

    void Start()
    {
        // Buscar el Damageable si no está asignado
        if (!bossDamageable)
        {
            bossDamageable = GetComponentInParent<Damageable>();
            
            // Si no está en el parent, buscar en la escena (por si está en otro objeto)
            if (!bossDamageable)
            {
                bossDamageable = FindObjectOfType<ImpDemonAI>()?.GetComponent<Damageable>();
            }
        }

        if (bossDamageable)
        {
            // Suscribirse a eventos
            bossDamageable.OnDamaged += OnBossDamaged;
            bossDamageable.OnDied += OnBossDied;

            // Inicializar la barra
            UpdateHealthBar();
            
            if (autoShow)
            {
                Show();
            }
        }
        else
        {
            Debug.LogError("[BossHealthBar] No se encontró el componente Damageable del boss!");
        }

        // Configurar nombre del boss
        if (bossNameText)
        {
            bossNameText.text = bossName;
        }
    }

    void OnDestroy()
    {
        if (bossDamageable)
        {
            bossDamageable.OnDamaged -= OnBossDamaged;
            bossDamageable.OnDied -= OnBossDied;
        }
    }

    void Update()
    {
        if (!_isVisible) return;

        // Animar la barra suavemente
        if (animateHealthChanges && Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.001f)
        {
            _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount, Time.deltaTime * animationSpeed);
            
            if (healthBarFill)
            {
                healthBarFill.fillAmount = _currentFillAmount;
            }
        }
    }

    private void CreateDefaultSprites()
    {
        // Crear sprite por defecto para la barra de vida si no existe
        if (!customHealthBarSprite && healthBarFill && healthBarFill.sprite == null)
        {
            healthBarFill.sprite = CreateSolidSprite(Color.white);
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
            healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            Debug.Log("[BossHealthBar] Sprite de barra creado automáticamente");
        }

        // Crear sprite por defecto para el background si no existe
        if (healthBarBackground && healthBarBackground.sprite == null)
        {
            healthBarBackground.sprite = CreateSolidSprite(Color.white);
            healthBarBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Gris oscuro
            Debug.Log("[BossHealthBar] Sprite de background creado automáticamente");
        }

        // Crear sprite por defecto para el icono del boss si no existe y está asignado
        if (!customBossIconSprite && bossIcon && bossIcon.sprite == null)
        {
            bossIcon.sprite = CreateDemonIconSprite();
            Debug.Log("[BossHealthBar] Icono del demonio creado automáticamente");
        }
    }

    private Sprite CreateSolidSprite(Color color)
    {
        // Crear una textura simple de 1x1 pixel
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        // Crear sprite desde la textura
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = "AutoGeneratedSprite";

        return sprite;
    }

    private Sprite CreateDemonIconSprite()
    {
        // Crear una textura de 128x128 con un círculo rojo (representando al demonio)
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        Color demonColor = new Color(0.8f, 0.1f, 0.1f, 1f); // Rojo demoniaco
        Color backgroundColor = new Color(0, 0, 0, 0); // Transparente
        Color eyeColor = new Color(1f, 0.9f, 0.2f, 1f); // Amarillo para los ojos
        Color hornColor = new Color(0.3f, 0.1f, 0.1f, 1f); // Rojo oscuro para cuernos

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.4f;

        // Dibujar el fondo transparente
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, backgroundColor);
            }
        }

        // Dibujar círculo rojo (cara del demonio)
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance < radius)
                {
                    // Gradiente sutil desde el centro
                    float gradient = 1f - (distance / radius) * 0.3f;
                    texture.SetPixel(x, y, demonColor * gradient);
                }
            }
        }

        // Dibujar cuernos (dos triángulos en la parte superior)
        DrawHorn(texture, size / 2 - 20, size * 0.75f, hornColor); // Cuerno izquierdo
        DrawHorn(texture, size / 2 + 20, size * 0.75f, hornColor); // Cuerno derecho

        // Dibujar ojos brillantes
        DrawCircle(texture, size / 2 - 15, size / 2 + 10, 8, eyeColor); // Ojo izquierdo
        DrawCircle(texture, size / 2 + 15, size / 2 + 10, 8, eyeColor); // Ojo derecho

        // Dibujar boca malvada (sonrisa)
        DrawSmile(texture, size / 2, size / 2 - 15, 30, Color.black);

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = "AutoGeneratedDemonIcon";

        return sprite;
    }

    private void DrawHorn(Texture2D texture, float centerX, float centerY, Color color)
    {
        // Dibujar un pequeño triángulo (cuerno)
        int baseWidth = 10;
        int height = 20;

        for (int dy = 0; dy < height; dy++)
        {
            int width = Mathf.RoundToInt(baseWidth * (1f - (float)dy / height));
            for (int dx = -width / 2; dx <= width / 2; dx++)
            {
                int x = Mathf.RoundToInt(centerX + dx);
                int y = Mathf.RoundToInt(centerY + dy);
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private void DrawCircle(Texture2D texture, float centerX, float centerY, float radius, Color color)
    {
        Vector2 center = new Vector2(centerX, centerY);
        int radiusInt = Mathf.CeilToInt(radius);

        for (int y = -radiusInt; y <= radiusInt; y++)
        {
            for (int x = -radiusInt; x <= radiusInt; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = Mathf.RoundToInt(centerX + x);
                    int py = Mathf.RoundToInt(centerY + y);
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    {
                        texture.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    private void DrawSmile(Texture2D texture, float centerX, float centerY, float width, Color color)
    {
        // Dibujar una sonrisa malvada (curva)
        for (int i = 0; i < width; i++)
        {
            float t = (float)i / width - 0.5f;
            float curve = t * t * 15f; // Parábola hacia abajo
            
            int x = Mathf.RoundToInt(centerX - width / 2 + i);
            int y = Mathf.RoundToInt(centerY - curve);

            // Dibujar línea gruesa
            for (int dy = -2; dy <= 2; dy++)
            {
                int py = y + dy;
                if (x >= 0 && x < texture.width && py >= 0 && py < texture.height)
                {
                    texture.SetPixel(x, py, color);
                }
            }
        }
    }

    private void OnBossDamaged(float damageAmount)
    {
        UpdateHealthBar();

        if (showDamageFlash)
        {
            StartCoroutine(DamageFlash());
        }

        // Mostrar la barra si estaba oculta
        if (!_isVisible)
        {
            Show();
        }
    }

    private void OnBossDied()
    {
        UpdateHealthBar();

        if (autoHideOnDeath)
        {
            StartCoroutine(HideAfterDelay(2f));
        }
    }

    private void UpdateHealthBar()
    {
        if (!bossDamageable) return;

        float healthPercentage = bossDamageable.Current / bossDamageable.Max;
        _targetFillAmount = Mathf.Clamp01(healthPercentage);

        if (!animateHealthChanges)
        {
            _currentFillAmount = _targetFillAmount;
            if (healthBarFill)
            {
                healthBarFill.fillAmount = _currentFillAmount;
            }
        }

        // Actualizar color según salud
        if (healthBarFill)
        {
            if (healthPercentage <= criticalThreshold)
                healthBarFill.color = criticalColor;
            else if (healthPercentage <= warningThreshold)
                healthBarFill.color = warningColor;
            else
                healthBarFill.color = healthyColor;
        }

        // Actualizar texto de salud
        if (showHealthNumbers && healthText)
        {
            healthText.text = $"{Mathf.Ceil(bossDamageable.Current)} / {bossDamageable.Max}";
        }
        else if (healthText)
        {
            healthText.text = "";
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (healthBarBackground)
        {
            Color originalColor = healthBarBackground.color;
            healthBarBackground.color = Color.white;
            
            yield return new WaitForSeconds(damageFlashDuration);
            
            healthBarBackground.color = originalColor;
        }
    }

    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    public void Show()
    {
        _isVisible = true;
        if (_canvasGroup)
        {
            StartCoroutine(FadeIn());
        }
    }

    public void Hide()
    {
        _isVisible = false;
        if (_canvasGroup)
        {
            StartCoroutine(FadeOut());
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }

    // Método público para asignar el boss desde otro script
    public void SetBoss(Damageable damageable, string name = null)
    {
        // Desuscribir del boss anterior si existe
        if (bossDamageable)
        {
            bossDamageable.OnDamaged -= OnBossDamaged;
            bossDamageable.OnDied -= OnBossDied;
        }

        bossDamageable = damageable;
        
        if (!string.IsNullOrEmpty(name))
        {
            bossName = name;
            if (bossNameText)
            {
                bossNameText.text = name;
            }
        }

        // Suscribir al nuevo boss
        if (bossDamageable)
        {
            bossDamageable.OnDamaged += OnBossDamaged;
            bossDamageable.OnDied += OnBossDied;
            UpdateHealthBar();
            Show();
        }
    }
}

