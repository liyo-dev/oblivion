using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.NPC;

namespace Game.UI
{
    /// <summary>
    /// Barra de maná world-space para NPCs en combate.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class NPCManaBarUI : MonoBehaviour
    {
        [Header("Referencias Automáticas")]
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Configuración")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.25f, 0f);
        [SerializeField] private bool hideWhenFull = false;
        [SerializeField] private float hideDelay = 2.5f;
        [SerializeField] private bool showNumericText = false;
        
        [Header("Animación")]
        [SerializeField] private float lerpSpeed = 8f;
        [SerializeField] private bool smoothTransition = true;
        
        [Header("Colores")]
        [SerializeField] private Color healthyColor = new Color(0f, 0.45f, 1f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.25f, 0.1f, 1f);
        [SerializeField] private float warningThreshold = 0.45f;
        [SerializeField] private float criticalThreshold = 0.2f;
        
        private Canvas _canvas;
        private NPCCombatBrain _combatBrain;
        private float _hideTimer;
        private float _currentFillAmount = 1f;
        private float _targetFillAmount = 1f;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (fillImage == null) fillImage = GetComponentInChildren<Image>();
            if (healthText == null) healthText = GetComponentInChildren<TextMeshProUGUI>();
            if (healthText != null)
                healthText.gameObject.SetActive(showNumericText);
            
            if (GetComponent<BillboardUI>() == null)
            {
                gameObject.AddComponent<BillboardUI>();
            }
            
            if (_canvas != null)
                _canvas.enabled = false;
        }

        private void Start()
        {
            _combatBrain = GetComponentInParent<NPCCombatBrain>();
            if (_combatBrain == null)
            {
                Debug.LogError("[NPCManaBarUI] No se encontró NPCCombatBrain en los padres.");
                gameObject.SetActive(false);
                return;
            }
            
            _combatBrain.OnManaChanged += OnManaChanged;
            transform.localPosition = offset;
            UpdateBarImmediate();
        }

        private void OnDestroy()
        {
            if (_combatBrain != null)
            {
                _combatBrain.OnManaChanged -= OnManaChanged;
            }
        }

        private void Update()
        {
            if (smoothTransition && fillImage != null && Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.001f)
            {
                _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount, Time.deltaTime * lerpSpeed);
                fillImage.fillAmount = _currentFillAmount;
            }
            
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f && hideWhenFull && _combatBrain != null && _combatBrain.GetManaPercent() >= 0.99f)
                {
                    Hide();
                }
            }
        }

        private void OnManaChanged(float manaPercent)
        {
            Show();
            UpdateBar(manaPercent);
            _hideTimer = hideDelay;
        }

        private void UpdateBarImmediate()
        {
            if (_combatBrain == null) return;
            UpdateBar(_combatBrain.GetManaPercent(), true);
        }

        private void UpdateBar(float manaPercent, bool instant = false)
        {
            if (fillImage == null || _combatBrain == null) return;
            
            _targetFillAmount = Mathf.Clamp01(manaPercent);
            if (instant || !smoothTransition)
            {
                _currentFillAmount = _targetFillAmount;
                fillImage.fillAmount = _currentFillAmount;
            }
            
            if (_targetFillAmount <= criticalThreshold)
                fillImage.color = criticalColor;
            else if (_targetFillAmount <= warningThreshold)
                fillImage.color = warningColor;
            else
                fillImage.color = healthyColor;
            
            if (showNumericText && healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(_combatBrain.GetCurrentMana())} / {Mathf.CeilToInt(_combatBrain.GetMaxMana())}";
            }
        }

        public void Show()
        {
            if (_canvas != null && !_canvas.enabled)
                _canvas.enabled = true;
        }

        public void Hide()
        {
            if (_canvas != null && _canvas.enabled)
                _canvas.enabled = false;
        }
    }
}
