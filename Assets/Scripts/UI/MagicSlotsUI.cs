using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// UI automática para los slots de magia con iconos de botones del mando.
/// Se crea automáticamente al añadir el script al GameObject.
/// Muestra 3 slots: Izquierda (Oeste), Derecha (Sur), Arriba (Norte)
/// </summary>
public class MagicSlotsUI : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Vector2 slotsPosition = new Vector2(-50, -50); // Abajo derecha
    [SerializeField] private float slotSize = 80f;
    [SerializeField] private float slotSpacing = 20f;
    [SerializeField] private bool showDebugInfo = false;

    [Header("Colores")]
    [SerializeField] private Color availableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color noManaColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color backgroundColorActive = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color backgroundColorInactive = new Color(0.1f, 0.1f, 0.1f, 0.9f);

    [Header("Botones del Mando")]
    [SerializeField] private string leftButtonText = "◀"; // Oeste
    [SerializeField] private string rightButtonText = "▼"; // Sur  
    [SerializeField] private string upButtonText = "▲"; // Norte

    // Referencias automáticas
    private MagicCaster _magicCaster;
    private ManaPool _manaPool;

    // UI Elements
    private Canvas _canvas;
    private GameObject _slotsPanel;
    private MagicSlotUI _leftSlot;
    private MagicSlotUI _rightSlot;
    private MagicSlotUI _upSlot;

    // Clase para manejar cada slot individual
    [System.Serializable]
    private class MagicSlotUI
    {
        public GameObject slotObject;
        public Image backgroundImage;
        public Image iconImage;
        public Image cooldownOverlay;
        public TextMeshProUGUI buttonText;
        public TextMeshProUGUI cooldownText;
        public MagicSlot slotType;
    }

    void Awake()
    {
        CreateSlotsUI();
        FindPlayerComponents();
    }

    void Start()
    {
        if (!_magicCaster || !_manaPool)
        {
            if (showDebugInfo)
                Debug.LogWarning("[MagicSlotsUI] No se encontraron MagicCaster o ManaPool. Buscando en toda la escena...");
            StartCoroutine(FindComponentsDelayed());
        }
    }

    void Update()
    {
        UpdateSlotsUI();
    }

    private void CreateSlotsUI()
    {
        // Buscar Canvas principal o usar el existente
        _canvas = FindObjectOfType<Canvas>();
        if (!_canvas)
        {
            var canvasGO = new GameObject("MagicSlots_Canvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 99;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Panel principal de slots
        _slotsPanel = new GameObject("MagicSlots_Panel");
        _slotsPanel.transform.SetParent(_canvas.transform, false);

        var slotsRect = _slotsPanel.AddComponent<RectTransform>();
        slotsRect.anchorMin = new Vector2(1, 0);
        slotsRect.anchorMax = new Vector2(1, 0);
        slotsRect.pivot = new Vector2(1, 0);
        slotsRect.anchoredPosition = slotsPosition;
        slotsRect.sizeDelta = new Vector2(slotSize * 2 + slotSpacing, slotSize * 2 + slotSpacing);

        // Crear los 3 slots en formación triangular
        CreateSlot(MagicSlot.Left, new Vector2(-slotSize - slotSpacing/2, slotSize/2), leftButtonText, out _leftSlot);
        CreateSlot(MagicSlot.Right, new Vector2(-slotSize - slotSpacing/2, -slotSize/2), rightButtonText, out _rightSlot);
        CreateSlot(MagicSlot.Special, new Vector2(-slotSpacing/2, slotSize/2), upButtonText, out _upSlot);
    }

    private void CreateSlot(MagicSlot slotType, Vector2 position, string buttonText, out MagicSlotUI slotUI)
    {
        slotUI = new MagicSlotUI();
        slotUI.slotType = slotType;

        // GameObject principal del slot
        slotUI.slotObject = new GameObject($"Slot_{slotType}");
        slotUI.slotObject.transform.SetParent(_slotsPanel.transform, false);

        var slotRect = slotUI.slotObject.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.anchoredPosition = position;
        slotRect.sizeDelta = new Vector2(slotSize, slotSize);

        // Background del slot
        slotUI.backgroundImage = slotUI.slotObject.AddComponent<Image>();
        slotUI.backgroundImage.color = backgroundColorActive;
        slotUI.backgroundImage.raycastTarget = false;

        // Icono del hechizo (inicialmente vacío)
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slotUI.slotObject.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(8, 8);
        iconRect.offsetMax = new Vector2(-8, -8);

        slotUI.iconImage = iconGO.AddComponent<Image>();
        slotUI.iconImage.color = availableColor;
        slotUI.iconImage.raycastTarget = false;
        // Sprite por defecto (círculo simple)
        slotUI.iconImage.sprite = CreateDefaultSpellIcon();

        // Overlay de cooldown
        var cooldownGO = new GameObject("CooldownOverlay");
        cooldownGO.transform.SetParent(slotUI.slotObject.transform, false);
        var cooldownRect = cooldownGO.AddComponent<RectTransform>();
        cooldownRect.anchorMin = Vector2.zero;
        cooldownRect.anchorMax = Vector2.one;
        cooldownRect.offsetMin = Vector2.zero;
        cooldownRect.offsetMax = Vector2.zero;

        slotUI.cooldownOverlay = cooldownGO.AddComponent<Image>();
        slotUI.cooldownOverlay.color = new Color(0f, 0f, 0f, 0.7f);
        slotUI.cooldownOverlay.raycastTarget = false;
        slotUI.cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        slotUI.cooldownOverlay.type = Image.Type.Filled;
        slotUI.cooldownOverlay.fillOrigin = 2; // Top
        slotUI.cooldownOverlay.gameObject.SetActive(false);

        // Texto del botón del mando
        var buttonTextGO = new GameObject("ButtonText");
        buttonTextGO.transform.SetParent(slotUI.slotObject.transform, false);
        var buttonTextRect = buttonTextGO.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = new Vector2(1, 0);
        buttonTextRect.anchorMax = new Vector2(1, 0);
        buttonTextRect.pivot = new Vector2(1, 0);
        buttonTextRect.offsetMin = new Vector2(-25, 5);
        buttonTextRect.offsetMax = new Vector2(-5, 25);

        slotUI.buttonText = buttonTextGO.AddComponent<TextMeshProUGUI>();
        slotUI.buttonText.text = buttonText;
        slotUI.buttonText.fontSize = 16;
        slotUI.buttonText.color = Color.white;
        slotUI.buttonText.alignment = TextAlignmentOptions.Center;
        slotUI.buttonText.raycastTarget = false;

        // Texto de cooldown
        var cooldownTextGO = new GameObject("CooldownText");
        cooldownTextGO.transform.SetParent(slotUI.slotObject.transform, false);
        var cooldownTextRect = cooldownTextGO.AddComponent<RectTransform>();
        cooldownTextRect.anchorMin = Vector2.zero;
        cooldownTextRect.anchorMax = Vector2.one;
        cooldownTextRect.offsetMin = Vector2.zero;
        cooldownTextRect.offsetMax = Vector2.zero;

        slotUI.cooldownText = cooldownTextGO.AddComponent<TextMeshProUGUI>();
        slotUI.cooldownText.text = "";
        slotUI.cooldownText.fontSize = 18;
        slotUI.cooldownText.color = Color.white;
        slotUI.cooldownText.alignment = TextAlignmentOptions.Center;
        slotUI.cooldownText.raycastTarget = false;
        slotUI.cooldownText.gameObject.SetActive(false);

        // Añadir efecto de brillo sutil
        AddGlowEffect(slotUI.slotObject);
    }

    private void FindPlayerComponents()
    {
        // Buscar componentes en el player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            _magicCaster = player.GetComponent<MagicCaster>() ?? player.GetComponentInParent<MagicCaster>();
            _manaPool = player.GetComponent<ManaPool>() ?? player.GetComponentInParent<ManaPool>();
        }

        // Si no se encontraron, buscar en este GameObject
        if (!_magicCaster) _magicCaster = GetComponent<MagicCaster>() ?? GetComponentInParent<MagicCaster>();
        if (!_manaPool) _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>();
    }

    private IEnumerator FindComponentsDelayed()
    {
        yield return new WaitForSeconds(1f);

        // Buscar en toda la escena como último recurso
        if (!_magicCaster) _magicCaster = FindObjectOfType<MagicCaster>();
        if (!_manaPool) _manaPool = FindObjectOfType<ManaPool>();

        if (showDebugInfo)
        {
            if (_magicCaster) Debug.Log("[MagicSlotsUI] MagicCaster encontrado");
            if (_manaPool) Debug.Log("[MagicSlotsUI] ManaPool encontrado");
        }
    }

    private void UpdateSlotsUI()
    {
        if (!_magicCaster || !_manaPool) return;

        UpdateSlot(_leftSlot);
        UpdateSlot(_rightSlot);
        UpdateSlot(_upSlot);
    }

    private void UpdateSlot(MagicSlotUI slot)
    {
        if (slot?.slotObject == null) return;

        var spell = _magicCaster.GetSpellForSlot(slot.slotType);
        bool hasSpell = spell != null;
        bool canCast = _magicCaster.CanCastSpell(slot.slotType);
        bool isOnCooldown = _magicCaster.IsOnCooldown(slot.slotType);
        float cooldownTime = _magicCaster.GetCooldownTime(slot.slotType);
        bool hasEnoughMana = hasSpell && _manaPool.Current >= spell.manaCost;

        // Actualizar visibilidad del slot
        slot.slotObject.SetActive(hasSpell);
        if (!hasSpell) return;

        // Actualizar colores con transiciones suaves
        Color targetIconColor = availableColor;
        Color targetBgColor = backgroundColorActive;
        
        if (!canCast)
        {
            if (isOnCooldown)
            {
                targetIconColor = cooldownColor;
                targetBgColor = backgroundColorInactive;
            }
            else if (!hasEnoughMana)
            {
                targetIconColor = noManaColor;
                targetBgColor = backgroundColorInactive;
            }
            else
            {
                targetIconColor = cooldownColor;
                targetBgColor = backgroundColorInactive;
            }
        }
        
        // Lerp suave para transiciones de color
        slot.iconImage.color = Color.Lerp(slot.iconImage.color, targetIconColor, Time.deltaTime * 8f);
        slot.backgroundImage.color = Color.Lerp(slot.backgroundImage.color, targetBgColor, Time.deltaTime * 8f);

        // Actualizar cooldown visual con animación mejorada
        if (isOnCooldown && cooldownTime > 0)
        {
            slot.cooldownOverlay.gameObject.SetActive(true);
            slot.cooldownText.gameObject.SetActive(true);
            
            float maxCooldown = spell.cooldown;
            float cooldownPercent = cooldownTime / maxCooldown;
            
            // Animación radial suave
            slot.cooldownOverlay.fillAmount = cooldownPercent;
            
            // Contador con decimales solo si es < 1 segundo
            if (cooldownTime < 1f)
                slot.cooldownText.text = cooldownTime.ToString("F1");
            else
                slot.cooldownText.text = Mathf.CeilToInt(cooldownTime).ToString();
            
            // Efecto de pulso cuando está por terminar (últimos 0.5 segundos)
            if (cooldownTime < 0.5f)
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                float scale = 1f + pulse * 0.15f;
                slot.slotObject.transform.localScale = Vector3.one * scale;
                
                // Color parpadeante final
                Color pulseColor = Color.Lerp(targetIconColor, Color.white, pulse * 0.5f);
                slot.iconImage.color = pulseColor;
            }
            else
            {
                // Resetear escala si no está pulsando
                slot.slotObject.transform.localScale = Vector3.Lerp(
                    slot.slotObject.transform.localScale, 
                    Vector3.one, 
                    Time.deltaTime * 10f
                );
            }
        }
        else
        {
            slot.cooldownOverlay.gameObject.SetActive(false);
            slot.cooldownText.gameObject.SetActive(false);
            
            // Asegurar que la escala vuelva a normal
            slot.slotObject.transform.localScale = Vector3.Lerp(
                slot.slotObject.transform.localScale, 
                Vector3.one, 
                Time.deltaTime * 10f
            );
            
            // Efecto de "ready" cuando vuelve a estar disponible
            if (canCast && slot.iconImage.color != availableColor)
            {
                // Mini-flash cuando está listo
                StartCoroutine(FlashSlotReady(slot));
            }
        }

        // Actualizar icono del hechizo si es posible
        // TODO: Si tienes iconos específicos para hechizos, asignarlos aquí
        if (spell.spawnVFX != null)
        {
            // Intentar usar algún sprite del VFX si está disponible
        }
    }
    
    // Efecto de flash cuando el hechizo está listo después del cooldown
    private IEnumerator FlashSlotReady(MagicSlotUI slot)
    {
        if (slot?.slotObject == null) yield break;
        
        // Flash rápido de color
        Color originalColor = slot.iconImage.color;
        slot.iconImage.color = Color.white;
        
        // Mini escala pop
        Vector3 originalScale = slot.slotObject.transform.localScale;
        slot.slotObject.transform.localScale = Vector3.one * 1.2f;
        
        yield return new WaitForSeconds(0.1f);
        
        // Volver a normal suavemente
        float elapsed = 0f;
        float duration = 0.15f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            slot.iconImage.color = Color.Lerp(Color.white, originalColor, t);
            slot.slotObject.transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
            
            yield return null;
        }
        
        slot.iconImage.color = originalColor;
        slot.slotObject.transform.localScale = Vector3.one;
    }

    private Sprite CreateDefaultSpellIcon()
    {
        // Crear un icono por defecto para los hechizos
        var texture = new Texture2D(64, 64);
        var center = new Vector2(32, 32);
        
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 28)
                {
                    float alpha = 1f - (distance / 28f) * 0.3f;
                    texture.SetPixel(x, y, new Color(0.8f, 0.4f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
    }

    private void AddGlowEffect(GameObject slot)
    {
        // Añadir un efecto de brillo sutil
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(slot.transform, false);
        glowGO.transform.SetAsFirstSibling(); // Detrás del contenido

        var glowRect = glowGO.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-4, -4);
        glowRect.offsetMax = new Vector2(4, 4);

        var glowImage = glowGO.AddComponent<Image>();
        glowImage.color = new Color(1f, 1f, 1f, 0.1f);
        glowImage.raycastTarget = false;
    }

    void OnDestroy()
    {
        // Limpiar la UI cuando se destruya el componente
        if (_slotsPanel) DestroyImmediate(_slotsPanel);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_slotsPanel && _slotsPanel.GetComponent<RectTransform>())
        {
            _slotsPanel.GetComponent<RectTransform>().anchoredPosition = slotsPosition;
        }
    }
#endif
}
